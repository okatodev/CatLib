using System.Collections.Generic;
using CatLib.Tests.Framework;
using I2.Loc;
using UnityEngine.Events;
using UnityEngine.UI;

namespace CatLib.Tests.Suites.Ui;

public sealed class ModsTabHygieneTest : TestCase
{
    public override string Suite => "Ui";

    public override int Order => 1;

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        foreach (var step in SettingsMenuFixture.EnsureModsTab(context))
        {
            yield return step;
        }

        var modsTab = SettingsMenuFixture.Tab;

        var tabLocalizations = modsTab.Tab.GetComponentsInChildren<Localize>(true).Length;
        var panelLocalizations = 0;
        var keptRevertLocalizations = 0;
        foreach (var localize in modsTab.Panel.GetComponentsInChildren<Localize>(true))
        {
            if (localize.GetComponentInParent<SelectableButton>(true) != null)
            {
                keptRevertLocalizations++;
            }
            else
            {
                panelLocalizations++;
            }
        }

        var dropdownLocalizations = modsTab.Panel.GetComponentsInChildren<LocalizeDropdown>(true).Length;
        var gameInterfaces =
            modsTab.Panel.GetComponentsInChildren<AudioSettingsInterface>(true).Length +
            modsTab.Panel.GetComponentsInChildren<GameplaySettingsInterface>(true).Length +
            modsTab.Panel.GetComponentsInChildren<ControlsSettingsInterface>(true).Length +
            modsTab.Panel.GetComponentsInChildren<InputsSettingsInterface>(true).Length;

        var onClick = modsTab.Tab.GetComponent<Button>().onClick;
        var persistent = onClick.GetPersistentEventCount();
        var enabled = 0;
        for (var index = 0; index < persistent; index++)
        {
            if (onClick.GetPersistentListenerState(index) != UnityEventCallState.Off)
            {
                enabled++;
            }
        }

        context.Note($"Tab button persistent listeners: {persistent}, still enabled: {enabled}");
        context.Note($"Localization kept on the revert button: {keptRevertLocalizations}");

        Assert.Equal(0, tabLocalizations, "Localize components left on the Mods tab");
        Assert.Equal(0, panelLocalizations, "Localize components left in the Mods panel outside the revert button");
        Assert.Equal(0, dropdownLocalizations, "LocalizeDropdown components left in the Mods panel");
        Assert.Equal(0, gameInterfaces, "Game settings interfaces left in the Mods panel");
        Assert.Equal(0, enabled, "Enabled persistent listeners on the Mods tab button");
        yield break;
    }
}
