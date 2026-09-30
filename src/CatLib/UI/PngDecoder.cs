using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;

namespace CatLib.UI;

public static class PngDecoder
{
    public const int MaxSide = 4096;
    private static readonly byte[] Signature = { 137, 80, 78, 71, 13, 10, 26, 10 };

    public static bool TryDecode(byte[] data, out int width, out int height, out byte[] rgba)
    {
        width = 0;
        height = 0;
        rgba = null;
        try
        {
            return Decode(data, out width, out height, out rgba);
        }
        catch (Exception)
        {
            rgba = null;
            return false;
        }
    }

    private static bool Decode(byte[] data, out int width, out int height, out byte[] rgba)
    {
        width = 0;
        height = 0;
        rgba = null;
        if (data == null || data.Length < 33 || !data.AsSpan(0, 8).SequenceEqual(Signature))
        {
            return false;
        }

        int bitDepth = 0, colorType = 0, interlace = 0;
        byte[] palette = null;
        byte[] transparency = null;
        using var compressed = new MemoryStream();
        var position = 8;
        while (position + 8 <= data.Length)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(position, 4));
            var type = System.Text.Encoding.ASCII.GetString(data, position + 4, 4);
            var start = position + 8;
            if (length < 0 || start + length > data.Length)
            {
                return false;
            }

            switch (type)
            {
                case "IHDR":
                    width = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(start, 4));
                    height = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(start + 4, 4));
                    bitDepth = data[start + 8];
                    colorType = data[start + 9];
                    interlace = data[start + 12];
                    break;
                case "PLTE":
                    palette = data.AsSpan(start, length).ToArray();
                    break;
                case "tRNS":
                    transparency = data.AsSpan(start, length).ToArray();
                    break;
                case "IDAT":
                    compressed.Write(data, start, length);
                    break;
                case "IEND":
                    position = data.Length;
                    continue;
            }

            position = start + length + 4;
        }

        if (width <= 0 || height <= 0 || width > MaxSide || height > MaxSide || interlace != 0)
        {
            return false;
        }

        var channels = colorType switch { 0 => 1, 2 => 3, 3 => 1, 4 => 2, 6 => 4, _ => 0 };
        if (channels == 0 || (colorType == 3 ? bitDepth > 8 : bitDepth != 8 && bitDepth != 16) || (colorType == 3 && palette == null))
        {
            return false;
        }

        var bitsPerPixel = channels * bitDepth;
        var stride = (width * bitsPerPixel + 7) / 8;
        var bytesPerPixel = Math.Max(1, bitsPerPixel / 8);
        compressed.Position = 0;
        var raw = new byte[(stride + 1) * height];
        using (var zlib = new ZLibStream(compressed, CompressionMode.Decompress))
        {
            var read = 0;
            while (read < raw.Length)
            {
                var count = zlib.Read(raw, read, raw.Length - read);
                if (count == 0)
                {
                    return false;
                }

                read += count;
            }
        }

        var previous = new byte[stride];
        var line = new byte[stride];
        rgba = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            var offset = y * (stride + 1);
            var filter = raw[offset];
            Array.Copy(raw, offset + 1, line, 0, stride);
            Unfilter(filter, line, previous, bytesPerPixel);
            for (var x = 0; x < width; x++)
            {
                var target = (y * width + x) * 4;
                Pixel(line, x, colorType, bitDepth, palette, transparency, rgba, target);
            }

            (previous, line) = (line, previous);
        }

        return true;
    }

    private static void Unfilter(byte filter, byte[] line, byte[] previous, int bpp)
    {
        for (var index = 0; index < line.Length; index++)
        {
            var left = index >= bpp ? line[index - bpp] : 0;
            var up = previous[index];
            var upLeft = index >= bpp ? previous[index - bpp] : 0;
            line[index] = filter switch
            {
                1 => (byte)(line[index] + left),
                2 => (byte)(line[index] + up),
                3 => (byte)(line[index] + (left + up) / 2),
                4 => (byte)(line[index] + Paeth(left, up, upLeft)),
                _ => line[index]
            };
        }
    }

    private static int Paeth(int a, int b, int c)
    {
        var p = a + b - c;
        var pa = Math.Abs(p - a);
        var pb = Math.Abs(p - b);
        var pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }

    private static void Pixel(byte[] line, int x, int colorType, int bitDepth, byte[] palette, byte[] transparency, byte[] rgba, int target)
    {
        if (colorType == 3)
        {
            var perByte = 8 / bitDepth;
            var shift = 8 - bitDepth * (x % perByte + 1);
            var index = (line[x / perByte] >> shift) & ((1 << bitDepth) - 1);
            rgba[target] = index * 3 + 2 < palette.Length ? palette[index * 3] : (byte)0;
            rgba[target + 1] = index * 3 + 2 < palette.Length ? palette[index * 3 + 1] : (byte)0;
            rgba[target + 2] = index * 3 + 2 < palette.Length ? palette[index * 3 + 2] : (byte)0;
            rgba[target + 3] = transparency != null && index < transparency.Length ? transparency[index] : (byte)255;
            return;
        }

        var size = bitDepth / 8;
        byte Sample(int channel) => line[(x * ChannelCount(colorType) + channel) * size];
        switch (colorType)
        {
            case 0:
                rgba[target] = rgba[target + 1] = rgba[target + 2] = Sample(0);
                rgba[target + 3] = 255;
                break;
            case 2:
                rgba[target] = Sample(0);
                rgba[target + 1] = Sample(1);
                rgba[target + 2] = Sample(2);
                rgba[target + 3] = 255;
                break;
            case 4:
                rgba[target] = rgba[target + 1] = rgba[target + 2] = Sample(0);
                rgba[target + 3] = Sample(1);
                break;
            default:
                rgba[target] = Sample(0);
                rgba[target + 1] = Sample(1);
                rgba[target + 2] = Sample(2);
                rgba[target + 3] = Sample(3);
                break;
        }
    }

    private static int ChannelCount(int colorType) => colorType switch { 0 => 1, 2 => 3, 4 => 2, _ => 4 };
}
