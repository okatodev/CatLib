using System.Collections.Generic;
using CatLib.Tests.Framework;
using StackIt.Logic;

namespace CatLib.Tests.Suites.StackIt;

public sealed class MarkGraphTest : TestCase
{
    public override string Suite => "StackIt";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var graph = new MarkGraph();
        graph.Add(1, -1, BehaviorConstraint.None);
        graph.Add(2, -1, BehaviorConstraint.Fragile);
        graph.Add(3, 1, BehaviorConstraint.Heavy);
        graph.AddBridge(3, new[] { 2 });
        Assert.True(graph.FragileBroken(2), "A fragile parcel with a bridge resting on it breaks, like with a parcel on top");
        Assert.False(graph.FragileBroken(1), "A plain parcel is never fragile-broken");
        Assert.True(graph.HeavyAbove(1), "The heavy bridge is the plain child of its centre parcel");
        Assert.True(graph.HeavyAbove(2), "A heavy bridge over a side parcel counts as heavy above it");

        var tower = new MarkGraph();
        tower.Add(10, -1, BehaviorConstraint.None);
        tower.Add(11, 10, BehaviorConstraint.None);
        tower.Add(20, -1, BehaviorConstraint.None);
        tower.Add(30, 20, BehaviorConstraint.None);
        tower.Add(31, 30, BehaviorConstraint.Heavy);
        tower.AddBridge(30, new[] { 11 });
        Assert.True(tower.HeavyAbove(11), "A heavy parcel on a bridge is above the bridge's side parcel");
        Assert.True(tower.HeavyAbove(10), "and above everything under that side parcel");
        Assert.True(tower.HeavyAbove(20), "and above the bridge's centre parcel, as in the game");

        var light = new MarkGraph();
        light.Add(1, -1, BehaviorConstraint.None);
        light.Add(2, -1, BehaviorConstraint.Fragile);
        light.Add(3, 1, BehaviorConstraint.Lover);
        light.AddBridge(3, new[] { 2, 3 });
        Assert.False(light.HeavyAbove(2), "No heavy parcel, nothing to damage");
        Assert.True(light.FragileBroken(2), "but a fragile one still breaks under any bridge");
        Assert.False(light.BridgeRestsOn(3), "A bridge never rests on itself");

        var loop = new MarkGraph();
        loop.Add(1, -1, BehaviorConstraint.None);
        loop.Add(2, -1, BehaviorConstraint.None);
        loop.AddBridge(1, new[] { 2 });
        loop.AddBridge(2, new[] { 1 });
        Assert.False(loop.HeavyAbove(1), "A broken graph with a loop ends without an endless walk");
        yield break;
    }
}
