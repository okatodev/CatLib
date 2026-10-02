using System.Collections.Generic;
using System.Linq;

namespace BoatTweaks.Logic;

public readonly record struct DeckPiece(int Row, int Column, int Rows, int Columns)
{
    public int CellCount => Rows * Columns;

    public (int Short, int Long) Footprint => (System.Math.Min(Rows, Columns), System.Math.Max(Rows, Columns));

    public IEnumerable<(int Row, int Column)> Cells()
    {
        for (var row = Row; row < Row + Rows; row++)
        {
            for (var column = Column; column < Column + Columns; column++)
            {
                yield return (row, column);
            }
        }
    }
}

public static class DeckPieces
{
    public static readonly (int Short, int Long)[] Footprints = { (4, 5), (4, 4), (2, 5), (3, 3), (2, 2), (1, 2), (1, 1) };

    private static readonly (int Rows, int Columns)[] Shapes = Footprints
        .SelectMany(footprint => footprint.Short == footprint.Long
            ? new[] { (footprint.Short, footprint.Long) }
            : new[] { (footprint.Short, footprint.Long), (footprint.Long, footprint.Short) })
        .ToArray();

    public static bool IsKnown((int Short, int Long) footprint) => System.Array.IndexOf(Footprints, footprint) >= 0;

    public static List<DeckPiece> Avoiding(IEnumerable<DeckPiece> pieces, ISet<(int Row, int Column)> cells) =>
        pieces.Where(piece => cells == null || !piece.Cells().Any(cells.Contains)).ToList();

    public static HashSet<(int Row, int Column)> FreedBy(IEnumerable<DeckPiece> pieces, ISet<(int Row, int Column)> cells) =>
        new(pieces.Where(piece => cells != null && piece.Cells().Any(cells.Contains)).SelectMany(piece => piece.Cells()));

    public static List<DeckPiece> Split(int rows, int columns, ISet<(int Row, int Column)> cells) => Split(rows, columns, cells, int.MaxValue);

    public static List<DeckPiece> SplitPiece(DeckPiece piece)
    {
        var cells = new HashSet<(int Row, int Column)>(piece.Cells());
        return Split(piece.Row + piece.Rows, piece.Column + piece.Columns, cells, piece.CellCount - 1);
    }

    public static List<DeckPiece> Split(int rows, int columns, ISet<(int Row, int Column)> cells, int maxCells)
    {
        var left = new HashSet<(int Row, int Column)>(cells);
        var pieces = new List<DeckPiece>();
        foreach (var (shapeRows, shapeColumns) in Shapes)
        {
            if (shapeRows * shapeColumns > maxCells)
            {
                continue;
            }

            for (var row = 0; row < rows; row++)
            {
                for (var column = 0; column < columns; column++)
                {
                    var piece = new DeckPiece(row, column, shapeRows, shapeColumns);
                    var fits = true;
                    foreach (var cell in piece.Cells())
                    {
                        if (!left.Contains(cell))
                        {
                            fits = false;
                            break;
                        }
                    }

                    if (!fits)
                    {
                        continue;
                    }

                    foreach (var cell in piece.Cells())
                    {
                        left.Remove(cell);
                    }

                    pieces.Add(piece);
                }
            }
        }

        return pieces;
    }
}
