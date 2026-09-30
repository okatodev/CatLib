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
        var options = CatLib.UI.ModsMenu.FindOptions(SettingsMenuFixture.Context);
        var tabs = options._tabs;
        for (var index = 0; index < tabs.Count; index++)
        {
            var panel = tabs[index].AssociatedPanel;
            var tabScroll = panel == null ? null : CatLib.UI.ModsTabBuilder.FindDirectScroll(panel.transform);
            context.Note($"game tab {index}: {CatLib.UI.ModsTabBuilder.SafeDescribe(tabScroll == null ? null : tabScroll.verticalScrollbar)}");
        }

        var reference = CatLib.UI.ModsTabBuilder.FindLaidOutScrollbar(options);
        context.Note($"reference: {CatLib.UI.ModsTabBuilder.SafeDescribe(reference)}");
        foreach (var (name, scroll) in new[] { ("list", modsTab.ListScroll), ("settings", modsTab.ContentScroll) })
        {
            var scrollbar = scroll.verticalScrollbar;
            if (scrollbar == null)
            {
                context.Note($"{name}: no vertical scrollbar");
                continue;
            }

            var rect = scrollbar.transform.TryCast<RectTransform>();
            context.Note($"{name}: {CatLib.UI.ModsTabBuilder.SafeDescribe(scrollbar)}");
            context.Note($"{name}: scrollbar anchors {rect.anchorMin}-{rect.anchorMax}, size {rect.sizeDelta}");
            Assert.True(rect.anchorMax.y > rect.anchorMin.y, $"The {name} scrollbar must stretch along the pane, not sit in a corner");
            if (reference != null)
            {
                var track = scrollbar.GetComponent<UnityEngine.UI.Image>();
                var referenceTrack = reference.GetComponent<UnityEngine.UI.Image>();
                var handle = scrollbar.handleRect == null ? null : scrollbar.handleRect.GetComponent<UnityEngine.UI.Image>();
                var referenceHandle = reference.handleRect == null ? null : reference.handleRect.GetComponent<UnityEngine.UI.Image>();
                context.Note($"{name}: track {SpriteName(track)} like {SpriteName(referenceTrack)}, handle {SpriteName(handle)} like {SpriteName(referenceHandle)}");
                Assert.Equal(SpriteName(referenceTrack), SpriteName(track), $"The {name} scrollbar track looks like the game's");
                Assert.Equal(SpriteName(referenceHandle), SpriteName(handle), $"The {name} scrollbar handle looks like the game's");
                Assert.Equal(referenceHandle == null ? default : referenceHandle.color, handle == null ? default : handle.color, $"The {name} scrollbar handle has the game's color");
            }
        }
    }

    private static string SpriteName(UnityEngine.UI.Image image) => image == null ? "none" : image.sprite == null ? "no sprite" : image.sprite.name;
}
