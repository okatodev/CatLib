using System;
using System.Collections.Generic;
using System.Linq;
using CatLib.DevTools;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.DevTools;

public sealed class DevMenuModelTest : TestCase
{
    public override string Suite => "DevTools";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var model = new DevMenuModel();
        model.Press(DevKey.Enter);
        model.Press(DevKey.Right);
        model.Press(DevKey.Digit1);
        Assert.Null(model.Status, "An empty menu ignores every key");

        var runs = new List<string>();
        var enabled = false;
        model.Add(DevItem.Command("Inspect", "Entity dump", () => runs.Add("dump")));
        model.Add(DevItem.Command("Inspect", "UI dump", () => "written"));
        var failing = model.Add(DevItem.Command("Inspect", "Broken", () => throw new InvalidOperationException("Expected exception thrown by CatLib.Tests")));
        model.Add(DevItem.Command("Network", "Probe", () => runs.Add("probe")));
        model.Add(DevItem.Toggle("Network", "Verbose", () => enabled, value => enabled = value, "Logs every message"));

        Assert.SequenceEqual(new[] { "Inspect", "Network" }, model.Groups, "Groups keep the order of registration");
        Assert.Equal("Inspect", model.CurrentGroup, "The first group is shown first");

        model.Press(DevKey.Enter);
        Assert.SequenceEqual(new[] { "dump" }, runs, "Enter runs the selected command");
        Assert.Equal("Entity dump: done", model.Status, "A command without a result reports done");

        model.Press(DevKey.Digit2);
        Assert.Equal("UI dump: written", model.Status, "Digits run the command with that number");
        Assert.False(model.StatusFailed, "A command that worked is not marked as failed");
        Assert.Equal(1, model.Selected, "and select it");

        model.Press(DevKey.Down);
        model.Press(DevKey.Enter);
        Assert.True(model.Status.StartsWith("Broken: failed, InvalidOperationException"), "A failing command is reported, not thrown");
        Assert.True(model.StatusFailed, "A failing command is marked as failed");

        model.Press(DevKey.Down);
        Assert.Equal(0, model.Selected, "Down wraps to the first item");
        model.Press(DevKey.Up);
        Assert.Equal(2, model.Selected, "Up wraps to the last item");

        model.Press(DevKey.Digit9);
        Assert.Equal(2, model.Selected, "A digit without an item changes nothing");

        model.Press(DevKey.Right);
        Assert.Equal("Network", model.CurrentGroup, "Right shows the next group");
        Assert.Equal(0, model.Selected, "Selection starts at the top of a group");
        Assert.Equal("off", model.Current[1].State, "A toggle shows its state");
        model.Press(DevKey.Digit2);
        Assert.True(enabled, "A toggle flips its value");
        Assert.Equal("Verbose: on", model.Status, "and reports the new state");
        Assert.Equal("Logs every message", model.SelectedItem.Hint, "The hint of the selected item");

        model.Press(DevKey.Right);
        Assert.Equal("Inspect", model.CurrentGroup, "Right wraps to the first group");
        model.Press(DevKey.Left);
        Assert.Equal("Network", model.CurrentGroup, "Left wraps to the last group");

        model.Press(DevKey.Left);
        model.Press(DevKey.Digit3);
        Assert.True(model.Remove(failing), "Items can be removed");
        Assert.Equal(1, model.Selected, "Selection stays inside the shorter group");
        Assert.Equal(2, model.Current.Count, "Two items are left");
        Assert.Throws<ArgumentException>(() => DevItem.Command("Inspect", "", () => { }), "An item needs a label");

        Assert.SequenceEqual(new[] { ("Inspect", 2), ("Network", 2) }, model.GroupSizes, "Every group with the number of its commands");
        model.Press(DevKey.NextGroup);
        Assert.Equal("Network", model.CurrentGroup, "Page Down shows the next group");
        model.Press(DevKey.PreviousGroup);
        Assert.Equal("Inspect", model.CurrentGroup, "Page Up shows the previous group");
        model.Press(DevKey.Last);
        Assert.Equal(1, model.Selected, "End selects the last command");
        model.Press(DevKey.First);
        Assert.Equal(0, model.Selected, "Home selects the first command");
        Assert.True(model.SelectGroup(1), "A click selects a group");
        Assert.Equal("Network", model.CurrentGroup, "The clicked group is shown");
        Assert.False(model.SelectGroup(5), "A group that is not there is not selected");
        Assert.True(model.SelectItem(1), "A click selects a command");
        Assert.Equal(1, model.Selected, "The clicked command is selected");
        Assert.False(model.SelectItem(-1), "A command that is not there is not selected");

        Assert.Equal(0, DevMenuModel.FirstVisible(5, 4, 10), "A short list is shown whole");
        Assert.Equal(0, DevMenuModel.FirstVisible(30, 3, 10), "The top of a long list stays at the top");
        Assert.Equal(10, DevMenuModel.FirstVisible(30, 15, 10), "The selection is kept in the middle");
        Assert.Equal(20, DevMenuModel.FirstVisible(30, 29, 10), "The end of a long list stays at the bottom");
        yield break;
    }
}
