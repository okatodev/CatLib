using System;
using System.Collections.Generic;
using System.Linq;
using CatLib.Diagnostics;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.Crash;

public sealed class CrashTextTest : TestCase
{
    public const int ProcessId = 0xa95c;

    public override string Suite => "Crash";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var english = CrashStrings.EnglishOnly;
        var random = new Random(7);
        var seen = new HashSet<string>();
        for (var index = 0; index < 200; index++)
        {
            seen.Add(english.Phrase(random));
        }

        Assert.Equal(CrashStrings.PhraseCount, seen.Count, "Every phrase shows up and phrases change from crash to crash");
        var russian = new CrashStrings(CatLib.UI.UiText.Catalog.Keys("ru").Where(key => key.StartsWith(CrashStrings.CatalogPrefix))
            .ToDictionary(key => key.Substring(CrashStrings.CatalogPrefix.Length), key => CatLib.UI.UiText.Catalog.FindExact(key, "ru")));
        Assert.Equal("Кто-то наступил на хвост", russian.Get("phrase.14"), "Russian phrases come from the catalog");
        foreach (var key in CrashStrings.Keys)
        {
            Assert.NotNull(CatLib.UI.UiText.Catalog.FindExact(CrashStrings.CatalogPrefix + key, "en"), "The catalog has the English text of crash." + key);
            Assert.Equal(CrashStrings.English[key], CatLib.UI.UiText.Catalog.FindExact(CrashStrings.CatalogPrefix + key, "en"), "The watcher's English matches the catalog for crash." + key);
        }

        Assert.True(CrashText.IsNormalExit(0), "Zero is a normal exit");
        Assert.False(CrashText.IsNormalExit(0xC0000005), "An access violation is not");
        Assert.Equal("0xC0000005", CrashText.Hex(0xC0000005), "Hex exit code");
        Assert.True(CrashText.DescribeExitCode(0xC0000005, english).Contains("access violation"), "Access violation is named");
        Assert.True(CrashText.DescribeExitCode(0xE0434352, english).Contains(".NET"), "A .NET exception is named");
        Assert.Equal("unknown reason", CrashText.DescribeExitCode(0x12345678, english), "Unknown codes");

        var exit = new DateTime(2026, 9, 27, 12, 5, 0, DateTimeKind.Utc);
        var info = CrashText.ParseEvents(SampleEvents(exit), ProcessId, exit);
        Assert.True(info.HasFault, "The Application Error of this process is found");
        Assert.Equal("steamclient64.dll", info.Module, "Faulting module");
        Assert.Equal("0x0000000000a86a9c", info.Offset, "Faulting offset");
        Assert.Equal("0xc0000005", info.ExceptionCode, "Exception code");
        Assert.Equal("C:\\Steam\\steamclient64.dll", info.ModulePath, "Module path");
        Assert.True(info.RuntimeMessage.StartsWith("Application: CatMailCo.exe"), "The .NET Runtime message close to the exit");
        Assert.Equal("System.InvalidOperationException: CatLib test crash", CrashText.RuntimeHeadline(info.RuntimeMessage), "The exception line is the headline");

        var other = CrashText.ParseEvents(SampleEvents(exit), 0x9999, exit.AddHours(1));
        Assert.False(other.HasFault, "Another process is ignored");
        Assert.Equal(string.Empty, other.RuntimeMessage, "A far .NET message is ignored");
        Assert.False(CrashText.ParseEvents("<broken", ProcessId, exit).HasFault, "Broken XML gives no fault");
        Assert.False(CrashText.ParseEvents(string.Empty, ProcessId, exit).HasFault, "No events give no fault");

        var session = CrashSession.Parse(new[]
        {
            "catlib=0.6.0", "game=1.0.4", "started=2026-09-27 11:20:00", "pid=43356", "language=en",
            "mod=Boat Tweaks 0.2.0 (catlib.boattweaks)", "mod=Shelf Labels 0.1.0 (catlib.shelflabels)",
            "event=12:00:01 Level.LevelLoadStarted", "event=12:04:59 Level.LevelUnloaded", "clean=2026-09-27 12:05:00"
        });
        var played = TimeSpan.FromMinutes(45);
        var summary = CrashText.Summary(session, 0xC0000005, info, played, english, true);
        Assert.True(summary.Contains("steamclient64.dll + 0x0000000000a86a9c"), "The summary names the place");
        Assert.True(summary.Contains("45 min"), "The summary says how long the game ran");
        Assert.True(summary.Contains("while the game was quitting"), "The summary notes a crash while quitting");
        Assert.True(summary.Contains("memory dump"), "The summary mentions a saved dump");
        Assert.True(CrashText.Summary(session, 0xC0000005, info, played, russian).Contains("45 мин"), "A Russian summary");

