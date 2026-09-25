using System.Collections.Generic;
using System.Linq;
using BoatTweaks;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.BoatTweaks;

public sealed class DeckGenerationTest : TestCase
{
    public const string BoatPrefill = "{\"storedEntities\":[{\"addressableKey\":\"p_Entity_Carryable_Parcel_VeryLong\",\"anchorCell\":{\"x\":5,\"y\":4},\"yawSteps\":3," +
        "\"footprintCells\":[{\"x\":4,\"y\":6},{\"x\":5,\"y\":6},{\"x\":4,\"y\":5},{\"x\":5,\"y\":5}],\"children\":[{\"addressableKey\":\"p_Entity_Carryable_Parcel_Standard\"," +
        "\"footprintCells\":[{\"x\":7,\"y\":7}]}]},{\"addressableKey\":\"p_Entity_Carryable_Parcel_OddCube_02\",\"footprintCells\":[{\"x\":1,\"y\":5}]}]}";

    public override string Suite => "BoatTweaks";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var reserved = PrefillFootprint.Parse(BoatPrefill);
        Assert.SequenceEqual(new[] { (4, 6), (5, 6), (4, 5), (5, 5), (1, 5) }, reserved, "Deck cells of arriving parcels, without parcels stacked on them");
        Assert.Equal(0, PrefillFootprint.Parse("not json").Count, "Broken prefill text reserves nothing");
        Assert.Equal(0, PrefillFootprint.Parse(null).Count, "No prefill reserves nothing");

        var first = PatternGenerator.Generate(8, 8, 0.25f, true, reserved, 42);
        var again = PatternGenerator.Generate(8, 8, 0.25f, true, reserved, 42);
        var other = PatternGenerator.Generate(8, 8, 0.25f, true, reserved, 43);
        context.Note("Seed 42: " + first);
        Assert.Equal(first.ToString(), again.ToString(), "The same seed gives the same deck on every player");
        Assert.True(first.ToString() != other.ToString(), "Another seed gives another deck");
        Assert.True(first.BlockedCells().All(cell => !reserved.Contains(cell)), "Cells of arriving parcels stay free");
        var hits = Enumerable.Range(1, 200)
            .Select(seed => PatternGenerator.Generate(8, 8, 0.6f, false, reserved, seed))
            .Sum(deck => deck.BlockedCells().Count(reserved.Contains));
        Assert.Equal(0, hits, "Across 200 dense decks no cell of an arriving parcel is ever taken");
        Assert.True(first.BlockedCells().All(cell => PatternGenerator.IsEdge(cell.Row, cell.Column, 8, 8)), "Edges only keeps the middle free");
        Assert.True(first.BlockedCount >= 14 && first.BlockedCount <= 17, $"About a quarter of 64 cells is taken, got {first.BlockedCount}");

        DeckPattern.TryParse(".##..##.\n.##..##.\n........\n........\n........\n........\n..##....\n..##....", out var saved, out _);
        var variants = new List<ISet<(int Row, int Column)>>
        {
            new HashSet<(int, int)> { (1, 2), (1, 5) },
            new HashSet<(int, int)> { (4, 4), (3, 3) },
            new HashSet<(int, int)> { (6, 3) },
            new HashSet<(int, int)>()
        };
        Assert.SequenceEqual(new[] { 1, 3 }, LayoutFit.FittingVariants(saved, variants), "Only variants whose arriving parcels miss the layout are kept");
        Assert.True(LayoutFit.Fits(saved, null), "A variant without arriving parcels always fits");

        var anywhere = PatternGenerator.Generate(8, 8, 0.6f, false, null, 7);
        Assert.True(anywhere.BlockedCells().Any(cell => !PatternGenerator.IsEdge(cell.Row, cell.Column, 8, 8)), "Without the edge rule the middle can be taken");
        Assert.True(PatternGenerator.Generate(8, 8, 5f, false, null, 1).BlockedCount <= 40, "Density is clamped to the maximum");
        yield break;
    }
}
