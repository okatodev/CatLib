using System.Collections.Generic;
using System.Linq;
using BoatTweaks;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.BoatTweaks;

public sealed class DeckPatternTest : TestCase
{
    public override string Suite => "BoatTweaks";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var text = "Boat Tweaks layout\nSaved from p_Entity_Storage_OnBoat_72_6_02\n#..#\r\n....\n.xX.\n";
        Assert.True(DeckPattern.TryParse(text, out var pattern, out var error), "Parse: " + error);
        Assert.Equal(3, pattern.Rows, "Notes are ignored, grid rows are kept");
        Assert.Equal(4, pattern.Columns, "Columns");
        Assert.SequenceEqual(new[] { (0, 0), (0, 3), (2, 1), (2, 2) }, pattern.BlockedCells(), "x and X count as taken");
        Assert.SequenceEqual(new[] { "#..#", "....", ".##." }, pattern.RowTexts(), "Written back with # and .");
        Assert.Equal("#..#/..../.##.", pattern.ToString(), "Compact form");

        Assert.False(DeckPattern.TryParse("#..\n#.", out _, out error), "Rows of different lengths are refused");
        Assert.Equal("rows have different lengths", error, "Reason for different lengths");
        Assert.False(DeckPattern.TryParse("just notes", out _, out error), "Text without a grid is refused");
        Assert.False(DeckPattern.IsGridLine("Saved from boat"), "Notes are not grid rows");

        var available = new[,] { { false, true, true }, { false, false, true } };
        var fromGame = DeckPattern.FromAvailability(available, new HashSet<(int, int)> { (1, 1) }, out var excluded);
        Assert.SequenceEqual(new[] { "#..", "#.." }, fromGame.RowTexts(), "Taken cells are saved, cells of arriving parcels are left out");
        Assert.Equal(1, excluded, "One cell of an arriving parcel was left out");
        Assert.Equal(3, DeckPattern.FromAvailability(available, null, out _).BlockedCount, "Without arriving parcels every taken cell is saved");

        var plans = new[]
        {
            DeckPlan.Game,
            DeckPlan.Empty,
            DeckPlan.FromPattern("my|deck", pattern),
            DeckPlan.FromGenerator(12345, 0.25f, true)
        };

        foreach (var plan in plans)
        {
            var decoded = DeckPlan.Decode(plan.Encode());
            Assert.Equal(plan.Kind, decoded.Kind, "Round trip of " + plan.Kind);
            Assert.Equal(plan.Seed, decoded.Seed, "Seed of " + plan.Kind);
            Assert.Equal(plan.EdgesOnly, decoded.EdgesOnly, "Edges of " + plan.Kind);
        }

        var decodedPattern = DeckPlan.Decode(plans[2].Encode());
        Assert.Equal("my_deck", decodedPattern.Name, "The separator is removed from names");
        Assert.SequenceEqual(pattern.RowTexts(), decodedPattern.Pattern.RowTexts(), "The pattern travels whole");
        Assert.Equal(0.25f, DeckPlan.Decode(plans[3].Encode()).Density, "Density with invariant culture");
        Assert.Equal(DeckPlanKind.Game, DeckPlan.Decode("pattern|x|#.|#").Kind, "A broken plan falls back to the game deck");
        Assert.Equal(DeckPlanKind.Game, DeckPlan.Decode(null).Kind, "No plan is the game deck");
        Assert.True(plans.Skip(1).All(plan => plan.ClearsGameDeck) && !DeckPlan.Game.ClearsGameDeck, "Only the game plan keeps the game deck");
        Assert.True(plans[2].BuildsDeck && plans[3].BuildsDeck && !DeckPlan.Empty.BuildsDeck, "Layouts and generation build a deck");
        yield break;
    }
}
