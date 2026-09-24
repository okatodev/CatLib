using System.Collections.Generic;
using System.Linq;
using CatLib.Tests.Framework;
using CatLib.UI;

namespace CatLib.Tests.Suites.Ui;

public sealed class SettingRowsTest : TestCase
{
    public override string Suite => "Ui";

    public override int Order => 6;

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        using var sandbox = new UiRowsSandbox("UiRows");
        foreach (var step in SettingsMenuFixture.SelectMod(context, sandbox.Settings))
        {
            yield return step;
        }

        var controller = SettingsMenuFixture.Tab.Controller;
        var rows = controller.Rows;
        var language = UiText.LanguageCode;

        context.Note("Rows: " + string.Join(", ", rows.Select(row => row.Setting.Key + "=" + row.Kind)));

        Assert.True(ReferenceEquals(sandbox.Settings, controller.Selected), "The sandbox mod must be selected");
        Assert.Equal(UiRowsSandbox.VisibleCount, rows.Count, "Rows for visible settings");
        Assert.Equal(UiRowsSandbox.SectionCount, controller.SectionHeaders.Count, "Section headers");
        Assert.False(rows.Any(row => row.Setting.Key == "Secret"), "Hidden settings must not get a row");

        Assert.Equal(ControlKind.Toggle, SettingsMenuFixture.Row<ToggleRow>("Enabled").Kind, "Enabled");
        Assert.Equal(ControlKind.Slider, SettingsMenuFixture.Row<SliderRow>("Count").Kind, "Count");
        Assert.Equal(ControlKind.Slider, SettingsMenuFixture.Row<SliderRow>("Speed").Kind, "Speed");
        Assert.Equal(ControlKind.Dropdown, SettingsMenuFixture.Row<DropdownRow>("Mode").Kind, "Mode");
        Assert.Equal(ControlKind.Dropdown, SettingsMenuFixture.Row<DropdownRow>("Color").Kind, "Color");
        Assert.Equal(ControlKind.Text, SettingsMenuFixture.Row<TextRow>("Name").Kind, "Name");
        Assert.Equal(ControlKind.Text, SettingsMenuFixture.Row<TextRow>("Seed").Kind, "Seed");

        Assert.Equal("Custom label", SettingsMenuFixture.Row<TextRow>("Name").Label.text, "Custom label");
        Assert.Equal("Seed", SettingsMenuFixture.Row<TextRow>("Seed").Label.text, "Prettified label");
        Assert.Equal("Fast Mode " + UiText.Get(UiText.RestartSuffix, language), SettingsMenuFixture.Row<ToggleRow>("FastMode").Label.text, "Restart suffix");
        Assert.Equal(3, SettingsMenuFixture.Row<DropdownRow>("Color").Dropdown.options.Count, "Color options");
        Assert.Equal("5", SettingsMenuFixture.Row<SliderRow>("Count").DisplayedText, "Count display");
        Assert.Equal("1.5", SettingsMenuFixture.Row<SliderRow>("Speed").DisplayedText, "Speed display");
        Assert.Equal("Cat", SettingsMenuFixture.Row<TextRow>("Name").Input.text, "Name text");
    }
}
