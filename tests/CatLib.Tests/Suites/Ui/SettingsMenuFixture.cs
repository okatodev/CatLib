using System.Collections.Generic;
using CatLib.Tests.Framework;
using CatLib.UI;

namespace CatLib.Tests.Suites.Ui;

internal static class SettingsMenuFixture
{
    public const double MissingTabMemorySeconds = 120;

    private static float _waitingSince = -1f;

    public static bool OpenedByTests { get; private set; }

    public static bool OpenedInLevel { get; private set; }

    public static TabInterface PreviousTab { get; private set; }

    public static MenuContext Context => Singleton<InterfaceManager>.HasInstance() ? MenuContext.InGame : MenuContext.MainMenu;

    public static ModsTab Tab => ModsMenu.Get(Context);

    public static IEnumerable<TestStep> EnsureModsTab(TestContext context)
    {
        if (Tab != null)
        {
            yield break;
        }

        var now = UnityEngine.Time.realtimeSinceStartup;
        if (_waitingSince >= 0f && now - _waitingSince < MissingTabMemorySeconds)
        {
            Assert.Fail("The Mods tab did not appear in the " + Context + " settings menu earlier in this run, so this test does not wait for it again");
        }

        if (Context == MenuContext.MainMenu && Singleton<MainMenuInterfacesManager>.HasInstance())
        {
            context.Note("The settings menu was not initialized yet, opening it");
            Singleton<MainMenuInterfacesManager>.Instance.ShowSettingsMenu();
            OpenedByTests = true;
        }
        else if (Context == MenuContext.InGame)
        {
            OpenInLevel(context);
        }

        _waitingSince = now;
        yield return Wait.Until(() => Tab != null, 10, "the Mods tab to be injected into the " + Context + " settings menu");
        _waitingSince = -1f;
    }

    private static void OpenInLevel(TestContext context)
    {
        var manager = Singleton<InterfaceManager>.Instance;
        var settings = manager.SettingsInterface;
        if (settings == null || settings.IsShown)
        {
            return;
        }

        context.Note("Opening the settings menu of the level through the pause menu");
        if (Singleton<PauseManager>.HasInstance() && !Singleton<PauseManager>.Instance.IsPaused)
        {
            Singleton<PauseManager>.Instance.PauseGame();
        }

        var pause = manager.PauseInterface;
        if (pause != null)
        {
            pause.SettingsButton_OnClick();
        }
        else
        {
            settings.Show();
        }

        OpenedByTests = true;
        OpenedInLevel = true;
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
        if (OpenedInLevel)
        {
            OpenedInLevel = false;
            if (Singleton<InterfaceManager>.HasInstance())
            {
                var manager = Singleton<InterfaceManager>.Instance;
                manager.SettingsInterface?.BackButton_OnClick();
                manager.PauseInterface?.ResumeButton_OnClick();
                context.Note("Closed the settings and pause menus opened by the tests");
            }

            return;
        }

        if (Singleton<MainMenuInterfacesManager>.HasInstance())
        {
            Singleton<MainMenuInterfacesManager>.Instance.GoBackToLastInterface();
            context.Note("Closed the settings menu opened by the tests");
        }
    }
}
