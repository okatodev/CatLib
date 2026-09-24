using System.Collections.Generic;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.Ui;

public sealed class ModsTabSwitchTest : TestCase
{
    public override string Suite => "Ui";

    public override int Order => 3;

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        foreach (var step in SettingsMenuFixture.EnsureModsTab(context))
        {
            yield return step;
        }

        var modsTab = SettingsMenuFixture.Tab;
        var options = modsTab.Options;
        var previous = options._selectedTab ?? options._tabs[0];

        modsTab.Tab.SetTabActive(true, true);
        yield return Wait.NextFrame();

        var otherActivePanels = 0;
        var tabs = options._tabs;
        for (var index = 0; index < tabs.Count; index++)
        {
            var tab = tabs[index];
            if (tab.Pointer != modsTab.Tab.Pointer && tab.AssociatedPanel != null && tab.AssociatedPanel.activeSelf)
            {
                otherActivePanels++;
            }
        }

        var selectedIsMods = options._selectedTab != null && options._selectedTab.Pointer == modsTab.Tab.Pointer;
        var modsPanelActive = modsTab.Panel.activeSelf;

        previous.SetTabActive(true, true);
        yield return Wait.NextFrame();

        var restored = options._selectedTab != null && options._selectedTab.Pointer == previous.Pointer;
        var modsPanelHidden = !modsTab.Panel.activeSelf;

        Assert.True(modsPanelActive, "Selecting the Mods tab must show the Mods panel");
        Assert.True(selectedIsMods, "OptionsInterface must treat the Mods tab as selected");
        Assert.Equal(0, otherActivePanels, "Game panels left visible while the Mods tab is selected");
        Assert.True(restored, "Selecting the previous tab again must work");
        Assert.True(modsPanelHidden, "Leaving the Mods tab must hide the Mods panel");
    }
}
