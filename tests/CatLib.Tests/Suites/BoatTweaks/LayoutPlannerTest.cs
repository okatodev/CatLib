using System;
using System.Collections.Generic;
using System.Linq;
using BoatTweaks;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.BoatTweaks;

public sealed class LayoutPlannerTest : TestCase
{
    public override string Suite => "BoatTweaks";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var random = new Random(7);
        var all = Array.Empty<int>();

        Assert.Equal(LayoutPlanKind.KeepGame, LayoutPlanner.Choose(LayoutMode.Game, 4, all, 1, -1, true, random).Kind, "Game mode keeps the game layouts");
        Assert.Equal(LayoutPlanKind.Empty, LayoutPlanner.Choose(LayoutMode.Empty, 4, all, 1, -1, true, random).Kind, "Empty mode");
        Assert.Equal(LayoutPlanKind.KeepGame, LayoutPlanner.Choose(LayoutMode.Empty, 0, all, 1, -1, true, random).Kind, "An empty pool is left alone");

        Assert.Equal(1, LayoutPlanner.Choose(LayoutMode.FixedVariant, 4, all, 2, -1, true, random).VariantIndex, "Fixed variant 2 is index 1");
        Assert.Equal(3, LayoutPlanner.Choose(LayoutMode.FixedVariant, 4, all, 9, -1, true, random).VariantIndex, "A fixed variant above the pool size uses the last one");
        Assert.Equal(0, LayoutPlanner.Choose(LayoutMode.FixedVariant, 4, all, 0, -1, true, random).VariantIndex, "A fixed variant below 1 uses the first one");

        Assert.SequenceEqual(new[] { 0, 1, 3 }, LayoutPlanner.Candidates(4, new[] { 1, 2, 4, 7 }), "Allowed variants outside the pool are ignored");
        Assert.SequenceEqual(new[] { 0, 1 }, LayoutPlanner.Candidates(2, new[] { 5, 6 }), "No allowed variant in range means all of the pool");

        var seen = new HashSet<int>();
        var last = -1;
        for (var boat = 0; boat < 200; boat++)
        {
            var plan = LayoutPlanner.Choose(LayoutMode.GameVariants, 4, new[] { 1, 3 }, 1, last, true, random);
            Assert.True(plan.VariantIndex == 0 || plan.VariantIndex == 2, "Only allowed layouts are chosen");
            Assert.True(plan.VariantIndex != last, "No layout repeats in a row");
            seen.Add(plan.VariantIndex);
            last = plan.VariantIndex;
        }

        Assert.Equal(2, seen.Count, "Both allowed layouts are used");

        var single = LayoutPlanner.Choose(LayoutMode.GameVariants, 4, new[] { 2 }, 1, 1, true, random);
        Assert.Equal(1, single.VariantIndex, "With a single allowed layout it repeats rather than failing");

        var repeats = Enumerable.Range(0, 200).Select(_ => LayoutPlanner.Choose(LayoutMode.GameVariants, 2, all, 1, 0, false, random).VariantIndex).Count(index => index == 0);
        Assert.True(repeats > 0, "Without the repeat rule the last layout can come again");
        yield break;
    }
}
