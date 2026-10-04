using System.Collections.Generic;
using System.Numerics;
using CatLib.Tests.Framework;
using StackIt.Logic;

namespace CatLib.Tests.Suites.StackIt;

public sealed class SupportCheckTest : TestCase
{
    public override string Suite => "StackIt";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var left = new Vector3(0.25f, 0f, 0f);
        var right = new Vector3(0.25f, 0f, 0.25f);
        var still = SupportCheck.Evaluate(false, new[] { new HeldState(left, left, true), new HeldState(right, right, true) });
        Assert.False(still.MainLost || still.SupportLost || still.MovedTogether, "Nothing moved");

        var turned = SupportCheck.Evaluate(false, new[] { new HeldState(left, left + new Vector3(0.004f, 0f, 0.003f), true) });
        Assert.False(turned.SupportLost, "Positions are compared under the parcel at the centre, so a turning boat moves nothing");

        var carried = SupportCheck.Evaluate(true, new[] { new HeldState(left, left, true), new HeldState(right, right, true) });
        Assert.True(carried.MovedTogether && !carried.MainLost && !carried.SupportLost,
            "A stack carried as a whole, with every parcel under the bridge on it, keeps the bridge");

        var mainTaken = SupportCheck.Evaluate(true, new[] { new HeldState(left, left, false), new HeldState(right, right, false) });
        Assert.True(mainTaken.MainLost && !mainTaken.SupportLost, "The parcel under the centre was picked up and the others stayed behind");

        var sideTaken = SupportCheck.Evaluate(false, new[] { new HeldState(left, left, false), new HeldState(right, right, true) });
        Assert.True(sideTaken.SupportLost && !sideTaken.MainLost, "A side parcel was picked up");
        Assert.SequenceEqual(new[] { 0 }, sideTaken.Lost, "Only that side is lost");

        var shifted = SupportCheck.Evaluate(false, new[] { new HeldState(left, left + new Vector3(0f, 0.1f, 0f), true) });
        Assert.True(shifted.SupportLost, "A side parcel that moved under the bridge is lost");

        var partial = SupportCheck.Evaluate(true, new[] { new HeldState(left, left, true), new HeldState(right, right, false) });
        Assert.True(partial.SupportLost && !partial.MainLost, "Carried with one side but not the other: the other side is lost");
        Assert.SequenceEqual(new[] { 1 }, partial.Lost, "The side left behind is the lost one");

        var waiting = SupportCheck.Evaluate(true, new HeldState[0]);
        Assert.True(!waiting.MainLost && waiting.MovedTogether, "Without found side parcels a new root is taken as it is, parcels may be loaded under it later");
        Assert.False(SupportCheck.Evaluate(false, new HeldState[0]).MainLost, "Waiting side parcels do not make a bridge fall");
        yield break;
    }
}
