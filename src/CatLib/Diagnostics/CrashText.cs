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

    public bool HasFault => Module.Length > 0;
}

internal static class CrashText
{
    public const int LogTailLines = 40;
    public const int EventTailLines = 15;
    public const int DetailEventLines = 6;
    public const int HeadlineLength = 200;
    public const string ApplicationErrorProvider = "Application Error";
    public const string RuntimeProvider = ".NET Runtime";

    public static readonly string[] EnglishPhrases =
    {
        "Your game got meowed :(",
        "Cats don't like water, and the game didn't like this",
        "Someone knocked the game off the table",
        "A parcel fell off the top shelf",
        "A cat sat on the keyboard",
        "The game curled up and fell asleep",
        "The boat sailed off without the game",
        "Nine lives, and one of them is gone",
        "The yarn got all tangled up",
        "Hiss! Something went wrong",
        "The game chased a laser dot and got lost",
        "Oops, the mail got wet",
        "The game is hiding under the sofa",
        "Somebody stepped on a tail"
    };

    public static readonly string[] RussianPhrases =
    {
        "Игру замяукали :(",
        "Коты не любят воду, а игра не любит вот это",
        "Кто-то смахнул игру со стола",
        "С верхней полки упала посылка",
        "На клавиатуру сел кот",
        "Игра свернулась клубочком и уснула",
        "Корабль уплыл без игры",
        "Из девяти жизней одна потрачена",
        "Клубок совсем запутался",
        "Шшш! Что-то пошло не так",
        "Игра погналась за лазерной точкой и потерялась",
        "Ой, почта промокла",
        "Игра спряталась под диван",
        "Кто-то наступил на хвост"
    };

    public static string Phrase(Random random, bool russian)
    {
        var phrases = russian ? RussianPhrases : EnglishPhrases;
        return phrases[random.Next(phrases.Length)];
    }

    public static string WindowTitle(bool russian) => russian ? "Cat Mail Co: отчёт о падении" : "Cat Mail Co: crash report";

    public static string OpenFolderButton(bool russian) => russian ? "Открыть папку отчёта" : "Open the report folder";

    public static string CopyButton(bool russian) => russian ? "Скопировать отчёт" : "Copy the report";

    public static string ShowDetails(bool russian) => russian ? "Подробности" : "Details";

    public static string HideDetails(bool russian) => russian ? "Скрыть подробности" : "Hide details";

    public static string Footer(bool russian, string folder) =>
        (russian ? "Отчёт и логи сохранены: " : "The report and logs are saved in ") + folder;

    public static string HelpLine(bool russian) => russian
        ? "Если это повторяется, отправьте папку отчёта авторам модов."
        : "If this keeps happening, send the report folder to the mod authors.";

    public static string Copied(bool russian) => russian ? "Отчёт скопирован" : "The report is copied";

    public static bool IsNormalExit(uint exitCode) => exitCode == 0;

    public static string DescribeExitCode(uint exitCode, bool russian)
    {
        switch (exitCode)
        {
            case 0:
                return russian ? "обычный выход" : "normal exit";
            case 1:
                return russian ? "игру закрыли извне, например через диспетчер задач" : "the game was closed from outside, for example from the Task Manager";
            case 0xC0000005:
                return russian ? "обращение по неверному адресу памяти (access violation)" : "access to an invalid memory address (access violation)";
            case 0xC0000374:
                return russian ? "повреждена куча памяти (heap corruption)" : "the memory heap is damaged (heap corruption)";
            case 0xC0000409:
                return russian ? "аварийное завершение (fail fast или переполнение буфера на стеке)" : "fail fast or a stack buffer overrun";
            case 0xC00000FD:
                return russian ? "переполнение стека (stack overflow)" : "stack overflow";
            case 0xC000001D:
                return russian ? "недопустимая инструкция процессора" : "illegal instruction";
            case 0xC0000094:
                return russian ? "деление на ноль" : "integer division by zero";
            case 0xC000013A:
                return russian ? "игру закрыли через окно консоли" : "the game was closed through its console window";
            case 0xE0434352:
                return russian ? "необработанное исключение .NET" : "an unhandled .NET exception";
            case 0x80131623:
                return russian ? "аварийное завершение .NET (FailFast)" : ".NET fail fast";
            default:
                return russian ? "неизвестная причина" : "unknown reason";
        }
    }

    public static string Hex(uint value) => "0x" + value.ToString("X8", CultureInfo.InvariantCulture);

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

