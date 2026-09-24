using System.Collections.Generic;
using CatLib.Tests.Framework;
using CatLib.UI;
using UnityEngine.EventSystems;

namespace CatLib.Tests.Suites.Ui;

public sealed class ContextStripTest : TestCase
{
    public override string Suite => "Ui";

    public override int Order => 12;

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        using var sandbox = new UiRowsSandbox("UiContext");
        foreach (var step in SettingsMenuFixture.SelectMod(context, sandbox.Settings))
        {
            yield return step;
        }

        var controller = SettingsMenuFixture.Tab.Controller;
        var eventSystem = EventSystem.current;
        Assert.NotNull(eventSystem, "EventSystem.current");

        var count = SettingsMenuFixture.Row<SliderRow>("Count");
        eventSystem.SetSelectedGameObject(count.Control.gameObject);
        yield return Wait.Frames(2);

        var language = UiText.LanguageCode;
        context.Note("Context: " + controller.ContextLabel.text.Replace("\n", " | "));
        Assert.True(ReferenceEquals(count, controller.ContextRow), "The selected row must drive the context strip");
        Assert.Equal(ContextText.For(sandbox.Count, language), controller.ContextLabel.text, "Context strip text");

        var color = SettingsMenuFixture.Row<DropdownRow>("Color");
        eventSystem.SetSelectedGameObject(color.Control.gameObject);
        yield return Wait.Frames(2);

        Assert.Equal(ContextText.For(sandbox.Color, language), controller.ContextLabel.text, "Context strip follows the selection");

        eventSystem.SetSelectedGameObject(null);
        yield return Wait.Frames(2);

        Assert.Equal(UiText.Get(UiText.HoverHint, language), controller.ContextLabel.text, "Hint without a focused row");
    }
}
