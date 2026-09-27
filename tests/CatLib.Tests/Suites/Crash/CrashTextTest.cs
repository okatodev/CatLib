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
        Assert.Equal(CrashText.EnglishPhrases.Length, CrashText.RussianPhrases.Length, "Every phrase has a translation");
        Assert.Equal(CrashText.EnglishPhrases.Length, CrashText.EnglishPhrases.Distinct().Count(), "English phrases are unique");
        Assert.Equal(CrashText.RussianPhrases.Length, CrashText.RussianPhrases.Distinct().Count(), "Russian phrases are unique");
        var random = new Random(7);
        var seen = new HashSet<string>();
        for (var index = 0; index < 200; index++)
        {
            seen.Add(CrashText.Phrase(random, false));
        }

        Assert.AtLeast(CrashText.EnglishPhrases.Length - 1, seen.Count, "Phrases change from crash to crash");
        Assert.True(CrashText.RussianPhrases.Contains(CrashText.Phrase(random, true)), "Russian phrases for a Russian game");

        Assert.True(CrashText.IsNormalExit(0), "Zero is a normal exit");
        Assert.False(CrashText.IsNormalExit(0xC0000005), "An access violation is not");
        Assert.Equal("0xC0000005", CrashText.Hex(0xC0000005), "Hex exit code");
        Assert.True(CrashText.DescribeExitCode(0xC0000005, false).Contains("access violation"), "Access violation is named");
        Assert.True(CrashText.DescribeExitCode(0xE0434352, false).Contains(".NET"), "A .NET exception is named");
        Assert.Equal("unknown reason", CrashText.DescribeExitCode(0x12345678, false), "Unknown codes");

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
        var summary = CrashText.Summary(session, 0xC0000005, info, played);
        Assert.True(summary.Contains("steamclient64.dll + 0x0000000000a86a9c"), "The summary names the place");
        Assert.True(summary.Contains("45 min"), "The summary says how long the game ran");
        Assert.True(summary.Contains("while the game was quitting"), "The summary notes a crash while quitting");

        var report = CrashText.Report(session, 0xC0000005, info, exit.ToLocalTime(), played, new[] { "last log line" }, "C:\\Dumps\\CatMailCo.exe.43356.dmp");
        foreach (var part in new[] { "Exit code: 0xC0000005", "Boat Tweaks 0.2.0", "Level.LevelUnloaded", "last log line", "Memory dump:", "   at CatLib.Test.Crash()" })
        {
            Assert.True(report.Contains(part), $"The report contains \"{part}\"");
        }

        var details = CrashText.Details(session, 0xC0000005, info, exit.ToLocalTime());
        Assert.True(details.Contains("Mods: Boat Tweaks 0.2.0, Shelf Labels 0.1.0"), "Details list the mods without ids");
        Assert.AtLeast(1, details.Split('\n').Length, "Details are not empty");
        Assert.True(details.Split('\n').Length <= 16, "Details stay short enough for a small window");

        var noFault = CrashText.Report(session, 1, new CrashEventInfo(), exit, TimeSpan.Zero, null);
        Assert.True(noFault.Contains("No crash record"), "A report without an event log record says so");
        Assert.Equal("1 h 5 min", CrashText.Duration(TimeSpan.FromMinutes(65), false), "Hours");
        Assert.Equal("12 с", CrashText.Duration(TimeSpan.FromSeconds(12), true), "Russian seconds");
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
