using System.Collections.Generic;
using CatLib.Tests.Framework;
using CatLib.UI;
using UnityEngine;
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

        var pointer = new Vector3(-10000f, -10000f, 0f);
        ModsPanel.PointerOverride = () => pointer;
        try
        {
            var controller = SettingsMenuFixture.Tab.Controller;
            var eventSystem = EventSystem.current;
            Assert.NotNull(eventSystem, "EventSystem.current");
            var language = UiText.LanguageCode;

            var count = SettingsMenuFixture.Row<SliderRow>("Count");
            eventSystem.SetSelectedGameObject(count.Control.gameObject);
            yield return Wait.Frames(2);

            context.Note("Context: " + controller.ContextLabel.text.Replace("\n", " | "));
            Assert.True(ReferenceEquals(count, controller.ContextRow), "The selected row must drive the context strip");
            Assert.Equal(ContextText.For(sandbox.Count, language), controller.ContextLabel.text, "Context strip text");

            var color = SettingsMenuFixture.Row<DropdownRow>("Color");
            eventSystem.SetSelectedGameObject(color.Control.gameObject);
            yield return Wait.Frames(2);

            Assert.Equal(ContextText.For(sandbox.Color, language), controller.ContextLabel.text, "Context strip follows the selection");

            eventSystem.SetSelectedGameObject(null);
            yield return Wait.Frames(2);

            Assert.Equal(UiText.Get(UiText.HoverHint, language), controller.ContextLabel.text, "Hint without a focused row and with the pointer away");

            var visibleRect = SettingsMenuFixture.Row<ToggleRow>("Enabled").Root.transform.TryCast<RectTransform>();
            var screen = RectTransformUtility.WorldToScreenPoint(null, visibleRect.TransformPoint(visibleRect.rect.center));
            var viewport = ModsTabBuilder.ViewportOf(SettingsMenuFixture.Tab.ContentScroll);
            Assert.True(RectTransformUtility.RectangleContainsScreenPoint(viewport, screen, null),
                $"Test setup: the Enabled row at {screen.x:0},{screen.y:0} must be inside the visible part of the settings pane");
            eventSystem.SetSelectedGameObject(count.Control.gameObject);
            yield return Wait.Frames(2);
            pointer = new Vector3(screen.x, screen.y, 0f);
            eventSystem.SetSelectedGameObject(null);
            yield return Wait.Frames(2);

            context.Note($"Pointer at {screen.x:0},{screen.y:0}: " + controller.ContextLabel.text.Replace("\n", " | "));
            Assert.Equal(ContextText.For(sandbox.Enabled, language), controller.ContextLabel.text,
                "After a click clears the selection, the row under a still pointer must drive the context strip");

            var hiddenRect = SettingsMenuFixture.Row<TextRow>("Seed").Root.transform.TryCast<RectTransform>();
            var hidden = RectTransformUtility.WorldToScreenPoint(null, hiddenRect.TransformPoint(hiddenRect.rect.center));
            if (!RectTransformUtility.RectangleContainsScreenPoint(viewport, hidden, null))
            {
                pointer = new Vector3(hidden.x, hidden.y, 0f);
                yield return Wait.Frames(2);
                Assert.Equal(UiText.Get(UiText.HoverHint, language), controller.ContextLabel.text,
                    "A row scrolled out of the visible area must not react to the pointer");
                context.Note("Checked that a row outside the visible area is ignored");
            }
        }
        finally
        {
            ModsPanel.PointerOverride = null;
            EventSystem.current?.SetSelectedGameObject(null);
        }
    }
}
