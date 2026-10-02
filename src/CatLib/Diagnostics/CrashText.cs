using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Xml;

namespace CatLib.Diagnostics;

internal sealed class CrashEventInfo
{
    public string Module { get; set; } = string.Empty;

    public string Offset { get; set; } = string.Empty;

    public string ExceptionCode { get; set; } = string.Empty;

    public string ModulePath { get; set; } = string.Empty;

    public string RuntimeMessage { get; set; } = string.Empty;

    public int ThreadId { get; set; }

    public bool FromDebugger { get; set; }

    public bool Early { get; set; }

    public string Access { get; set; } = string.Empty;

    public int HangSeconds { get; set; }

    public bool Hung => HangSeconds > 0;

    public bool HasFault => Module.Length > 0;
}

internal sealed class CrashModule
{
    public CrashModule(string path, ulong start, ulong size)
    {
        Path = path ?? string.Empty;
        Start = start;
        Size = size;
    }

    public string Path { get; }

    public string Name => Path.Substring(Path.LastIndexOfAny(new[] { '\\', '/' }) + 1);

    public ulong Start { get; }

    public ulong Size { get; }
}

internal static class CrashText
{
    public const int LogTailLines = 40;
    public const int EventTailLines = 15;
    public const int DetailEventLines = 6;
    public const int HeadlineLength = 200;
    public const string ApplicationErrorProvider = "Application Error";
    public const string RuntimeProvider = ".NET Runtime";

    public const string ExecuteAccess = "execute";

    public static bool IsNormalExit(uint exitCode) => exitCode == 0;

    public static string AccessText(ulong kind, ulong address)
    {
        var what = kind == 0 ? "read" : kind == 1 ? "write" : kind == 8 ? ExecuteAccess : "access " + kind.ToString(CultureInfo.InvariantCulture);
        return what + " at " + Hex64(address);
    }

    public static string ExitKey(uint exitCode)
    {
        switch (exitCode)
        {
            case 0:
                return "exit.normal";
            case 1:
                return "exit.closedOutside";
            case 0xC0000005:
                return "exit.accessViolation";
            case 0xC0000374:
                return "exit.heapCorruption";
            case 0xC0000409:
                return "exit.failFast";
            case 0xC00000FD:
                return "exit.stackOverflow";
            case 0xC000001D:
                return "exit.illegalInstruction";
            case 0xC0000094:
                return "exit.divideByZero";
            case 0xC000013A:
                return "exit.consoleClosed";
            case 0xE0434352:
                return "exit.dotnetException";
            case 0x80131623:
                return "exit.dotnetFailFast";
            default:
                return "exit.unknown";
        }
    }

    public static string DescribeExitCode(uint exitCode, CrashStrings strings) => (strings ?? CrashStrings.EnglishOnly).Get(ExitKey(exitCode));

    public static string Hex(uint value) => "0x" + value.ToString("X8", CultureInfo.InvariantCulture);

    public static string Hex64(ulong value) => "0x" + value.ToString("x16", CultureInfo.InvariantCulture);

    public static CrashEventInfo Locate(IList<CrashModule> modules, ulong address, uint exceptionCode, int threadId)
    {
        var info = new CrashEventInfo { ExceptionCode = "0x" + exceptionCode.ToString("x8", CultureInfo.InvariantCulture), ThreadId = threadId, FromDebugger = true };
        foreach (var module in modules ?? new List<CrashModule>())
        {
            if (address >= module.Start && address - module.Start < module.Size)
            {
                info.Module = module.Name;
                info.ModulePath = module.Path;
                info.Offset = Hex64(address - module.Start);
                return info;
            }
        }

        info.Module = "?";
        info.Offset = Hex64(address);
        return info;
    }

