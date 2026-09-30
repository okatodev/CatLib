using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace CatLib.Localization;

public sealed class TranslationReport
{
    internal TranslationReport(string ownerId, string language, string reference, int total, int translated,
        IReadOnlyList<string> missing, IReadOnlyList<string> unknown, IReadOnlyList<string> broken, IReadOnlyList<string> lost)
    {
        OwnerId = ownerId;
        Language = language;
        Reference = reference;
        Total = total;
        Translated = translated;
        Missing = missing;
        Unknown = unknown;
        Broken = broken;
        Lost = lost;
    }

    public string OwnerId { get; }

    public string Language { get; }

    public string Reference { get; }

    public int Total { get; }

    public int Translated { get; }

    public IReadOnlyList<string> Missing { get; }

    public IReadOnlyList<string> Unknown { get; }

    public IReadOnlyList<string> Broken { get; }

    public IReadOnlyList<string> Lost { get; }

    public bool IsComplete => Missing.Count == 0 && Broken.Count == 0;

    public bool IsEmpty => Translated == 0;
}

public static class TranslationCheck
{
    private static readonly Regex Placeholder = new(@"(?<!\{)\{(\d+)(?:[,:][^}]*)?\}", RegexOptions.CultureInvariant);

    public static TranslationReport Compare(TextCatalog catalog, string language, string reference = CatLanguage.Fallback)
    {
        if (catalog == null)
        {
            throw new ArgumentNullException(nameof(catalog));
        }

        var code = CatLanguage.Normalize(language);
        var referenceCode = CatLanguage.Normalize(reference);
        var referenceKeys = new HashSet<string>(catalog.Keys(referenceCode), StringComparer.Ordinal);
        var keys = new HashSet<string>(catalog.Keys(code), StringComparer.Ordinal);
        var pluralGroups = new HashSet<string>(StringComparer.Ordinal);
        foreach (var key in referenceKeys)
        {
            var group = PluralGroup(key);
            if (group != null && referenceKeys.Contains(group + "." + PluralRules.Other))
            {
                pluralGroups.Add(group);
            }
        }

        var required = new List<string>();
        foreach (var key in referenceKeys)
        {
            var group = PluralGroup(key);
            if (group == null || !pluralGroups.Contains(group))
            {
                required.Add(key);
            }
        }

        foreach (var group in pluralGroups)
        {
            required.AddRange(PluralRules.Categories(code).Select(category => group + "." + category));
        }

        required.Sort(StringComparer.Ordinal);
        var missing = required.Where(key => !keys.Contains(key)).ToList();
        var unknown = keys
            .Where(key => !referenceKeys.Contains(key) && !IsPluralForm(key, pluralGroups))
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToList();

        var broken = new List<string>();
        var lost = new List<string>();
        foreach (var key in keys.OrderBy(key => key, StringComparer.Ordinal))
        {
            var text = catalog.FindExact(key, code);
            var group = PluralGroup(key);
            var isPlural = group != null && pluralGroups.Contains(group);
            var referenceText = catalog.FindExact(isPlural ? group + "." + PluralRules.Other : key, referenceCode);
            if (text == null || referenceText == null)
            {
                continue;
            }

            var expected = Indexes(referenceText);
            var used = Indexes(text);
            var extra = used.Where(index => !expected.Contains(index)).ToList();
            if (extra.Count > 0)
            {
                broken.Add($"{key}: uses {{{string.Join("}, {", extra)}}} that {referenceCode} does not have");
            }
            else if (!CanFormat(text, expected.Count == 0 ? 0 : expected.Max() + 1))
            {
                broken.Add($"{key}: has a stray {{ or }}, write {{{{ and }}}} for a brace");
            }
            else if (!isPlural)
            {
                var dropped = expected.Where(index => !used.Contains(index)).ToList();
                if (dropped.Count > 0)
                {
                    lost.Add($"{key}: does not show {{{string.Join("}, {", dropped)}}}");
                }
            }
        }

        var total = required.Count;
        var translated = total - missing.Count;
        return new TranslationReport(catalog.OwnerId, code, referenceCode, total, translated, missing, unknown, broken, lost);
    }

    public static IReadOnlyList<int> PlaceholdersOf(string text) => Indexes(text).OrderBy(index => index).ToList();

    private static HashSet<int> Indexes(string text)
    {
        var result = new HashSet<int>();
        if (string.IsNullOrEmpty(text))
        {
            return result;
        }

        var escaped = text.Replace("{{", string.Empty).Replace("}}", string.Empty);
        foreach (Match match in Placeholder.Matches(escaped))
        {
            if (int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var index))
            {
                result.Add(index);
            }
        }

        return result;
    }

    private static bool CanFormat(string text, int arguments)
    {
        try
        {
            string.Format(CultureInfo.InvariantCulture, text, Enumerable.Repeat((object)1, Math.Max(arguments, 1)).ToArray());
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string PluralGroup(string key)
    {
        var dot = key.LastIndexOf('.');
        return dot > 0 && PluralRules.IsCategory(key.Substring(dot + 1)) ? key.Substring(0, dot) : null;
    }

    private static bool IsPluralForm(string key, HashSet<string> groups)
    {
        var group = PluralGroup(key);
        return group != null && groups.Contains(group);
    }
}
