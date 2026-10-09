using System;
using System.Globalization;
using System.IO;
using CatLib.CrashWatcher.Symbols;
using CatLib.Diagnostics;

namespace CatLib.CrashWatcher;

internal static class Program
{
    public const string ProcessArgument = "--pid";
    public const string SessionArgument = "--session";
    public const string PreviewArgument = "--preview";
    public const string DumpArgument = "--dump";
    public const string LogFileName = "watcher.log";
    public const string InteropFolder = "interop";
    public const string SymbolsFolder = "Symbols";
    public const string NoSymbolsArgument = "--no-symbols";

    public static string WatcherVersion
    {
        get
        {
            var version = typeof(Program).Assembly.GetName().Version;
            return version == null ? "unknown" : version.Major + "." + version.Minor + "." + version.Build;
        }
    }

    [STAThread]
    private static int Main(string[] args)
    {
        var processId = 0;
        string sessionFile = null;
        var preview = false;
        var dump = false;
        var symbols = true;
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
            else if (args[index] == NoSymbolsArgument)
            {
                symbols = false;
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
            return preview ? Preview(sessionFile, reportsDirectory, log) : Watch(processId, sessionFile, reportsDirectory, dump, symbols, log);
        }
        catch (Exception exception)
        {
            log.Write($"The crash watcher failed: {exception}");
            return 1;
        }
    }

    private static int Watch(int processId, string sessionFile, string reportsDirectory, bool dump, bool downloadSymbols, WatcherLog log)
    {
        var store = new SymbolStore(Path.GetFullPath(Path.Combine(reportsDirectory, "..", SymbolsFolder)), log);
        using (var names = new CodeNames(InteropDirectory(reportsDirectory), log, store) { Undecorate = Undecorate })
        {
            if (downloadSymbols)
            {
                SymbolPrefetch.Start(processId, store, names, log);
            }

            return Watch(processId, sessionFile, reportsDirectory, dump, names, log);
        }
    }

    private static string Undecorate(string name)
    {
        if (string.IsNullOrEmpty(name) || name[0] != '?')
        {
            return name;
        }

        try
        {
            var output = new System.Text.StringBuilder(1024);
            return NativeMethods.UnDecorateSymbolName(name, output, (uint)output.Capacity, NativeMethods.UndecorateNameOnly) > 0 ? output.ToString() : CodeNames.Readable(name);
        }
        catch (Exception exception) when (exception is DllNotFoundException || exception is EntryPointNotFoundException)
        {
            return CodeNames.Readable(name);
        }
    }

    private static string InteropDirectory(string reportsDirectory) => Path.GetFullPath(Path.Combine(reportsDirectory, "..", "..", InteropFolder));

