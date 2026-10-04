using System.Collections.Generic;
using System.Linq;
using CatLib.Tests.Framework;
using CatLib.UI;

namespace CatLib.Tests.Suites.Ui;

public sealed class ModIconTest : TestCase
{
    public override string Suite => "Ui";

    public override int Order => 17;

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        foreach (var step in SettingsMenuFixture.EnsureModsTab(context))
        {
            yield return step;
        }

        var catLib = CatLib.Core.CatLibRuntime.Settings;
        context.Note($"CatLib folder: {catLib.PluginDirectory ?? "unknown"}, icon: {catLib.IconPath ?? "none"}");
        Assert.NotNull(catLib.IconPath, "The build copies icon.png next to CatLib.dll and CatLib finds it");
        var item = SettingsMenuFixture.Tab.Controller.Items.FirstOrDefault(entry => ReferenceEquals(entry.Settings, catLib));
        Assert.NotNull(item, "CatLib is in the mods list");
        Assert.True(item.Icon != null && item.Icon.gameObject.activeSelf && item.Icon.sprite != null, "CatLib shows its icon in the mods list");
        Assert.True(item.IconScale > 0f && item.IconScale <= 1f, "The icon fits its place");
        Assert.True(item.Icon.rectTransform.sizeDelta.x <= ModListItem.IconSize + 0.01f, "The icon is not larger than its place");
        foreach (var entry in SettingsMenuFixture.Tab.Controller.Items)
        {
            var sprite = entry.Icon == null ? null : entry.Icon.sprite;
            var drawn = sprite == null ? "no icon" : $"icon {sprite.texture.width}x{sprite.texture.height}, drawing {sprite.rect.width:0}x{sprite.rect.height:0}, scale {entry.IconScale:0.00}";
            context.Note($"{entry.Settings.DisplayName}: {drawn}, second line \"{entry.Meta?.text}\"");
            Assert.True(entry.Meta != null && entry.Meta.gameObject.activeSelf && entry.Meta.text.Length > 0, entry.Settings.DisplayName + " has a second line");
            var shown = entry.Meta.text;
            foreach (var language in new[] { "ru", "de" })
            {
                entry.Meta.text = ModListMeta.Text(entry.Settings.Version, 25, ModBadgeKind.None, language);
                entry.Meta.ForceMeshUpdate();
                Assert.False(entry.Meta.isTextTruncated, $"{entry.Settings.DisplayName}: the second line fits in {language}: {entry.Meta.text}");
            }

            entry.Meta.text = shown;
        }
    }
}
