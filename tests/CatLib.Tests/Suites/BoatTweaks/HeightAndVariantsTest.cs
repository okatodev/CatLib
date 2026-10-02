using System.Collections.Generic;
using BoatTweaks.Logic;
using BoatTweaks;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.BoatTweaks;

public sealed class HeightAndVariantsTest : TestCase
{
    public override string Suite => "BoatTweaks";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        Assert.Equal((1.5f, 3f), HeightRule.Apply(1.5f, 3f, 1f, 1f), "Scale 1 keeps the game values");
        Assert.Equal((3f, 6f), HeightRule.Apply(1.5f, 3f, 2f, 2f), "Both values scale");
        Assert.Equal((3f, 3f), HeightRule.Apply(1.5f, 3f, 3f, 1f), "The approved height never exceeds the maximum");
        Assert.Equal((-1f, 6f), HeightRule.Apply(-1f, 3f, 2f, 2f), "No approved height stays without one");
        Assert.Equal((6f, 12f), HeightRule.Apply(1.5f, 3f, 99f, 99f), "Scales are clamped to the allowed range");
        Assert.Equal((0.75f, 1.5f), HeightRule.Apply(1.5f, 3f, 0f, 0f), "Scales below the minimum use the minimum");
        Assert.Equal(1.75f, HeightRule.VisualHeight(3.25f, 3f, 1.5f), "The approved height mark goes down with the approved height and keeps its offset");
        Assert.Equal(3.25f, HeightRule.VisualHeight(1.75f, 1.5f, 3f), "The approved height mark goes up with the approved height and keeps its offset");
        Assert.Equal(2f, HeightRule.VisualHeight(2f, 0f, 3f), "A boat without an approved height keeps its mark");

        Assert.SequenceEqual(new[] { 1, 2, 4 }, VariantList.Parse("4, 2;1 2"), "Sorted distinct numbers with any separator");
        Assert.SequenceEqual(new int[0], VariantList.Parse("  "), "Empty text means no restriction");
        Assert.SequenceEqual(new[] { 3 }, VariantList.Parse("a, -1, 0, 3"), "Invalid entries are ignored");

        Assert.True(DeckDecor.IsProp("Visuals", "prop_LD_Crate_Small (2)"), "Crates under Visuals are decoration");
        Assert.True(DeckDecor.IsProp("Visuals", "prop_LD_Lamp"), "Lamps under Visuals are decoration");
        Assert.False(DeckDecor.IsProp("Visuals", "Cube (6)"), "The deck floor under Visuals stays");
        Assert.False(DeckDecor.IsProp("Colliders", "prop_Something"), "Only direct children of Visuals count");
        Assert.False(DeckDecor.IsProp("Visuals", null), "Nameless objects are ignored");
        Assert.True(DeckDecor.IsLargeCrate("prop_LD_Crate_Standard"), "The standard crate covers 2x2 cells");
        Assert.True(DeckDecor.IsSmallCrate("prop_LD_Crate_Small_02 (1)") && !DeckDecor.IsLargeCrate("prop_LD_Crate_Small"), "Small crates cover two cells");
        Assert.True(DeckDecor.IsSmallProp("prop_LD_Bottle (2)") && DeckDecor.IsSmallProp("prop_LD_Lamp"), "Bottles and lamps fill single cells");
        Assert.False(DeckDecor.IsSmallProp("prop_LD_Rope_Small"), "Rope coils are wider than a cell and are not used");
        Assert.Equal((2, 2), DeckDecor.Footprint(0.46f, 0.49f, 0.25f), "A prop about two cells wide and deep covers 2x2");
        Assert.Equal((1, 2), DeckDecor.Footprint(0.48f, 0.22f, 0.25f), "A long prop covers a pair, whichever way it lies");
        Assert.Equal((1, 1), DeckDecor.Footprint(0.08f, 0.3f, 0.25f), "A thin prop still covers one cell");
        Assert.False(DeckPieces.IsKnown((1, 3)), "Props of a size no deck piece has are not used");
        Assert.True(DeckPieces.IsKnown((2, 2)) && DeckPieces.IsKnown((3, 3)) && DeckPieces.IsKnown((2, 5)) && DeckPieces.IsKnown((4, 5)), "Squares, big crates and long crates have pieces");
        Assert.Equal<(int, int)?>((2, 2), DeckDecor.ByName("prop_LD_Crate_Standard"), "Without a model the standard crate is still known as 2x2");
        Assert.True(DeckDecor.IsExcluded("prop_LD_Rope_Small") && DeckDecor.IsExcluded("prop_LD_Paddle (3)") && !DeckDecor.IsExcluded("prop_LD_Crate_Tall"), "Rope coils and paddles never decorate blocked cells");
        Assert.True(DeckDecor.IsFlat(0.05f, 0.25f) && !DeckDecor.IsFlat(0.3f, 0.25f), "Flat props like rope coils do not decorate blocked cells");

        var tracker = new InstanceTracker();
        Assert.False(tracker.Observe(System.IntPtr.Zero), "No manager at the start is not a change");
        Assert.True(tracker.Observe(new System.IntPtr(100)), "A manager appearing is a change");
        Assert.False(tracker.Observe(new System.IntPtr(100)), "The same manager is not a change");
        Assert.True(tracker.Observe(new System.IntPtr(200)), "A new manager after a level reload is a change");
        Assert.True(tracker.Observe(System.IntPtr.Zero), "The manager going away is a change");
        yield break;
    }
}
