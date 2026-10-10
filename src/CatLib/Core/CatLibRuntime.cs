using System;
using BepInEx.Unity.IL2CPP;
using CatLib.Config;
using CatLib.DevTools;
using CatLib.Diagnostics;
using CatLib.Game.Bridge;
using CatLib.Net;
using CatLib.Logging;
using CatLib.Saves;
using CatLib.Threading;
using CatLib.UI;

namespace CatLib.Core;

public static class CatLibRuntime
{
    public static bool IsInitialized { get; private set; }

    public static CatLogger Log { get; private set; }

    public static CatSettings Settings { get; private set; }

    internal static event Action ShuttingDown;

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
        CatLib.Game.GameCompatibility.Initialize(Log.Scope("Version"));
        CatLib.Game.CatParcels.Initialize(Log.Scope("Parcels"));
        CatConfig.Initialize(Log.Scope("Config"));
        CatLib.Localization.TranslationTools.Initialize(Log.Scope("Lang"));
        ModsMenu.Initialize(Log.Scope("UI"));
        Settings = CatSettings.For(plugin);
        CrashWatch.Initialize(Log.Scope("Crash"), Settings);
        SessionNetwork.Initialize(Log.Scope("Net"), Settings);
        CatSaves.Initialize(Log.Scope("Saves"));
        CatLib.Assets.ContentPacks.Log = Log.Scope("Assets");
        CatLib.Game.Fixes.UdpClientCloser.Initialize(Log.Scope("Fixes"));
        DevMenu.Initialize(Log.Scope("DevTools"), Settings);
        plugin.AddComponent<CatLibBehaviour>();
        IsInitialized = true;
        Log.Info($"CatLib {PluginMeta.Version} initialized on managed thread {MainThread.ManagedThreadId}");
    }

    internal static void Quit()
    {
        CrashWatch.MarkCleanExit();
        SessionNetwork.OnQuitting();
        CatLib.Game.Fixes.UdpClientCloser.CloseAll("the game is quitting");
    }

    internal static void ShutDown()
    {
        try
        {
            ShuttingDown?.Invoke();
        }
        catch (Exception exception)
        {
            Log?.Error("A shutdown handler failed", exception);
        }
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