    private static int Watch(int processId, string sessionFile, string reportsDirectory, bool dump, CodeNames names, WatcherLog log)
    {
        var dumpPath = Path.Combine(reportsDirectory, "crash_" + processId.ToString(CultureInfo.InvariantCulture) + ".dmp");
        var hangDumpPath = Path.ChangeExtension(dumpPath, ".hang.dmp");
        var hang = new HangWatcher(processId, sessionFile, dump ? hangDumpPath : null, names, log);
        hang.Start();
        var outcome = dump ? GameDebugger.Watch(processId, dumpPath, names, log) : null;
        var exit = outcome?.Exit ?? GameProcess.WaitForExit(processId);
        hang.Stop();
        var session = CrashSession.Parse(ReportWriter.ReadLines(sessionFile));
        session.WatcherVersion = WatcherVersion;
        if (session.ProcessId == 0)
        {
            session.ProcessId = processId;
        }

        if (session.IsNewerFormat)
        {
            log.Write($"The session file has format {session.Format}, this crash watcher {WatcherVersion} reads format {CrashSession.CurrentFormat}: update CatLib Crash Watcher, the report may miss details");
        }

        var quitSeconds = QuitSeconds(session, exit);
        if (quitSeconds >= 0)
        {
            log.Write($"The game (process {processId}) closed with {CrashText.Hex(exit.ExitCode)} {quitSeconds} s after it began to quit");
        }

        if (CrashText.IsNormalExit(exit.ExitCode))
        {
            if (hang.Hung)
            {
                log.Write($"The game (process {processId}) closed normally after all, the dump of the hang is not kept");
            }

            TryDelete(sessionFile, log);
            TryDelete(dumpPath, log);
            TryDelete(hangDumpPath, log);
            return 0;
        }

        log.Write($"The game (process {processId}) exited with {CrashText.Hex(exit.ExitCode)}{(outcome == null ? string.Empty : $" after {outcome.PassedExceptions} handled exception(s)")}{(hang.Hung ? ", it was hung while quitting" : string.Empty)}, writing a report");
        var expectFault = outcome?.Crash == null && CrashEvents.LooksLikeFault(exit.ExitCode);
        var events = CrashEvents.Collect(processId, exit.Exited.ToUniversalTime(), expectFault, log);
        var info = CrashText.WithManagedException(CrashText.Merge(outcome?.Crash, events), session);
        var reportDump = outcome?.DumpPath;
        if (hang.Hung && !info.HasFault)
        {
            info.HangSeconds = quitSeconds > 0 ? quitSeconds : hang.HungSeconds(exit.Exited);
            reportDump = reportDump ?? hang.DumpPath;
            if (info.Threads.Count == 0)
            {
                info.Threads.AddRange(hang.Stacks);
            }
        }

        NameCrash(info, exit, names, log);
        info.GameMethod = CrashText.NearestGameMethod(info);
        info.CalledFrom = CrashText.CalledFrom(info);
        if (info.CalledFrom.Length > 0)
        {
            log.Write($"The crash in {info.Module} was called from {info.CalledFrom}");
        }

        var report = ReportWriter.Write(reportsDirectory, sessionFile, session, exit, info, reportDump, log);
        TryDelete(sessionFile, log);
        if (!string.Equals(report.DumpPath, hangDumpPath, StringComparison.OrdinalIgnoreCase))
        {
            TryDelete(hangDumpPath, log);
        }

        log.Write($"Report written to {report.Folder}");

        var strings = session.Strings;
        new CrashWindow(report, strings, exit.ImagePath, log).Show(strings.Phrase(new Random()));
        return 0;
    }

    private static int Preview(string sessionFile, string reportsDirectory, WatcherLog log)
    {
        var session = CrashSession.Parse(ReportWriter.ReadLines(sessionFile));
        session.WatcherVersion = WatcherVersion;
        var exit = new GameExit { ExitCode = 0xC0000005, Started = DateTime.Now.AddMinutes(-42), Exited = DateTime.Now };
        var info = new CrashEventInfo { Module = "example.dll", Offset = "0x0000000000001234", ExceptionCode = "0xc0000005", Method = "ExampleType.ExampleMethod(int) + 0x34" };
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

    private static void NameCrash(CrashEventInfo info, GameExit exit, CodeNames names, WatcherLog log)
    {
        if (!info.HasFault || info.Module == "?" || info.Method.Length > 0)
        {
            return;
        }

        var path = info.ModulePath;
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            var gameDirectory = string.IsNullOrEmpty(exit.ImagePath) ? null : Path.GetDirectoryName(exit.ImagePath);
            path = gameDirectory == null ? null : Path.Combine(gameDirectory, info.Module);
        }

        var offset = info.Offset.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? info.Offset.Substring(2) : info.Offset;
        if (path == null || !ulong.TryParse(offset, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
        {
            return;
        }

        try
        {
            info.Method = names.Name(path, value) ?? string.Empty;
        }
        catch (Exception exception)
        {
            log.Write($"Naming the method of the crash failed: {exception.Message}");
            return;
        }

        if (info.Method.Length > 0)
        {
            log.Write($"The crash in {info.Module} + {info.Offset} is in {info.Method}");
        }
    }

    private static int QuitSeconds(CrashSession session, GameExit exit)
    {
        if (string.IsNullOrEmpty(session.CleanExit) ||
            !DateTime.TryParseExact(session.CleanExit, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var began))
        {
            return -1;
        }

        return Math.Max(0, (int)Math.Round((exit.Exited - began).TotalSeconds));
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
