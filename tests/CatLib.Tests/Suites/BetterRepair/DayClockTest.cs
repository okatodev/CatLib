using System.Collections.Generic;
using BetterRepair.Logic;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.BetterRepair;

public sealed class DayClockTest : TestCase
{
    public override string Suite => "BetterRepair";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var clock = new DayClock();
        Assert.False(clock.Observe(0, out var changed), "The first period seen is not a new day, a loaded save may start in the day");
        Assert.False(changed, "and not a change");
        Assert.False(clock.Observe(0, out changed), "The same period again");
        Assert.False(changed, "is no change");
        Assert.False(clock.Observe(1, out changed), "Evening is not a new day");
        Assert.True(changed, "but a change");
        Assert.False(clock.Observe(2, out _), "Evening recap is not a new day");
        Assert.False(clock.Observe(3, out _), "Night is not a new day");
        Assert.False(clock.Observe(4, out _), "Dawn is not a new day");
        Assert.True(clock.Observe(5, out changed), "The dawn recap starts a new day, the game brings its cardboard back there");
        Assert.True(changed, "and it is a change");
        Assert.False(clock.Observe(0, out changed), "Day right after the dawn recap is the same new day, not a second one");
        Assert.True(changed, "but a change");
        clock.Observe(4, out _);
        Assert.True(clock.Observe(0, out _), "Day reached without the dawn recap still starts a new day");
        clock.Reset();
        Assert.False(clock.Observe(5, out _), "After a reset the first period seen is not a new day");
        yield break;
    }
}
