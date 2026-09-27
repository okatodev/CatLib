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
}

internal static class ReportWriter
{
    public const int KeptReports = 10;
    public const string ReportFileName = "report.txt";
    public const string SessionFileName = "session.txt";
    public const string FolderTimeFormat = "yyyy-MM-dd_HH-mm-ss";

    public static CrashReport Write(string reportsDirectory, string sessionFile, CrashSession session, GameExit exit, CrashEventInfo info, WatcherLog log)
    {
        var folder = Path.Combine(reportsDirectory, exit.Exited.ToString(FolderTimeFormat, CultureInfo.InvariantCulture) + "_" + session.ProcessId.ToString(CultureInfo.InvariantCulture));
        Directory.CreateDirectory(folder);

        var bepinexLog = CopyInto(session.BepInExLog, folder, log);
        CopyInto(session.PlayerLog, folder, log);
        CopyInto(sessionFile, folder, log, SessionFileName);

        var logTail = CrashText.Tail(ReadLines(bepinexLog ?? session.BepInExLog), CrashText.LogTailLines);
        var dump = FindDump(session.ProcessId);
        var text = CrashText.Report(session, exit.ExitCode, info, exit.Exited, exit.Played, logTail, dump);
        File.WriteAllText(Path.Combine(folder, ReportFileName), text, Encoding.UTF8);
        Prune(reportsDirectory, folder, log);

        return new CrashReport
        {
            Folder = folder,
            Text = text,
            Summary = CrashText.Summary(session, exit.ExitCode, info, exit.Played),
            Details = CrashText.Details(session, exit.ExitCode, info, exit.Exited)
        };
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
