using System;
using System.Diagnostics;
using CatLib.Config;
using CatLib.Events;
using CatLib.Game.Bridge;
using CatLib.Threading;
using CatLib.UI;

namespace CatLib.Core;

public static class FrameLoop
{
    private static readonly Stopwatch Clock = new();

    public static event Action Update;

    public static long FrameCount { get; private set; }

    public static double Realtime => Clock.Elapsed.TotalSeconds;

    internal static void Initialize()
    {
        Clock.Start();
    }

    internal static void Tick()
    {
        FrameCount++;
        MainThread.Drain(CatLibRuntime.Log);
        ManagerRegistry.Update();
        CatConfig.Update();
        ModsMenu.Update();
        SafeInvoker.Invoke(Update, "FrameLoop.Update", CatLibRuntime.Log);
    }
}
