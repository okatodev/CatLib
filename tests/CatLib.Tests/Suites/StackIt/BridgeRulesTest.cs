using System.Collections.Generic;
using CatLib.Tests.Framework;
using StackIt.Logic;

namespace CatLib.Tests.Suites.StackIt;

public sealed class BridgeRulesTest : TestCase
{
    public override string Suite => "StackIt";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        Assert.Equal(BridgeVerdict.Plain, BridgeRules.Judge(new[] { CellSupport.Main, CellSupport.Main }), "All cells on the parcel below is the game's own case");
        Assert.Equal(BridgeVerdict.Bridge, BridgeRules.Judge(new[] { CellSupport.Main, CellSupport.Other, CellSupport.Main, CellSupport.Other }),
            "A 2x2 parcel across the joint of two parcels is a bridge");
        Assert.Equal(BridgeVerdict.Bridge, BridgeRules.Judge(new[] { CellSupport.Main, CellSupport.Other, CellSupport.Other, CellSupport.Other }),
            "A parcel on four parcels is a bridge too");
        Assert.Equal(BridgeVerdict.Blocked, BridgeRules.Judge(new[] { CellSupport.Main, CellSupport.Other, CellSupport.Missing }),
            "A cell above empty space blocks the bridge, nothing hangs over");
        Assert.Equal(BridgeVerdict.Blocked, BridgeRules.Judge(new[] { CellSupport.Main, CellSupport.Taken }), "A taken cell blocks it");
        Assert.Equal(BridgeVerdict.Blocked, BridgeRules.Judge(new CellSupport[0]), "A parcel without cells is never placed");

        Assert.True(BridgeRules.IsLevel(0.810f, 0.819f), "Standard and StandardLong tops differ by 9 mm and count as level");
        Assert.True(BridgeRules.IsLevel(0.982f, 1.0f), "Two Standards next to a Tall differ by 18 mm and count as level");
        Assert.False(BridgeRules.IsLevel(0.75f, 1.0f), "A step of a cell is not level");
        Assert.False(BridgeRules.IsLevel(0.5f, 0.53f), "3 cm is a step");

        Assert.True(BridgeRules.IsAligned(0.01f, -0.02f), "A cell centre a few millimetres off is the same cell");
        Assert.False(BridgeRules.IsAligned(0.125f, 0f), "Half a cell off is not aligned");
        Assert.False(BridgeRules.HasMoved(0.005f, 0f, 0.005f), "Jitter is not a move");
        Assert.True(BridgeRules.HasMoved(0f, 0.05f, 0f), "Lifting by 5 cm is a move");
        yield break;
    }
}
