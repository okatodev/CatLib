using System.Collections.Generic;
using CatLib.Tests.Framework;
using CatLib.UI;

namespace CatLib.Tests.Suites.Ui;

internal static class SettingsMenuFixture
{
    public static bool OpenedByTests { get; private set; }

    public static TabInterface PreviousTab { get; private set; }

    public static MenuContext Context => Singleton<InterfaceManager>.HasInstance() ? MenuContext.InGame : MenuContext.MainMenu;

    public static ModsTab Tab => ModsMenu.Get(Context);

    public static IEnumerable<TestStep> EnsureModsTab(TestContext context)
    {
        if (Tab != null)
        {
            yield break;
        }

        if (Context == MenuContext.MainMenu && Singleton<MainMenuInterfacesManager>.HasInstance())
        {
            context.Note("The settings menu was not initialized yet, opening it");
            Singleton<MainMenuInterfacesManager>.Instance.ShowSettingsMenu();
            OpenedByTests = true;
        }
        else
        {
            context.Note("Open the settings menu from the pause menu before running UI tests in a level");
        }

        yield return Wait.Until(() => Tab != null, 10, "the Mods tab to be injected into the " + Context + " settings menu");
    }

    public static IEnumerable<TestStep> ShowModsTab(TestContext context)
    {
        foreach (var step in EnsureModsTab(context))
        {
            yield return step;
        }

        var options = Tab.Options;
        if (options._selectedTab == null || options._selectedTab.Pointer != Tab.Tab.Pointer)
        {
            PreviousTab ??= options._selectedTab ?? options._tabs[0];
            Tab.Tab.SetTabActive(true, true);
            yield return Wait.NextFrame();
        }
    }

    public static IEnumerable<TestStep> SelectMod(TestContext context, CatLib.Config.CatSettings settings)
    {
        foreach (var step in ShowModsTab(context))
        {
            yield return step;
        }

        var controller = Tab.Controller;
        yield return Wait.Until(() => HasItem(controller, settings), 3, "the mod to appear in the Mods list");
        controller.Select(settings);
        yield return Wait.NextFrame();
    }

    public static bool HasItem(ModsPanel controller, CatLib.Config.CatSettings settings)
    {
        foreach (var item in controller.Items)
        {
            if (ReferenceEquals(item.Settings, settings))
            {
                return true;
            }
        }

        return false;
    }

    public static T Row<T>(string key) where T : SettingRow
    {
        foreach (var row in Tab.Controller.Rows)
        {
            if (row.Setting.Key == key && row is T typed)
            {
                return typed;
            }
        }

        Assert.Fail($"No {typeof(T).Name} for {key} in the Mods panel");
        return null;
    }

    public static void RestoreTab()
    {
        if (PreviousTab != null && UiClone.IsAlive(PreviousTab))
        {
            PreviousTab.SetTabActive(true, true);
        }

        PreviousTab = null;
    }

    public static void CloseIfOpened(TestContext context)
    {
        if (!OpenedByTests)
        {
            return;
        }

        OpenedByTests = false;
        if (Singleton<MainMenuInterfacesManager>.HasInstance())
        {
            Singleton<MainMenuInterfacesManager>.Instance.GoBackToLastInterface();
            context.Note("Closed the settings menu opened by the tests");
        }
    }
}
