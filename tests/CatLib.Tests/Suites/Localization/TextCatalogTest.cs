using System;
using System.Collections.Generic;
using System.IO;
using CatLib.Localization;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.Localization;

public sealed class TextCatalogTest : TestCase
{
    public override string Suite => "Localization";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        Assert.SequenceEqual(new[] { "ru-ru", "ru", "en" }, CatLanguage.Chain("ru_RU"), "Region, base language, then English");
        Assert.SequenceEqual(new[] { "en" }, CatLanguage.Chain(""), "An unknown language falls back to English");
        Assert.SequenceEqual(new[] { "zh-cn", "zh", "en" }, CatLanguage.Chain("zh-CN"), "Any region code");

        var catalog = new TextCatalog("test.catalog");
        catalog.LoadJson("en", "{ \"mod\": { \"name\": \"Boat\" }, \"greeting\": \"Hello {0}\", \"only.en\": \"English only\" }");
        catalog.LoadJson("ru", "{ \"mod.name\": \"Корабль\", \"greeting\": \"Привет, {0}\" }");
        catalog.Add("ru-RU", "regional", "Региональный");

        Assert.Equal("Корабль", catalog.Find("mod.name", "ru"), "Nested JSON objects become dotted keys");
        Assert.Equal("Корабль", catalog.Find("mod.name", "ru-RU"), "A regional code falls back to its base language");
        Assert.Equal("Региональный", catalog.Find("regional", "ru-ru"), "Regional entries win for their region");
        Assert.Equal("English only", catalog.Find("only.en", "ru"), "Missing translations fall back to English");
        Assert.False(catalog.Has("only.en", "ru"), "Has checks the language itself, without falling back");
        Assert.True(catalog.Has("only.en", "EN"), "Has normalizes the language code");
        Assert.Null(catalog.Find("missing", "ru"), "Unknown keys return null from Find");
        Assert.Equal("missing", catalog.Get("missing", "ru"), "Unknown keys return the key from Get");
        Assert.Equal("Привет, Cat", catalog.FormatFor("ru", "greeting", "Cat"), "Formatting in a given language");
        var current = catalog.Format("greeting", "Cat");
        Assert.Equal(catalog.FormatFor(CatLanguage.Current, "greeting", "Cat"), current, "Format uses the current game language");
        Assert.True(current.EndsWith("Cat"), "A single string argument is an argument, never a language");
        Assert.SequenceEqual(new[] { "en", "ru", "ru-ru" }, catalog.Languages, "Known languages");

        catalog.Add("ru", "mod.name", "Лодка");
        Assert.Equal("Лодка", catalog.Find("mod.name", "ru"), "Later entries replace earlier ones");

        Assert.Throws<FormatException>(() => catalog.LoadJson("de", "{ not json"), "Invalid JSON is reported");
        Assert.Throws<FormatException>(() => catalog.LoadJson("de", "[\"a\"]"), "The root must be an object");
        Assert.Throws<FormatException>(() => catalog.LoadJson("de", "{ \"count\": 3 }"), "Values must be strings");

        var directory = Path.Combine(Path.GetTempPath(), "CatLibCatalog_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "de.json"), "{ \"mod.name\": \"Boot\" }");
            Assert.Equal(1, catalog.LoadDirectory(directory), "Files in a directory are loaded by language");
            Assert.Equal("Boot", catalog.Find("mod.name", "de"), "Language taken from the file name");
            Assert.Equal(0, catalog.LoadDirectory(Path.Combine(directory, "missing")), "A missing directory loads nothing");
        }
        finally
        {
            Directory.Delete(directory, true);
        }

        Assert.True(ReferenceEquals(CatLocalization.For("test.registry"), CatLocalization.For("test.registry")), "One catalog per owner");
        yield break;
    }
}
