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
        Assert.Equal(1, model.Selected, "and select it");

        model.Press(DevKey.Down);
        model.Press(DevKey.Enter);
        Assert.True(model.Status.StartsWith("Broken: failed, InvalidOperationException"), "A failing command is reported, not thrown");

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
        yield break;
    }
}
