using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ShelfLabels;

public sealed class PlacementBoard
{
    public const string SaveKeyPrefix = "place/";

    private readonly Dictionary<int, Placement> _placements = new();

    public event Action<IReadOnlyList<int>> Changed;

    public int Count => _placements.Count;

    public IReadOnlyList<KeyValuePair<int, Placement>> Entries => _placements.OrderBy(pair => pair.Key).ToList();

    public Placement Get(int labelId, Placement fallback) => _placements.TryGetValue(labelId, out var placement) ? placement : fallback;

    public bool Has(int labelId) => _placements.ContainsKey(labelId);

    public bool Set(int labelId, Placement placement) => Apply(new[] { new KeyValuePair<int, Placement>(labelId, placement) }) > 0;

    public int Apply(IEnumerable<KeyValuePair<int, Placement>> entries)
    {
        var changed = new List<int>();
        foreach (var (labelId, placement) in entries)
        {
            if (labelId <= 0 || !IsDefined(placement))
            {
                continue;
            }

            if (_placements.TryGetValue(labelId, out var current) && current == placement)
            {
                continue;
            }

            _placements[labelId] = placement;
            changed.Add(labelId);
        }

        if (changed.Count > 0)
        {
            Changed?.Invoke(changed);
        }

        return changed.Count;
    }

    public void Clear()
    {
        if (_placements.Count == 0)
        {
            return;
        }

        var removed = _placements.Keys.ToList();
        _placements.Clear();
        Changed?.Invoke(removed);
    }

    public static Placement Next(Placement placement) => placement switch
    {
        Placement.Auto => Placement.Right,
        Placement.Right => Placement.Left,
        Placement.Left => Placement.Below,
        Placement.Below => Placement.None,
        _ => Placement.Auto
    };

    public static bool IsDefined(Placement placement) => placement >= Placement.Auto && placement <= Placement.None;

    public static string SaveKey(int labelId) => SaveKeyPrefix + labelId.ToString(CultureInfo.InvariantCulture);

    public static bool TryParseSaveKey(string key, out int labelId)
    {
        labelId = 0;
        return key != null
               && key.StartsWith(SaveKeyPrefix, StringComparison.Ordinal)
               && int.TryParse(key.Substring(SaveKeyPrefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out labelId)
               && labelId > 0;
    }

    public static List<KeyValuePair<int, Placement>> Parse(string text)
    {
        var result = new List<KeyValuePair<int, Placement>>();
        if (string.IsNullOrEmpty(text))
        {
            return result;
        }

        foreach (var part in text.Split(';'))
        {
            var split = part.Split('=');
            if (split.Length == 2
                && int.TryParse(split[0], NumberStyles.None, CultureInfo.InvariantCulture, out var labelId)
                && int.TryParse(split[1], NumberStyles.None, CultureInfo.InvariantCulture, out var code)
                && labelId > 0
                && IsDefined((Placement)code))
            {
                result.Add(new KeyValuePair<int, Placement>(labelId, (Placement)code));
            }
        }

        return result;
    }

    public static string Format(int labelId, Placement placement) =>
        labelId.ToString(CultureInfo.InvariantCulture) + "=" + ((int)placement).ToString(CultureInfo.InvariantCulture);

    public List<string> Chunks(int maxBytes)
    {
        var chunks = new List<string>();
        var builder = new StringBuilder();
        foreach (var (labelId, placement) in Entries)
        {
            var entry = Format(labelId, placement);
            if (builder.Length > 0 && builder.Length + 1 + entry.Length > maxBytes)
            {
                chunks.Add(builder.ToString());
                builder.Clear();
            }

            if (builder.Length > 0)
            {
                builder.Append(';');
            }

            builder.Append(entry);
        }

        if (builder.Length > 0)
        {
            chunks.Add(builder.ToString());
        }

        return chunks;
    }
}
