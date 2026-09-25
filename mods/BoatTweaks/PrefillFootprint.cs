using System.Collections.Generic;
using System.Text.Json;

namespace BoatTweaks;

public static class PrefillFootprint
{
    public static HashSet<(int Row, int Column)> Parse(string json)
    {
        var cells = new HashSet<(int Row, int Column)>();
        if (string.IsNullOrWhiteSpace(json))
        {
            return cells;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("storedEntities", out var entities) || entities.ValueKind != JsonValueKind.Array)
            {
                return cells;
            }

            foreach (var entity in entities.EnumerateArray())
            {
                if (!entity.TryGetProperty("footprintCells", out var footprint) || footprint.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var cell in footprint.EnumerateArray())
                {
                    if (cell.TryGetProperty("x", out var x) && cell.TryGetProperty("y", out var y))
                    {
                        cells.Add((x.GetInt32(), y.GetInt32()));
                    }
                }
            }
        }
        catch (JsonException)
        {
            cells.Clear();
        }

        return cells;
    }
}