        var report = CrashText.Report(session, 0xC0000005, info, exit.ToLocalTime(), played, new[] { "last log line" }, "C:\\Dumps\\CatMailCo.exe.43356.dmp");
        foreach (var part in new[] { "Exit code: 0xC0000005", "Boat Tweaks 0.2.0", "Level.LevelUnloaded", "last log line", "Memory dump:", "   at CatLib.Test.Crash()" })
        {
            Assert.True(report.Contains(part), $"The report contains \"{part}\"");
        }

        var details = CrashText.Details(session, 0xC0000005, info, exit.ToLocalTime(), english);
        Assert.True(details.Contains("Mods: Boat Tweaks 0.2.0, Shelf Labels 0.1.0"), "Details list the mods without ids");
        Assert.AtLeast(1, details.Split('\n').Length, "Details are not empty");
        Assert.True(details.Split('\n').Length <= 16, "Details stay short enough for a small window");

        var noFault = CrashText.Report(session, 1, new CrashEventInfo(), exit, TimeSpan.Zero, null);
        Assert.True(noFault.Contains("No crash record"), "A report without a crash record says so");

        var hung = new CrashEventInfo { HangSeconds = 49 };
        var hangSummary = CrashText.Summary(session, 1, hung, played, english, true);
        Assert.True(hangSummary.Contains("stopped responding while quitting") && hangSummary.Contains("49 s"), "The summary says the game hung while quitting and for how long");
        Assert.False(hangSummary.Contains("It happened while the game was quitting"), "A hang replaces the plain quitting note");
        Assert.True(CrashText.Summary(session, 1, hung, played, russian).Contains("зависла при выходе"), "A Russian hang summary");
        var hangReport = CrashText.Report(session, 1, hung, exit.ToLocalTime(), played, null, "C:\\Reports\\crash.dmp");
        Assert.True(hangReport.Contains("Hang: the game began to quit at 2026-09-27 12:05:00 and was still running 49 s later"), "The report dates the hang");
        Assert.True(hangReport.Contains("every thread"), "The report says what the dump of a hang shows");
        Assert.False(hangReport.Contains("No crash record"), "A hang is a record of its own");
        Assert.Equal("1 h 5 min", CrashText.Duration(TimeSpan.FromMinutes(65), english), "Hours");
        Assert.Equal("12 с", CrashText.Duration(TimeSpan.FromSeconds(12), russian), "Russian seconds");

        var modules = new List<CrashModule>
        {
            new CrashModule("D:\\Game\\GameAssembly.dll", 0x7FF800000000, 0x5000000),
            new CrashModule("C:\\Steam\\steamclient64.dll", 0x7FF900000000, 0x2000000)
        };
        var located = CrashText.Locate(modules, 0x7FF900A86A9C, 0xC0000005, 1234);
        Assert.Equal("steamclient64.dll", located.Module, "The crash address is inside steamclient64.dll");
        Assert.Equal("0x0000000000a86a9c", located.Offset, "Offset from the module start, like the event log writes it");
        Assert.Equal("0xc0000005", located.ExceptionCode, "Exception code");
        Assert.True(located.FromDebugger, "Seen by the watcher");
        var unknown = CrashText.Locate(modules, 0x1234, 0xC0000005, 1);
        Assert.Equal("?", unknown.Module, "An address outside every module");
        var merged = CrashText.Merge(located, info);
        Assert.Equal("steamclient64.dll", merged.Module, "The watcher's own record wins");
        Assert.True(merged.RuntimeMessage.StartsWith("Application: CatMailCo.exe"), "The .NET message still comes from the event log");
        Assert.Equal("execute at 0x0000000000000010", CrashText.AccessText(8, 0x10), "An access violation that jumped to a bad address");
        Assert.Equal("read at 0x0000000000000000", CrashText.AccessText(0, 0), "A null read");
        Assert.False(CrashText.IsManaged(located), "A crash in steamclient64.dll is native");
        Assert.True(CrashText.IsManaged(new CrashEventInfo { Module = "?", Access = "read at 0x0000000000000000" }), "A null read in code outside every module is managed code that .NET turns into an exception");
        Assert.False(CrashText.IsManaged(new CrashEventInfo { Module = "?", Access = "execute at 0x0000000000000010" }), "A jump to a bad address is a crash");
        Assert.True(CrashText.IsManaged(new CrashEventInfo { Module = "coreclr.dll", ModulePath = "D:\\Game\\dotnet\\coreclr.dll" }), "The .NET runtime handles its own exceptions");
        Assert.True(CrashText.IsManaged(new CrashEventInfo { Module = "System.Private.CoreLib.dll", ModulePath = "D:\\Game\\dotnet\\System.Private.CoreLib.dll" }), "Precompiled .NET code too");

