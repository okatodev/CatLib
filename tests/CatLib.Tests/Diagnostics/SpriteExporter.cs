using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using CatLib.Logging;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CatLib.Tests.Diagnostics;

internal sealed class SpriteExporter
{
    public const int MaxSide = 4096;

    private readonly string _root;
    private readonly CatLogger _log;

    public SpriteExporter(string root, CatLogger log)
    {
        _root = root;
        _log = log;
    }

    public string Export()
    {
        var scene = SceneManager.GetActiveScene().name;
        var directory = Path.Combine(_root, "Sprites_" + Safe(scene) + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(directory);

        var groups = new Dictionary<IntPtr, (Texture2D Texture, List<Sprite> Sprites)>();
        foreach (var item in Resources.FindObjectsOfTypeAll(Il2CppType.Of<Sprite>()))
        {
            var sprite = item.TryCast<Sprite>();
            var texture = sprite == null ? null : sprite.texture;
            if (texture == null)
            {
                continue;
            }

            if (!groups.TryGetValue(texture.Pointer, out var group))
            {
                group = (texture, new List<Sprite>());
                groups[texture.Pointer] = group;
            }

            group.Sprites.Add(sprite);
        }

        var index = new StringBuilder();
        index.AppendLine("CatLib sprite export, scene " + scene);
        index.AppendLine("texture file\ttexture\twidth\theight\tsprite\tx\ty\twidth\theight (pixels, y from the bottom of the texture)");
        var written = 0;
        var failed = 0;
        foreach (var group in groups.Values.OrderBy(entry => entry.Texture.name, StringComparer.Ordinal))
        {
            var texture = group.Texture;
            var file = Safe(texture.name) + "_" + texture.GetInstanceID().ToString(CultureInfo.InvariantCulture).TrimStart('-') + ".png";
            var status = file;
            if (texture.width > MaxSide || texture.height > MaxSide)
            {
                status = "skipped, too large";
            }
            else
            {
                try
                {
                    File.WriteAllBytes(Path.Combine(directory, file), Png(Read(texture), texture.width, texture.height));
                    written++;
                }
                catch (Exception exception)
                {
                    status = "failed: " + exception.Message;
                    failed++;
                }
            }

            foreach (var sprite in group.Sprites.OrderBy(entry => entry.name, StringComparer.Ordinal))
            {
                Rect rect;
                try
                {
                    rect = sprite.textureRect;
                }
                catch (Exception)
                {
                    rect = sprite.rect;
                }

                index.AppendLine(string.Join("\t", status, texture.name, texture.width.ToString(CultureInfo.InvariantCulture), texture.height.ToString(CultureInfo.InvariantCulture),
                    sprite.name, Number(rect.x), Number(rect.y), Number(rect.width), Number(rect.height)));
            }
        }

        index.AppendLine();
        index.AppendLine("Other textures (not exported): name\twidth\theight");
        foreach (var item in Resources.FindObjectsOfTypeAll(Il2CppType.Of<Texture2D>()))
        {
            var texture = item.TryCast<Texture2D>();
            if (texture != null && !groups.ContainsKey(texture.Pointer))
            {
                index.AppendLine(texture.name + "\t" + texture.width.ToString(CultureInfo.InvariantCulture) + "\t" + texture.height.ToString(CultureInfo.InvariantCulture));
            }
        }

        File.WriteAllText(Path.Combine(directory, "index.txt"), index.ToString(), Encoding.UTF8);
        _log.Message($"Exported {written} sprite texture(s) with {groups.Values.Sum(group => group.Sprites.Count)} sprite(s) to {directory}, {failed} failed");
        return directory;
    }

    public string ExportTextures(IEnumerable<string> names)
    {
        var wanted = new HashSet<string>(names.Select(name => name.Trim()).Where(name => name.Length > 0), StringComparer.OrdinalIgnoreCase);
        var directory = Path.Combine(_root, "Textures_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(directory);
        var seen = new HashSet<IntPtr>();
        var written = new List<string>();
        foreach (var item in Resources.FindObjectsOfTypeAll(Il2CppType.Of<Texture2D>()))
        {
            var texture = item.TryCast<Texture2D>();
            if (texture == null || !wanted.Contains(texture.name) || !seen.Add(texture.Pointer) || texture.width > MaxSide || texture.height > MaxSide)
            {
                continue;
            }

            var file = Safe(texture.name) + "_" + texture.GetInstanceID().ToString(CultureInfo.InvariantCulture).TrimStart('-') + ".png";
            try
            {
                File.WriteAllBytes(Path.Combine(directory, file), Png(Read(texture), texture.width, texture.height));
                written.Add(texture.name);
            }
            catch (Exception exception)
            {
                _log.Warning($"Exporting {texture.name} failed: {exception.Message}");
            }
        }

        var missing = wanted.Where(name => !written.Contains(name, StringComparer.OrdinalIgnoreCase)).ToList();
        _log.Message($"Exported {written.Count} texture(s) to {directory}{(missing.Count == 0 ? string.Empty : ", not loaded now: " + string.Join(", ", missing))}");
        return directory;
    }

    private static byte[] Read(Texture2D texture)
    {
        var width = texture.width;
        var height = texture.height;
        var target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        target.Create();
        var previous = RenderTexture.active;
        var readable = new Texture2D(width, height, TextureFormat.RGBA32, false);
        try
        {
            Graphics.Blit(texture, target);
            RenderTexture.active = target;
            readable.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            readable.Apply(false, false);
            var pixels = readable.GetPixels32();
            var bytes = new byte[width * height * 4];
            var span = pixels.AsSpan();
            for (var y = 0; y < height; y++)
            {
                var source = (height - 1 - y) * width;
                var row = y * width * 4;
                for (var x = 0; x < width; x++)
                {
                    var color = span[source + x];
                    var offset = row + x * 4;
                    bytes[offset] = color.r;
                    bytes[offset + 1] = color.g;
                    bytes[offset + 2] = color.b;
                    bytes[offset + 3] = color.a;
                }
            }

            return bytes;
        }
        finally
        {
            RenderTexture.active = previous;
            target.Release();
            UnityEngine.Object.Destroy(target);
            UnityEngine.Object.Destroy(readable);
        }
    }

    internal static byte[] Png(byte[] rgba, int width, int height)
    {
        using var output = new MemoryStream();
        output.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var header = new byte[13];
        WriteBigEndian(header, 0, (uint)width);
        WriteBigEndian(header, 4, (uint)height);
        header[8] = 8;
        header[9] = 6;
        Chunk(output, "IHDR", header);
        using (var compressed = new MemoryStream())
        {
            using (var zlib = new ZLibStream(compressed, System.IO.Compression.CompressionLevel.Optimal, true))
            {
                for (var y = 0; y < height; y++)
                {
                    zlib.WriteByte(0);
                    zlib.Write(rgba, y * width * 4, width * 4);
                }
            }

            Chunk(output, "IDAT", compressed.ToArray());
        }

        Chunk(output, "IEND", Array.Empty<byte>());
        return output.ToArray();
    }

    private static void Chunk(Stream output, string type, byte[] data)
    {
        var length = new byte[4];
        WriteBigEndian(length, 0, (uint)data.Length);
        output.Write(length);
        var typeBytes = Encoding.ASCII.GetBytes(type);
        output.Write(typeBytes);
        output.Write(data);
        var crc = Crc(Crc(0xFFFFFFFF, typeBytes), data) ^ 0xFFFFFFFF;
        var crcBytes = new byte[4];
        WriteBigEndian(crcBytes, 0, crc);
        output.Write(crcBytes);
    }

    private static readonly uint[] CrcTable = Enumerable.Range(0, 256).Select(index =>
    {
        var value = (uint)index;
        for (var bit = 0; bit < 8; bit++)
        {
            value = (value & 1) != 0 ? 0xEDB88320 ^ (value >> 1) : value >> 1;
        }

        return value;
    }).ToArray();

    private static uint Crc(uint crc, byte[] data)
    {
        foreach (var value in data)
        {
            crc = CrcTable[(crc ^ value) & 0xFF] ^ (crc >> 8);
        }

        return crc;
    }

    private static void WriteBigEndian(byte[] buffer, int offset, uint value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }

    private static string Number(float value) => ((int)Math.Round(value)).ToString(CultureInfo.InvariantCulture);

    private static string Safe(string name)
    {
        var builder = new StringBuilder();
        foreach (var character in string.IsNullOrEmpty(name) ? "unnamed" : name)
        {
            builder.Append(char.IsLetterOrDigit(character) || character == '_' || character == '-' ? character : '_');
        }

        return builder.ToString();
    }
}
