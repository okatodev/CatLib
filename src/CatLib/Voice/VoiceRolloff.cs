using System;
using System.Collections.Generic;

namespace CatLib.Voice;

public static class VoiceRolloff
{
    public const int Steps = 6;
    public const float MinHearDistance = 2f;
    public const float MaxHearDistance = 200f;

    public static IReadOnlyList<(float Distance, float Volume)> Points(float fullVolumeDistance, float hearDistance)
    {
        hearDistance = Clamp(hearDistance, MinHearDistance, MaxHearDistance);
        var full = Clamp(fullVolumeDistance, 0f, hearDistance * 0.9f) / hearDistance;
        var points = new List<(float, float)> { (0f, 1f) };
        if (full > 0f)
        {
            points.Add((full, 1f));
        }

        for (var step = 1; step <= Steps; step++)
        {
            var part = step / (float)Steps;
            var left = 1f - part;
            points.Add((full + (1f - full) * part, left * left));
        }

        return points;
    }

    public static float VolumeAt(float distance, float fullVolumeDistance, float hearDistance)
    {
        var points = Points(fullVolumeDistance, hearDistance);
        var x = Math.Max(0f, distance) / Clamp(hearDistance, MinHearDistance, MaxHearDistance);
        if (x >= 1f)
        {
            return 0f;
        }

        for (var index = 1; index < points.Count; index++)
        {
            var (x1, y1) = points[index];
            var (x0, y0) = points[index - 1];
            if (x <= x1)
            {
                return x1 <= x0 ? y1 : y0 + (y1 - y0) * (x - x0) / (x1 - x0);
            }
        }

        return 0f;
    }

    private static float Clamp(float value, float min, float max) => float.IsNaN(value) ? min : Math.Max(min, Math.Min(max, value));
}
