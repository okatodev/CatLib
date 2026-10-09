using System;
using System.Collections.Generic;
using System.Linq;
using CatLib.Diagnostics;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.Crash;

public sealed class CrashStacksTest : TestCase
{
    public const int GameThread = 1234;
    public const int SteamThread = 77;

    public override string Suite => "Crash";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var session = CrashSession.Parse(new[] { "catlib=0.8.0", "game=1.0.4", "started=2026-10-09 20:00:00", "pid=43356", "language=ru" });
        session.MainThreadId = GameThread;
        var info = new CrashEventInfo
        {
            Module = "GameAssembly.dll",
            Offset = "0x00000000004d5f30",
            ExceptionCode = "0xc0000005",
            ThreadId = SteamThread,
            FromDebugger = true,
            Method = "EntityInteractableStore.Start() + 0x60"
        };
        info.Threads.Add(Stack(SteamThread, "Steam", 30, "GameAssembly.dll + 0x4d5f30            EntityInteractableStore.Start() + 0x60"));
        info.Threads.Add(Stack(GameThread, string.Empty, 3, "UnityPlayer.dll + 0x1234"));
        for (var worker = 0; worker < 3; worker++)
        {
            info.Threads.Add(Stack(5 + worker, "Job.Worker " + worker, 2, "ntdll.dll + 0x164a14             NtWaitForAlertByThreadId + 0x14"));
        }

        var lonely = Stack(42, string.Empty, 2, "? 0x0000000050000000  not in a module");
        lonely.Note = "no return address found on the stack";
        info.Threads.Add(lonely);

        info.Threads[0].GameMethod = "UnityEngine.Diagnostics.Utils.ForceCrash(ForcedCrashCategory) + 0x2a";
        Assert.Equal(info.Threads[0].GameMethod, CrashText.NearestGameMethod(info), "The nearest game method of the crashing thread");
        info.GameMethod = CrashText.NearestGameMethod(info);
        var time = new DateTime(2026, 10, 9, 20, 30, 0);
        var report = CrashText.Report(session, 0xC0000005, info, time, TimeSpan.FromMinutes(30), new[] { "log" });
        Assert.True(report.Contains("Method: EntityInteractableStore.Start() + 0x60"), "The report names the method of the crash");
        Assert.True(report.Contains("Game code: UnityEngine.Diagnostics.Utils.ForceCrash(ForcedCrashCategory) + 0x2a, the nearest game method on the crashing thread"), "And the nearest game method");
        Assert.True(report.Contains("Stack of thread 77 \"Steam\", the crashing thread:"), "The report shows the stack of the crashing thread");
        Assert.True(report.Contains("Stack of thread 1234, the game thread:"), "And of the game thread");
        Assert.True(report.Contains("... 6 more in " + CrashText.StacksFileName), "A long stack is cut in the report");
        Assert.True(report.Contains("Stacks of all 6 threads: " + CrashText.StacksFileName), "The report points to the file with every thread");
        Assert.True(report.IndexOf("Stack of thread 77", StringComparison.Ordinal) < report.IndexOf("Mods:", StringComparison.Ordinal), "Stacks come before the mods and the log");

        var english = CrashText.Details(session, 0xC0000005, info, time, CrashStrings.EnglishOnly);
        Assert.True(english.Contains("Method: EntityInteractableStore.Start() + 0x60"), "The window names the method");
        var russian = new CrashStrings(CatLib.UI.UiText.Catalog.Keys("ru").Where(key => key.StartsWith(CrashStrings.CatalogPrefix))
            .ToDictionary(key => key.Substring(CrashStrings.CatalogPrefix.Length), key => CatLib.UI.UiText.Catalog.FindExact(key, "ru")));
        var russianDetails = CrashText.Details(session, 0xC0000005, info, time, russian);
        Assert.True(russianDetails.Contains("Метод: EntityInteractableStore.Start()"), "In the language of the game");
        Assert.True(russianDetails.Contains("Код игры: UnityEngine.Diagnostics.Utils.ForceCrash"), "With the game code");
        var same = new CrashEventInfo { Module = "GameAssembly.dll", Offset = "0x1", ThreadId = 1, Method = "A.B() + 0x1" };
        same.Threads.Add(new CrashThreadStack { ThreadId = 1, GameMethod = "A.B() + 0x1" });
        Assert.Equal(string.Empty, CrashText.NearestGameMethod(same), "A crash inside a game method does not repeat it");

