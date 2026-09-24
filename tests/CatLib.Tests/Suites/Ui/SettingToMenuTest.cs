using System.Collections.Generic;
using CatLib.Tests.Framework;
using CatLib.UI;

namespace CatLib.Tests.Suites.Ui;

public sealed class SettingToMenuTest : TestCase
{
    public override string Suite => "Ui";

    public override int Order => 8;

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        using var sandbox = new UiRowsSandbox("UiRead");
        foreach (var step in SettingsMenuFixture.SelectMod(context, sandbox.Settings))
        {
            yield return step;
        }

        var text = sandbox.Config.ContentWith("General", "Enabled", "false");
        text = Settings.ConfigSandbox.ReplaceValue(text, "General", "Count", "3");
        text = Settings.ConfigSandbox.ReplaceValue(text, "Mode", "Color", "Green");
        text = Settings.ConfigSandbox.ReplaceValue(text, "Text", "Name", "Edited");
        System.IO.File.WriteAllText(sandbox.Config.FilePath, text);

        yield return Wait.Until(() => sandbox.Config.Reports.Count >= 1, 5, "the edited file to be reloaded");
        yield return Wait.Frames(2);

        Assert.False(SettingsMenuFixture.Row<ToggleRow>("Enabled").Toggle.isOn, "Toggle after a file edit");
        Assert.Equal(3f, SettingsMenuFixture.Row<SliderRow>("Count").Slider.value, "Slider after a file edit");
        Assert.Equal("3", SettingsMenuFixture.Row<SliderRow>("Count").DisplayedText, "Slider text after a file edit");
        Assert.Equal(1, SettingsMenuFixture.Row<DropdownRow>("Color").Dropdown.value, "Dropdown after a file edit");
        Assert.Equal("Edited", SettingsMenuFixture.Row<TextRow>("Name").Input.text, "Text after a file edit");

        sandbox.Count.Entry.Value = 9;
        yield return Wait.Frames(2);

        Assert.Equal(9f, SettingsMenuFixture.Row<SliderRow>("Count").Slider.value, "Slider after a change from code");
    }
}