        var managed = CrashSession.Parse(CatLib.Diagnostics.CrashWatch.ExceptionLines(new InvalidOperationException("CatLib test crash")));
        Assert.Equal("System.InvalidOperationException: CatLib test crash", managed.ExceptionLines[0], "CatLib writes the unhandled .NET exception");
        var withMessage = CrashText.WithManagedException(CrashText.Locate(modules, 0x7FF900A86A9C, 0xE0434352, 5), managed);
        Assert.Equal("System.InvalidOperationException: CatLib test crash", CrashText.RuntimeHeadline(withMessage.RuntimeMessage), "The watcher shows it when Windows has none");

        located.Early = true;
        located.Access = CrashText.AccessText(0, 0);
        var early = CrashText.Report(session, 0xC0000005, located, exit, played, null);
        Assert.True(early.Contains("when it was raised, the game closed right after"), "A crash caught when it was raised says so");
        Assert.True(early.Contains("Access: read at 0x0000000000000000"), "The report names the access");
        session.MainThreadId = 1234;
        var fromWatcher = CrashText.Report(session, 0xC0000005, located, exit, played, null, "D:\\Crashes\\x\\crash.dmp");
        Assert.True(fromWatcher.Contains("Thread: 1234, the game thread"), "The report says the crash was on the game thread");
        Assert.True(fromWatcher.Contains("seen by the crash watcher"), "The report says where the record came from");
        Assert.True(CrashText.Details(session, 0xC0000005, located, exit, russian).Contains("Поток: 1234, поток игры"), "Localized details name the thread");
        Assert.Equal("abc...", CrashText.Shorten("abcdefghij", 6), "Long text is shortened");
        Assert.SequenceEqual(new[] { "a", "b" }, CrashText.Tail(new[] { "x", "a", "b" }, 2), "Tail keeps the last lines");
        yield break;
    }

    private static string SampleEvents(DateTime exitUtc)
    {
        var time = exitUtc.AddSeconds(-2).ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ");
        var runtime = "Application: CatMailCo.exe\nCoreCLR Version: 6.0.36\nDescription: The process was terminated due to an unhandled exception.\nException Info: System.InvalidOperationException: CatLib test crash\n   at CatLib.Test.Crash()";
        return
            Event("Application Error", 1000, time, "Unity.exe", "1.0", "0", "ntdll.dll", "10.0", "0", "c0000005", "0x1", "0x1234", "0", "C:\\Other\\Unity.exe", "C:\\Windows\\ntdll.dll") +
            Event(".NET Runtime", 1026, time, runtime) +
            Event("Application Error", 1000, time, "CatMailCo.exe", "6000.3.5.2", "68b1", "steamclient64.dll", "9.0", "68a0", "c0000005", "0000000000a86a9c", "0xa95c", "01dc2f", "D:\\Game\\CatMailCo.exe", "C:\\Steam\\steamclient64.dll", "id", "", "");
    }

    private static string Event(string provider, int id, string time, params string[] data) =>
        "<Event xmlns='http://schemas.microsoft.com/win/2004/08/events/event'><System><Provider Name='" + provider + "'/><EventID>" + id +
        "</EventID><TimeCreated SystemTime='" + time + "'/></System><EventData>" +
        string.Concat(data.Select(value => "<Data>" + System.Security.SecurityElement.Escape(value) + "</Data>")) + "</EventData></Event>";
}
