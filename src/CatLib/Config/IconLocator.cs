using System;
using System.IO;

namespace CatLib.Config;

public static class IconLocator
{
    public const string FileName = "icon.png";
    public const int MaxLevelsUp = 2;

    public static string Find(string pluginDirectory)
    {
        if (string.IsNullOrWhiteSpace(pluginDirectory))
        {
            return null;
        }

        try
        {
            var directory = new DirectoryInfo(pluginDirectory);
            for (var level = 0; directory != null && level <= MaxLevelsUp; level++)
            {
                if (IsSharedFolder(directory.Name))
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

    private static bool IsSharedFolder(string name) =>
        string.Equals(name, "plugins", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name, "BepInEx", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name, "patchers", StringComparison.OrdinalIgnoreCase);
}
