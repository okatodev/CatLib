using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace CatLib.Localization;

public sealed class TextCatalog
{
    private readonly object _sync = new();
    private readonly Dictionary<string, Dictionary<string, string>> _languages = new(StringComparer.Ordinal);

    public TextCatalog(string ownerId)
    {
        OwnerId = ownerId;
    }

    public string OwnerId { get; }

    public IReadOnlyList<string> Languages
    {
        get
        {
            lock (_sync)
            {
                return _languages.Keys.OrderBy(language => language, StringComparer.Ordinal).ToList();
            }
        }
    }

    public int CountFor(string language)
    {
        lock (_sync)
        {
            return _languages.TryGetValue(CatLanguage.Normalize(language), out var entries) ? entries.Count : 0;
        }
    }

    public void Add(string language, string key, string text)
    {
        if (string.IsNullOrEmpty(key) || text == null)
        {
            return;
        }

        lock (_sync)
        {
            var code = CatLanguage.Normalize(language);
            if (!_languages.TryGetValue(code, out var entries))
            {
                entries = new Dictionary<string, string>(StringComparer.Ordinal);
                _languages[code] = entries;
            }

            entries[key] = text;
        }
    }

    public void Add(string language, IEnumerable<KeyValuePair<string, string>> entries)
    {
        foreach (var pair in entries)
        {
            Add(language, pair.Key, pair.Value);
        }
    }

    public int LoadJson(string language, string json)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json ?? string.Empty, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        }
        catch (JsonException exception)
        {
            throw new FormatException($"Translations for \"{language}\" are not valid JSON: {exception.Message}", exception);
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new FormatException($"Translations for \"{language}\" must be a JSON object");
            }

            var entries = new List<KeyValuePair<string, string>>();
            Flatten(document.RootElement, string.Empty, entries);
            Add(language, entries);
            return entries.Count;
        }
    }

    public int LoadDirectory(string directory)
    {
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            return 0;
        }

        var total = 0;
        foreach (var file in Directory.GetFiles(directory, "*.json"))
        {
            total += LoadJson(Path.GetFileNameWithoutExtension(file), File.ReadAllText(file));
        }

        return total;
    }

    public int LoadEmbedded(Assembly assembly, string resourcePrefix)
    {
        var total = 0;
        foreach (var name in assembly.GetManifestResourceNames())
        {
            if (!name.StartsWith(resourcePrefix, StringComparison.Ordinal) || !name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var language = name.Substring(resourcePrefix.Length, name.Length - resourcePrefix.Length - ".json".Length).Trim('.');
            using var stream = assembly.GetManifestResourceStream(name);
            using var reader = new StreamReader(stream);
            total += LoadJson(language, reader.ReadToEnd());
        }

        return total;
    }

    public bool Has(string key, string language)
    {
        lock (_sync)
        {
            return _languages.TryGetValue(CatLanguage.Normalize(language), out var entries) && entries.ContainsKey(key);
        }
    }

    public string Find(string key, string language)
    {
        lock (_sync)
        {
            foreach (var code in CatLanguage.Chain(language))
            {
                if (_languages.TryGetValue(code, out var entries) && entries.TryGetValue(key, out var text))
                {
                    return text;
                }
            }
        }

        return null;
    }

    public string Get(string key, string language) => Find(key, language) ?? key;

    public string Get(string key) => Get(key, CatLanguage.Current);

    public string Format(string key, string language, params object[] arguments) =>
        string.Format(CultureInfo.InvariantCulture, Get(key, language), arguments);

    public string Format(string key, params object[] arguments) => Format(key, CatLanguage.Current, arguments);

    private static void Flatten(JsonElement element, string prefix, List<KeyValuePair<string, string>> entries)
    {
        foreach (var property in element.EnumerateObject())
        {
            var key = prefix.Length == 0 ? property.Name : prefix + "." + property.Name;
            switch (property.Value.ValueKind)
            {
                case JsonValueKind.Object:
                    Flatten(property.Value, key, entries);
                    break;
                case JsonValueKind.String:
                    entries.Add(new KeyValuePair<string, string>(key, property.Value.GetString()));
                    break;
                default:
                    throw new FormatException($"Translation \"{key}\" must be a string or an object");
            }
        }
    }
}
