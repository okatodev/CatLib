using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ShelfLabels;

public sealed class LabelBoard
{
    public const int MaxSpriteIndex = 255;

    private readonly Dictionary<LabelSlot, int> _pictures = new();

    public event Action<IReadOnlyList<LabelSlot>> Changed;

    public int Count => _pictures.Count;

    public IReadOnlyList<KeyValuePair<LabelSlot, int>> Entries => _pictures.OrderBy(pair => pair.Key.LabelId).ThenBy(pair => pair.Key.Slot).ToList();

    public int Get(LabelSlot slot) => _pictures.TryGetValue(slot, out var index) ? index : 0;

    public bool Has(LabelSlot slot) => _pictures.ContainsKey(slot);

    public bool Set(LabelSlot slot, int index) => Apply(new[] { new KeyValuePair<LabelSlot, int>(slot, index) }) > 0;

    public int Apply(IEnumerable<KeyValuePair<LabelSlot, int>> entries)
    {
        var changed = new List<LabelSlot>();
        foreach (var (slot, index) in entries)
        {
            if (!slot.IsValid || index < 0 || index > MaxSpriteIndex)
            {
                continue;
            }

            if (_pictures.TryGetValue(slot, out var current) && current == index)
            {
                continue;
            }

            _pictures[slot] = index;
            changed.Add(slot);
        }

        if (changed.Count > 0)
        {
            Changed?.Invoke(changed);
        }

        return changed.Count;
    }

    public void Clear()
    {
        if (_pictures.Count == 0)
        {
            return;
        }

        var removed = _pictures.Keys.ToList();
        _pictures.Clear();
        Changed?.Invoke(removed);
    }

    public int Step(LabelSlot slot, int step, int spriteCount)
    {
        if (spriteCount <= 0)
        {
            return -1;
        }

        var current = Get(slot);
        var next = ((current + step) % spriteCount + spriteCount) % spriteCount;
        Set(slot, next);
        return next;
    }

    public static string Format(IEnumerable<KeyValuePair<LabelSlot, int>> entries) =>
        string.Join(";", entries.Select(pair => pair.Key.Text + "=" + pair.Value.ToString(CultureInfo.InvariantCulture)));

    public static List<KeyValuePair<LabelSlot, int>> Parse(string text)
    {
        var result = new List<KeyValuePair<LabelSlot, int>>();
        if (string.IsNullOrEmpty(text))
        {
            return result;
        }

        foreach (var part in text.Split(';'))
        {
            var split = part.Split('=');
            if (split.Length == 2
                && LabelSlot.TryParse(split[0], out var slot)
                && int.TryParse(split[1], NumberStyles.None, CultureInfo.InvariantCulture, out var index)
                && index <= MaxSpriteIndex)
            {
                result.Add(new KeyValuePair<LabelSlot, int>(slot, index));
            }
        }

        return result;
    }

    public List<string> Chunks(int maxBytes)
    {
        var chunks = new List<string>();
        var builder = new StringBuilder();
        foreach (var pair in Entries)
        {
            var entry = pair.Key.Text + "=" + pair.Value.ToString(CultureInfo.InvariantCulture);
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
