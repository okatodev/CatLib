using System;
using CatLib.Assets;
using UnityEngine;

namespace CatLib.Game;

internal static class CurseIcon
{
    public const string SwirlTexture = "t_VFX_Cursed";
    public const int Size = 128;
    public const int SwirlSize = 78;
    public const float OuterRadius = 62f;
    public const float RimWidth = 5f;
    public const int GlowRadius = 5;
    public const double RetrySeconds = 10;

    public static readonly Shade Rim = new(244, 238, 228, 255);
    public static readonly Shade Disk = new(38, 18, 62, 245);
    public static readonly Shade Glow = new(129, 20, 233, 255);
    public static readonly Shade Line = new(123, 51, 237, 255);
    public static readonly Shade Core = new(236, 222, 255, 255);

    private static Sprite _sprite;
    private static double _lastTry = double.NegativeInfinity;

    public static Sprite Sprite(double now)
    {
        if (_sprite != null && !_sprite.WasCollected)
        {
            return _sprite;
        }

        if (now - _lastTry < RetrySeconds)
        {
            return null;
        }

        _lastTry = now;
        var texture = GameImages.FindTexture(SwirlTexture);
        if (texture == null)
        {
            return null;
        }

        var icon = Draw(GameImages.Read(texture));
        _sprite = ImageData.ToSprite(icon.ToTexture("CatLib curse icon"), "CatLib curse icon");
        return _sprite;
    }

    public static ImageData Draw(ImageData swirl)
    {
        if (swirl == null)
        {
            throw new ArgumentNullException(nameof(swirl));
        }

        var mask = Mask(swirl).Trimmed().Fitted(SwirlSize, SwirlSize);
        var offset = (Size - SwirlSize) / 2;
        var coverage = new float[Size * Size];
        for (var y = 0; y < SwirlSize; y++)
        {
            for (var x = 0; x < SwirlSize; x++)
            {
                coverage[(y + offset) * Size + x + offset] = mask.Rgba[(y * SwirlSize + x) * 4 + 3] / 255f;
            }
        }

        var glow = Blur(Blur(Blur(coverage, GlowRadius), GlowRadius), GlowRadius);
        var icon = ImageData.Blank(Size, Size);
        var center = (Size - 1) / 2f;
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                var distance = (float)Math.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
                var outside = Math.Clamp(OuterRadius - distance + 0.5f, 0f, 1f);
                if (outside <= 0f)
                {
                    continue;
                }

                var inside = Math.Clamp(OuterRadius - RimWidth - distance + 0.5f, 0f, 1f);
                var color = Mix(Rim, Disk, inside);
                var index = y * Size + x;
                var halo = Math.Clamp(glow[index] * 2.2f, 0f, 1f) * inside;
                color = Over(color, Glow, halo * 0.85f);
                var line = coverage[index] * inside;
                var lineColor = Mix(Line, Core, Math.Clamp((coverage[index] - 0.55f) / 0.45f, 0f, 1f));
                color = Over(color, lineColor, line);
                var target = index * 4;
                icon.Rgba[target] = color.R;
                icon.Rgba[target + 1] = color.G;
                icon.Rgba[target + 2] = color.B;
                icon.Rgba[target + 3] = (byte)Math.Round(color.A * outside);
            }
        }

        return icon;
    }

    private static ImageData Mask(ImageData swirl)
    {
        var mask = ImageData.Blank(swirl.Width, swirl.Height);
        for (var index = 0; index < swirl.Width * swirl.Height; index++)
        {
            var source = index * 4;
            var bright = Math.Max(swirl.Rgba[source], Math.Max(swirl.Rgba[source + 1], swirl.Rgba[source + 2]));
            mask.Rgba[source] = 255;
            mask.Rgba[source + 1] = 255;
            mask.Rgba[source + 2] = 255;
            mask.Rgba[source + 3] = (byte)(bright * swirl.Rgba[source + 3] / 255);
        }

        return mask;
    }

    private static float[] Blur(float[] values, int radius)
    {
        var across = new float[values.Length];
        var result = new float[values.Length];
        var span = radius * 2 + 1;
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                var sum = 0f;
                for (var step = -radius; step <= radius; step++)
                {
                    var column = x + step;
                    if (column >= 0 && column < Size)
                    {
                        sum += values[y * Size + column];
                    }
                }

                across[y * Size + x] = sum / span;
            }
        }

        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                var sum = 0f;
                for (var step = -radius; step <= radius; step++)
                {
                    var row = y + step;
                    if (row >= 0 && row < Size)
                    {
                        sum += across[row * Size + x];
                    }
                }

                result[y * Size + x] = sum / span;
            }
        }

        return result;
    }

    internal readonly struct Shade
    {
        public Shade(byte r, byte g, byte b, byte a)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        public byte R { get; }

        public byte G { get; }

        public byte B { get; }

        public byte A { get; }
    }

    private static Shade Mix(Shade from, Shade to, float share) => new(
        (byte)Math.Round(from.R + (to.R - from.R) * share),
        (byte)Math.Round(from.G + (to.G - from.G) * share),
        (byte)Math.Round(from.B + (to.B - from.B) * share),
        (byte)Math.Round(from.A + (to.A - from.A) * share));

    private static Shade Over(Shade under, Shade over, float share)
    {
        var mixed = Mix(under, over, Math.Clamp(share, 0f, 1f));
        return new Shade(mixed.R, mixed.G, mixed.B, under.A);
    }
}
