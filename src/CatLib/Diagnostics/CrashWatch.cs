using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using CatLib.Config;
using CatLib.Game;
using CatLib.Game.Events;
using CatLib.Localization;
using CatLib.Logging;
using UnityEngine;

namespace CatLib.Diagnostics;

internal static class CrashWatch
{
    public const string WatcherFileName = "CatLib.CrashWatcher.exe";
    public const string SessionFilePrefix = "session_";
    public const int KeptEvents = 200;
    public const int EventsBeforeTrim = 2000;
    public static readonly string[] ClearedVariablePrefixes = { "DOORSTOP_", "DOTNET_", "COMPlus_", "CORECLR_" };

    private static readonly object Gate = new();
    private static readonly HashSet<string> SkippedEvents = new(StringComparer.Ordinal) { "Network.NetworkTick" };
    private static readonly List<string> Header = new();
    private static readonly Queue<string> RecentEvents = new();
    private static CatLogger _log;
    private static int _eventsInFile;
    private static string _sessionFile;
    private static bool _modsWritten;
    private static bool _closed;

    public static string ReportsDirectory => Path.Combine(Paths.BepInExRootPath, "CatLib", "Crashes");

    public static bool IsWatching => _sessionFile != null;

    internal static void Initialize(CatLogger log, CatSettings settings)
    {
        _log = log;
        var enabled = settings.Local("Diagnostics", "CrashWindow", true,
            "When the game closes unexpectedly, a small window shows what happened and a report with the logs is kept in BepInEx/CatLib/Crashes.")
            .RequiresRestart();
        if (!enabled.Value)
        {
            _log.Info("The crash window is turned off in the CatLib settings");
            return;
        }

        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            Start();
        }
        catch (Exception exception)
        {
            _sessionFile = null;
            _log.Warning($"The crash watcher could not start: {exception.Message}");
        }
    }

    internal static void MarkCleanExit()
    {
        AppendHeader(CrashSession.Line(CrashSession.CleanKey, Now()));
        _closed = true;
    }

    private static void Start()
    {
        var watcher = Path.Combine(Path.GetDirectoryName(typeof(CrashWatch).Assembly.Location) ?? string.Empty, WatcherFileName);
        if (!File.Exists(watcher))
        {
            _log.Warning($"The crash watcher {WatcherFileName} is missing next to CatLib.dll, crashes will not be reported");
            return;
        }

        Directory.CreateDirectory(ReportsDirectory);
        var process = Process.GetCurrentProcess();
        _sessionFile = Path.Combine(ReportsDirectory, SessionFilePrefix + process.Id.ToString(CultureInfo.InvariantCulture) + ".txt");
        var session = new CrashSession
        {
            CatLibVersion = PluginMeta.Version,
            GameVersion = GameInfo.ApplicationVersion ?? string.Empty,
            Started = Now(),
            ProcessId = process.Id,
            Language = SafeLanguage(),
            PlayerLog = Application.consoleLogPath ?? string.Empty,
            BepInExLog = Path.Combine(Paths.BepInExRootPath, "LogOutput.log"),
            ReportsDirectory = ReportsDirectory
        };
        Header.AddRange(session.HeaderLines());
        File.WriteAllLines(_sessionFile, Header, Encoding.UTF8);
        RemoveStaleSessions(process.Id);

        var start = new ProcessStartInfo(watcher)
        {
            Arguments = $"--pid {process.Id.ToString(CultureInfo.InvariantCulture)} --session \"{_sessionFile}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = ReportsDirectory
        };
        foreach (var name in start.Environment.Keys.Where(IsClearedVariable).ToList())
        {
            start.Environment.Remove(name);
        }

        Process.Start(start);

        GameEventStream.Raised += OnEvent;
        BootstrapEvents.MainMenuLoaded += OnMainMenuLoaded;
        _log.Info($"Crash watcher started, reports go to {ReportsDirectory}");
    }

    private static void OnMainMenuLoaded()
    {
        AppendHeader(CrashSession.Line(CrashSession.LanguageKey, SafeLanguage()));
        if (!string.IsNullOrEmpty(GameInfo.GameVersion))
        {
            AppendHeader(CrashSession.Line(CrashSession.GameKey, GameInfo.GameVersion));
        }

        if (_modsWritten)
        {
            return;
        }

        _modsWritten = true;
        var lines = new List<string>();
        foreach (var plugin in IL2CPPChainloader.Instance.Plugins.Values.OrderBy(info => info.Metadata.GUID, StringComparer.Ordinal))
        {
            lines.Add(CrashSession.Line(CrashSession.ModKey, $"{plugin.Metadata.Name} {plugin.Metadata.Version} ({plugin.Metadata.GUID})"));
        }

        AppendHeader(lines.ToArray());
    }

    private static void OnEvent(GameEventRecord record)
    {
        if (SkippedEvents.Contains(record.Name))
        {
            return;
        }

        var text = record.Arguments.Length == 0 ? record.Name : record.Name + " " + record.Arguments;
        var line = CrashSession.Line(CrashSession.EventKey, Now() + " " + text);
        lock (Gate)
        {
            RecentEvents.Enqueue(line);
            while (RecentEvents.Count > KeptEvents)
            {
                RecentEvents.Dequeue();
            }

            _eventsInFile++;
            if (_eventsInFile > EventsBeforeTrim)
            {
                _eventsInFile = RecentEvents.Count;
                Rewrite();
                return;
            }

            Append(line);
        }
    }

    private static void AppendHeader(params string[] lines)
    {
        lock (Gate)
        {
            Header.AddRange(lines);
            Append(lines);
        }
    }

    private static void Rewrite()
    {
        if (_sessionFile == null || _closed)
        {
            return;
        }

        Write(() => File.WriteAllLines(_sessionFile, Header.Concat(RecentEvents), Encoding.UTF8));
    }

    private static void Append(params string[] lines)
    {
        if (_sessionFile == null || _closed)
        {
            return;
        }

        lock (Gate)
        {
            Write(() => File.AppendAllLines(_sessionFile, lines, Encoding.UTF8));
        }
    }

    private static void Write(Action write)
    {
        try
        {
            write();
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static bool IsClearedVariable(string name) =>
        ClearedVariablePrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    private static void RemoveStaleSessions(int currentProcessId)
    {
        foreach (var file in Directory.GetFiles(ReportsDirectory, SessionFilePrefix + "*.txt"))
        {
            var name = Path.GetFileNameWithoutExtension(file).Substring(SessionFilePrefix.Length);
            if (!int.TryParse(name, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) || id == currentProcessId || IsRunning(id))
            {
                continue;
            }

            try
            {
                File.Delete(file);
            }
            catch (IOException)
            {
            }
        }
    }

    private static bool IsRunning(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static string SafeLanguage()
    {
        try
        {
            return CatLanguage.Current ?? "en";
        }
        catch (Exception)
        {
            return "en";
        }
    }

    private static string Now() => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
}
