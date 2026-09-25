using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using CatLib.Localization;
using CatLib.Logging;

namespace CatLib.Config;

public sealed class CatSettings : IDisposable
{
    private readonly List<ISettingNode> _settings = new();
    private readonly CatLogger _log;

    internal CatSettings(ConfigFile configFile, string ownerId, string displayName, string version, CatLogger log)
    {
        ConfigFile = configFile;
        OwnerId = ownerId;
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? ownerId : displayName;
        Version = version;
        _log = log;
        ConfigFile.SettingChanged += OnConfigFileSettingChanged;
    }

    public string OwnerId { get; }

    public string DisplayName { get; }

    public string Version { get; }

    public TextCatalog Texts => CatLocalization.For(OwnerId);

    public ConfigFile ConfigFile { get; }

    public string FilePath => ConfigFile.ConfigFilePath;

    public bool IsDisposed { get; private set; }

    public IReadOnlyList<ISetting> Settings
    {
        get
        {
            lock (_settings)
            {
                return _settings.Cast<ISetting>().ToList();
            }
        }
    }

    public static CatSettings For(BasePlugin plugin)
    {
        if (plugin == null)
        {
            throw new ArgumentNullException(nameof(plugin));
        }

        var metadata = MetadataHelper.GetMetadata(plugin);
        if (metadata == null)
        {
            throw new ArgumentException($"{plugin.GetType().FullName} has no BepInPlugin attribute", nameof(plugin));
        }

        return For(plugin.Config, metadata.GUID, metadata.Name, metadata.Version?.ToString());
    }

    public static CatSettings For(ConfigFile configFile, string ownerId, string displayName = null, string version = null)
    {
        if (configFile == null)
        {
            throw new ArgumentNullException(nameof(configFile));
        }

        if (string.IsNullOrWhiteSpace(ownerId))
        {
            throw new ArgumentException("Owner id must not be empty.", nameof(ownerId));
        }

        return CatConfig.GetOrCreate(configFile, ownerId, displayName, version);
    }

    public Setting<T> Local<T>(string section, string key, T defaultValue, string description, AcceptableValueBase acceptableValues = null) =>
        Declare(SettingScope.Local, section, key, defaultValue, description, acceptableValues);

    public Setting<T> Session<T>(string section, string key, T defaultValue, string description, AcceptableValueBase acceptableValues = null) =>
        Declare(SettingScope.Session, section, key, defaultValue, description, acceptableValues);

    public int ResetToDefaults(Func<ISetting, bool> filter = null)
    {
        ISettingNode[] snapshot;
        lock (_settings)
        {
            snapshot = _settings.ToArray();
        }

        var changed = 0;
        var saveOnConfigSet = ConfigFile.SaveOnConfigSet;
        ConfigFile.SaveOnConfigSet = false;
        try
        {
            foreach (var setting in snapshot)
            {
                if (filter != null && !filter(setting))
                {
                    continue;
                }

                var entry = setting.EntryBase;
                if (Equals(entry.BoxedValue, entry.DefaultValue))
                {
                    continue;
                }

                entry.BoxedValue = entry.DefaultValue;
                changed++;
            }
        }
        finally
        {
            ConfigFile.SaveOnConfigSet = saveOnConfigSet;
        }

        if (changed > 0 && saveOnConfigSet)
        {
            ConfigFile.Save();
            CatConfig.RememberCurrentContent(this);
        }

        _log.Info($"Reset {changed} setting(s) of {OwnerId} to their defaults");
        return changed;
    }

    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        IsDisposed = true;
        ConfigFile.SettingChanged -= OnConfigFileSettingChanged;
        lock (_settings)
        {
            foreach (var setting in _settings)
            {
                setting.Detach();
            }

            _settings.Clear();
        }

        CatConfig.Unregister(this);
    }

    internal void RefreshAll()
    {
        ISettingNode[] snapshot;
        lock (_settings)
        {
            snapshot = _settings.ToArray();
        }

        foreach (var setting in snapshot)
        {
            try
            {
                setting.Refresh();
            }
            catch (Exception exception)
            {
                _log.Error($"Refreshing {setting.Id} failed", exception);
            }
        }
    }

    internal ISetting Find(ConfigDefinition definition)
    {
        lock (_settings)
        {
            return _settings.FirstOrDefault(setting => setting.EntryBase.Definition.Equals(definition));
        }
    }

    private Setting<T> Declare<T>(SettingScope scope, string section, string key, T defaultValue, string description, AcceptableValueBase acceptableValues)
    {
        if (IsDisposed)
        {
            throw new ObjectDisposedException(nameof(CatSettings), $"Settings of {OwnerId} were disposed");
        }

        var definition = new ConfigDefinition(section, key);
        lock (_settings)
        {
            var existing = _settings.FirstOrDefault(setting => setting.EntryBase.Definition.Equals(definition));
            if (existing != null)
            {
                if (existing is Setting<T> typed && typed.Scope == scope)
                {
                    return typed;
                }

                throw new InvalidOperationException($"{OwnerId}/{section}/{key} is already declared as {existing.Scope} {existing.ValueType.Name}");
            }

            var entry = ConfigFile.Bind(definition, defaultValue, new ConfigDescription(description ?? string.Empty, acceptableValues, scope));
            var setting = new Setting<T>(this, entry, scope, _log);
            _settings.Add(setting);
            CatConfig.RememberCurrentContent(this);
            return setting;
        }
    }

    private void OnConfigFileSettingChanged(object sender, SettingChangedEventArgs args)
    {
        if (CatConfig.IsApplyingFile || !ConfigFile.SaveOnConfigSet)
        {
            return;
        }

        CatConfig.RememberCurrentContent(this);
    }
}
