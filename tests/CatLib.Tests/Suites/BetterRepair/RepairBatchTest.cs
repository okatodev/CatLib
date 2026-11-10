using System.Collections.Generic;
using BetterRepair.Logic;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.BetterRepair;

public sealed class RepairBatchTest : TestCase
{
    public override string Suite => "BetterRepair";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        Assert.False(RepairBatch.TakesOver(false, 4, 3, 3), "Turned off, the game repairs one parcel at a time");
        Assert.False(RepairBatch.TakesOver(true, 4, 1, 1), "One parcel on the table is the game's own repair");
        Assert.False(RepairBatch.TakesOver(true, 2, 3, 3), "More parcels than allowed keep the button off, like in the game");
        Assert.False(RepairBatch.TakesOver(true, 4, 3, 0), "Nothing damaged, nothing to repair");
        Assert.True(RepairBatch.TakesOver(true, 4, 4, 1), "Four parcels with one damaged are repaired by the mod");

        Assert.Equal(2, RepairBatch.Size(true, 2, 2, 2, 3), "Two damaged parcels, two at once, two sheets");
        Assert.Equal(3, RepairBatch.Size(true, 4, 4, 3, 99), "Only the damaged parcels use cardboard");
        Assert.Equal(2, RepairBatch.Size(true, 4, 4, 4, 2), "Two sheets left repair two of four parcels");
        Assert.Equal(0, RepairBatch.Size(true, 4, 3, 3, 0), "No cardboard, no repair");
        Assert.Equal(0, RepairBatch.Size(true, 2, 3, 3, 9), "Three parcels with two allowed are left to the game");
        Assert.Equal(8, RepairBatch.Size(true, 50, 8, 8, 99), "At most eight at once");
        Assert.Equal(2, RepairBatch.Clamp(0), "At least two at once");
        Assert.Equal(8, RepairBatch.Clamp(20), "At most eight at once");
        yield break;
    }
}
