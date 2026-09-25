using System;
using System.Collections.Generic;
using System.Linq;

namespace BoatTweaks;

public static class PatternGenerator
{
    public const float MinimumDensity = 0.05f;
    public const float MaximumDensity = 0.6f;
    public const int EdgeWidth = 2;

    private static readonly (int Rows, int Columns)[] Shapes = { (2, 2), (2, 1), (1, 2), (1, 1) };

    public static DeckPattern Generate(int rows, int columns, float density, bool edgesOnly, ISet<(int Row, int Column)> reserved, int seed)
    {
        var random = new Random(seed);
        var blocked = new bool[rows, columns];
        var target = (int)Math.Round(Math.Min(Math.Max(density, MinimumDensity), MaximumDensity) * rows * columns);
        var count = 0;

        bool Allowed(int row, int column) =>
            row >= 0 && column >= 0 && row < rows && column < columns
            && !blocked[row, column]
            && (reserved == null || !reserved.Contains((row, column)))
            && (!edgesOnly || IsEdge(row, column, rows, columns));

        var starts = Enumerable.Range(0, rows * columns).Select(index => (Row: index / columns, Column: index % columns)).OrderBy(_ => random.Next()).ToList();
        foreach (var (row, column) in starts)
        {
            if (count >= target)
            {
                break;
            }

            foreach (var shape in Shapes.OrderBy(_ => random.Next()))
            {
                var cells = Enumerable.Range(0, shape.Rows).SelectMany(dr => Enumerable.Range(0, shape.Columns).Select(dc => (Row: row + dr, Column: column + dc))).ToList();
                if (count + cells.Count > target + 1 || !cells.All(cell => Allowed(cell.Row, cell.Column)))
                {
                    continue;
                }

                foreach (var cell in cells)
                {
                    blocked[cell.Row, cell.Column] = true;
                }

                count += cells.Count;
                break;
            }
        }

        return new DeckPattern(blocked);
    }

    public static bool IsEdge(int row, int column, int rows, int columns) =>
        row < EdgeWidth || column < EdgeWidth || row >= rows - EdgeWidth || column >= columns - EdgeWidth;
}
