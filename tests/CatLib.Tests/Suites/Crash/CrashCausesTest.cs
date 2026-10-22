using System.Collections.Generic;
using CatLib.Diagnostics;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.Crash;

public sealed class CrashCausesTest : TestCase
{
    public override string Suite => "Crash";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var session = new CrashSession { MainThreadId = 10 };
        var steam = Info("steamclient64.dll", "0xc0000409", 20);
        steam.Threads.Add(Stack(20, "steamclient64.dll + 0xfdc0b1"));
        steam.Threads.Add(Stack(10, "steamclient64.dll + 0xfae1fb", "steam_api64.dll + 0x8a8d            SteamAPI_Shutdown + 0x5d"));
        Assert.Equal(CrashCauses.SteamShutdown, CrashCauses.Find(session, 0xC0000409, steam), "Steam crashing in its own thread while the game thread shuts it down");
        var report = CrashText.Report(session, 0xC0000409, Named(steam, CrashCauses.Find(session, 0xC0000409, steam)), System.DateTime.Now, System.TimeSpan.Zero, new string[0]);
        Assert.True(report.Contains("Known cause: " + CrashCauses.Report[CrashCauses.SteamShutdown]), "The report names the cause");

        var running = Info("steamclient64.dll", "0xc0000005", 20);
        running.Threads.Add(Stack(20, "steamclient64.dll + 0xa97a1c"));
        running.Threads.Add(Stack(10, "UnityPlayer.dll + 0x1"));
        Assert.Equal(string.Empty, CrashCauses.Find(session, 0xC0000005, running), "Steam crashing while the game plays has no known cause yet");

        var breakpoint = Info("steamclient64.dll", "0x80000003", 20);
        Assert.Equal(CrashCauses.SteamBreakpoint, CrashCauses.Find(session, 0x80000003, breakpoint), "A breakpoint of Steam");

        var overflow = Info("GameAssembly.dll", "0xc0000409", 10);
        overflow.Threads.Add(Stack(10, "GameAssembly.dll + 0x7", "GameAssembly.dll + 0x8  Steamworks.SteamNetworkingMessages.GetSessionConnectionInfo(SteamNetworkingIdentity&, SteamNetConnectionInfo_t&, SteamNetConnectionRealTimeStatus_t&) + 0x40"));
        Assert.Equal(CrashCauses.SteamStructOverflow, CrashCauses.Find(session, 0xC0000409, overflow), "The stack overrun of GetSessionConnectionInfo");

        var dev = new CrashSession { MainThreadId = 10 };
        dev.Operations.Add(new CrashOperation { Id = 1, Text = "CatLib.Tests: crash in .NET code" });
        Assert.Equal(CrashCauses.DeveloperMenu, CrashCauses.Find(dev, 0xC0000005, Info("?", "0xc0000005", 10)), "A crash from the developer menu");
        dev.Operations[0].Done = true;
        Assert.Equal(string.Empty, CrashCauses.Find(dev, 0xC0000005, Info("?", "0xc0000005", 10)), "but not once that command finished");

        foreach (var cause in CrashCauses.Report.Keys)
        {
            Assert.NotEqual(CrashCauses.KeyPrefix + cause, CrashStrings.EnglishOnly.Get(CrashCauses.KeyPrefix + cause), $"The window has a text for {cause}");
            Assert.NotEqual(CrashStrings.English[CrashCauses.KeyPrefix + cause],
                CatLib.UI.UiText.Catalog.Find(CrashStrings.CatalogPrefix + CrashCauses.KeyPrefix + cause, "ru"), $"and it is translated, {cause} in Russian");
        }

        yield break;
    }

    private static CrashEventInfo Info(string module, string code, int thread) =>
        new CrashEventInfo { Module = module, Offset = "0x1", ExceptionCode = code, ThreadId = thread, FromDebugger = true };

    private static CrashEventInfo Named(CrashEventInfo info, string cause)
    {
        info.Cause = cause;
        return info;
    }

    private static CrashThreadStack Stack(int thread, params string[] frames)
    {
        var stack = new CrashThreadStack { ThreadId = thread };
        stack.Frames.AddRange(frames);
        return stack;
    }
}
