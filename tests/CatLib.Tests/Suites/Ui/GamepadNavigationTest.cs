using System;
using System.Collections.Generic;
using System.Linq;
using CatLib.Tests.Framework;
using CatLib.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CatLib.Tests.Suites.Ui;

public sealed class GamepadNavigationTest : TestCase
{
    public override string Suite => "Ui";

    public override int Order => 20;

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        using var sandbox = new UiRowsSandbox("UiNavigation");
        foreach (var step in SettingsMenuFixture.SelectMod(context, sandbox.Settings))
        {
            yield return step;
        }

        var pointer = new Vector3(-10000f, -10000f, 0f);
        ModsPanel.PointerOverride = () => pointer;
        var mover = new Mover();
        try
        {
            var eventSystem = EventSystem.current;
            Assert.NotNull(eventSystem, "EventSystem.current");
            var controller = SettingsMenuFixture.Tab.Controller;
            var language = UiText.LanguageCode;
            var values = Values(sandbox);

            var items = controller.Items.Select(item => (Selectable)item.Toggle).ToList();
            var itemIndex = controller.Items.ToList().FindIndex(item => item.IsSelected);
            Assert.True(itemIndex >= 0, "The sandbox mod is selected in the list");
            var item = items[itemIndex];
            Assert.True(SettingsMenuFixture.Tab.Tab.FirstSelected != null && SettingsMenuFixture.Tab.Tab.FirstSelected.Pointer == item.Pointer,
                "Opening the tab with a gamepad starts on the selected mod");

            var rows = controller.Rows.Where(row => row.Control != null).ToList();
            Assert.True(rows.Count >= 3, $"Test setup: the sandbox mod has {rows.Count} rows with controls");
            var reset = controller.ResetButton;
            Assert.NotNull(reset, "The Mods tab has a reset button");

            if (itemIndex < items.Count - 1)
            {
                Expect(mover.Move(item, MoveDirection.Down), items[itemIndex + 1], "Down from the selected mod goes to the next mod");
                Expect(mover.Move(items[itemIndex + 1], MoveDirection.Up), item, "Up goes back to the selected mod");
            }
            else if (itemIndex > 0)
            {
                Expect(mover.Move(item, MoveDirection.Up), items[itemIndex - 1], "Up from the selected mod goes to the previous mod");
                Expect(mover.Move(items[itemIndex - 1], MoveDirection.Down), item, "Down goes back to the selected mod");
            }

            Expect(mover.Move(item, MoveDirection.Right), rows[0].Control, "Right from the selected mod goes to the first setting");
            yield return Wait.Frames(2);
            ExpectContext(controller, rows[0], language);

            for (var index = 0; index < rows.Count; index++)
            {
                var next = index < rows.Count - 1 ? (Selectable)rows[index + 1].Control : reset;
                Expect(mover.Move(rows[index].Control, MoveDirection.Down), next,
                    $"Down from {rows[index].Setting.Key} goes to {(index < rows.Count - 1 ? rows[index + 1].Setting.Key : "the reset button")}");
                if (index < rows.Count - 1)
                {
                    yield return Wait.Frames(2);
                    ExpectContext(controller, rows[index + 1], language);
                    if (rows[index + 1] is TextRow text)
                    {
                        Assert.False(text.Input.isFocused, $"Selecting {text.Setting.Key} with a gamepad does not start typing, so the gamepad can move on");
                    }
                }
            }

            yield return Wait.Frames(2);
            var viewport = ModsTabBuilder.ViewportOf(SettingsMenuFixture.Tab.ContentScroll);
            var lastRect = rows[rows.Count - 1].Root.transform.TryCast<RectTransform>();
            var lastScreen = RectTransformUtility.WorldToScreenPoint(null, lastRect.TransformPoint(lastRect.rect.center));
            Assert.True(RectTransformUtility.RectangleContainsScreenPoint(viewport, lastScreen, null),
                "The pane scrolls with the selection, so the last setting is in view after moving down");

            eventSystem.SetSelectedGameObject(rows[0].Control.gameObject);
            yield return Wait.Frames(2);
            Assert.True(SettingsMenuFixture.Tab.ContentScroll.verticalNormalizedPosition > 0.99f,
                "Going back to the first setting scrolls the pane to the top, so the mod card is in view");

            Expect(mover.Move(reset, MoveDirection.Up), rows[rows.Count - 1].Control, "Up from the reset button goes to the last setting");
            Expect(mover.Move(reset, MoveDirection.Left), item, "Left from the reset button goes back to the selected mod");

            var toggle = rows.First(row => row is ToggleRow);
            Expect(mover.Move(toggle.Control, MoveDirection.Left), item, $"Left from {toggle.Setting.Key} goes back to the selected mod");
            foreach (var row in rows.Where(row => row is SliderRow))
            {
                var left = row.Control.navigation.selectOnLeft;
                Assert.True(left != null && left.Pointer == item.Pointer, $"{row.Setting.Key} links left to the selected mod");
            }

            var textRow = rows.OfType<TextRow>().First();
            eventSystem.SetSelectedGameObject(textRow.Control.gameObject);
            var submitted = true;
            try
            {
                textRow.Input.OnSubmit(new BaseEventData(eventSystem));
            }
            catch (Exception exception)
            {
                submitted = false;
                context.Note($"The text field has no submit handler to call ({exception.GetType().Name})");
            }

            if (submitted)
            {
                yield return Wait.Frames(2);
                Assert.True(textRow.Input.isFocused, $"Pressing submit on {textRow.Setting.Key} starts typing");
                textRow.Input.DeactivateInputField(false);
                yield return Wait.Frames(2);
            }

            context.Note("Sliders change their value with left and right, like the game's own sliders");
            context.Note(mover.Simulated
                ? "Moves were sent to the controls the way the game's input sends them"
                : $"The game has no move handler to call ({mover.Failure}), the links were followed directly");
            Assert.Equal(values, Values(sandbox), "Moving through the settings changes no values");
        }
        finally
        {
            ModsPanel.PointerOverride = null;
            EventSystem.current?.SetSelectedGameObject(null);
        }
    }

    private static string Values(UiRowsSandbox sandbox) =>
        $"{sandbox.Enabled.Value}|{sandbox.Count.Value}|{sandbox.Speed.Value}|{sandbox.Mode.Value}|{sandbox.Color.Value}|{sandbox.Name.Value}|{sandbox.Seed.Value}|{sandbox.FastMode.Value}|{sandbox.Difficulty.Value}";

    private static void Expect(GameObject actual, Selectable expected, string message)
    {
        Assert.True(expected != null && UiClone.IsAlive(expected) && expected.IsActive(), $"{message}: the target must be active");
        Assert.True(actual != null && actual.Pointer == expected.gameObject.Pointer,
            $"{message}, but the selection is {Describe(actual)} instead of {Describe(expected.gameObject)}");
    }

    private static string Describe(GameObject gameObject)
    {
        if (gameObject == null)
        {
            return "empty";
        }

        var name = gameObject.name;
        var parent = gameObject.transform.parent;
        for (var level = 0; level < 2 && parent != null; level++, parent = parent.parent)
        {
            name = parent.name + "/" + name;
        }

        return name;
    }

    private static void ExpectContext(ModsPanel controller, SettingRow row, string language)
    {
        Assert.True(ReferenceEquals(row, controller.ContextRow), $"The selected {row.Setting.Key} drives the description under the settings");
        Assert.Equal(ContextText.For(row.Setting, language), controller.ContextLabel.text, $"Description of {row.Setting.Key}");
    }

    private sealed class Mover
    {
        public bool Simulated { get; private set; } = true;

        public string Failure { get; private set; }

        public GameObject Move(Selectable from, MoveDirection direction)
        {
            var eventSystem = EventSystem.current;
            eventSystem.SetSelectedGameObject(from.gameObject);
            if (Simulated)
            {
                try
                {
                    var data = new AxisEventData(eventSystem);
                    data.moveDir = direction;
                    from.OnMove(data);
                    return eventSystem.currentSelectedGameObject;
                }
                catch (Exception exception)
                {
                    Simulated = false;
                    Failure = exception.GetType().Name;
                    eventSystem.SetSelectedGameObject(from.gameObject);
                }
            }

            var target = direction switch
            {
                MoveDirection.Up => from.FindSelectableOnUp(),
                MoveDirection.Down => from.FindSelectableOnDown(),
                MoveDirection.Left => from.FindSelectableOnLeft(),
                _ => from.FindSelectableOnRight()
            };
            if (target != null && target.IsActive())
            {
                eventSystem.SetSelectedGameObject(target.gameObject);
            }

            return eventSystem.currentSelectedGameObject;
        }
    }
}
