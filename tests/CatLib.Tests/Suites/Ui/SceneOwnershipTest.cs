using System.Collections.Generic;
using CatLib.Tests.Framework;
using CatLib.UI;
using UnityEngine;

namespace CatLib.Tests.Suites.Ui;

public sealed class SceneOwnershipTest : TestCase
{
    public const string LegacyStagingName = "CatLib.UiStaging";

    public override string Suite => "Ui";

    public override int Order => 18;

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        foreach (var step in SettingsMenuFixture.EnsureModsTab(context))
        {
            yield return step;
        }

        var modsTab = SettingsMenuFixture.Tab;
        var menuScene = modsTab.Options.gameObject.scene;
        context.Note("Settings menu scene: " + menuScene.name);

        Assert.True(Equals(menuScene, modsTab.Tab.gameObject.scene), "The Mods tab must belong to the scene of its settings menu");
        Assert.True(Equals(menuScene, modsTab.Panel.scene), "The Mods panel must belong to the scene of its settings menu");
        foreach (var row in modsTab.Controller.Rows)
        {
            Assert.True(Equals(menuScene, row.Root.scene), $"The row of {row.Setting.Id} must belong to the scene of its settings menu");
        }

        var leftovers = 0;
        foreach (var gameObject in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (gameObject.name == UiClone.StagingName || gameObject.name == LegacyStagingName)
            {
                leftovers++;
            }
        }

        Assert.Equal(0, leftovers, "No staging objects may outlive an injection, they would keep a reference to a scene that gets unloaded");
    }
}
