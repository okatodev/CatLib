using System.Collections.Generic;
using CatLib.Tests.Framework;
using CatLib.UI;

namespace CatLib.Tests.Suites.Ui;

public sealed class RestartSuspendTest : TestCase
{
    public override string Suite => "Ui";

    public override int Order => 17;

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        foreach (var step in SettingsMenuFixture.EnsureModsTab(context))
        {
            yield return step;
        }

        var menu = SettingsMenuFixture.Context;
        foreach (var step in SettingsMenuFixture.ShowModsTab(context))
        {
            yield return step;
        }

        var before = ModsMenu.Get(menu);
        var options = before.Options;

        ModsMenu.SuspendForRestart();
        yield return Wait.Frames(2);

        Assert.True(ModsMenu.IsSuspended, "The Mods tab must be suspended after a restart starts");
        Assert.Null(ModsMenu.Get(menu), "The Mods tab must be released during a restart");

        yield return Wait.Frames(5);
        Assert.Null(ModsMenu.Get(menu), "Nothing may be injected while the game is restarting");

        ModsMenu.Resume();
        yield return Wait.Until(() => ModsMenu.Get(menu) != null, 5, "the Mods tab to be injected again");

        var after = ModsMenu.Get(menu);
        var tabsInBar = 0;
        for (var index = 0; index < options.TabsParent.childCount; index++)
        {
            tabsInBar += options.TabsParent.GetChild(index).gameObject.name == ModsTabBuilder.TabName ? 1 : 0;
        }

        var registered = 0;
        for (var index = 0; index < options._tabs.Count; index++)
        {
            registered += options._tabs[index].gameObject.name == ModsTabBuilder.TabName ? 1 : 0;
        }

        var panelParent = after.Panel.transform.parent;
        var panels = 0;
        for (var index = 0; index < panelParent.childCount; index++)
        {
            panels += panelParent.GetChild(index).gameObject.name == ModsTabBuilder.PanelName ? 1 : 0;
        }

        var dead = 0;
        for (var index = 0; index < options._inputFields.Length; index++)
        {
            dead += UiClone.IsAlive(options._inputFields[index]) ? 0 : 1;
        }

        for (var index = 0; index < options._dropdowns.Length; index++)
        {
            dead += UiClone.IsAlive(options._dropdowns[index]) ? 0 : 1;
        }

        var selectedAlive = options._selectedTab == null || UiClone.IsAlive(options._selectedTab);

        context.Note($"Tabs in the bar: {tabsInBar}, registered: {registered}, panels: {panels}, dead controls: {dead}");
        Assert.True(selectedAlive, "The selected tab of the game menu must not be a destroyed Mods tab");
        Assert.False(ReferenceEquals(before, after), "A new Mods tab must be built after resuming");
        Assert.Equal(1, tabsInBar, "Mods tabs in the tab bar");
        Assert.Equal(1, registered, "Mods tabs registered in OptionsInterface._tabs");
        Assert.Equal(1, panels, "Mods panels next to the game panels");
        Assert.Equal(0, dead, "Destroyed controls left in the OptionsInterface arrays");
    }
}
