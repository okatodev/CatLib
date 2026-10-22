using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using CatLib.Diagnostics;

namespace CatLib.CrashWatcher;

internal sealed class CrashReport
{
    public string Folder { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public string Details { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string DumpPath { get; set; }
}

internal static class ReportWriter
{
    public const int KeptReports = 10;
    public const int KeptDumps = 3;
    public const string DumpFileName = "crash.dmp";
    public const string ReportFileName = "report.txt";
    public const string SessionFileName = "session.txt";
    public const string FolderTimeFormat = "yyyy-MM-dd_HH-mm-ss";

    public static CrashReport Write(string reportsDirectory, string sessionFile, CrashSession session, GameExit exit, CrashEventInfo info, string ownDump, WatcherLog log)
    {
        var folder = Path.Combine(reportsDirectory, exit.Exited.ToString(FolderTimeFormat, CultureInfo.InvariantCulture) + "_" + session.ProcessId.ToString(CultureInfo.InvariantCulture));
        Directory.CreateDirectory(folder);

        var bepinexLog = CopyInto(session.BepInExLog, folder, log);
        CopyInto(session.PlayerLog, folder, log);
        CopyInto(sessionFile, folder, log, SessionFileName);

        var fileLines = ReadLines(bepinexLog ?? session.BepInExLog);
        var fromMemory = CrashLogRing.Missing(fileLines, info.MemoryLog);
        info.MemoryLogMissing = fromMemory.Count;
        if (fromMemory.Count > 0)
        {
            AppendMemoryLines(bepinexLog, fromMemory, log);
            fileLines.AddRange(fromMemory);
            log.Write($"{fromMemory.Count} line(s) of the BepInEx log were only in the game's memory, they are added to the report");
        }

        var logTail = CrashText.Tail(fileLines, CrashText.LogTailLines);
        var keepsDump = KeepsDump(exit.ExitCode);
        if (!keepsDump)
        {
            DropDump(ownDump, log);
            ownDump = null;
        }

        var dump = MoveDump(ownDump, folder, log);
        var ownDumpSaved = dump != null;
        dump = dump ?? (keepsDump ? FindDump(session.ProcessId) : null);
        var text = CrashText.Report(session, exit.ExitCode, info, exit.Exited, exit.Played, logTail, dump);
        File.WriteAllText(Path.Combine(folder, ReportFileName), text, Encoding.UTF8);
        if (info.Threads.Count > 0)
        {
            try
            {
                File.WriteAllText(Path.Combine(folder, CrashText.StacksFileName), CrashText.AllStacks(session, info, exit.Exited), Encoding.UTF8);
            }
            catch (Exception exception)
            {
                log.Write($"Could not write {CrashText.StacksFileName}: {exception.Message}");
            }
        }

        Prune(reportsDirectory, folder, log);

        var strings = session.Strings;
        return new CrashReport
        {
            Folder = folder,
            Text = text,
            DumpPath = dump,
            Summary = CrashText.Summary(session, exit.ExitCode, info, exit.Played, strings, ownDumpSaved),
            Details = CrashText.Details(session, exit.ExitCode, info, exit.Exited, strings)
        };
    }

    public const string MemoryLinesMark = "----- CatLib: the lines below were still in the game's memory, the log file did not have them yet -----";

    private static void AppendMemoryLines(string copiedLog, List<string> lines, WatcherLog log)
    {
        if (string.IsNullOrEmpty(copiedLog))
        {
            return;
        }

        try
        {
            var text = new List<string> { string.Empty, MemoryLinesMark };
            text.AddRange(lines);
            File.AppendAllLines(copiedLog, text, Encoding.UTF8);
        }
        catch (Exception exception)
        {
            log.Write($"Could not add the lines from memory to {copiedLog}: {exception.Message}");
        }
    }

    public static List<string> ReadLines(string path)
    {
        var lines = new List<string>();
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return lines;
        }

        try
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new StreamReader(stream, Encoding.UTF8, true))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    lines.Add(line);
                }
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        return lines;
    }

    private static string CopyInto(string source, string folder, WatcherLog log, string name = null)
    {
        if (string.IsNullOrEmpty(source) || !File.Exists(source))
        {
            return null;
        }

        var target = Path.Combine(folder, name ?? Path.GetFileName(source));
        try
        {
            using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var output = new FileStream(target, FileMode.Create, FileAccess.Write))
            {
                input.CopyTo(output);
            }

            return target;
        }
        catch (Exception exception)
        {
            log.Write($"Could not copy {source}: {exception.Message}");
            return null;
        }
    }

    public static bool KeepsDump(uint exitCode) => CrashText.KeepsDump(exitCode);

    private static void DropDump(string path, WatcherLog log)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return;
        }

        try
        {
            File.Delete(path);
            log.Write("The game was closed through its console window, its memory dump is not kept so it takes none of the places for dumps");
        }
        catch (Exception exception)
        {
            log.Write($"Could not remove the memory dump of a game closed through its console window: {exception.Message}");
        }
    }

    private static string MoveDump(string source, string folder, WatcherLog log)
    {
        if (string.IsNullOrEmpty(source) || !File.Exists(source))
        {
            return null;
        }

        var target = Path.Combine(folder, DumpFileName);
        try
        {
            File.Move(source, target);
            return target;
        }
        catch (Exception exception)
        {
            log.Write($"Could not move the memory dump into the report folder: {exception.Message}");
            return source;
        }
    }

    private static string FindDump(int processId)
    {
        try
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CrashDumps");
            if (!Directory.Exists(folder))
            {
                return null;
            }

            return Directory.GetFiles(folder, "*." + processId.ToString(CultureInfo.InvariantCulture) + ".dmp").FirstOrDefault();
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static void Prune(string reportsDirectory, string current, WatcherLog log)
    {
        var folders = Directory.GetDirectories(reportsDirectory)
            .Where(path => File.Exists(Path.Combine(path, ReportFileName)))
            .OrderByDescending(path => Path.GetFileName(path), StringComparer.Ordinal)
            .ToList();
        foreach (var folder in CrashText.DumpsToDrop(folders.Take(KeptReports).ToList(), path => File.Exists(Path.Combine(path, DumpFileName)), KeptDumps))
        {
            var dump = Path.Combine(folder, DumpFileName);
            try
            {
                File.Delete(dump);
            }
            catch (Exception exception)
            {
                log.Write($"Could not remove the old memory dump {dump}: {exception.Message}");
            }
        }

        foreach (var folder in folders.Skip(KeptReports))
        {
            if (string.Equals(folder, current, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                Directory.Delete(folder, true);
            }
            catch (Exception exception)
            {
                log.Write($"Could not remove the old report {folder}: {exception.Message}");
            }
        }
    }
}
