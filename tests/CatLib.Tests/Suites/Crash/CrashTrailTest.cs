using System.Collections.Generic;
using System.Linq;
using CatLib.Diagnostics;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.Crash;

public sealed class CrashTrailTest : TestCase
{
    public override string Suite => "Crash";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var file = new List<string> { "[Info   :CatLib] a", "[Info   :Unity] repeated", "[Info   :Unity] repeated", "[Info   :CatLib] b" };
        var memory = new List<string> { "[Info   :Unity] repeated", "[Info   :Unity] repeated", "[Info   :CatLib] b", "[Warning:CatLib] c", "[Error  :CatLib] d" };
        Assert.SequenceEqual(new[] { "[Warning:CatLib] c", "[Error  :CatLib] d" }, CrashLogRing.Missing(file, memory), "The lines after the end of the file are missing from it");
        Assert.Equal(0, CrashLogRing.Missing(memory, memory).Count, "A file that has everything misses nothing");
        var cut = new List<string> { "[Info   :CatLib] a", "[Info   :CatLib] b", "[Warning:Cat" };
        Assert.SequenceEqual(new[] { "[Warning:CatLib] c", "[Error  :CatLib] d" }, CrashLogRing.Missing(cut, memory), "A line the file has only half of comes whole from memory");
        var repeated = new List<string> { "x", "[Info   :Unity] repeated", "[Info   :Unity] repeated" };
        var repeatedMemory = new List<string> { "[Info   :Unity] repeated", "[Info   :Unity] repeated", "[Info   :Unity] repeated", "[Info   :Unity] repeated", "y" };
        Assert.SequenceEqual(new[] { "y" }, CrashLogRing.Missing(repeated, repeatedMemory), "Repeated lines are matched from the newest end");
        Assert.SequenceEqual(memory, CrashLogRing.Missing(new List<string>(), memory), "An empty file misses everything");
        Assert.SequenceEqual(memory, CrashLogRing.Missing(new List<string> { "older" }, memory), "A file far behind misses everything the memory has");

        var lines = new List<string>
        {
            CrashSession.Line(CrashSession.FormatKey, "2"),
            CrashSession.Line(CrashSession.LogMemoryKey, "0x1d5e12b0000 65536"),
            CrashSession.Line(CrashSession.DotnetStringKey, "0x7ffa00001000"),
            CrashSession.Line(CrashSession.Il2CppStringKey, "0x1d600002000"),
            CrashSession.Line(CrashSession.DoingKey, CrashSession.DoingValue(1, "2026-10-22 10:00:00", "Too Late writes the snapshot")),
            CrashSession.Line(CrashSession.DoneKey, "1"),
            CrashSession.Line(CrashSession.DoingKey, CrashSession.DoingValue(2, "2026-10-22 10:00:05", "Steam shuts down (SteamAPI.Shutdown)")),
            CrashSession.Line(CrashSession.DoingKey, CrashSession.DoingValue(2, "2026-10-22 10:00:05", "Steam shuts down (SteamAPI.Shutdown)"))
        };
        var session = CrashSession.Parse(lines);
        Assert.Equal(0x1d5e12b0000UL, session.LogMemoryAddress, "The address of the log in memory");
        Assert.Equal(65536, session.LogMemorySize, "and its size");
        Assert.Equal(0x7ffa00001000UL, session.DotnetStringType, "The .NET string type");
        Assert.Equal(0x1d600002000UL, session.Il2CppStringType, "The IL2CPP string type");
        Assert.Equal(1, session.Unfinished.Count, "A finished operation is left out, one written twice by a rewrite counts once");
        Assert.Equal("Steam shuts down (SteamAPI.Shutdown)", session.Unfinished[0].Text, "The unfinished one keeps its text");
        Assert.Equal("2026-10-22 10:00:05", session.Unfinished[0].Time, "and its time");

        var header = new CrashSession { LogMemoryAddress = 0xABC, LogMemorySize = 1024, DotnetStringType = 0x10, Il2CppStringType = 0x20 }.HeaderLines().ToList();
        var back = CrashSession.Parse(header);
        Assert.Equal(0xABCUL, back.LogMemoryAddress, "The header writes the memory keys and reads them back");
        Assert.Equal(0x20UL, back.Il2CppStringType, "all of them");

        Assert.SequenceEqual(new[] { "d4", "d5" }, CrashText.DumpsToDrop(new[] { "d1", "c", "d2", "d3", "d4", "d5" }, folder => folder.StartsWith("d"), 3),
            "Only reports with a dump take one of the three places");
        Assert.False(CrashText.KeepsDump(CrashText.ConsoleClosedCode), "A game closed through its console window keeps no dump");
        Assert.True(CrashText.KeepsDump(0xC0000005), "a crash does");
        yield break;
    }
}
