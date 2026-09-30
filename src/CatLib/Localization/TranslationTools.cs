using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using CatLib.DevTools;
using CatLib.Game.Events;
using CatLib.Logging;

namespace CatLib.Localization;

public static class TranslationTools
{
    public const int ListedKeys = 5;

    private static CatLogger _log;
    private static bool _summarized;

    internal static void Initialize(CatLogger log)
    {
        _log = log;
        CatLocalization.Log = log;
        BootstrapEvents.MainMenuLoaded += Summarize;
        CatLib.Core.FrameLoop.Update += () => CatLanguage.Poll(_log);
        DevMenu.Command("Translations", "Translation report", () => "written to " + WriteReport(),
            "Writes which texts every mod misses in every game language, and texts that would break.");
        DevMenu.Command("Translations", "Export for translators", () =>
            {
                var language = CatLanguage.Current;
                var count = Export(language);
                return $"{count} file(s) for {language} in {ExportDirectory}";
            },
            "Writes every text of every mod for the current game language, English where a translation is missing. Put the files into BepInEx/config/CatLib/Translations.");
        DevMenu.Command("Translations", "Reload translations", () => $"{CatLocalization.ReloadAll()} text(s) from {CatLocalization.TranslationsDirectory}",
            "Reads the translation files in BepInEx/config/CatLib/Translations again, the Mods tab shows them at once.");
    }