    public static string Summary(CrashSession session, uint exitCode, CrashEventInfo info, TimeSpan played)
    {
        var russian = session.IsRussian;
        var builder = new StringBuilder();
        builder.Append(russian ? "Игра закрылась неожиданно: " : "The game closed unexpectedly: ");
        builder.Append(DescribeExitCode(exitCode, russian));
        builder.Append(" (").Append(Hex(exitCode)).Append(").");
        if (info.HasFault)
        {
            builder.Append(russian ? " Место: " : " Where: ").Append(info.Module).Append(" + ").Append(info.Offset).Append('.');
        }

        if (played > TimeSpan.Zero)
        {
            builder.Append(russian ? " Игра проработала " : " The game ran for ").Append(Duration(played, russian)).Append('.');
        }

        if (!string.IsNullOrEmpty(session.CleanExit))
        {
            builder.Append(russian ? " Это случилось уже при выходе из игры." : " It happened while the game was quitting.");
        }

        return builder.ToString();
    }

    public static string Report(CrashSession session, uint exitCode, CrashEventInfo info, DateTime exitTime, TimeSpan played, IList<string> logTail, string dumpPath = null)
    {
        var russian = session.IsRussian;
        var builder = new StringBuilder();
        builder.AppendLine(Summary(session, exitCode, info, played));
        builder.AppendLine();
        builder.AppendLine((russian ? "Время: " : "Time: ") + exitTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        builder.AppendLine((russian ? "Код выхода: " : "Exit code: ") + Hex(exitCode) + ", " + DescribeExitCode(exitCode, russian));
        if (info.HasFault)
        {
            builder.AppendLine((russian ? "Модуль: " : "Module: ") + info.Module + " + " + info.Offset + ", " + (russian ? "исключение " : "exception ") + info.ExceptionCode);
            if (info.ModulePath.Length > 0)
            {
                builder.AppendLine((russian ? "Путь модуля: " : "Module path: ") + info.ModulePath);
            }
        }
        else
        {
            builder.AppendLine(russian ? "Запись о падении в журнале Windows не найдена." : "No crash record was found in the Windows event log.");
        }

        builder.AppendLine((russian ? "Игра: " : "Game: ") + session.GameVersion + ", CatLib " + session.CatLibVersion + ", " + (russian ? "запущена " : "started ") + session.Started);
        if (!string.IsNullOrEmpty(dumpPath))
        {
            builder.AppendLine((russian ? "Дамп памяти: " : "Memory dump: ") + dumpPath);
        }

        if (info.RuntimeMessage.Length > 0)
        {
            builder.AppendLine();
            builder.AppendLine(russian ? "Сообщение .NET:" : ".NET message:");
            foreach (var line in SplitLines(info.RuntimeMessage))
            {
                builder.AppendLine("  " + line);
            }
        }

        builder.AppendLine();
        builder.AppendLine(russian ? "Моды:" : "Mods:");
        foreach (var mod in session.Mods)
        {
            builder.AppendLine("  " + mod);
        }

        builder.AppendLine();
        builder.AppendLine(russian ? "Последние события игры:" : "Last game events:");
        foreach (var line in Tail(session.Events, EventTailLines))
        {
            builder.AppendLine("  " + line);
        }

        builder.AppendLine();
        builder.AppendLine(russian ? "Конец лога BepInEx:" : "End of the BepInEx log:");
        foreach (var line in logTail ?? new List<string>())
        {
            builder.AppendLine("  " + line);
        }

        return builder.ToString();
    }

    public static string Details(CrashSession session, uint exitCode, CrashEventInfo info, DateTime exitTime)
    {
        var russian = session.IsRussian;
        var builder = new StringBuilder();
        builder.AppendLine((russian ? "Время: " : "Time: ") + exitTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        builder.AppendLine((russian ? "Код выхода: " : "Exit code: ") + Hex(exitCode));
        if (info.HasFault)
        {
            builder.AppendLine((russian ? "Модуль: " : "Module: ") + info.Module + " + " + info.Offset + ", " + (russian ? "исключение " : "exception ") + info.ExceptionCode);
        }

        var headline = RuntimeHeadline(info.RuntimeMessage);
        if (headline.Length > 0)
        {
            builder.AppendLine(".NET: " + headline);
        }

        builder.AppendLine((russian ? "Игра " : "Game ") + session.GameVersion + ", CatLib " + session.CatLibVersion);
        if (session.Mods.Count > 0)
        {
            builder.AppendLine((russian ? "Моды: " : "Mods: ") + string.Join(", ", ModNames(session.Mods)));
        }

        var events = Tail(session.Events, DetailEventLines);
        if (events.Count > 0)
        {
            builder.AppendLine(russian ? "Последние события:" : "Last events:");
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

    public static string Duration(TimeSpan span, bool russian)
    {
        if (span.TotalHours >= 1)
        {
            return ((int)span.TotalHours).ToString(CultureInfo.InvariantCulture) + (russian ? " ч " : " h ") +
                   span.Minutes.ToString(CultureInfo.InvariantCulture) + (russian ? " мин" : " min");
        }

        if (span.TotalMinutes >= 1)
        {
            return ((int)span.TotalMinutes).ToString(CultureInfo.InvariantCulture) + (russian ? " мин" : " min");
        }

        return ((int)span.TotalSeconds).ToString(CultureInfo.InvariantCulture) + (russian ? " с" : " s");
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
