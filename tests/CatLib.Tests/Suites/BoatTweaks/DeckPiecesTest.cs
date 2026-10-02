using System.Collections.Generic;
using System.Linq;
using BoatTweaks.Logic;
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

        var parcel = new HashSet<(int Row, int Column)> { (1, 4) };
        var kept = DeckPieces.Avoiding(pieces, parcel);
        Assert.Equal(pieces.Count - 1, kept.Count, "A parcel on a piece removes that one piece");
        Assert.True(kept.All(piece => !(piece.Rows == 2 && piece.Columns == 2 && piece.Row == 0 && piece.Column == 3)), "The touched square is removed whole, not cut into smaller pieces");
        Assert.Equal(4, DeckPieces.FreedBy(pieces, parcel).Count, "All four cells of the square become free");
        Assert.Equal(pieces.Count, DeckPieces.Avoiding(pieces, null).Count, "Without arriving parcels every piece stays");

        DeckPattern.TryParse("###.....\n###.....\n###.....\n........\n#####...\n#####...\n........\n.......#", out var large, out _);
        var largePieces = large.Pieces();
        context.Note("Large pieces: " + string.Join(", ", largePieces.Select(piece => $"{piece.Rows}x{piece.Columns}@{piece.Row},{piece.Column}")));
        Assert.True(largePieces.Any(piece => piece.Rows == 3 && piece.Columns == 3 && piece.Row == 0 && piece.Column == 0), "A 3x3 block becomes one big crate");
        Assert.True(largePieces.Any(piece => piece.Rows == 2 && piece.Columns == 5 && piece.Row == 4), "A 2x5 block becomes one long crate");
        Assert.Equal(3, largePieces.Count, "Big blocks are not cut into smaller crates");

        var parts = DeckPieces.SplitPiece(new DeckPiece(1, 1, 3, 3));
        Assert.Equal(9, parts.Sum(piece => piece.CellCount), "A big piece without its prop is decorated by smaller ones that cover it");
        Assert.True(parts.All(piece => piece.CellCount < 9) && parts.Any(piece => piece.CellCount == 4), "The smaller pieces start with a square");
        yield break;
    }
}