    public static bool IsManaged(CrashEventInfo info)
    {
        if (info.Module == "?")
        {
            return !info.Access.StartsWith(CrashText.ExecuteAccess, StringComparison.Ordinal);
        }

        var path = info.ModulePath.Replace('/', '\\');
        return string.Equals(info.Module, "coreclr.dll", StringComparison.OrdinalIgnoreCase)
               || string.Equals(info.Module, "clrjit.dll", StringComparison.OrdinalIgnoreCase)
               || path.IndexOf("\\dotnet\\", StringComparison.OrdinalIgnoreCase) >= 0
               || path.IndexOf("\\BepInEx\\", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public static CrashEventInfo WithManagedException(CrashEventInfo info, CrashSession session)
    {
        if (info != null && info.RuntimeMessage.Length == 0 && session != null && session.ExceptionLines.Count > 0)
        {
            info.RuntimeMessage = string.Join("\n", session.ExceptionLines);
        }

        return info;
    }

    public static CrashEventInfo Merge(CrashEventInfo fromDebugger, CrashEventInfo fromEvents)
    {
        if (fromDebugger == null)
        {
            return fromEvents ?? new CrashEventInfo();
        }

        if (fromEvents != null && fromDebugger.RuntimeMessage.Length == 0)
        {
            fromDebugger.RuntimeMessage = fromEvents.RuntimeMessage;
        }

        return fromDebugger;
    }

    public static CrashEventInfo ParseEvents(string xml, int processId, DateTime exitTimeUtc)
    {
        var info = new CrashEventInfo();
        if (string.IsNullOrWhiteSpace(xml))
        {
            return info;
        }

        var document = new XmlDocument();
        try
        {
            document.LoadXml("<Events>" + xml + "</Events>");
        }
        catch (XmlException)
        {
            return info;
        }

        var pidHex = processId.ToString("x", CultureInfo.InvariantCulture);
        foreach (XmlNode eventNode in document.DocumentElement.ChildNodes)
        {
            var provider = Attribute(Find(eventNode, "System", "Provider"), "Name");
            var created = Attribute(Find(eventNode, "System", "TimeCreated"), "SystemTime");
            var data = DataValues(eventNode);
            if (provider == ApplicationErrorProvider && !info.HasFault && data.Count >= 9)
            {
                var pid = data[8].Trim().ToLowerInvariant();
                if (pid.StartsWith("0x", StringComparison.Ordinal))
                {
                    pid = pid.Substring(2);
                }

                if (pid.TrimStart('0') != pidHex.TrimStart('0'))
                {
                    continue;
                }

                info.Module = data[3];
                info.ExceptionCode = Prefixed(data[6]);
                info.Offset = Prefixed(data[7]);
                info.ModulePath = data.Count > 11 ? data[11] : string.Empty;
            }
            else if (provider == RuntimeProvider && info.RuntimeMessage.Length == 0 && data.Count > 0 && IsNear(created, exitTimeUtc))
            {
                info.RuntimeMessage = data[0].Trim();
            }
        }

        return info;
    }

    public static List<string> Tail(IList<string> lines, int count)
    {
        var tail = new List<string>();
        if (lines == null)
        {
            return tail;
        }

        for (var index = Math.Max(0, lines.Count - count); index < lines.Count; index++)
        {
            tail.Add(lines[index]);
        }

        return tail;
    }

    public static string Summary(CrashSession session, uint exitCode, CrashEventInfo info, TimeSpan played, CrashStrings strings, bool dumpSaved = false)
    {
        strings = strings ?? CrashStrings.EnglishOnly;
        var builder = new StringBuilder();
        builder.Append(strings.Format("summary", DescribeExitCode(exitCode, strings), Hex(exitCode)));
        if (info.HasFault)
        {
            builder.Append(' ').Append(strings.Format("where", info.Module + " + " + info.Offset));
        }

        if (played > TimeSpan.Zero)
        {
            builder.Append(' ').Append(strings.Format("ran", Duration(played, strings)));
        }

        if (info.Hung)
        {
            builder.Append(' ').Append(strings.Format("hang", Duration(TimeSpan.FromSeconds(info.HangSeconds), strings)));
        }
        else if (!string.IsNullOrEmpty(session.CleanExit))
        {
            builder.Append(' ').Append(strings.Get("quitting"));
        }

        if (dumpSaved)
        {
            builder.Append(' ').Append(strings.Get("dumpSaved"));
        }

        return builder.ToString();
    }

    public static string ThreadText(CrashSession session, CrashEventInfo info, CrashStrings strings)
    {
        if (info.ThreadId == 0)
        {
            return string.Empty;
        }

        var id = info.ThreadId.ToString(CultureInfo.InvariantCulture);
        if (session.MainThreadId == 0)
        {
            return id;
        }

        return id + ", " + strings.Get(info.ThreadId == session.MainThreadId ? "gameThread" : "otherThread");
    }

    public static string Report(CrashSession session, uint exitCode, CrashEventInfo info, DateTime exitTime, TimeSpan played, IList<string> logTail, string dumpPath = null)
    {
        var english = CrashStrings.EnglishOnly;
        var builder = new StringBuilder();
        builder.AppendLine(Summary(session, exitCode, info, played, english));
        builder.AppendLine();
        builder.AppendLine("Time: " + exitTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        builder.AppendLine("Exit code: " + Hex(exitCode) + ", " + DescribeExitCode(exitCode, english));
        if (info.HasFault)
        {
            var source = !info.FromDebugger ? ", from the Windows event log" : info.Early ? ", seen by the crash watcher when it was raised, the game closed right after" : ", seen by the crash watcher";
            builder.AppendLine("Module: " + info.Module + " + " + info.Offset + ", exception " + info.ExceptionCode + source);
            if (info.Access.Length > 0)
            {
                builder.AppendLine("Access: " + info.Access);
            }
            if (info.ModulePath.Length > 0)
            {
                builder.AppendLine("Module path: " + info.ModulePath);
            }

            if (info.ThreadId != 0)
            {
                builder.AppendLine("Thread: " + ThreadText(session, info, english));
            }
        }
        else if (!info.Hung)
        {
            builder.AppendLine("No crash record: the crash watcher saw no exception and the Windows event log has none for this process.");
        }

        if (info.Hung)
        {
            builder.AppendLine("Hang: the game began to quit at " + session.CleanExit + " and was still running " + info.HangSeconds.ToString(CultureInfo.InvariantCulture) +
                               " s later" + (string.IsNullOrEmpty(dumpPath) ? string.Empty : "; the memory dump shows every thread at the moment it hung"));
        }

        builder.AppendLine("Game: " + session.GameVersion + ", CatLib " + session.CatLibVersion +
                           (string.IsNullOrEmpty(session.WatcherVersion) ? string.Empty : ", crash watcher " + session.WatcherVersion) +
                           ", started " + session.Started + ", language " + session.Language);
        if (session.IsNewerFormat)
        {
            builder.AppendLine("Session format " + session.Format.ToString(CultureInfo.InvariantCulture) + " is newer than this crash watcher reads (" +
                               CrashSession.CurrentFormat.ToString(CultureInfo.InvariantCulture) + "), some details may be missing: update CatLib Crash Watcher");
        }
        if (!string.IsNullOrEmpty(session.GameBuild))
        {
            builder.AppendLine("Game build check: " + session.GameBuild);
        }
        if (!string.IsNullOrEmpty(dumpPath))
        {
            builder.AppendLine("Memory dump: " + dumpPath);
        }

        if (info.RuntimeMessage.Length > 0)
        {
            builder.AppendLine();
            builder.AppendLine(".NET message:");
            foreach (var line in SplitLines(info.RuntimeMessage))
            {
                builder.AppendLine("  " + line);
            }
        }

        builder.AppendLine();
        builder.AppendLine("Mods:");
        foreach (var mod in session.Mods)
        {
            builder.AppendLine("  " + mod);
        }

        builder.AppendLine();
        builder.AppendLine("Last game events:");
        foreach (var line in Tail(session.Events, EventTailLines))
        {
            builder.AppendLine("  " + line);
        }

        builder.AppendLine();
        builder.AppendLine("End of the BepInEx log:");
        foreach (var line in logTail ?? new List<string>())
        {
            builder.AppendLine("  " + line);
        }

        return builder.ToString();
    }

    public static string Details(CrashSession session, uint exitCode, CrashEventInfo info, DateTime exitTime, CrashStrings strings)
    {
        strings = strings ?? CrashStrings.EnglishOnly;
        var builder = new StringBuilder();
        builder.AppendLine(strings.Get("detailTime") + ": " + exitTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        builder.AppendLine(strings.Get("detailExitCode") + ": " + Hex(exitCode));
        if (info.HasFault)
        {
            builder.AppendLine(strings.Get("detailModule") + ": " + info.Module + " + " + info.Offset + ", " + strings.Get("detailException") + " " + info.ExceptionCode);
            var thread = ThreadText(session, info, strings);
            if (thread.Length > 0)
            {
                builder.AppendLine(strings.Get("detailThread") + ": " + thread);
            }
        }

        var headline = RuntimeHeadline(info.RuntimeMessage);
        if (headline.Length > 0)
        {
            builder.AppendLine(".NET: " + headline);
        }

        builder.AppendLine(strings.Get("detailGame") + " " + session.GameVersion + ", CatLib " + session.CatLibVersion);
        if (session.Mods.Count > 0)
        {
            builder.AppendLine(strings.Get("detailMods") + ": " + string.Join(", ", ModNames(session.Mods)));
        }

        var events = Tail(session.Events, DetailEventLines);
        if (events.Count > 0)
        {
            builder.AppendLine(strings.Get("detailEvents") + ":");
            foreach (var line in events)
            {
                builder.AppendLine("  " + line);
            }
        }

        return builder.ToString().TrimEnd();
    }

    public static string RuntimeHeadline(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return string.Empty;
        }

        var lines = SplitLines(message);
        foreach (var line in lines)
        {
            if (line.StartsWith("Exception Info:", StringComparison.OrdinalIgnoreCase))
            {
                return Shorten(line.Substring("Exception Info:".Length).Trim(), HeadlineLength);
            }
        }

        foreach (var line in lines)
        {
            if (line.StartsWith("Description:", StringComparison.OrdinalIgnoreCase))
            {
                return Shorten(line.Substring("Description:".Length).Trim(), HeadlineLength);
            }
        }

        return Shorten(lines.Count == 0 ? string.Empty : lines[0], HeadlineLength);
    }

    public static List<string> ModNames(IEnumerable<string> mods)
    {
        var names = new List<string>();
        foreach (var mod in mods)
        {
            var bracket = mod.LastIndexOf(" (", StringComparison.Ordinal);
            names.Add(bracket > 0 ? mod.Substring(0, bracket) : mod);
        }

        return names;
    }

    public static List<string> SplitLines(string text)
    {
        var lines = new List<string>();
        foreach (var line in (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            var trimmed = line.TrimEnd();
            if (trimmed.Length > 0)
            {
                lines.Add(trimmed);
            }
        }

        return lines;
    }

    public static string Shorten(string text, int length) =>
        text == null || text.Length <= length ? text ?? string.Empty : text.Substring(0, length - 3) + "...";

    public static string Duration(TimeSpan span, CrashStrings strings)
    {
        strings = strings ?? CrashStrings.EnglishOnly;
        if (span.TotalHours >= 1)
        {
            return strings.Format("hoursMinutes", ((int)span.TotalHours).ToString(CultureInfo.InvariantCulture), span.Minutes.ToString(CultureInfo.InvariantCulture));
        }

        if (span.TotalMinutes >= 1)
        {
            return strings.Format("minutes", ((int)span.TotalMinutes).ToString(CultureInfo.InvariantCulture));
        }

        return strings.Format("seconds", ((int)span.TotalSeconds).ToString(CultureInfo.InvariantCulture));
    }

    private static string Prefixed(string value)
    {
        var trimmed = (value ?? string.Empty).Trim();
        return trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? trimmed : "0x" + trimmed;
    }

    private static bool IsNear(string systemTime, DateTime exitTimeUtc)
    {
        if (!DateTime.TryParse(systemTime, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var created))
        {
            return false;
        }

        return Math.Abs((created - exitTimeUtc).TotalSeconds) <= 120;
    }

    private static XmlNode Find(XmlNode node, string first, string second)
    {
        var child = Child(node, first);
        return child == null ? null : Child(child, second);
    }

    private static XmlNode Child(XmlNode node, string name)
    {
        foreach (XmlNode child in node.ChildNodes)
        {
            if (child.LocalName == name)
            {
                return child;
            }
        }

        return null;
    }

    private static string Attribute(XmlNode node, string name) => node?.Attributes?[name]?.Value ?? string.Empty;

    private static List<string> DataValues(XmlNode eventNode)
    {
        var values = new List<string>();
        var data = Child(eventNode, "EventData");
        if (data == null)
        {
            return values;
        }

        foreach (XmlNode child in data.ChildNodes)
        {
            if (child.LocalName == "Data")
            {
                values.Add(child.InnerText ?? string.Empty);
            }
        }

        return values;
    }
}
