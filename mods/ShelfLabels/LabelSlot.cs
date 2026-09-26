using System;
using System.Globalization;

namespace ShelfLabels;

public readonly record struct LabelSlot(int LabelId, int Slot)
{
    public const int MaxSlots = 3;
    public const string SaveKeyPrefix = "label/";

    public bool IsValid => LabelId > 0 && Slot >= 1 && Slot <= MaxSlots;

    public string SaveKey => SaveKeyPrefix + Text;

    public string Text => LabelId.ToString(CultureInfo.InvariantCulture) + "/" + Slot.ToString(CultureInfo.InvariantCulture);

    public static bool TryParse(string text, out LabelSlot slot)
    {
        slot = default;
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        var parts = text.Split('/');
        if (parts.Length != 2
            || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var labelId)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var number))
        {
            return false;
        }

        slot = new LabelSlot(labelId, number);
        return slot.IsValid;
    }

    public static bool TryParseSaveKey(string key, out LabelSlot slot)
    {
        slot = default;
        return key != null && key.StartsWith(SaveKeyPrefix, StringComparison.Ordinal) && TryParse(key.Substring(SaveKeyPrefix.Length), out slot);
    }

    public override string ToString() => Text;
}
