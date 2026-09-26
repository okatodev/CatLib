using System.Collections.Generic;
using System.Linq;
using CatLib.Tests.Framework;
using ShelfLabels;

namespace CatLib.Tests.Suites.ShelfLabels;

public sealed class PlacementBoardTest : TestCase
{
    public override string Suite => "ShelfLabels";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var order = new List<Placement>();
        var current = Placement.Auto;
        for (var index = 0; index < 5; index++)
        {
            current = PlacementBoard.Next(current);
            order.Add(current);
        }

        Assert.SequenceEqual(new[] { Placement.Right, Placement.Left, Placement.Below, Placement.None, Placement.Auto }, order, "The hotkey goes around all placements");

        var board = new PlacementBoard();
        var changed = new List<IReadOnlyList<int>>();
        board.Changed += changed.Add;
        Assert.Equal(Placement.Below, board.Get(390, Placement.Below), "A label without its own placement uses the default");
        Assert.True(board.Set(390, Placement.Left), "Set");
        Assert.False(board.Set(390, Placement.Left), "The same placement is not a change");
        Assert.False(board.Set(0, Placement.Left), "Invalid ids are refused");
        Assert.False(board.Set(5, (Placement)9), "Unknown placements are refused");
        board.Set(12, Placement.None);
        Assert.Equal(2, changed.Count, "Changes are reported");

        var text = string.Join(";", board.Entries.Select(pair => PlacementBoard.Format(pair.Key, pair.Value)));
        Assert.Equal("12=4;390=2", text, "Format");
        Assert.Equal(2, PlacementBoard.Parse(text + ";7=9;x=1;8").Count, "Malformed entries are skipped");
        Assert.Equal("place/390", PlacementBoard.SaveKey(390), "Save key");
        Assert.True(PlacementBoard.TryParseSaveKey("place/390", out var id) && id == 390, "Save key round trip");
        Assert.False(PlacementBoard.TryParseSaveKey("label/390/1", out _), "Picture keys are not placements");
        Assert.Equal(1, board.Chunks(6).Count(chunk => chunk == "12=4"), "Chunks split by size");
        yield break;
    }
}
