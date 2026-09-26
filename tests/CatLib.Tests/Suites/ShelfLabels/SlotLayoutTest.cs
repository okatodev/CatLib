using System.Collections.Generic;
using CatLib.Tests.Framework;
using ShelfLabels;

namespace CatLib.Tests.Suites.ShelfLabels;

public sealed class SlotLayoutTest : TestCase
{
    public override string Suite => "ShelfLabels";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        Near((0.32f, 0f), SlotLayout.Offset(1, Placement.Right, 0.3f, 0.1f, 0.02f), "First slot to the right");
        Near((-0.64f, 0f), SlotLayout.Offset(2, Placement.Left, 0.3f, 0.1f, 0.02f), "Second slot to the left");
        Near((0f, -0.36f), SlotLayout.Offset(3, Placement.Below, 0.3f, 0.1f, 0.02f), "Third slot below");
        Assert.Equal(Placement.Left, SlotLayout.NameSide("prop_StorageTag_Left"), "A left holder prefers the left side");
        Assert.Equal(Placement.Right, SlotLayout.NameSide("prop_StorageTag_02"), "Others prefer the right side");
        Assert.Equal(Placement.Right, SlotLayout.ChooseSide(Placement.Right, 0, 0), "A tie keeps the preferred side");
        Assert.Equal(Placement.Left, SlotLayout.ChooseSide(Placement.Right, 2, 0), "A blocked side gives way");
        Assert.Equal(Placement.Left, SlotLayout.ChooseSide(Placement.Left, 1, 1), "Equally blocked keeps the preferred side");
        yield break;
    }

    private static void Near((float Right, float Up) expected, (float Right, float Up) actual, string what) =>
        Assert.True(System.Math.Abs(expected.Right - actual.Right) < 0.0001f && System.Math.Abs(expected.Up - actual.Up) < 0.0001f, $"{what}: expected {expected}, actual {actual}");
}
