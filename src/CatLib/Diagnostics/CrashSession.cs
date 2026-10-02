using System;
using System.Collections.Generic;
using System.Globalization;

namespace CatLib.Diagnostics;

internal sealed class CrashSession
{
    public const int CurrentFormat = 1;
    public const string FormatKey = "format";
    public const string CatLibKey = "catlib";
    public const string GameKey = "game";
    public const string GameBuildKey = "gameBuild";
    public const string StartedKey = "started";
    public const string ProcessKey = "pid";
    public const string LanguageKey = "language";
    public const string PlayerLogKey = "playerLog";
    public const string BepInExLogKey = "bepinexLog";
    public const string ReportsKey = "reports";
    public const string ModKey = "mod";
    public const string EventKey = "event";
    public const string CleanKey = "clean";
    public const string MainThreadKey = "mainThread";
    public const string ExceptionKey = "exception";
    public const string TextPrefix = "text.";

    public int Format { get; set; }

    public bool IsNewerFormat => Format > CurrentFormat;

    public string WatcherVersion { get; set; } = string.Empty;

    public string CatLibVersion { get; set; } = string.Empty;

    public string GameVersion { get; set; } = string.Empty;

    public string GameBuild { get; set; } = string.Empty;

    public string Started { get; set; } = string.Empty;

    public int ProcessId { get; set; }

    public string Language { get; set; } = "en";

    public string PlayerLog { get; set; } = string.Empty;

    public string BepInExLog { get; set; } = string.Empty;

    public string ReportsDirectory { get; set; } = string.Empty;

    public List<string> Mods { get; } = new List<string>();

    public List<string> Events { get; } = new List<string>();

    public string CleanExit { get; set; }

    public int MainThreadId { get; set; }

    public List<string> ExceptionLines { get; } = new List<string>();

    public Dictionary<string, string> Texts { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

    public CrashStrings Strings => new CrashStrings(Texts);

    public static string Line(string key, string value) => key + "=" + Flatten(value);

    public IEnumerable<string> HeaderLines()
    {
        yield return Line(FormatKey, Format.ToString(CultureInfo.InvariantCulture));
        yield return Line(CatLibKey, CatLibVersion);
        yield return Line(GameKey, GameVersion);
        yield return Line(StartedKey, Started);
        yield return Line(ProcessKey, ProcessId.ToString(CultureInfo.InvariantCulture));
        yield return Line(LanguageKey, Language);
        yield return Line(PlayerLogKey, PlayerLog);
        yield return Line(BepInExLogKey, BepInExLog);
        yield return Line(ReportsKey, ReportsDirectory);
        if (MainThreadId != 0)
        {
            yield return Line(MainThreadKey, MainThreadId.ToString(CultureInfo.InvariantCulture));
        }
        foreach (var mod in Mods)
        {
            yield return Line(ModKey, mod);
        }
    }

    public static CrashSession Parse(IEnumerable<string> lines)
    {
        var session = new CrashSession();
        foreach (var line in lines)
        {
            var split = line == null ? -1 : line.IndexOf('=');
            if (split <= 0)
            {
                continue;
            }

            var key = line.Substring(0, split);
            var value = line.Substring(split + 1);
            if (key.StartsWith(TextPrefix, StringComparison.Ordinal))
            {
                session.Texts[key.Substring(TextPrefix.Length)] = value;
                continue;
            }

            switch (key)
            {
                case FormatKey:
                    session.Format = int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var format) ? format : 0;
                    break;
                case CatLibKey:
                    session.CatLibVersion = value;
                    break;
                case GameKey:
                    session.GameVersion = value;
                    break;
                case GameBuildKey:
                    session.GameBuild = value;
                    break;
                case StartedKey:
                    session.Started = value;
                    break;
                case ProcessKey:
                    session.ProcessId = int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : 0;
                    break;
                case LanguageKey:
                    session.Language = value;
                    break;
                case PlayerLogKey:
                    session.PlayerLog = value;
                    break;
                case BepInExLogKey:
                    session.BepInExLog = value;
                    break;
                case ReportsKey:
                    session.ReportsDirectory = value;
                    break;
                case ModKey:
                    session.Mods.Add(value);
                    break;
                case EventKey:
                    session.Events.Add(value);
                    break;
                case CleanKey:
                    session.CleanExit = value;
                    break;
                case ExceptionKey:
                    session.ExceptionLines.Add(value);
                    break;
                case MainThreadKey:
                    session.MainThreadId = int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var thread) ? thread : 0;
                    break;
            }
        }

        return session;
    }

    private static string Flatten(string value) => (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ');
}
