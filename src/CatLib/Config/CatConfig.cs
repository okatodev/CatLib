using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx.Configuration;
using CatLib.Events;
using CatLib.Logging;

namespace CatLib.Config;

public static class CatConfig
{
    public const int DefaultDebounceMilliseconds = 250;
    public const int DefaultMaxReadAttempts = 40;

    private static readonly object Sync = new();
    private static readonly Dictionary<string, FileState> Files = new(StringComparer.OrdinalIgnoreCase);
    private static CatLogger _log;
    private static ConfigFileWatcher _watcher;
    private static int _debounceMilliseconds = DefaultDebounceMilliseconds;

    public static event Action<ConfigReloadReport> FileReloaded;
    public static event Action<SettingValueProblem> ValueRejected;
    public static event Action<SettingValueProblem> ValueAdjusted;
    public static event Action<ISetting> RestartRequired;

    public static int DebounceMilliseconds
    {
        get => _debounceMilliseconds;
        set => _debounceMilliseconds = Math.Clamp(value, 0, 5000);
    }

    public static int MaxReadAttempts { get; set; } = DefaultMaxReadAttempts;

    public static IReadOnlyList<CatSettings> All
    {
        get
        {
            lock (Sync)
            {
                return Files.Values.Select(state => state.Settings).ToList();
            }
        }
    }

    internal static bool IsApplyingFile { get; private set; }

    private static CatLogger Log => _log ??= CatLogger.Create("CatLib").Scope("Config");

    private static ConfigFileWatcher Watcher => _watcher ??= new ConfigFileWatcher(Log);

