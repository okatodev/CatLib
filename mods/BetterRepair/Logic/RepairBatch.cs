using System;

namespace BetterRepair.Logic;

public static class RepairBatch
{
    public const int MinimumAtOnce = 2;
    public const int MaximumAtOnce = 8;
    public const int DefaultAtOnce = 2;

    public static int Clamp(int atOnce) => Math.Max(MinimumAtOnce, Math.Min(MaximumAtOnce, atOnce));

    public static bool TakesOver(bool enabled, int atOnce, int parcels, int damaged) =>
        enabled && parcels >= 2 && parcels <= Clamp(atOnce) && damaged >= 1;

    public static int Size(bool enabled, int atOnce, int parcels, int damaged, int sheets)
    {
        if (!TakesOver(enabled, atOnce, parcels, damaged))
        {
            return 0;
        }

        return Math.Max(0, Math.Min(damaged, Math.Min(Clamp(atOnce), sheets)));
    }
}
