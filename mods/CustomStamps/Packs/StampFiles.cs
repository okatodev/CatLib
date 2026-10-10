using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CustomStamps.Packs;

public static class StampFiles
{
    public const string PackFile = "stamps.txt";
    public const string DecorativeFolder = "decorative";
    public const string WeightFolder = "weight";
    public const string IconFile = "icon.png";
    public const string KeyPrefix = "CustomStamps/";
    public const int MaxPerKind = 64;
    public const string NotSupported = "NotSupported";
    public const string TooMany = "TooMany";

    private static readonly HashSet<string> Images = new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg" };

    private static readonly HashSet<string> OtherImages = new(StringComparer.OrdinalIgnoreCase) { ".gif", ".bmp", ".webp", ".tga", ".psd", ".tif", ".tiff", ".heic", ".avif" };

    public static string FolderOf(StampKind kind) => kind == StampKind.Weight ? WeightFolder : DecorativeFolder;

    public static List<(StampKind Kind, string Path)> Find(string packFolder, List<(string File, string Reason)> skipped)
    {
        var found = new List<(StampKind, string)>();
        AddFolder(found, skipped, StampKind.Decorative, packFolder, true);
        AddFolder(found, skipped, StampKind.Decorative, Path.Combine(packFolder, DecorativeFolder), false);
        AddFolder(found, skipped, StampKind.Weight, Path.Combine(packFolder, WeightFolder), false);
        return found;
    }

    public static string Key(string packId, StampKind kind, string path)
    {
        var dot = packId.IndexOf('.');
        var pack = dot >= 0 ? packId.Substring(dot + 1) : packId;
        return KeyPrefix + pack + "/" + FolderOf(kind) + "/" + Clean(Path.GetFileNameWithoutExtension(path));
    }

    public static bool IsCustomKey(string key) => key != null && key.StartsWith(KeyPrefix, StringComparison.Ordinal);

    public static int Compare(string left, string right)
    {
        var a = Path.GetFileNameWithoutExtension(left) ?? string.Empty;
        var b = Path.GetFileNameWithoutExtension(right) ?? string.Empty;
        int i = 0, j = 0;
        while (i < a.Length && j < b.Length)
        {
            if (char.IsDigit(a[i]) && char.IsDigit(b[j]))
            {
                var startA = i;
                var startB = j;
                while (i < a.Length && char.IsDigit(a[i]))
                {
                    i++;
                }

                while (j < b.Length && char.IsDigit(b[j]))
                {
                    j++;
                }

                var numberA = a.Substring(startA, i - startA).TrimStart('0');
                var numberB = b.Substring(startB, j - startB).TrimStart('0');
                var byLength = numberA.Length.CompareTo(numberB.Length);
                if (byLength != 0)
                {
                    return byLength;
                }

                var byDigits = string.CompareOrdinal(numberA, numberB);
                if (byDigits != 0)
                {
                    return byDigits;
                }

                continue;
            }

            var byChar = char.ToLowerInvariant(a[i]).CompareTo(char.ToLowerInvariant(b[j]));
            if (byChar != 0)
            {
                return byChar;
            }

            i++;
            j++;
        }

        return (a.Length - i).CompareTo(b.Length - j);
    }

    private static void AddFolder(List<(StampKind, string)> found, List<(string File, string Reason)> skipped, StampKind kind, string folder, bool root)
    {
        if (!Directory.Exists(folder))
        {
            return;
        }

        var images = new List<string>();
        foreach (var file in Directory.EnumerateFiles(folder))
        {
            var name = Path.GetFileName(file);
            var extension = Path.GetExtension(file);
            if (root && string.Equals(name, IconFile, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (Images.Contains(extension))
            {
                images.Add(file);
            }
            else if (OtherImages.Contains(extension))
            {
                skipped?.Add((Relative(folder, file, root), NotSupported));
            }
        }

        images.Sort(Compare);
        var count = found.Count(entry => entry.Item1 == kind);
        foreach (var image in images)
        {
            if (count >= MaxPerKind)
            {
                skipped?.Add((Relative(folder, image, root), TooMany));
                continue;
            }

            found.Add((kind, image));
            count++;
        }
    }

    private static string Relative(string folder, string file, bool root) =>
        root ? Path.GetFileName(file) : Path.GetFileName(folder) + "/" + Path.GetFileName(file);

    private static string Clean(string name)
    {
        var builder = new StringBuilder(name.Length);
        foreach (var character in name)
        {
            builder.Append(char.IsLetterOrDigit(character) || character == '-' || character == '_' ? character : '_');
        }

        return builder.ToString();
    }
}
