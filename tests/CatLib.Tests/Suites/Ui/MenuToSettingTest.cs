using System.Collections.Generic;
using System.IO;
using CatLib.Tests.Framework;
using CatLib.Tests.Suites.Presentation;
using CatLib.UI;

namespace CatLib.Tests.Suites.Ui;

public sealed class MenuToSettingTest : TestCase
{
    public override string Suite => "Ui";

    public override int Order => 7;

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        using var sandbox = new UiRowsSandbox("UiWrite");
        foreach (var step in SettingsMenuFixture.SelectMod(context, sandbox.Settings))
        {
            yield return step;
        }

        SettingsMenuFixture.Row<ToggleRow>("Enabled").Toggle.isOn = false;
        SettingsMenuFixture.Row<DropdownRow>("Mode").Dropdown.value = 1;
        SettingsMenuFixture.Row<DropdownRow>("Color").Dropdown.value = 2;
        SettingsMenuFixture.Row<TextRow>("Name").Input.onEndEdit.Invoke("Tabby");
        SettingsMenuFixture.Row<TextRow>("Seed").Input.onEndEdit.Invoke("7");
        SettingsMenuFixture.Row<TextRow>("Seed").Input.onEndEdit.Invoke("not a number");

        var count = SettingsMenuFixture.Row<SliderRow>("Count");
        var speed = SettingsMenuFixture.Row<SliderRow>("Speed");
        count.Slider.value = 8f;
        speed.Slider.value = 2.3456f;

        Assert.False(sandbox.Enabled.Value, "Toggle must apply immediately");
        Assert.Equal(SampleMode.SlowMode, sandbox.Mode.Value, "Enum dropdown must apply immediately");
        Assert.Equal("Blue", sandbox.Color.Value, "Listed dropdown must apply immediately");
        Assert.Equal("Tabby", sandbox.Name.Value, "Text must apply on end edit");
        Assert.Equal(7, sandbox.Seed.Value, "Number text must apply on end edit");
        Assert.Equal("7", SettingsMenuFixture.Row<TextRow>("Seed").Input.text, "Invalid text must be reverted");
        Assert.Equal(5, sandbox.Count.Value, "Slider must not apply while it is being moved");
        Assert.True(count.HasPendingValue, "Slider must hold a pending value");
        Assert.Equal("8", count.DisplayedText, "Slider text must follow the handle immediately");

        yield return Wait.Seconds(SliderRow.CommitDelayMilliseconds / 1000.0 + 0.2);

        Assert.Equal(8, sandbox.Count.Value, "Slider must apply after the commit delay");
        Assert.Equal(2.35f, sandbox.Speed.Value, "Fractional slider must apply rounded to two digits");
        Assert.Equal("2.35", speed.DisplayedText, "Fractional slider text");

        var file = File.ReadAllText(sandbox.Config.FilePath);
        Assert.True(file.Contains("Enabled = false"), "The toggle change must be saved to the file");
        Assert.True(file.Contains("Count = 8"), "The slider change must be saved to the file");
        Assert.True(file.Contains("Name = Tabby"), "The text change must be saved to the file");
        context.Note("One warning log line about \"not a number\" is expected");
    }
}
