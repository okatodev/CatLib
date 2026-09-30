using System;
using System.Collections.Generic;
using System.IO;
using CatLib.Localization;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.Localization;

public sealed class CatalogFeaturesTest : TestCase
{
    public override string Suite => "Localization";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var catalog = new TextCatalog("test.features");
        catalog.LoadJson("en", "{ \"parcels\": { \"one\": \"{0} parcel\", \"other\": \"{0} parcels\" }, \"left\": \"{0} of {1} left\", \"title\": \"Title\" }");
        catalog.LoadJson("ru", "{ \"parcels\": { \"one\": \"{0} посылка\", \"few\": \"{0} посылки\", \"many\": \"{0} посылок\" }, \"left\": \"Осталось {0} из {2}\" }");

        Assert.Equal("1 parcel", catalog.PluralFor("en", "parcels", 1), "English one");
        Assert.Equal("5 parcels", catalog.PluralFor("en", "parcels", 5), "English other");
        Assert.Equal("21 посылка", catalog.PluralFor("ru", "parcels", 21), "Russian one");
        Assert.Equal("3 посылки", catalog.PluralFor("ru", "parcels", 3), "Russian few");
        Assert.Equal("11 посылок", catalog.PluralFor("ru", "parcels", 11), "Russian many");
        Assert.Equal("2 parcels", catalog.PluralFor("de", "parcels", 2), "A language without the text falls back to English forms");
        catalog.Add("ja", "parcels.other", "小包{0}個");
        Assert.Equal("小包1個", catalog.PluralFor("ja", "parcels", 1), "A language with one form uses other");

        Assert.Equal("3 of 5 left", catalog.FormatFor("ru", "left", 3, 5), "A translation that uses a missing argument shows English instead of failing");
        catalog.Add("ru", "broken", "{0");
        Assert.Equal("{0", catalog.FormatFor("ru", "broken", 1), "A broken text without English is shown as it is");

        context.Note("Two warning log lines about texts of test.features that cannot be filled in are expected");

        var text = catalog.Text("title");
        Assert.Equal("Title", text.In("ru"), "A text handle resolves in any language");
        catalog.Add("ru", "title", "Заголовок");
        Assert.Equal("Заголовок", text.In("ru"), "A text handle always shows the latest translation");

        var directory = Path.Combine(Path.GetTempPath(), "CatLibOverrides_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "ru.json"), "// players' fix\n{ \"title\": \"Название\" }");
            File.WriteAllText(Path.Combine(directory, "de.json"), "{ \"title\": \"Titel\", }");
            File.WriteAllText(Path.Combine(directory, "fr.json"), "{ broken");
            var load = catalog.LoadOverrides(directory);
            Assert.Equal(2, load.Texts, "Texts read from the translation files");
            Assert.Equal(1, load.Errors.Count, "A broken file is skipped and reported");
            Assert.Equal("Название", catalog.Find("title", "ru"), "A translation file wins over the built-in text");
            Assert.Equal("Titel", catalog.Find("title", "de"), "A translation file adds a new language");
            catalog.LoadJson("ru", "{ \"title\": \"Заголовок 2\" }");
            Assert.Equal("Название", catalog.Find("title", "ru"), "Built-in texts loaded later do not hide a translation file");
            File.Delete(Path.Combine(directory, "ru.json"));
            catalog.ReloadOverrides();
            Assert.Equal("Заголовок 2", catalog.Find("title", "ru"), "Reloading drops removed files");
            Assert.Null(catalog.FindExact("title", "ru-ru"), "FindExact does not fall back");
        }
        finally
        {
            Directory.Delete(directory, true);
        }

        yield break;
    }
}
