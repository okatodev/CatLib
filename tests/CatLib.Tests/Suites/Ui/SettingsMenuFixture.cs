using System.Collections.Generic;
using CatLib.Tests.Framework;
using CatLib.UI;

namespace CatLib.Tests.Suites.Ui;

internal static class SettingsMenuFixture
{
    public static bool OpenedByTests { get; private set; }

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
