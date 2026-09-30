using System.Collections.Generic;
using System.Linq;
using CatLib.Tests.Framework;

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
        foreach (var entry in SettingsMenuFixture.Tab.Controller.Items)
        {
            var sprite = entry.Icon == null ? null : entry.Icon.sprite;
            context.Note($"{entry.Settings.DisplayName}: {(sprite != null ? $"icon {sprite.texture.width}x{sprite.texture.height}" : "no icon")}");
        }
    }
}
