using System.Collections.Generic;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.Ui;

public sealed class ModsTabInjectedTest : TestCase
{
    public override string Suite => "Ui";

    public override int Order => 0;

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        foreach (var step in SettingsMenuFixture.EnsureModsTab(context))
        {
            yield return step;
        }

        var modsTab = SettingsMenuFixture.Tab;
        var tabs = modsTab.Options._tabs;
        var index = tabs.IndexOf(modsTab.Tab);
        var gameTab = tabs[0];

        context.Note($"Context: {modsTab.Context}");
        context.Note($"Registered tabs: {tabs.Count}, Mods tab index: {index}");

        Assert.True(index >= 0, "The Mods tab must be registered in OptionsInterface._tabs");
        Assert.Equal(tabs.Count - 1, index, "The Mods tab must be the last registered tab");
        Assert.True(modsTab.IsVisible, "The Mods tab must be visible while mods declare settings");
        Assert.True(modsTab.Tab.transform.parent.Pointer == gameTab.transform.parent.Pointer, "The Mods tab must live in the tab bar");
        Assert.True(modsTab.Panel.transform.parent.Pointer == gameTab.AssociatedPanel.transform.parent.Pointer, "The Mods panel must live next to the game panels");
        Assert.True(modsTab.Tab.AssociatedPanel.Pointer == modsTab.Panel.Pointer, "The tab must point to the Mods panel");
    }
}
