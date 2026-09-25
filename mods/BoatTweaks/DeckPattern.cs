using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BoatTweaks;

public sealed class DeckPattern
{
    public const string BlockedChars = "#xX";
    public const string FreeChars = ".o_";

    private readonly bool[,] _blocked;

    public DeckPattern(bool[,] blocked)
    {
        _blocked = (bool[,])blocked.Clone();
    }

    public int Rows => _blocked.GetLength(0);

    public int Columns => _blocked.GetLength(1);

    public int BlockedCount => BlockedCells().Count();

    public bool IsBlocked(int row, int column) => _blocked[row, column];

    public IEnumerable<(int Row, int Column)> BlockedCells()
    {
        for (var row = 0; row < Rows; row++)
        {
            for (var column = 0; column < Columns; column++)
            {
                if (_blocked[row, column])
                {
                    yield return (row, column);
                }
            }
        }
    }

    public IReadOnlyList<string> RowTexts()
    {
        var rows = new List<string>();
        for (var row = 0; row < Rows; row++)
        {
            var builder = new StringBuilder(Columns);
            for (var column = 0; column < Columns; column++)
            {
                builder.Append(_blocked[row, column] ? '#' : '.');
            }

            rows.Add(builder.ToString());
        }

        return rows;
    }

    public static DeckPattern FromAvailability(bool[,] available, ISet<(int Row, int Column)> arriving, out int excluded)
    {
        excluded = 0;
        var blocked = new bool[available.GetLength(0), available.GetLength(1)];
        for (var row = 0; row < blocked.GetLength(0); row++)
        {
            for (var column = 0; column < blocked.GetLength(1); column++)
            {
                if (available[row, column])
                {
                    continue;
                }

                if (arriving != null && arriving.Contains((row, column)))
                {
                    excluded++;
                    continue;
                }

                blocked[row, column] = true;
            }
        }

        return new DeckPattern(blocked);
    }

    public static bool IsGridLine(string line) =>
        !string.IsNullOrEmpty(line) && line.All(character => BlockedChars.IndexOf(character) >= 0 || FreeChars.IndexOf(character) >= 0);

    public static bool TryParse(string text, out DeckPattern pattern, out string error)
    {
        pattern = null;
        var rows = (text ?? string.Empty)
            .Split('\n')
            .Select(line => line.Trim())
            .Where(IsGridLine)
            .ToList();

        if (rows.Count == 0)
        {
            error = "no rows made of # and .";
            return false;
        }

        if (rows.Any(row => row.Length != rows[0].Length))
        {
            error = "rows have different lengths";
            return false;
        }

        var blocked = new bool[rows.Count, rows[0].Length];
        for (var row = 0; row < rows.Count; row++)
        {
            for (var column = 0; column < rows[row].Length; column++)
            {
                blocked[row, column] = BlockedChars.IndexOf(rows[row][column]) >= 0;
            }
        }

        pattern = new DeckPattern(blocked);
        error = null;
        return true;
    }

    public override string ToString() => string.Join("/", RowTexts());
}
