using System.Collections.Generic;
using CatLib.Tests.Framework;
using CatLib.UI;
using UnityEngine.UI;

namespace CatLib.Tests.Suites.Ui;

public sealed class ViewportMaskTest : TestCase
{
    public override string Suite => "Ui";

    public override int Order => 16;

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        foreach (var step in SettingsMenuFixture.EnsureModsTab(context))
        {
            yield return step;
        }

        var modsTab = SettingsMenuFixture.Tab;
        foreach (var (name, scroll) in new[] { ("list", modsTab.ListScroll), ("settings", modsTab.ContentScroll) })
        {
            var viewport = ModsTabBuilder.ViewportOf(scroll);
            Assert.NotNull(viewport, $"Viewport of the {name} pane");

            var mask = viewport.GetComponent<Mask>();
            var graphic = viewport.GetComponent<Image>();
            context.Note($"{name}: mask {(mask == null ? "missing" : mask.enabled ? "enabled" : "disabled")}, graphic {(graphic == null ? "missing" : graphic.enabled ? "enabled" : "disabled")}");

            Assert.True(mask != null && mask.enabled, $"The {name} pane must have an enabled mask");
            Assert.True(graphic != null && graphic.enabled, $"The mask graphic of the {name} pane must be enabled, otherwise nothing is clipped");
            Assert.True(scroll.content.IsChildOf(viewport), $"The {name} content must be inside the masked viewport");
        }
    }
}
