using System;
using System.Collections.Generic;
using System.Numerics;

namespace StackIt.Logic;

public static class SlidePlanner
{
    public const float Margin = 0.08f;
    public const float MinimumDistance = 0.05f;
    public const float MaximumDistance = 1f;

    public static SlidePlan Plan(Vector2 center, IReadOnlyList<Vector2> remaining, IReadOnlyList<Vector2> lost, float cell = 0.25f)
    {
        if (lost == null || lost.Count == 0)
        {
            return default;
        }

        var sum = Vector2.Zero;
        foreach (var point in lost)
        {
            sum += point;
        }

        var direction = sum / lost.Count - center;
        if (direction.LengthSquared() < 1e-6f)
        {
            return default;
        }

        direction = Vector2.Normalize(direction);
        if (remaining == null || remaining.Count == 0)
        {
            return new SlidePlan(direction, 0f);
        }

        var extent = cell * 0.5f * (Math.Abs(direction.X) + Math.Abs(direction.Y));
        var edge = float.MinValue;
        foreach (var point in remaining)
        {
            edge = Math.Max(edge, Vector2.Dot(point - center, direction) + extent);
        }

        return new SlidePlan(direction, Math.Clamp(edge + Margin, MinimumDistance, MaximumDistance));
    }
}
