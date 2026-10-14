using System.Collections.Generic;

namespace TooLate.Logic;

public readonly struct SpawnSpot
{
    public SpawnSpot(float x, float y, float z, int nearest)
    {
        X = x;
        Y = y;
        Z = z;
        Nearest = nearest;
    }

    public float X { get; }

    public float Y { get; }

    public float Z { get; }

    public int Nearest { get; }
}

public static class SpawnSpots
{
    public static IReadOnlyList<SpawnSpot> Extra(IReadOnlyList<(float X, float Y, float Z)> game, int wanted)
    {
        var extra = new List<SpawnSpot>();
        var count = game?.Count ?? 0;
        if (count < 2 || wanted <= count)
        {
            return extra;
        }

        for (var index = 0; index + 1 < count && count + extra.Count < wanted; index++)
        {
            var from = game[index];
            var to = game[index + 1];
            extra.Add(new SpawnSpot((from.X + to.X) / 2f, (from.Y + to.Y) / 2f, (from.Z + to.Z) / 2f, index));
        }

        var first = game[0];
        var second = game[1];
        var last = game[count - 1];
        var beforeLast = game[count - 2];
        for (var step = 1; count + extra.Count < wanted; step++)
        {
            extra.Add(new SpawnSpot(last.X + (last.X - beforeLast.X) * step / 2f, last.Y, last.Z + (last.Z - beforeLast.Z) * step / 2f, count - 1));
            if (count + extra.Count < wanted)
            {
                extra.Add(new SpawnSpot(first.X - (second.X - first.X) * step / 2f, first.Y, first.Z - (second.Z - first.Z) * step / 2f, 0));
            }
        }

        return extra;
    }
}
