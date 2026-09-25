using System;
using System.Collections.Generic;
using CatLib.Game.Events;
using CatLib.Logging;

namespace CatLib.UI;

internal static class ModsMenu
{
    public const string InjectedEventName = "UI.ModsTabInjected";
    public const string RemovedEventName = "UI.ModsTabRemoved";
    public const string SuspendedEventName = "UI.Suspended";
    public const string ResumedEventName = "UI.Resumed";

    private static readonly Dictionary<MenuContext, ModsTab> Tabs = new();
    private static readonly Dictionary<MenuContext, IntPtr> FailedPointers = new();
    private static CatLogger _log;
    private static bool _suspendRequested;

    public static bool IsSuspended { get; private set; }

    public static IReadOnlyDictionary<MenuContext, ModsTab> Current => Tabs;

    public static ModsTab Get(MenuContext context) => Tabs.TryGetValue(context, out var tab) && tab.IsAlive ? tab : null;

    internal static void Initialize(CatLogger log)
    {
        _log = log;
        PlayerMessages.Initialize(log);
        BootstrapEvents.GameRestartStarted += SuspendForRestart;
        BootstrapEvents.MainMenuLoaded += Resume;
        BootstrapEvents.LevelLoadFinalized += Resume;
    }

    internal static void SuspendForRestart()
    {
        _suspendRequested = true;
    }

    internal static void Resume()
    {
        _suspendRequested = false;
        if (!IsSuspended)
        {
            return;
        }

        IsSuspended = false;
        _log?.Info("Resumed the Mods tab after the restart");
        GameEventStream.Publish(ResumedEventName);
    }

    internal static void Update()
    {
        if (_log == null)
        {
            return;
        }

        if (_suspendRequested && !IsSuspended)
        {
            IsSuspended = true;
            foreach (var context in new List<MenuContext>(Tabs.Keys))
            {
                Remove(context, Tabs[context]);
            }

            FailedPointers.Clear();
            _log.Info("The game is restarting, the Mods tab is released until the next menu");
            GameEventStream.Publish(SuspendedEventName);
        }

        if (IsSuspended)
        {
            return;
        }

        MenuNotices.Update();
        Track(MenuContext.MainMenu, FindMainMenuOptions());
        Track(MenuContext.InGame, FindInGameOptions());

        foreach (var tab in Tabs.Values)
        {
            try
            {
                tab.Update();
            }
            catch (Exception exception)
            {
                _log.Error($"Updating the {tab.Context} Mods tab failed", exception);
            }
        }
    }

    internal static OptionsInterface FindOptions(MenuContext context) =>
        context == MenuContext.MainMenu ? FindMainMenuOptions() : FindInGameOptions();

    private static void Track(MenuContext context, OptionsInterface options)
    {
        if (Tabs.TryGetValue(context, out var existing))
        {
            if (existing.IsAlive && options != null && existing.OptionsPointer == options.Pointer)
            {
                return;
            }

            Remove(context, existing);
        }

        if (options == null || !ModsTabBuilder.IsReady(options))
        {
            return;
        }

        if (FailedPointers.TryGetValue(context, out var failed) && failed == options.Pointer)
        {
            return;
        }

        try
        {
            var stale = ModsTabBuilder.RemoveStale(options);
            if (stale > 0)
            {
                _log.Info($"Removed {stale} leftover Mods tab object(s) from the {context} settings menu");
            }

            var tab = ModsTabBuilder.Build(context, options, _log);
            Tabs[context] = tab;
            FailedPointers.Remove(context);
            _log.Info($"Injected the Mods tab into the {context} settings menu");
            GameEventStream.Publish(InjectedEventName, "context=" + context);
        }
        catch (Exception exception)
        {
            FailedPointers[context] = options.Pointer;
            _log.Error($"Could not inject the Mods tab into the {context} settings menu", exception);
        }
    }

    private static void Remove(MenuContext context, ModsTab tab)
    {
        try
        {
            tab.Detach();
        }
        catch (Exception exception)
        {
            _log.Warning($"Detaching the {context} Mods tab failed: {exception.Message}");
        }

        Tabs.Remove(context);
        _log.Info($"The {context} settings menu is gone, its Mods tab was released");
        GameEventStream.Publish(RemovedEventName, "context=" + context);
    }

    private static OptionsInterface FindMainMenuOptions()
    {
        if (!Singleton<MainMenuInterfacesManager>.HasInstance())
        {
            return null;
        }

        var settings = Singleton<MainMenuInterfacesManager>.Instance.SettingsMenuInterface;
        return settings == null ? null : settings.TryCast<OptionsInterface>();
    }

    private static OptionsInterface FindInGameOptions()
    {
        if (!Singleton<InterfaceManager>.HasInstance())
        {
            return null;
        }

        return Singleton<InterfaceManager>.Instance.SettingsInterface;
    }
}
