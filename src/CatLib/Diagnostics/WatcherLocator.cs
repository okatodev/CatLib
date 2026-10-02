using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace CatLib.Diagnostics;

internal static class WatcherLocator
{
    public const int MaxDepth = 4;

    public static string Find(string ownDirectory, string pluginsDirectory, string fileName)
    {
        if (!string.IsNullOrEmpty(ownDirectory))
        {
            var beside = Path.Combine(ownDirectory, fileName);
            if (File.Exists(beside))
            {
                return beside;
            }
        }

        if (string.IsNullOrEmpty(pluginsDirectory) || !Directory.Exists(pluginsDirectory))
        {
            return null;
        }

        string best = null;
        Version bestVersion = null;
        foreach (var path in Search(pluginsDirectory, fileName, 0))
        {
            var version = VersionOf(path);
            if (best == null || version > bestVersion)
            {
                best = path;
                bestVersion = version;
            }
        }

        return best;
    }

    public static Version VersionOf(string path)
    {
        try
        {
            var info = FileVersionInfo.GetVersionInfo(path);
            return new Version(info.FileMajorPart, info.FileMinorPart, info.FileBuildPart);
        }
        catch (Exception)
        {
            return new Version(0, 0, 0);
        }
    }

    public static string VersionText(string path)
    {
        var version = VersionOf(path);
        return version.Major == 0 && version.Minor == 0 && version.Build == 0 ? "of unknown version" : version.ToString(3);
    }

    private static IEnumerable<string> Search(string directory, string fileName, int depth)
    {
        string[] files;
        string[] directories;
        try
        {
            files = Directory.GetFiles(directory, fileName);
            directories = depth < MaxDepth ? Directory.GetDirectories(directory) : Array.Empty<string>();
        }
        catch (Exception)
        {
            yield break;
        }

        foreach (var file in files.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            yield return file;
        }

        foreach (var child in directories.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var file in Search(child, fileName, depth + 1))
            {
                yield return file;
            }
        }
    }
}
