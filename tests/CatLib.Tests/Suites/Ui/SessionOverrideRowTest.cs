using System.Collections.Generic;
using CatLib.Net;
using CatLib.Tests.Framework;
using CatLib.UI;
using UnityEngine.EventSystems;

namespace CatLib.Tests.Suites.Ui;

public sealed class SessionOverrideRowTest : TestCase
{
    public override string Suite => "Ui";

    public override int Order => 15;

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        using var sandbox = new UiRowsSandbox("UiOverride");
        foreach (var step in SettingsMenuFixture.SelectMod(context, sandbox.Settings))
        {
            yield return step;
        }

        var language = UiText.LanguageCode;
        var row = SettingsMenuFixture.Row<SliderRow>("Difficulty");
        try
        {
            var result = SessionSettings.Instance.Apply(new[] { new SessionSettingValue(sandbox.Settings.OwnerId, "Session", "Difficulty", "4") });
            Assert.Equal(1, result.Applied, "Override applied");
            EventSystem.current?.SetSelectedGameObject(row.Control.gameObject);
            yield return Wait.Frames(3);

            var controller = SettingsMenuFixture.Tab.Controller;
            var selected = EventSystem.current?.currentSelectedGameObject;
            context.Note("Label: " + row.Label.text + ", context: " + controller.ContextLabel.text.Replace("\n", " | "));
            Assert.Equal(4f, row.Slider.value, "The row must show the host value");
            Assert.True(selected != null && selected.Pointer == row.Control.gameObject.Pointer, "An overridden row must stay selectable for gamepad players");
            Assert.True(row.Control.interactable, "An overridden row must stay in the navigation");
            Assert.Equal(SettingRow.OverriddenAlpha, row.ValueGroup.alpha, "An overridden value must be dimmed");
            Assert.False(row.ValueGroup.blocksRaycasts, "An overridden value must ignore the mouse");
            Assert.Equal("Difficulty " + UiText.Get(UiText.HostSuffix, language), row.Label.text, "Host suffix in the label");
            Assert.True(controller.ContextLabel.text.Contains(UiText.Format(UiText.HostValue, language, "2")), "The context must show the player's own value");

            row.Slider.value = 5f;
            Assert.Equal(4f, row.Slider.value, "Moving an overridden slider must snap back to the host value");
            yield return Wait.Seconds(SliderRow.CommitDelayMilliseconds / 1000.0 + 0.2);
            Assert.Equal(2, sandbox.Difficulty.LocalValue, "An overridden row must never write the local value");
            Assert.Equal(4, sandbox.Difficulty.Value, "The host value stays in effect");

            SessionSettings.Instance.Clear();
            yield return Wait.Frames(3);

            Assert.Equal(2f, row.Slider.value, "The row must show the local value after the session");
            Assert.Equal(1f, row.ValueGroup.alpha, "The value must not be dimmed after the session");
            Assert.True(row.ValueGroup.blocksRaycasts, "The value must accept the mouse after the session");
            Assert.Equal("Difficulty", row.Label.text, "Label without the host suffix");
        }
        finally
        {
            SessionSettings.Instance.Clear();
            EventSystem.current?.SetSelectedGameObject(null);
        }
    }
}