    public static string ExportDirectory
    {
        get
        {
            try
            {
                return Path.Combine(BepInEx.Paths.BepInExRootPath, "CatLib", "Translations", "Export");
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    public static IReadOnlyList<string> SummaryLines(TextCatalog catalog, IReadOnlyList<string> gameLanguages)
    {
        var lines = new List<string>();
        if (catalog.CountFor(CatLanguage.Fallback) == 0)
        {
            return lines;
        }

        var name = CatLocalization.NameOf(catalog);
        var languages = catalog.Languages;
        lines.Add($"{name} translations: {string.Join(", ", languages.Select(language => $"{language} {catalog.CountFor(language)}"))}");
        foreach (var language in languages.Where(language => language != CatLanguage.Fallback))
        {
            var report = TranslationCheck.Compare(catalog, language);
            if (report.Missing.Count > 0)
            {
                lines.Add($"!{name}: {language} misses {report.Missing.Count} of {report.Total} text(s), English is shown for them: {List(report.Missing)}");
            }

            foreach (var broken in report.Broken)
            {
                lines.Add($"!{name}: {language} text {broken}, English is shown instead");
            }

            if (report.Unknown.Count > 0)
            {
                lines.Add($"{name}: {language} has text(s) English does not have, they are not used: {List(report.Unknown)}");
            }
        }

        var untranslated = (gameLanguages ?? Array.Empty<string>())
            .Where(language => language != CatLanguage.Fallback && !languages.Any(known => known != CatLanguage.Fallback && CatLanguage.Chain(language).Contains(known)))
            .ToList();
        if (untranslated.Count > 0)
        {
            lines.Add($"{name} has no texts in {string.Join(", ", untranslated)}, English is shown there");
        }

        return lines;
    }

    public static string WriteReport()
    {
        var builder = new StringBuilder();
        builder.AppendLine("CatLib translation report");
        builder.AppendLine("Created: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        builder.AppendLine("Game language: " + CatLanguage.Current);
        var gameLanguages = CatLanguage.GameLanguages;
        builder.AppendLine("Game languages: " + (gameLanguages.Count == 0 ? "unknown" : string.Join(", ", gameLanguages)));
        builder.AppendLine("Translation files: " + CatLocalization.TranslationsDirectory);
        foreach (var catalog in CatLocalization.All.Where(catalog => catalog.CountFor(CatLanguage.Fallback) > 0))
        {
            builder.AppendLine();
            builder.AppendLine($"== {CatLocalization.NameOf(catalog)} ({catalog.OwnerId}), {catalog.CountFor(CatLanguage.Fallback)} English text(s)");
            foreach (var language in catalog.Languages.Union(gameLanguages).Distinct().Where(language => language != CatLanguage.Fallback).OrderBy(language => language, StringComparer.Ordinal))
            {
                var report = TranslationCheck.Compare(catalog, language);
                builder.AppendLine($"{language}: {report.Translated} of {report.Total}{(report.IsComplete ? ", complete" : string.Empty)}");
                Section(builder, "missing", report.Missing);
                Section(builder, "breaks, English is shown", report.Broken);
                Section(builder, "loses a value", report.Lost);
                Section(builder, "not in English, unused", report.Unknown);
            }
        }

        var directory = Path.Combine(BepInEx.Paths.BepInExRootPath, "CatLib", "Translations");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "report_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".txt");
        File.WriteAllText(path, builder.ToString());
        _log?.Message($"Translation report written to {path}");
        return path;
    }

    public static int Export(string language)
    {
        var code = CatLanguage.Normalize(language);
        var count = 0;
        foreach (var catalog in CatLocalization.All.Where(catalog => catalog.CountFor(CatLanguage.Fallback) > 0))
        {
            var directory = Path.Combine(ExportDirectory, catalog.OwnerId);
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, code + ".json"), ExportJson(catalog, code));
            count++;
        }

        _log?.Message($"Exported {count} translation file(s) for {code} to {ExportDirectory}");
        return count;
    }

    public static string ExportJson(TextCatalog catalog, string language)
    {
        var code = CatLanguage.Normalize(language);
        var report = TranslationCheck.Compare(catalog, code);
        var missing = new HashSet<string>(report.Missing, StringComparer.Ordinal);
        var keys = catalog.Keys(CatLanguage.Fallback)
            .Where(key => !IsForeignPluralForm(key, code, catalog))
            .Union(report.Missing)
            .Union(catalog.Keys(code).Where(key => !report.Unknown.Contains(key)))
            .Distinct()
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToList();

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            writer.WriteStartObject();
            foreach (var key in keys)
            {
                writer.WriteString(key, catalog.FindExact(key, code) ?? EnglishFor(catalog, key) ?? string.Empty);
            }

            writer.WriteEndObject();
        }

        var header = new StringBuilder();
        header.AppendLine($"// {CatLocalization.NameOf(catalog)} in {code}. Keep the keys and every {{0}}, {{1}} as they are.");
        if (missing.Count > 0)
        {
            header.AppendLine($"// Still in English: {string.Join(", ", missing.OrderBy(key => key, StringComparer.Ordinal))}");
        }

        return header + Encoding.UTF8.GetString(stream.ToArray()) + Environment.NewLine;
    }

    private static string EnglishFor(TextCatalog catalog, string key)
    {
        var english = catalog.FindExact(key, CatLanguage.Fallback);
        if (english != null)
        {
            return english;
        }

        var dot = key.LastIndexOf('.');
        return dot > 0 ? catalog.FindExact(key.Substring(0, dot) + "." + PluralRules.Other, CatLanguage.Fallback) : null;
    }

    private static bool IsForeignPluralForm(string key, string language, TextCatalog catalog)
    {
        var dot = key.LastIndexOf('.');
        if (dot <= 0)
        {
            return false;
        }

        var category = key.Substring(dot + 1);
        return PluralRules.IsCategory(category)
               && catalog.FindExact(key.Substring(0, dot) + "." + PluralRules.Other, CatLanguage.Fallback) != null
               && !PluralRules.Categories(language).Contains(category);
    }

    private static void Summarize()
    {
        if (_summarized)
        {
            return;
        }

        _summarized = true;
        try
        {
            var gameLanguages = CatLanguage.GameLanguages;
            foreach (var catalog in CatLocalization.All)
            {
                foreach (var line in SummaryLines(catalog, gameLanguages))
                {
                    if (line.StartsWith("!", StringComparison.Ordinal))
                    {
                        _log.Warning(line.Substring(1));
                    }
                    else
                    {
                        _log.Info(line);
                    }
                }
            }
        }
        catch (Exception exception)
        {
            _log.Error("Checking the translations failed", exception);
        }
    }

    private static void Section(StringBuilder builder, string title, IReadOnlyList<string> entries)
    {
        if (entries.Count == 0)
        {
            return;
        }

        builder.AppendLine($"  {title}:");
        foreach (var entry in entries)
        {
            builder.AppendLine("    " + entry);
        }
    }

    private static string List(IReadOnlyList<string> keys) =>
        string.Join(", ", keys.Take(ListedKeys)) + (keys.Count > ListedKeys ? $" and {keys.Count - ListedKeys} more" : string.Empty);
}
