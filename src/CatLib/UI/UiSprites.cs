using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace CatLib.UI;

public static class UiSprites
{
    private static readonly Dictionary<int, Sprite> Plates = new();
    private static readonly Dictionary<string, Sprite> Loaded = new(StringComparer.Ordinal);
    private static readonly Dictionary<(IntPtr Sprite, int Share), Sprite> SlicedSprites = new();
    private static readonly Dictionary<(int Radius, int Thickness), Sprite> Rings = new();

    public static Sprite RoundedPlate(int radius)
    {
        radius = Math.Max(2, radius);
        if (Plates.TryGetValue(radius, out var cached) && cached != null && !cached.WasCollected)
        {
            return cached;
        }

        var size = radius * 2 + 4;
        var pixels = new Color32[size * size];
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var dx = Math.Max(Math.Max(radius - (x + 0.5f), x + 0.5f - (size - radius)), 0f);
                var dy = Math.Max(Math.Max(radius - (y + 0.5f), y + 0.5f - (size - radius)), 0f);
                var coverage = Mathf.Clamp01(radius - (float)Math.Sqrt(dx * dx + dy * dy) + 0.5f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Math.Round(coverage * 255f));
            }
        }

        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.SetPixels32(new Il2CppStructArray<Color32>(pixels), 0);
        texture.Apply(false, false);
        texture.hideFlags = HideFlags.DontUnloadUnusedAsset;
        var border = new Vector4(radius, radius, radius, radius);
        var sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        sprite.name = "CatLib rounded plate " + radius;
        sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
        Plates[radius] = sprite;
        return sprite;
    }

    public static Sprite FromPng(byte[] bytes, string name)
    {
        if (bytes == null || bytes.Length == 0)
        {
            return null;
        }

        var texture = ModIcons.Load(bytes, name);
        if (texture == null)
        {
            return null;
        }

        texture.name = name;
        texture.filterMode = FilterMode.Trilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.hideFlags = HideFlags.DontUnloadUnusedAsset;
        var sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        sprite.name = name;
        sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
        return sprite;
    }

    public static Sprite FromResource(Assembly assembly, string resourceName)
    {
        var key = assembly.GetName().Name + "/" + resourceName;
        if (Loaded.TryGetValue(key, out var cached) && (cached == null || !cached.WasCollected))
        {
            return cached;
        }

        Sprite sprite = null;
        try
        {
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream != null)
            {
                using var memory = new MemoryStream();
                stream.CopyTo(memory);
                sprite = FromPng(memory.ToArray(), resourceName);
            }
        }
        catch (Exception)
        {
            sprite = null;
        }

        Loaded[key] = sprite;
        return sprite;
    }
    public static Sprite Sliced(Sprite sprite, float share)
    {
        if (sprite == null || sprite.border != Vector4.zero || share <= 0f)
        {
            return sprite;
        }

        var key = (sprite.Pointer, Mathf.RoundToInt(share * 1000f));
        if (SlicedSprites.TryGetValue(key, out var cached) && cached != null && !cached.WasCollected)
        {
            return cached;
        }

        try
        {
            var rect = sprite.rect;
            var border = new Vector4(rect.width * share, rect.height * share, rect.width * share, rect.height * share);
            var created = Sprite.Create(sprite.texture, sprite.textureRect, new Vector2(0.5f, 0.5f), sprite.pixelsPerUnit, 0, SpriteMeshType.FullRect, border);
            created.name = sprite.name + " (CatLib sliced)";
            created.hideFlags = HideFlags.DontUnloadUnusedAsset;
            SlicedSprites[key] = created;
            return created;
        }
        catch (Exception)
        {
            SlicedSprites[key] = sprite;
            return sprite;
        }
    }

    public static float BorderMultiplier(Sprite sprite, float borderOnScreen, float referencePixelsPerUnit = 100f)
    {
        if (sprite == null || borderOnScreen <= 0f || sprite.border.x <= 0f)
        {
            return 1f;
        }

        return Mathf.Max(0.01f, sprite.border.x * referencePixelsPerUnit / (sprite.pixelsPerUnit * borderOnScreen));
    }

    public static Sprite InkRing(int radius, int thickness)
    {
        radius = Math.Max(8, radius);
        thickness = Math.Clamp(thickness, 2, radius - 2);
        if (Rings.TryGetValue((radius, thickness), out var cached) && cached != null && !cached.WasCollected)
        {
            return cached;
        }

        var size = radius * 2 + 4;
        var pixels = new Color32[size * size];
        var random = new System.Random(radius * 31 + thickness);
        var center = size / 2f;
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var dx = x + 0.5f - center;
                var dy = y + 0.5f - center;
                var distance = (float)Math.Sqrt(dx * dx + dy * dy);
                var outer = Mathf.Clamp01(radius - distance + 0.5f);
                var inner = Mathf.Clamp01(distance - (radius - thickness) + 0.5f);
                var coverage = Math.Min(outer, inner);
                var grain = 0.72f + (float)random.NextDouble() * 0.28f;
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Math.Round(coverage * grain * 255f));
            }
        }

        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.SetPixels32(new Il2CppStructArray<Color32>(pixels), 0);
        texture.Apply(false, false);
        texture.hideFlags = HideFlags.DontUnloadUnusedAsset;
        var sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        sprite.name = "CatLib ink ring " + radius;
        sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
        Rings[(radius, thickness)] = sprite;
        return sprite;
    }
}
