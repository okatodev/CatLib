using System;
using System.Collections.Generic;
using System.Linq;

namespace BoatTweaks;

public static class LayoutPlanner
{
    public static LayoutPlan Choose(LayoutMode mode, int poolSize, IReadOnlyList<int> allowedVariants, int fixedVariant, int lastVariantIndex, bool avoidRepeats, Random random)
    {
        if (poolSize <= 0)
        {
            return LayoutPlan.KeepGame;
        }

        switch (mode)
        {
            case LayoutMode.Empty:
                return LayoutPlan.Empty;
            case LayoutMode.FixedVariant:
                return LayoutPlan.Variant(Math.Min(Math.Max(fixedVariant, 1), poolSize) - 1);
            case LayoutMode.GameVariants:
                var candidates = Candidates(poolSize, allowedVariants);
                if (avoidRepeats && candidates.Count > 1)
                {
                    candidates.Remove(lastVariantIndex);
                }

                return LayoutPlan.Variant(candidates[random.Next(candidates.Count)]);
            default:
                return LayoutPlan.KeepGame;
        }
    }

    public static List<int> Candidates(int poolSize, IReadOnlyList<int> allowedVariants)
    {
        var inRange = allowedVariants.Where(variant => variant >= 1 && variant <= poolSize).Select(variant => variant - 1).ToList();
        return inRange.Count > 0 ? inRange : Enumerable.Range(0, poolSize).ToList();
    }
}
