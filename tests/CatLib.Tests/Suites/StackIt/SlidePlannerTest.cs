using System;
using System.Collections.Generic;
using System.Numerics;
using CatLib.Tests.Framework;
using StackIt.Logic;

namespace CatLib.Tests.Suites.StackIt;

public sealed class SlidePlannerTest : TestCase
{
    public override string Suite => "StackIt";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var small = SlidePlanner.Plan(Vector2.Zero, new[] { new Vector2(0.125f, 0f) }, new[] { new Vector2(-0.125f, 0f) });
        Assert.True(small.Slides, "A 2x1 parcel centred on the joint slides off");
        Assert.True(Math.Abs(small.Direction.X + 1f) < 0.001f, "towards the side that is gone");
        Assert.True(Math.Abs(small.Distance - SlidePlanner.Margin) < 0.001f, "just far enough for its centre, which is on the joint, to pass the edge of the parcel that stayed");

        var veryLong = SlidePlanner.Plan(Vector2.Zero,
            new[] { new Vector2(-0.5f, 0f), new Vector2(-0.25f, 0f), new Vector2(0f, 0f), new Vector2(0.25f, 0f) },
            new[] { new Vector2(0.5f, 0f) });
        Assert.True(Math.Abs(veryLong.Distance - (0.375f + SlidePlanner.Margin)) < 0.001f, "A 5x2 parcel resting mostly on one parcel slides further, past that parcel's edge");

        var overhang = SlidePlanner.Plan(Vector2.Zero, new[] { new Vector2(-0.375f, 0f) }, new[] { new Vector2(0.125f, 0f), new Vector2(0.375f, 0f) });
        Assert.Equal(SlidePlanner.MinimumDistance, overhang.Distance, "A parcel whose centre is already past the edge only moves a little before falling");

        var nothing = SlidePlanner.Plan(Vector2.Zero, new Vector2[0], new[] { new Vector2(0.125f, 0f) });
        Assert.True(nothing.HasDirection && !nothing.Slides, "With nothing left under it, it just falls");
        Assert.False(SlidePlanner.Plan(Vector2.Zero, new[] { Vector2.One }, new Vector2[0]).HasDirection, "Nothing lost, nothing to plan");

        var far = SlidePlanner.Plan(Vector2.Zero, new[] { new Vector2(-5f, 0f) }, new[] { new Vector2(1f, 0f) });
        Assert.True(far.Distance <= SlidePlanner.MaximumDistance, "A slide is never longer than a metre");
        yield break;
    }
}
