using System.Collections.Generic;
using CatLib.Tests.Framework;
using CatLib.Tests.Suites.Presentation;
using CatLib.UI;
using UnityEngine.EventSystems;

namespace CatLib.Tests.Suites.Ui;

public sealed class ResetButtonTest : TestCase
{
    public override string Suite => "Ui";

    public override int Order => 13;

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        using var sandbox = new UiRowsSandbox("UiReset");
        foreach (var step in SettingsMenuFixture.SelectMod(context, sandbox.Settings))
        {
            yield return step;
        }

        var controller = SettingsMenuFixture.Tab.Controller;
        var options = SettingsMenuFixture.Tab.Options;
        sandbox.Enabled.Entry.Value = false;
        sandbox.Count.Entry.Value = 9;
        sandbox.Mode.Entry.Value = SampleMode.HTTPMode;
        sandbox.Name.Entry.Value = "Tabby";
        sandbox.Secret.Entry.Value = "changed";
        var messages = PlayerMessages.Count;

        Assert.NotNull(controller.ResetButton, "The Mods panel must have a reset button");
        EventSystem.current?.SetSelectedGameObject(null);
        controller.ResetButton.onClick.Invoke();
        Assert.NotNull(options._actionOnRevertConfirmed, "The reset button must open the game's confirmation popup even when nothing is selected");
        Assert.True(sandbox.Count.Value == 9, "Nothing may change before the reset is confirmed");

        options.ConfirmRevertYesButton.onClick.Invoke();
        yield return Wait.Frames(2);

        Assert.True(sandbox.Enabled.Value, "Enabled after the reset");
        Assert.Equal(5, sandbox.Count.Value, "Count after the reset");
        Assert.Equal(SampleMode.FastMode, sandbox.Mode.Value, "Mode after the reset");
        Assert.Equal("Cat", sandbox.Name.Value, "Name after the reset");
        Assert.Equal("changed", sandbox.Secret.Value, "Hidden settings must keep their value");
        Assert.Equal(5f, SettingsMenuFixture.Row<SliderRow>("Count").Slider.value, "The slider must show the default");
        Assert.True(PlayerMessages.Count > messages, "The reset must be reported to the player");
        Assert.Equal(UiText.Format(UiText.MessageReset, UiText.LanguageCode, sandbox.Settings.DisplayName), PlayerMessages.Last, "Reset message");
    }
}
