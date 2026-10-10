using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CatLib.Assets;

public sealed class PackFile
{
    public const string NameKey = "name";
    public const string VersionKey = "version";
    public const string AuthorKey = "author";
    public const string DescriptionKey = "description";
    public const string WebsiteKey = "website";

    private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _problems = new();

    private PackFile()
    {
    }

    public IReadOnlyDictionary<string, string> Values => _values;

    public IReadOnlyList<string> Problems => _problems;

    public string this[string key] => Get(key);

    public string Get(string key, string fallback = null) =>
        _values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;

    public static PackFile Read(string path) => Parse(File.ReadAllText(path, Encoding.UTF8));

    public static PackFile Parse(string text)
    {
        var file = new PackFile();
        var lines = (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        string continued = null;
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            var trimmed = line.Trim().TrimStart('﻿');
            if (trimmed.Length == 0 || trimmed.StartsWith("#", StringComparison.Ordinal) || trimmed.StartsWith("//", StringComparison.Ordinal))
            {
                continued = null;
                continue;
            }

            if (continued != null && (line.StartsWith(" ", StringComparison.Ordinal) || line.StartsWith("\t", StringComparison.Ordinal)))
            {
                file._values[continued] = file._values[continued] + "\n" + trimmed;
                continue;
            }

            var separator = Separator(trimmed);
            if (separator <= 0)
            {
                file._problems.Add($"line {index + 1} is not \"key: value\": {trimmed}");
                continued = null;
                continue;
            }

            var key = trimmed.Substring(0, separator).Trim();
            var value = trimmed.Substring(separator + 1).Trim();
            if (file._values.ContainsKey(key))
            {
                file._problems.Add($"line {index + 1} repeats \"{key}\", the last value is used");
            }

            file._values[key] = value;
            continued = key;
        }

        return file;
    }

    private static int Separator(string line)
    {
        var colon = line.IndexOf(':');
        var equals = line.IndexOf('=');
        if (colon < 0)
        {
            return equals;
        }

        return equals < 0 ? colon : Math.Min(colon, equals);
    }
}
