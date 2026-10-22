using System;
using System.Collections.Generic;
using System.Globalization;

namespace CatLib.Diagnostics;

internal sealed class CrashSession
{
    public const int CurrentFormat = 2;
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
    public const string LogMemoryKey = "logMemory";
    public const string DotnetStringKey = "dotnetString";
    public const string Il2CppStringKey = "il2cppString";
    public const string DoingKey = "doing";
    public const string DoneKey = "done";

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

    public ulong LogMemoryAddress { get; set; }

    public int LogMemorySize { get; set; }

    public ulong DotnetStringType { get; set; }

    public ulong Il2CppStringType { get; set; }

    public List<CrashOperation> Operations { get; } = new List<CrashOperation>();

    public List<CrashOperation> Unfinished
    {
        get
        {
            var open = new List<CrashOperation>();
            foreach (var operation in Operations)
            {
                if (!operation.Done)
                {
                    open.Add(operation);
                }
            }

            return open;
        }
    }

    public static string Address(ulong value) => "0x" + value.ToString("x", CultureInfo.InvariantCulture);

    public static bool TryParseAddress(string text, out ulong value)
    {
        value = 0;
        var trimmed = (text ?? string.Empty).Trim();
        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed.Substring(2);
        }

        return trimmed.Length > 0 && ulong.TryParse(trimmed, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
    }

    public static string DoingValue(long id, string time, string operation) => id.ToString(CultureInfo.InvariantCulture) + " " + time + " " + operation;

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

        if (LogMemoryAddress != 0 && LogMemorySize > 0)
        {
            yield return Line(LogMemoryKey, Address(LogMemoryAddress) + " " + LogMemorySize.ToString(CultureInfo.InvariantCulture));
        }

        if (DotnetStringType != 0)
        {
            yield return Line(DotnetStringKey, Address(DotnetStringType));
        }

        if (Il2CppStringType != 0)
        {
            yield return Line(Il2CppStringKey, Address(Il2CppStringType));
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
                case LogMemoryKey:
                    var parts = value.Split(' ');
                    if (parts.Length >= 2 && TryParseAddress(parts[0], out var ring) &&
                        int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var size))
                    {
                        session.LogMemoryAddress = ring;
                        session.LogMemorySize = size;
                    }

                    break;
                case DotnetStringKey:
                    session.DotnetStringType = TryParseAddress(value, out var dotnet) ? dotnet : 0;
                    break;
                case Il2CppStringKey:
                    session.Il2CppStringType = TryParseAddress(value, out var il2cpp) ? il2cpp : 0;
                    break;
                case DoingKey:
                    var operation = CrashOperation.Parse(value);
                    if (operation != null)
                    {
                        session.Operations.RemoveAll(known => known.Id == operation.Id);
                        session.Operations.Add(operation);
                    }

                    break;
                case DoneKey:
                    if (long.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var done))
                    {
                        foreach (var known in session.Operations)
                        {
                            if (known.Id == done)
                            {
                                known.Done = true;
                            }
                        }
                    }

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

internal sealed class CrashOperation
{
    public long Id { get; set; }

    public string Time { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public bool Done { get; set; }

    public static CrashOperation Parse(string value)
    {
        var parts = (value ?? string.Empty).Split(new[] { ' ' }, 4);
        if (parts.Length < 4 || !long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
        {
            return null;
        }

        return new CrashOperation { Id = id, Time = parts[1] + " " + parts[2], Text = parts[3] };
    }
}
