using System;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace CatLib.Assets;

public static class GameImages
{
    public const int MaxSide = 4096;

    public static Sprite FindSprite(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        foreach (var item in Resources.FindObjectsOfTypeAll(Il2CppType.Of<Sprite>()))
        {
            var sprite = item.TryCast<Sprite>();
            if (sprite != null && sprite.texture != null && string.Equals(sprite.name, name, StringComparison.Ordinal))
            {
                return sprite;
            }
        }

        return null;
    }

    public static Texture2D FindTexture(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        foreach (var item in Resources.FindObjectsOfTypeAll(Il2CppType.Of<Texture2D>()))
        {
            var texture = item.TryCast<Texture2D>();
            if (texture != null && string.Equals(texture.name, name, StringComparison.Ordinal))
            {
                return texture;
            }
        }

        return null;
    }

    public static bool TryReadArea(string textureName, int x, int y, int width, int height, out ImageData image, out string problem)
    {
        image = null;
        problem = null;
        try
        {
            var texture = FindTexture(textureName);
            if (texture == null)
            {
                problem = $"the game has no texture {textureName} loaded now";
                return false;
            }

            if (x < 0 || y < 0 || width <= 0 || height <= 0 || x + width > texture.width || y + height > texture.height)
            {
                problem = $"{width}x{height} at {x},{y} is outside {textureName} of {texture.width}x{texture.height}";
                return false;
            }

            var whole = Read(texture);
            image = whole.Cropped(new ImageArea(x, whole.Height - y - height, width, height));
            return true;
        }
        catch (Exception exception)
        {
            problem = exception.Message;
            return false;
        }
    }

    public static bool TryRead(string spriteName, out ImageData image, out string problem)
    {
        image = null;
        problem = null;
        try
        {
            var sprite = FindSprite(spriteName);
            if (sprite == null)
            {
                problem = $"the game has no sprite {spriteName} loaded now";
                return false;
            }

            image = Read(sprite);
            return true;
        }
        catch (Exception exception)
        {
            problem = exception.Message;
            return false;
        }
    }

    public static ImageData Read(Sprite sprite)
    {
        if (sprite == null)
        {
            throw new ArgumentNullException(nameof(sprite));
        }

        var texture = sprite.texture ?? throw new InvalidOperationException($"The sprite {sprite.name} has no texture");
        var whole = Read(texture);
        Rect rect;
        try
        {
            rect = sprite.textureRect;
        }
        catch (Exception)
        {
            rect = sprite.rect;
        }

        var x = Mathf.Clamp(Mathf.RoundToInt(rect.x), 0, whole.Width - 1);
        var width = Mathf.Clamp(Mathf.RoundToInt(rect.width), 1, whole.Width - x);
        var height = Mathf.Clamp(Mathf.RoundToInt(rect.height), 1, whole.Height);
        var y = Mathf.Clamp(whole.Height - Mathf.RoundToInt(rect.y) - height, 0, whole.Height - height);
        return whole.Cropped(new ImageArea(x, y, width, height));
    }

    public static ImageData Read(Texture texture)
    {
        if (texture == null)
        {
            throw new ArgumentNullException(nameof(texture));
        }

        var width = texture.width;
        var height = texture.height;
        if (width <= 0 || height <= 0 || width > MaxSide || height > MaxSide)
        {
            throw new InvalidOperationException($"The texture {texture.name} is {width}x{height}, at most {MaxSide}x{MaxSide} is read");
        }

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
            for (var row = 0; row < height; row++)
            {
                var source = (height - 1 - row) * width;
                var offset = row * width * 4;
                for (var column = 0; column < width; column++)
                {
                    var color = pixels[source + column];
                    var target4 = offset + column * 4;
                    bytes[target4] = color.r;
                    bytes[target4 + 1] = color.g;
                    bytes[target4 + 2] = color.b;
                    bytes[target4 + 3] = color.a;
                }
            }

            return new ImageData(width, height, bytes);
        }
        finally
        {
            RenderTexture.active = previous;
            target.Release();
            UnityEngine.Object.Destroy(target);
            UnityEngine.Object.Destroy(readable);
        }
    }
}
