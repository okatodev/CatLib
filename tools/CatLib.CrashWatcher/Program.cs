using System;
using System.Globalization;
using System.IO;
using CatLib.Diagnostics;

namespace CatLib.CrashWatcher;

internal static class Program
{
    public const string ProcessArgument = "--pid";
    public const string SessionArgument = "--session";
    public const string PreviewArgument = "--preview";
    public const string DumpArgument = "--dump";
    public const string LogFileName = "watcher.log";

    [STAThread]
    private static int Main(string[] args)
    {
        var processId = 0;
        string sessionFile = null;
        var preview = false;
        var dump = false;
        for (var index = 0; index < args.Length; index++)
        {
            if (args[index] == ProcessArgument && index + 1 < args.Length)
            {
                int.TryParse(args[++index], NumberStyles.Integer, CultureInfo.InvariantCulture, out processId);
            }
            else if (args[index] == SessionArgument && index + 1 < args.Length)
            {
                sessionFile = args[++index];
            }
            else if (args[index] == PreviewArgument)
            {
                preview = true;
            }
            else if (args[index] == DumpArgument)
            {
                dump = true;
            }
        }

        if (string.IsNullOrEmpty(sessionFile) || (processId <= 0 && !preview))
        {
            return 2;
        }

        var reportsDirectory = Path.GetDirectoryName(Path.GetFullPath(sessionFile)) ?? ".";
        var log = new WatcherLog(Path.Combine(reportsDirectory, LogFileName));
        try
        {
            return preview ? Preview(sessionFile, reportsDirectory, log) : Watch(processId, sessionFile, reportsDirectory, dump, log);
        }
        catch (Exception exception)
        {
            log.Write($"The crash watcher failed: {exception}");
            return 1;
        }
    }

    private static int Watch(int processId, string sessionFile, string reportsDirectory, bool dump, WatcherLog log)
    {
        var dumpPath = Path.Combine(reportsDirectory, "crash_" + processId.ToString(CultureInfo.InvariantCulture) + ".dmp");
        var outcome = dump ? GameDebugger.Watch(processId, dumpPath, log) : null;
        var exit = outcome?.Exit ?? GameProcess.WaitForExit(processId);
        var session = CrashSession.Parse(ReportWriter.ReadLines(sessionFile));
        if (session.ProcessId == 0)
        {
            session.ProcessId = processId;
        }

        if (CrashText.IsNormalExit(exit.ExitCode))
        {
            TryDelete(sessionFile, log);
            TryDelete(dumpPath, log);
            return 0;
        }

        log.Write($"The game (process {processId}) exited with {CrashText.Hex(exit.ExitCode)}{(outcome == null ? string.Empty : $" after {outcome.PassedExceptions} handled exception(s)")}, writing a report");
        var expectFault = outcome?.Crash == null && CrashEvents.LooksLikeFault(exit.ExitCode);
        var events = CrashEvents.Collect(processId, exit.Exited.ToUniversalTime(), expectFault, log);
        var info = CrashText.WithManagedException(CrashText.Merge(outcome?.Crash, events), session);
        var report = ReportWriter.Write(reportsDirectory, sessionFile, session, exit, info, outcome?.DumpPath, log);
        TryDelete(sessionFile, log);
        log.Write($"Report written to {report.Folder}");

        var strings = session.Strings;
        new CrashWindow(report, strings, exit.ImagePath, log).Show(strings.Phrase(new Random()));
        return 0;
    }

    private static int Preview(string sessionFile, string reportsDirectory, WatcherLog log)
    {
        var session = CrashSession.Parse(ReportWriter.ReadLines(sessionFile));
        var exit = new GameExit { ExitCode = 0xC0000005, Started = DateTime.Now.AddMinutes(-42), Exited = DateTime.Now };
        var info = new CrashEventInfo { Module = "example.dll", Offset = "0x0000000000001234", ExceptionCode = "0xc0000005" };
        var strings = session.Strings;
        info.ThreadId = session.MainThreadId;
        var report = new CrashReport
        {
            Folder = reportsDirectory,
            Summary = CrashText.Summary(session, exit.ExitCode, info, exit.Played, strings, true),
            Details = CrashText.Details(session, exit.ExitCode, info, exit.Exited, strings)
        };
        report.Text = CrashText.Report(session, exit.ExitCode, info, exit.Exited, exit.Played, new string[0]);
        new CrashWindow(report, strings, string.Empty, log).Show(strings.Phrase(new Random()));
        return 0;
    }

    private static void TryDelete(string path, WatcherLog log)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception)
        {
            log.Write($"Could not remove {path}: {exception.Message}");
        }
    }
}
