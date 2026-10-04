using System;
using System.IO;
using System.Text.Json;

namespace CatLib.Config;

public static class ManifestLocator
{
    public const string FileName = "manifest.json";
    public const int MaxDescriptionLength = 1000;

    public static string Find(string pluginDirectory)
    {
        if (string.IsNullOrWhiteSpace(pluginDirectory))
        {
            return null;
        }

        try
        {
            var directory = new DirectoryInfo(pluginDirectory);
            for (var level = 0; directory != null && level <= IconLocator.MaxLevelsUp; level++)
            {
                if (string.Equals(directory.Name, "plugins", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(directory.Name, "BepInEx", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                var candidate = Path.Combine(directory.FullName, FileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }
        }
        catch (Exception)
        {
        }

        return null;
    }

    public static string Description(string pluginDirectory)
    {
        var path = Find(pluginDirectory);
        if (path == null)
        {
            return null;
        }

        try
        {
            return ParseDescription(File.ReadAllText(path));
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static string ParseDescription(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("description", out var description)
                || description.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            var text = description.GetString()?.Trim();
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }

            return text.Length > MaxDescriptionLength ? text.Substring(0, MaxDescriptionLength) : text;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
