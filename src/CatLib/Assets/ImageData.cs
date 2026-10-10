using System;
using System.Collections.Generic;
using System.IO;
using CatLib.UI;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace CatLib.Assets;

public sealed class ImageData
{
    public const long MaxFileBytes = 16L * 1024 * 1024;
    public const byte TransparentBelow = 8;

    public ImageData(int width, int height, byte[] rgba)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "An image needs at least one pixel");
        }

        if (rgba == null || rgba.Length != width * height * 4)
        {
            throw new ArgumentException($"An image of {width}x{height} needs {width * height * 4} bytes of RGBA", nameof(rgba));
        }

        Width = width;
        Height = height;
        Rgba = rgba;
    }

    public int Width { get; }

    public int Height { get; }

    public byte[] Rgba { get; }

    public float Aspect => (float)Width / Height;

    public static ImageData Blank(int width, int height) => new(width, height, new byte[width * height * 4]);

    public static bool TryDecode(byte[] bytes, out ImageData image) => TryDecode(bytes, out image, out _);

    public static bool TryDecode(byte[] bytes, out ImageData image, out string problem)
    {
        image = null;
        problem = null;
        if (JpegDecoder.IsJpeg(bytes))
        {
            if (!JpegDecoder.TryDecode(bytes, out var jpegWidth, out var jpegHeight, out var jpegRgba, out problem))
            {
                problem = "the JPG image could not be read: " + problem;
                return false;
            }

            image = new ImageData(jpegWidth, jpegHeight, jpegRgba);
            return true;
        }

        if (!PngDecoder.TryDecode(bytes, out var width, out var height, out var rgba))
        {
            problem = IsPng(bytes)
                ? $"the PNG image could not be read, it may be damaged or larger than {PngDecoder.MaxSide}x{PngDecoder.MaxSide}"
                : "it is neither a PNG nor a JPG image";
            return false;
        }

        image = new ImageData(width, height, rgba);
        return true;
    }

    public static bool IsPng(byte[] bytes) => bytes != null && bytes.Length > 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47;

    public static bool IsJpeg(byte[] bytes) => JpegDecoder.IsJpeg(bytes);

    public static bool TryRead(string path, out ImageData image, out string problem)
    {
        image = null;
        problem = null;
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists)
            {
                problem = "the file does not exist";
                return false;
            }

            if (info.Length > MaxFileBytes)
            {
                problem = $"the file is larger than {MaxFileBytes / (1024 * 1024)} MiB";
                return false;
            }

            if (!TryDecode(File.ReadAllBytes(path), out image, out problem))
            {
                return false;
            }

            return true;
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
        {
            problem = exception.Message;
            return false;
        }
    }

    public ImageArea VisibleBounds(byte threshold = TransparentBelow)
    {
        int left = Width, right = -1, top = Height, bottom = -1;
        for (var y = 0; y < Height; y++)
        {
            var row = y * Width * 4;
            for (var x = 0; x < Width; x++)
            {
                if (Rgba[row + x * 4 + 3] < threshold)
                {
                    continue;
                }

                left = Math.Min(left, x);
                right = Math.Max(right, x);
                top = Math.Min(top, y);
                bottom = Math.Max(bottom, y);
            }
        }

        return right < 0 ? new ImageArea(0, 0, 0, 0) : new ImageArea(left, top, right - left + 1, bottom - top + 1);
    }

    public ImageData Cropped(ImageArea area)
    {
        var x0 = Math.Clamp(area.X, 0, Width - 1);
        var y0 = Math.Clamp(area.Y, 0, Height - 1);
        var width = Math.Clamp(area.Width, 1, Width - x0);
        var height = Math.Clamp(area.Height, 1, Height - y0);
        if (x0 == 0 && y0 == 0 && width == Width && height == Height)
        {
            return this;
        }

        var result = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            Buffer.BlockCopy(Rgba, ((y0 + y) * Width + x0) * 4, result, y * width * 4, width * 4);
        }

        return new ImageData(width, height, result);
    }

    public ImageData Trimmed(int margin = 0, byte threshold = TransparentBelow)
    {
        var bounds = VisibleBounds(threshold);
        if (bounds.IsEmpty)
        {
            return this;
        }

        var cropped = Cropped(bounds);
        return margin <= 0 ? cropped : cropped.Padded(cropped.Width + margin * 2, cropped.Height + margin * 2);
    }

    public ImageData Padded(int width, int height)
    {
        width = Math.Max(width, Width);
        height = Math.Max(height, Height);
        if (width == Width && height == Height)
        {
            return this;
        }

        var result = new byte[width * height * 4];
        var left = (width - Width) / 2;
        var top = (height - Height) / 2;
        for (var y = 0; y < Height; y++)
        {
            Buffer.BlockCopy(Rgba, y * Width * 4, result, ((top + y) * width + left) * 4, Width * 4);
        }

        return new ImageData(width, height, result);
    }

    public ImageData PaddedToAspect(float aspect)
    {
        if (aspect <= 0f || Math.Abs(Aspect - aspect) < 0.01f)
        {
            return this;
        }

        return Aspect > aspect
            ? Padded(Width, (int)Math.Round(Width / aspect))
            : Padded((int)Math.Round(Height * aspect), Height);
    }

    public ImageData Scaled(int maxSide)
    {
        if (maxSide <= 0 || (Width <= maxSide && Height <= maxSide))
        {
            return this;
        }

        var factor = (float)maxSide / Math.Max(Width, Height);
        return Resized(Math.Max(1, (int)Math.Round(Width * factor)), Math.Max(1, (int)Math.Round(Height * factor)));
    }

    public ImageData Fitted(int width, int height)
    {
        var factor = Math.Min((float)width / Width, (float)height / Height);
        var resized = Resized(Math.Max(1, Math.Min(width, (int)Math.Round(Width * factor))), Math.Max(1, Math.Min(height, (int)Math.Round(Height * factor))));
        return resized.Padded(width, height);
    }

    public ImageData Resized(int width, int height)
    {
        if (width == Width && height == Height)
        {
            return this;
        }

        return width <= Width && height <= Height ? AreaAverage(width, height) : Bilinear(width, height);
    }

    public ImageData Outlined(int thickness, byte red = 255, byte green = 255, byte blue = 255, byte threshold = 128)
    {
        if (thickness <= 0)
        {
            return this;
        }

        var padded = Padded(Width + thickness * 2, Height + thickness * 2);
        var width = padded.Width;
        var height = padded.Height;
        var distance = new double[width * height];
        const double far = 1e20;
        for (var index = 0; index < distance.Length; index++)
        {
            distance[index] = padded.Rgba[index * 4 + 3] >= threshold ? 0.0 : far;
        }

        var line = new double[Math.Max(width, height)];
        var result = new double[Math.Max(width, height)];
        for (var x = 0; x < width; x++)
        {
            for (var y = 0; y < height; y++)
            {
                line[y] = distance[y * width + x];
            }

            Transform(line, height, result);
            for (var y = 0; y < height; y++)
            {
                distance[y * width + x] = result[y];
            }
        }

        for (var y = 0; y < height; y++)
        {
            Array.Copy(distance, y * width, line, 0, width);
            Transform(line, width, result);
            Array.Copy(result, 0, distance, y * width, width);
        }

        var outlined = new ImageData(width, height, new byte[width * height * 4]);
        for (var index = 0; index < distance.Length; index++)
        {
            var coverage = (float)Math.Clamp(thickness + 0.5 - Math.Sqrt(distance[index]), 0.0, 1.0);
            var target = index * 4;
            outlined.Rgba[target] = red;
            outlined.Rgba[target + 1] = green;
            outlined.Rgba[target + 2] = blue;
            outlined.Rgba[target + 3] = (byte)Math.Round(coverage * 255f);
        }

        outlined.Draw(padded, 0, 0);
        return outlined;
    }

    private static void Transform(double[] values, int length, double[] output)
    {
        var hull = new int[length];
        var bounds = new double[length + 1];
        var k = 0;
        hull[0] = 0;
        bounds[0] = double.NegativeInfinity;
        bounds[1] = double.PositiveInfinity;
        for (var q = 1; q < length; q++)
        {
            var crossing = Crossing(values, q, hull[k]);
            while (crossing <= bounds[k])
            {
                k--;
                crossing = Crossing(values, q, hull[k]);
            }

            k++;
            hull[k] = q;
            bounds[k] = crossing;
            bounds[k + 1] = double.PositiveInfinity;
        }

        k = 0;
        for (var q = 0; q < length; q++)
        {
            while (bounds[k + 1] < q)
            {
                k++;
            }

            var p = hull[k];
            output[q] = (q - p) * (double)(q - p) + values[p];
        }
    }

    private static double Crossing(double[] values, int q, int p) =>
        ((values[q] + q * (double)q) - (values[p] + p * (double)p)) / (2.0 * q - 2.0 * p);

    public void Draw(ImageData image, int left, int top)
    {
        for (var y = 0; y < image.Height; y++)
        {
            var targetY = top + y;
            if (targetY < 0 || targetY >= Height)
            {
                continue;
            }

            for (var x = 0; x < image.Width; x++)
            {
                var targetX = left + x;
                if (targetX < 0 || targetX >= Width)
                {
                    continue;
                }

                var source = (y * image.Width + x) * 4;
                var target = (targetY * Width + targetX) * 4;
                var alpha = image.Rgba[source + 3] / 255f;
                var under = Rgba[target + 3] / 255f;
                var outAlpha = alpha + under * (1f - alpha);
                for (var channel = 0; channel < 3; channel++)
                {
                    var value = outAlpha <= 0f ? 0f : (image.Rgba[source + channel] * alpha + Rgba[target + channel] * under * (1f - alpha)) / outAlpha;
                    Rgba[target + channel] = (byte)Math.Clamp((int)Math.Round(value), 0, 255);
                }

                Rgba[target + 3] = (byte)Math.Clamp((int)Math.Round(outAlpha * 255f), 0, 255);
            }
        }
    }

    public static ImageData Collage(IReadOnlyList<ImageData> images, int side, int gap)
    {
        var canvas = Blank(side, side);
        if (images == null || images.Count == 0)
        {
            return canvas;
        }

        var shown = Math.Min(images.Count, 9);
        var columns = shown == 1 ? 1 : shown <= 4 ? 2 : 3;
        var rows = (shown + columns - 1) / columns;
        var cell = (side - gap * (columns + 1)) / columns;
        var top = (side - (rows * cell + (rows - 1) * gap)) / 2;
        for (var index = 0; index < shown; index++)
        {
            var row = index / columns;
            var inRow = Math.Min(columns, shown - row * columns);
            var left = (side - (inRow * cell + (inRow - 1) * gap)) / 2;
            var tile = images[index].Trimmed().Fitted(cell, cell);
            canvas.Draw(tile, left + (index % columns) * (cell + gap), top + row * (cell + gap));
        }

        return canvas;
    }

    public byte[] ToPng() => PngEncoder.Encode(this);

    public Texture2D ToTexture(string name, bool mipmaps = true)
    {
        var pixels = new Color32[Width * Height];
        for (var y = 0; y < Height; y++)
        {
            var source = y * Width * 4;
            var target = (Height - 1 - y) * Width;
            for (var x = 0; x < Width; x++, source += 4)
            {
                pixels[target + x] = new Color32(Rgba[source], Rgba[source + 1], Rgba[source + 2], Rgba[source + 3]);
            }
        }

        var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, mipmaps);
        texture.name = name ?? string.Empty;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = mipmaps ? FilterMode.Trilinear : FilterMode.Bilinear;
        texture.anisoLevel = mipmaps ? 4 : 1;
        texture.SetPixels32(new Il2CppStructArray<Color32>(pixels), 0);
        texture.Apply(mipmaps, false);
        texture.hideFlags = HideFlags.DontUnloadUnusedAsset;
        return texture;
    }

    public static Sprite ToSprite(Texture2D texture, string name = null)
    {
        if (texture == null)
        {
            return null;
        }

        var sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        sprite.name = name ?? texture.name;
        sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
        return sprite;
    }

    private ImageData AreaAverage(int width, int height)
    {
        var result = new byte[width * height * 4];
        var scaleX = (double)Width / width;
        var scaleY = (double)Height / height;
        for (var y = 0; y < height; y++)
        {
            var top = y * scaleY;
            var bottom = (y + 1) * scaleY;
            for (var x = 0; x < width; x++)
            {
                var left = x * scaleX;
                var right = (x + 1) * scaleX;
                double r = 0, g = 0, b = 0, a = 0, area = 0;
                for (var sy = (int)top; sy < Math.Min(Height, (int)Math.Ceiling(bottom)); sy++)
                {
                    var coverY = Math.Min(bottom, sy + 1) - Math.Max(top, sy);
                    for (var sx = (int)left; sx < Math.Min(Width, (int)Math.Ceiling(right)); sx++)
                    {
                        var weight = coverY * (Math.Min(right, sx + 1) - Math.Max(left, sx));
                        var index = (sy * Width + sx) * 4;
                        var alpha = Rgba[index + 3] * weight;
                        r += Rgba[index] * alpha;
                        g += Rgba[index + 1] * alpha;
                        b += Rgba[index + 2] * alpha;
                        a += alpha;
                        area += weight;
                    }
                }

                var target = (y * width + x) * 4;
                if (a > 0)
                {
                    result[target] = (byte)Math.Clamp(Math.Round(r / a), 0, 255);
                    result[target + 1] = (byte)Math.Clamp(Math.Round(g / a), 0, 255);
                    result[target + 2] = (byte)Math.Clamp(Math.Round(b / a), 0, 255);
                }

                result[target + 3] = area > 0 ? (byte)Math.Clamp(Math.Round(a / area), 0, 255) : (byte)0;
            }
        }

        return new ImageData(width, height, result);
    }

    private ImageData Bilinear(int width, int height)
    {
        var result = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            var sourceY = Math.Clamp((y + 0.5) * Height / height - 0.5, 0, Height - 1);
            var y0 = (int)sourceY;
            var y1 = Math.Min(Height - 1, y0 + 1);
            var fy = sourceY - y0;
            for (var x = 0; x < width; x++)
            {
                var sourceX = Math.Clamp((x + 0.5) * Width / width - 0.5, 0, Width - 1);
                var x0 = (int)sourceX;
                var x1 = Math.Min(Width - 1, x0 + 1);
                var fx = sourceX - x0;
                double r = 0, g = 0, b = 0, a = 0;
                Sample(x0, y0, (1 - fx) * (1 - fy));
                Sample(x1, y0, fx * (1 - fy));
                Sample(x0, y1, (1 - fx) * fy);
                Sample(x1, y1, fx * fy);
                var target = (y * width + x) * 4;
                if (a > 0)
                {
                    result[target] = (byte)Math.Clamp(Math.Round(r / a), 0, 255);
                    result[target + 1] = (byte)Math.Clamp(Math.Round(g / a), 0, 255);
                    result[target + 2] = (byte)Math.Clamp(Math.Round(b / a), 0, 255);
                }

                result[target + 3] = (byte)Math.Clamp(Math.Round(a), 0, 255);

                void Sample(int sx, int sy, double weight)
                {
                    var index = (sy * Width + sx) * 4;
                    var alpha = Rgba[index + 3] * weight;
                    r += Rgba[index] * alpha;
                    g += Rgba[index + 1] * alpha;
                    b += Rgba[index + 2] * alpha;
                    a += alpha;
                }
            }
        }

        return new ImageData(width, height, result);
    }
}

public readonly struct ImageArea
{
    public ImageArea(int x, int y, int width, int height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public int X { get; }

    public int Y { get; }

    public int Width { get; }

    public int Height { get; }

    public bool IsEmpty => Width <= 0 || Height <= 0;

    public override string ToString() => $"{Width}x{Height} at {X},{Y}";
}
