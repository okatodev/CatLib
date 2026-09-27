using System.Collections.Generic;
using CatLib.Tests.Framework;
using UnityEngine;

namespace CatLib.Tests.Suites.Ui;

public sealed class ScrollbarLayoutTest : TestCase
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
            var scrollbar = scroll.verticalScrollbar;
            if (scrollbar == null)
            {
                context.Note($"{name}: no vertical scrollbar");
                continue;
            }

            var rect = scrollbar.transform.TryCast<RectTransform>();
            context.Note($"{name}: scrollbar anchors {rect.anchorMin}-{rect.anchorMax}, size {rect.sizeDelta}");
            Assert.True(rect.anchorMax.y > rect.anchorMin.y, $"The {name} scrollbar must stretch along the pane, not sit in a corner");
        }
    }
}
