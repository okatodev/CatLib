using System.Collections.Generic;
using CatLib.Tests.Framework;
using StackIt.Logic;

namespace CatLib.Tests.Suites.StackIt;

public sealed class GhostHoldTest : TestCase
{
    public override string Suite => "StackIt";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        Assert.False(GhostHold.IsOver(false, false, 0.3f), "A falling parcel waits for the host's movement on another player's game");
        Assert.True(GhostHold.IsOver(false, true, 0.05f), "and moves as soon as the host's movement arrives");
        Assert.True(GhostHold.IsOver(false, false, GhostHold.MostSeconds), "or falls on its own when none comes");
        Assert.True(GhostHold.IsOver(true, false, 0f), "A parcel picked up or put down meanwhile is let go at once");
        yield break;
    }
}