        var raised = new CrashEventInfo { Module = "KERNELBASE.dll", Offset = "0xc41ca", ExceptionCode = "0xc0000005", ThreadId = GameThread, FromDebugger = true, Method = "RaiseException + 0x8a" };
        raised.Threads.Add(new CrashThreadStack { ThreadId = GameThread, Caller = "UnityPlayer.dll + 0xdb437  Utils_CUSTOM_ForceCrash + 0x17" });
        raised.CalledFrom = CrashText.CalledFrom(raised);
        Assert.Equal("UnityPlayer.dll + 0xdb437  Utils_CUSTOM_ForceCrash + 0x17", raised.CalledFrom, "A crash in Windows names the code that called it");
        Assert.True(CrashText.Report(session, 0xC0000005, raised, time, TimeSpan.FromMinutes(1), null).Contains("Called from: UnityPlayer.dll + 0xdb437  Utils_CUSTOM_ForceCrash + 0x17, the first frame outside Windows"), "In the report");
        Assert.True(CrashText.Details(session, 0xC0000005, raised, time, russian).Contains("Вызвано из: UnityPlayer.dll"), "And in the window, in the language of the game");
        info.Threads[0].Caller = "UnityPlayer.dll + 0x1";
        Assert.Equal(string.Empty, CrashText.CalledFrom(info), "A crash outside Windows needs no caller line");
        info.Threads[0].Caller = string.Empty;
        Assert.True(CrashText.IsSystemModule("ntdll.dll") && CrashText.IsSystemModule("ucrtbase.DLL") && !CrashText.IsSystemModule("UnityPlayer.dll"), "Windows and the C runtime are told apart");

        var all = CrashText.AllStacks(session, info, time);
        var lines = all.Replace("\r\n", "\n").Split('\n');
        Assert.True(lines[0].StartsWith("Stacks of 6 threads of the game when it crashed, 2026-10-09 20:30:00"), "The file says what it shows");
        var crashed = Array.FindIndex(lines, line => line.StartsWith("Thread 77", StringComparison.Ordinal));
        var game = Array.FindIndex(lines, line => line.StartsWith("Thread 1234", StringComparison.Ordinal));
        var workers = Array.FindIndex(lines, line => line.StartsWith("3 threads with the same stack: 5 \"Job.Worker 0\", 6 \"Job.Worker 1\", 7 \"Job.Worker 2\"", StringComparison.Ordinal));
        Assert.True(crashed > 0 && game > crashed && workers > game, "The crashing thread, the game thread, then threads grouped by the same stack");
        Assert.Equal(30, lines.Count(line => line.Contains("EntityInteractableStore.Start()")), "The file keeps every frame");
        Assert.True(all.Contains("(no return address found on the stack)"), "A walk that stopped early says why");
        Assert.Equal(2, lines.Count(line => line.Contains("NtWaitForAlertByThreadId")), "Three threads with the same two frames show them once");

        var hang = new CrashEventInfo { HangSeconds = 12 };
        hang.Threads.Add(Stack(GameThread, string.Empty, 2, "steam_api64.dll + 0x1234"));
        var hangText = CrashText.AllStacks(session, hang, time);
        Assert.True(hangText.Contains("when it stopped responding while quitting"), "The file of a hang says so");
        Assert.False(hangText.Contains("crashing thread"), "A hang has no crashing thread");
        var hangReport = CrashText.Report(session, 1, hang, time, TimeSpan.Zero, null);
        Assert.True(hangReport.Contains("Stack of thread 1234, the game thread:"), "The report of a hang shows the game thread");

        var plain = CrashText.Report(session, 0xC0000005, new CrashEventInfo { Module = "x.dll", Offset = "0x1", ExceptionCode = "0xc0000005" }, time, TimeSpan.Zero, null);
        Assert.False(plain.Contains("Method:") || plain.Contains("Stack of"), "Without names and stacks the report stays as before");
        yield break;
    }

    private static CrashThreadStack Stack(int id, string name, int frames, string frame)
    {
        var stack = new CrashThreadStack { ThreadId = id, Name = name };
        for (var index = 0; index < frames; index++)
        {
            stack.Frames.Add(frame);
        }

        return stack;
    }
}
