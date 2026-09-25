using System.Collections.Generic;
using System.Linq;
using BoatTweaks;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.BoatTweaks;

public sealed class DeckPiecesTest : TestCase
{
    public override string Suite => "BoatTweaks";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        DeckPattern.TryParse("##.##..#\n##.##..#\n........\n##......\n#.......\n........\n......##\n......##", out var pattern, out _);
        var cells = new HashSet<(int Row, int Column)>(pattern.BlockedCells());
        var pieces = DeckPieces.Split(8, 8, cells);
        context.Note("Pieces: " + string.Join(", ", pieces.Select(piece => $"{piece.Rows}x{piece.Columns}@{piece.Row},{piece.Column}")));

        var covered = pieces.SelectMany(piece => piece.Cells()).ToList();
        Assert.Equal(cells.Count, covered.Count, "Every taken cell is covered exactly once");
        Assert.True(covered.All(cells.Contains), "No piece covers a free cell");
        Assert.Equal(covered.Count, covered.Distinct().Count(), "Pieces never overlap");
        Assert.Equal(3, pieces.Count(piece => piece.Rows == 2 && piece.Columns == 2), "Every 2x2 block becomes one large crate");
        Assert.True(pieces.Any(piece => piece.Rows == 2 && piece.Columns == 1 && piece.Column == 7), "A vertical pair becomes one small crate");
        Assert.True(pieces.Any(piece => piece.Rows == 1 && piece.Columns == 2 && piece.Row == 3), "A horizontal pair becomes one small crate");
        Assert.Equal(1, pieces.Count(piece => piece.CellCount == 1), "Only the lonely cell stays single");
        Assert.Equal(0, DeckPieces.Split(8, 8, new HashSet<(int, int)>()).Count, "An empty deck has no pieces");
        yield break;
    }
}
