using System;
using System.Collections.Generic;

namespace StackIt.Logic;

public static class BridgeRules
{
    public const float LevelTolerance = 0.02f;
    public const float AlignTolerance = 0.07f;
    public const float MoveTolerance = 0.02f;
    public const float GraceSeconds = 5f;

    public static BridgeVerdict Judge(IReadOnlyList<CellSupport> cells)
    {
        if (cells == null || cells.Count == 0)
        {
            return BridgeVerdict.Blocked;
        }

        var other = false;
        foreach (var cell in cells)
        {
            switch (cell)
            {
                case CellSupport.Taken:
                case CellSupport.Missing:
                    return BridgeVerdict.Blocked;
                case CellSupport.Other:
                    other = true;
                    break;
            }
        }

        return other ? BridgeVerdict.Bridge : BridgeVerdict.Plain;
    }

    public static bool IsLevel(float top, float other, float tolerance = LevelTolerance) => Math.Abs(top - other) <= tolerance;

    public static bool IsAligned(float dx, float dz, float tolerance = AlignTolerance) => dx * dx + dz * dz <= tolerance * tolerance;

    public static bool HasMoved(float dx, float dy, float dz, float tolerance = MoveTolerance) => dx * dx + dy * dy + dz * dz > tolerance * tolerance;
}
