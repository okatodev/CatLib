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
    private readonly Dictionary<string, Dictionary<string, string>> _overrides = new(StringComparer.Ordinal);
    private readonly HashSet<string> _reportedFormats = new(StringComparer.Ordinal);

    public TextCatalog(string ownerId)
    {
        OwnerId = ownerId;
    }

    public string OwnerId { get; }

    public string OverridesDirectory { get; private set; }

    public int Revision { get; private set; }

    public IReadOnlyList<string> Languages
    {
        get
        {
            lock (_sync)
            {
                return _languages.Keys.Concat(_overrides.Keys).Distinct().OrderBy(language => language, StringComparer.Ordinal).ToList();
            }
        }
    }

    public IReadOnlyList<string> OverrideLanguages
    {
        get
        {
            lock (_sync)
            {
                return _overrides.Keys.OrderBy(language => language, StringComparer.Ordinal).ToList();
            }
        }
    }

    public int Count => Languages.Sum(CountFor);

    public int CountFor(string language)
    {
        return Keys(language).Count;
    }

    public IReadOnlyCollection<string> Keys(string language)
    {
        var code = CatLanguage.Normalize(language);
        lock (_sync)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            if (_languages.TryGetValue(code, out var entries))
            {
                keys.UnionWith(entries.Keys);
            }

            if (_overrides.TryGetValue(code, out var overrides))
            {
                keys.UnionWith(overrides.Keys);
            }

            return keys;
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
            Put(_languages, CatLanguage.Normalize(language), key, text);
            Revision++;
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
        var entries = Parse(language, json);
        Add(language, entries);
        return entries.Count;
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

    public TranslationLoad LoadOverrides(string directory)
    {
        var loaded = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        var errors = new List<string>();
        var count = 0;
        if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
        {
            foreach (var file in Directory.GetFiles(directory, "*.json").OrderBy(path => path, StringComparer.Ordinal))
            {
                var language = CatLanguage.Normalize(Path.GetFileNameWithoutExtension(file));
                try
                {
                    foreach (var pair in Parse(language, File.ReadAllText(file)))
                    {
                        Put(loaded, language, pair.Key, pair.Value);
                        count++;
                    }
                }
                catch (Exception exception) when (exception is FormatException or IOException or UnauthorizedAccessException)
                {
                    errors.Add($"{Path.GetFileName(file)}: {exception.Message}");
                }
            }
        }

        lock (_sync)
        {
            OverridesDirectory = directory;
            _overrides.Clear();
            foreach (var pair in loaded)
            {
                _overrides[pair.Key] = pair.Value;
            }

            _reportedFormats.Clear();
            Revision++;
            CatLocalization.Touch();
        }

        return new TranslationLoad(count, loaded.Keys.OrderBy(language => language, StringComparer.Ordinal).ToList(), errors);
    }

    public TranslationLoad ReloadOverrides() => LoadOverrides(OverridesDirectory);

    public bool Has(string key, string language)
    {
        var code = CatLanguage.Normalize(language);
        lock (_sync)
        {
            return Contains(_overrides, code, key) || Contains(_languages, code, key);
        }
    }

    public string Find(string key, string language)
    {
        if (key == null)
        {
            return null;
        }

        lock (_sync)
        {
            foreach (var code in CatLanguage.Chain(language))
            {
                var text = Lookup(code, key);
                if (text != null)
                {
                    return text;
                }
            }
        }

        return null;
    }

    public string FindExact(string key, string language)
    {
        if (key == null)
        {
            return null;
        }

        lock (_sync)
        {
            return Lookup(CatLanguage.Normalize(language), key);
        }
    }

    public string Get(string key, string language) => Find(key, language) ?? key;

    public string Get(string key) => Get(key, CatLanguage.Current);

    public string FormatFor(string language, string key, params object[] arguments) =>
        SafeFormat(language, key, Get(key, language), arguments);

    public string Format(string key, params object[] arguments) => FormatFor(CatLanguage.Current, key, arguments);

    public string FindPlural(string key, long count, string language)
    {
        if (key == null)
        {
            return null;
        }

        lock (_sync)
        {
            foreach (var code in CatLanguage.Chain(language))
            {
                var text = Lookup(code, key + "." + PluralRules.Category(code, count)) ?? Lookup(code, key + "." + PluralRules.Other) ?? Lookup(code, key);
                if (text != null)
                {
                    return text;
                }
            }
        }

        return null;
    }

    public string PluralFor(string language, string key, long count, params object[] arguments)
    {
        var all = new object[(arguments?.Length ?? 0) + 1];
        all[0] = count;
        if (arguments != null)
        {
            Array.Copy(arguments, 0, all, 1, arguments.Length);
        }

        return SafeFormat(language, key, FindPlural(key, count, language) ?? key, all);
    }

    public string Plural(string key, long count, params object[] arguments) => PluralFor(CatLanguage.Current, key, count, arguments);

    public LocalText Text(string key) => new(this, key);

    private string SafeFormat(string language, string key, string template, object[] arguments)
    {
        try
        {
            return string.Format(CultureInfo.InvariantCulture, template, arguments ?? Array.Empty<object>());
        }
        catch (FormatException exception)
        {
            string fallback;
            lock (_sync)
            {
                fallback = Lookup(CatLanguage.Fallback, key);
                if (_reportedFormats.Add(CatLanguage.Normalize(language) + "|" + key))
                {
                    CatLocalization.Log?.Warning($"The {CatLanguage.Normalize(language)} text \"{key}\" of {OwnerId} cannot be filled in ({exception.Message}), English is shown instead");
                }
            }

            if (fallback != null && !ReferenceEquals(fallback, template))
            {
                try
                {
                    return string.Format(CultureInfo.InvariantCulture, fallback, arguments ?? Array.Empty<object>());
                }
                catch (FormatException)
                {
                }
            }

            return template;
        }
    }

    private string Lookup(string code, string key)
    {
        if (_overrides.TryGetValue(code, out var overrides) && overrides.TryGetValue(key, out var text))
        {
            return text;
        }

        return _languages.TryGetValue(code, out var entries) && entries.TryGetValue(key, out text) ? text : null;
    }

    private static bool Contains(Dictionary<string, Dictionary<string, string>> layer, string code, string key) =>
        layer.TryGetValue(code, out var entries) && entries.ContainsKey(key);

    private static void Put(Dictionary<string, Dictionary<string, string>> layer, string code, string key, string text)
    {
        if (!layer.TryGetValue(code, out var entries))
        {
            entries = new Dictionary<string, string>(StringComparer.Ordinal);
            layer[code] = entries;
        }

        entries[key] = text;
    }

    private static List<KeyValuePair<string, string>> Parse(string language, string json)
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
            return entries;
        }
    }

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
