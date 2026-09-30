using System;
using System.Collections.Generic;
using System.IO;
using CatLib.Config;
using CatLib.Logging;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace CatLib.UI;

internal static class ModIcons
{
    public const int MaxBytes = 2 * 1024 * 1024;
    public const int ThunderstoreSide = 256;

    private static readonly Dictionary<string, Sprite> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> Reported = new(StringComparer.Ordinal);

    public static CatLogger Log { get; set; }

    public static Sprite For(CatSettings settings)
    {
        if (settings == null)
        {
            return null;
        }

        if (string.IsNullOrEmpty(settings.IconPath))
        {
            if (Reported.Add(settings.OwnerId))
            {
                Log?.Info(string.IsNullOrEmpty(settings.PluginDirectory)
                    ? $"{settings.DisplayName} has no icon: its plugin folder is unknown"
                    : $"{settings.DisplayName} has no icon: no {IconLocator.FileName} in {settings.PluginDirectory} or up to {IconLocator.MaxLevelsUp} folders above");
            }

            return null;
        }

        var sprite = For(settings.IconPath);
        if (Reported.Add(settings.OwnerId))
        {
            try
            {
                Report(settings, sprite);
            }
            catch (Exception exception)
            {
                Log?.Warning($"Describing the icon of {settings.DisplayName} failed: {exception.GetType().Name}: {exception.Message}");
            }
        }

        return sprite;
    }

    private static void Report(CatSettings settings, Sprite sprite)
    {
        if (sprite == null)
        {
            Log?.Warning($"{settings.DisplayName} has an icon at {settings.IconPath}, but it could not be loaded");
            return;
        }

        var texture = sprite.texture;
        Log?.Info($"{settings.DisplayName} icon {texture.width}x{texture.height} from {settings.IconPath}");
        if (texture.width != ThunderstoreSide || texture.height != ThunderstoreSide)
        {
            Log?.Warning($"The icon of {settings.DisplayName} is {texture.width}x{texture.height}, Thunderstore expects {ThunderstoreSide}x{ThunderstoreSide}");
        }
    }

    public static Sprite For(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        if (Cache.TryGetValue(path, out var cached) && (cached == null || !cached.WasCollected))
        {
            return cached;
        }

        Sprite sprite = null;
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length > MaxBytes)
            {
                Log?.Warning($"The mod icon {path} is missing or larger than {MaxBytes / 1024} KiB");
            }
            else
            {
                var bytes = File.ReadAllBytes(path);
                var texture = Load(bytes, path);
                if (texture != null)
                {
                    texture.name = "CatLib icon " + Path.GetFileName(Path.GetDirectoryName(path));
                    texture.filterMode = FilterMode.Trilinear;
                    texture.wrapMode = TextureWrapMode.Clamp;
                    texture.hideFlags = HideFlags.DontUnloadUnusedAsset;
                    sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
                    sprite.name = texture.name;
                    sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
                }
                else
                {
                    Log?.Warning($"The mod icon {path} is not a PNG image CatLib can read");
                }
            }
        }
        catch (Exception exception)
        {
            Log?.Warning($"Loading the mod icon {path} failed: {exception.GetType().Name}: {exception.Message}");
        }

        Cache[path] = sprite;
        return sprite;
    }

    private static Texture2D Load(byte[] bytes, string path)
    {
        try
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, true);
            if (ImageConversion.LoadImage(texture, bytes, false) && texture.width > 2)
            {
                return texture;
            }

            UnityEngine.Object.Destroy(texture);
            Log?.Info($"The game did not decode {path}, CatLib decodes it itself");
        }
        catch (Exception exception)
        {
            Log?.Info($"The game cannot decode images itself ({exception.GetType().Name}: {exception.Message}), CatLib decodes {path}");
        }

        if (!PngDecoder.TryDecode(bytes, out var width, out var height, out var rgba))
        {
            return null;
        }

        var pixels = new Color32[width * height];
        for (var y = 0; y < height; y++)
        {
            var source = y * width * 4;
            var target = (height - 1 - y) * width;
            for (var x = 0; x < width; x++, source += 4)
            {
                pixels[target + x] = new Color32(rgba[source], rgba[source + 1], rgba[source + 2], rgba[source + 3]);
            }
        }

        var decoded = new Texture2D(width, height, TextureFormat.RGBA32, true);
        decoded.SetPixels32(new Il2CppStructArray<Color32>(pixels), 0);
        decoded.Apply(true, false);
        return decoded;
    }
}
