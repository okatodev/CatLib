using System;
using BepInEx.Unity.IL2CPP;
using CatLib.Game.Bridge;
using CatLib.Logging;
using CatLib.Threading;

namespace CatLib.Core;

public static class CatLibRuntime
{
    public static bool IsInitialized { get; private set; }

    public static CatLogger Log { get; private set; }

    internal static void Initialize(BasePlugin plugin)
    {
        if (IsInitialized)
        {
            return;
        }

        Log = CatLogger.From(plugin.Log);
        MainThread.Initialize();
        FrameLoop.Initialize();
        ManagerRegistry.Initialize(Log.Scope("Bridge"));
        plugin.AddComponent<CatLibBehaviour>();
        IsInitialized = true;
        Log.Info($"CatLib {PluginMeta.Version} initialized on managed thread {MainThread.ManagedThreadId}");
    }

    internal static void Tick()
    {
        try
        {
            FrameLoop.Tick();
        }
        catch (Exception exception)
        {
            Log.Error("Frame tick failed", exception);
        }
    }
}
