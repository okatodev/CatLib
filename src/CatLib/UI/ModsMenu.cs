using System;
using System.Collections.Generic;
using CatLib.Game.Events;
using CatLib.Logging;

namespace CatLib.UI;

internal static class ModsMenu
{
    public const string InjectedEventName = "UI.ModsTabInjected";
    public const string RemovedEventName = "UI.ModsTabRemoved";

    private static readonly Dictionary<MenuContext, ModsTab> Tabs = new();
    private static readonly Dictionary<MenuContext, IntPtr> FailedPointers = new();
    private static CatLogger _log;

    public static IReadOnlyDictionary<MenuContext, ModsTab> Current => Tabs;

    public static ModsTab Get(MenuContext context) => Tabs.TryGetValue(context, out var tab) && tab.IsAlive ? tab : null;

    internal static void Initialize(CatLogger log)
    {
        _log = log;
        PlayerMessages.Initialize(log);
    }

    internal static void Update()
    {
        if (_log == null)
        {
            return;
        }

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
