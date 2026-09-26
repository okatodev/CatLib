using System;

namespace ShelfLabels;

public static class SlotLayout
{
    public static (float Right, float Up) Offset(int slot, Placement placement, float width, float height, float spacing)
    {
        if (slot < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(slot));
        }

        return placement switch
        {
            Placement.Below => (0f, -slot * (height + spacing)),
            Placement.Left => (-slot * (width + spacing), 0f),
            _ => (slot * (width + spacing), 0f)
        };
    }

    public static Placement NameSide(string holderName) =>
        holderName != null && holderName.IndexOf("_Left", StringComparison.OrdinalIgnoreCase) >= 0 ? Placement.Left : Placement.Right;

    public static Placement ChooseSide(Placement preferred, int blockedPreferred, int blockedOther)
    {
        var other = preferred == Placement.Left ? Placement.Right : Placement.Left;
        return blockedOther < blockedPreferred ? other : preferred;
    }
}
