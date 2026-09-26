using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ShelfLabels;

public sealed class StandBoard
{
    public const string SaveKeyPrefix = "stand/";

    private readonly Dictionary<int, bool> _hidden = new();

    public event Action<IReadOnlyList<int>> Changed;

    public int Count => _hidden.Count;

    public IReadOnlyList<KeyValuePair<int, bool>> Entries => _hidden.OrderBy(pair => pair.Key).ToList();

    public bool IsHidden(int labelId, bool fallback) => _hidden.TryGetValue(labelId, out var hidden) ? hidden : fallback;

    public bool Set(int labelId, bool hidden) => Apply(new[] { new KeyValuePair<int, bool>(labelId, hidden) }) > 0;

    public int Apply(IEnumerable<KeyValuePair<int, bool>> entries)
    {
        var changed = new List<int>();
        foreach (var (labelId, hidden) in entries)
        {
            if (labelId <= 0 || (_hidden.TryGetValue(labelId, out var current) && current == hidden))
            {
                continue;
            }

            _hidden[labelId] = hidden;
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
        if (_hidden.Count == 0)
        {
            return;
        }

        var removed = _hidden.Keys.ToList();
        _hidden.Clear();
        Changed?.Invoke(removed);
    }

    public static string SaveKey(int labelId) => SaveKeyPrefix + labelId.ToString(CultureInfo.InvariantCulture);

    public static bool TryParseSaveKey(string key, out int labelId)
    {
        labelId = 0;
        return key != null
               && key.StartsWith(SaveKeyPrefix, StringComparison.Ordinal)
               && int.TryParse(key.Substring(SaveKeyPrefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out labelId)
               && labelId > 0;
    }

    public static string Format(int labelId, bool hidden) => labelId.ToString(CultureInfo.InvariantCulture) + "=" + (hidden ? "1" : "0");

    public static List<KeyValuePair<int, bool>> Parse(string text)
    {
        var result = new List<KeyValuePair<int, bool>>();
        if (string.IsNullOrEmpty(text))
        {
            return result;
        }

        foreach (var part in text.Split(';'))
        {
            var split = part.Split('=');
            if (split.Length == 2
                && int.TryParse(split[0], NumberStyles.None, CultureInfo.InvariantCulture, out var labelId)
                && labelId > 0
                && (split[1] == "0" || split[1] == "1"))
            {
                result.Add(new KeyValuePair<int, bool>(labelId, split[1] == "1"));
            }
        }

        return result;
    }

    public List<string> Chunks(int maxBytes)
    {
        var chunks = new List<string>();
        var builder = new StringBuilder();
        foreach (var (labelId, hidden) in Entries)
        {
            var entry = Format(labelId, hidden);
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
