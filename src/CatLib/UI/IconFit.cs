using System;

namespace CatLib.UI;

internal readonly struct IconBounds
{
    public IconBounds(int x, int y, int width, int height, float coverage)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
        Coverage = coverage;
    }

    public int X { get; }

    public int Y { get; }

    public int Width { get; }

    public int Height { get; }

    public float Coverage { get; }

    public bool IsEmpty => Width <= 0 || Height <= 0;
}

internal static class IconFit
{
    public const byte AlphaThreshold = 16;
    public const float TargetFill = 0.6f;
    public const float MinScale = 0.55f;

    public static IconBounds Measure(byte[] alpha, int width, int height)
    {
        if (alpha == null || width <= 0 || height <= 0 || alpha.Length < width * height)
        {
            return default;
        }

        int minX = width, minY = height, maxX = -1, maxY = -1, filled = 0;
        for (var y = 0; y < height; y++)
        {
            var row = y * width;
            for (var x = 0; x < width; x++)
            {
                if (alpha[row + x] <= AlphaThreshold)
                {
                    continue;
                }

                filled++;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
        }

        if (maxX < 0)
        {
            return default;
        }

        var boundsWidth = maxX - minX + 1;
        var boundsHeight = maxY - minY + 1;
        return new IconBounds(minX, minY, boundsWidth, boundsHeight, filled / (float)(boundsWidth * boundsHeight));
    }

    public static float Scale(int width, int height, float coverage, float targetFill = TargetFill)
    {
        if (width <= 0 || height <= 0 || coverage <= 0f)
        {
            return 1f;
        }

        var longest = Math.Max(width, height);
        var filled = coverage * width * height / ((float)longest * longest);
        var scale = (float)Math.Sqrt(targetFill / filled);
        return Math.Max(MinScale, Math.Min(1f, scale));
    }
}
