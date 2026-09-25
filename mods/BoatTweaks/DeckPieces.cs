using System.Collections.Generic;

namespace BoatTweaks;

public readonly record struct DeckPiece(int Row, int Column, int Rows, int Columns)
{
    public int CellCount => Rows * Columns;

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
    private static readonly (int Rows, int Columns)[] Shapes = { (2, 2), (1, 2), (2, 1), (1, 1) };

    public static List<DeckPiece> Split(int rows, int columns, ISet<(int Row, int Column)> cells)
    {
        var left = new HashSet<(int Row, int Column)>(cells);
        var pieces = new List<DeckPiece>();
        foreach (var (shapeRows, shapeColumns) in Shapes)
        {
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