    public static ConfigReloadReport ReloadNow(CatSettings settings)
    {
        if (settings == null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        var state = Find(settings);
        return state == null ? null : Reload(state, true);
    }

    internal static void Initialize(CatLogger log)
    {
        _log = log;
        _watcher ??= new ConfigFileWatcher(log);
    }

    internal static CatSettings GetOrCreate(ConfigFile configFile, string ownerId)
    {
        var path = ConfigFileWatcher.Normalize(configFile.ConfigFilePath);
        lock (Sync)
        {
            if (Files.TryGetValue(path, out var existing))
            {
                if (!ReferenceEquals(existing.Settings.ConfigFile, configFile))
                {
                    throw new InvalidOperationException($"{path} is already managed through a different ConfigFile instance");
                }

                if (!string.Equals(existing.Settings.OwnerId, ownerId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"{path} is already managed by {existing.Settings.OwnerId}");
                }

                return existing.Settings;
            }

            var settings = new CatSettings(configFile, ownerId, Log.Scope(ownerId));
            var state = new FileState(path, settings);
            state.LastKnownContent = TryRead(path);
            Files[path] = state;
            Watcher.Track(path);
            Log.Debug($"Tracking {path} for {ownerId}");
            return settings;
        }
    }

    internal static void Unregister(CatSettings settings)
    {
        var path = ConfigFileWatcher.Normalize(settings.FilePath);
        lock (Sync)
        {
            if (Files.TryGetValue(path, out var state) && ReferenceEquals(state.Settings, settings))
            {
                Files.Remove(path);
                Watcher.Untrack(path);
                Log.Debug($"Stopped tracking {path}");
            }
        }
    }

    internal static void RememberCurrentContent(CatSettings settings)
    {
        var state = Find(settings);
        if (state == null)
        {
            return;
        }

        var content = TryRead(state.Path);
        if (content != null)
        {
            state.LastKnownContent = content;
        }
    }

    internal static void RaiseRestartRequired(ISetting setting)
    {
        Log.Message($"{setting.Id} changed to {Describe(setting.BoxedLocalValue)} and will apply after a restart, current value is {Describe(setting.BoxedValue)}");
        SafeInvoker.Invoke(RestartRequired, setting, "CatConfig.RestartRequired", Log);
    }

    internal static void Update()
    {
        if (_watcher == null)
        {
            return;
        }

        foreach (var path in _watcher.TakeDue(TimeSpan.FromMilliseconds(_debounceMilliseconds)))
        {
            FileState state;
            lock (Sync)
            {
                Files.TryGetValue(path, out state);
            }

            if (state == null)
            {
                continue;
            }

            try
            {
                Reload(state, false);
            }
            catch (Exception exception)
            {
                Log.Error($"Reloading {path} failed", exception);
            }
        }
    }

    private static FileState Find(CatSettings settings)
    {
        var path = ConfigFileWatcher.Normalize(settings.FilePath);
        lock (Sync)
        {
            return Files.TryGetValue(path, out var state) && ReferenceEquals(state.Settings, settings) ? state : null;
        }
    }

    private static ConfigReloadReport Reload(FileState state, bool forced)
    {
        if (!File.Exists(state.Path))
        {
            state.Attempts = 0;
            Log.Debug($"{Path.GetFileName(state.Path)} is missing, keeping current values");
            return null;
        }

        string content;
        try
        {
            content = ReadShared(state.Path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            state.Attempts++;
            if (state.Attempts >= MaxReadAttempts)
            {
                Log.Warning($"Giving up on {Path.GetFileName(state.Path)} after {state.Attempts} attempts: {exception.Message}");
                state.Attempts = 0;
            }
            else
            {
                Watcher.Mark(state.Path);
            }

            return null;
        }

        var attempts = state.Attempts + 1;
        state.Attempts = 0;

        if (!forced && string.Equals(content, state.LastKnownContent, StringComparison.Ordinal))
        {
            return null;
        }

        state.LastKnownContent = content;
        return Apply(state, ConfigText.Parse(content), attempts, forced);
    }

    private static ConfigReloadReport Apply(FileState state, Dictionary<ConfigDefinition, string> values, int attempts, bool forced)
    {
        var settings = state.Settings;
        var configFile = settings.ConfigFile;
        var rejected = new List<SettingValueProblem>();
        var adjusted = new List<SettingValueProblem>();
        var changed = 0;

        var saveOnConfigSet = configFile.SaveOnConfigSet;
        IsApplyingFile = true;
        configFile.SaveOnConfigSet = false;
        try
        {
            foreach (var definition in configFile.Keys.ToList())
            {
                if (!values.TryGetValue(definition, out var rawValue))
                {
                    continue;
                }

                var entry = configFile[definition];
                var before = entry.BoxedValue;

                object parsed;
                try
                {
                    parsed = TomlTypeConverter.ConvertToValue(rawValue, entry.SettingType);
                }
                catch (Exception exception)
                {
                    rejected.Add(Problem(settings, definition, rawValue, before, "could not be parsed (" + exception.Message.TrimEnd('.') + ")"));
                    continue;
                }

                entry.BoxedValue = parsed;
                var after = entry.BoxedValue;

                if (!Equals(parsed, after))
                {
                    adjusted.Add(Problem(settings, definition, rawValue, after, "is outside the accepted values"));
                }

                if (!Equals(before, after))
                {
                    changed++;
                }
            }
        }
        finally
        {
            configFile.SaveOnConfigSet = saveOnConfigSet;
            IsApplyingFile = false;
        }

        var report = new ConfigReloadReport(state.Path, settings.OwnerId, changed, rejected.Count, adjusted.Count, attempts, forced);
        var summary = $"Reloaded {Path.GetFileName(state.Path)}: {changed} changed, {rejected.Count} rejected, {adjusted.Count} adjusted";
        if (changed + rejected.Count + adjusted.Count > 0)
        {
            Log.Info(summary);
        }
        else
        {
            Log.Debug(summary);
        }

        foreach (var problem in rejected)
        {
            Log.Warning($"{problem.OwnerId}: value \"{problem.RawValue}\" of [{problem.Section}] {problem.Key} {problem.Reason}. Keeping {problem.EffectiveValue}");
            SafeInvoker.Invoke(ValueRejected, problem, "CatConfig.ValueRejected", Log);
        }

        foreach (var problem in adjusted)
        {
            Log.Warning($"{problem.OwnerId}: value \"{problem.RawValue}\" of [{problem.Section}] {problem.Key} {problem.Reason}. Using {problem.EffectiveValue}");
            SafeInvoker.Invoke(ValueAdjusted, problem, "CatConfig.ValueAdjusted", Log);
        }

        settings.RefreshAll();

        SafeInvoker.Invoke(FileReloaded, report, "CatConfig.FileReloaded", Log);
        return report;
    }

    private static SettingValueProblem Problem(CatSettings settings, ConfigDefinition definition, string rawValue, object effective, string reason) =>
        new(settings.OwnerId, definition.Section, definition.Key, rawValue, Describe(effective), reason, settings.Find(definition));

    private static string Describe(object value) =>
        value == null ? "null" : TomlTypeConverter.ConvertToString(value, value.GetType());

    private static string ReadShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream, Encoding.UTF8, true);
        return reader.ReadToEnd();
    }

    private static string TryRead(string path)
    {
        try
        {
            return File.Exists(path) ? ReadShared(path) : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private sealed class FileState
    {
        private readonly object _contentLock = new();
        private string _lastKnownContent;

        public FileState(string path, CatSettings settings)
        {
            Path = path;
            Settings = settings;
        }

        public string Path { get; }

        public CatSettings Settings { get; }

        public int Attempts { get; set; }

        public string LastKnownContent
        {
            get
            {
                lock (_contentLock)
                {
                    return _lastKnownContent;
                }
            }
            set
            {
                lock (_contentLock)
                {
                    _lastKnownContent = value;
                }
            }
        }
    }
}
