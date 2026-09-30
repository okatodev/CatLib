using System.Collections.Generic;
using System.Linq;
using CatLib.Localization;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.Localization;

public sealed class TranslationCheckTest : TestCase
{
    public override string Suite => "Localization";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var catalog = new TextCatalog("test.check");
        catalog.LoadJson("en", "{ \"a\": \"A\", \"b\": \"B {0}\", \"c\": \"C {0} {1}\", \"items\": { \"one\": \"{0} item\", \"other\": \"{0} items\" } }");
        catalog.LoadJson("ru", "{ \"a\": \"А\", \"b\": \"Б {1}\", \"c\": \"В {0}\", \"old\": \"Старое\", \"items\": { \"one\": \"{0} штука\", \"few\": \"{0} штуки\" } }");

        var report = TranslationCheck.Compare(catalog, "ru");
        context.Note("Missing: " + string.Join(", ", report.Missing));
        context.Note("Broken: " + string.Join(" | ", report.Broken));
        Assert.SequenceEqual(new[] { "items.many" }, report.Missing, "Russian needs the one, few and many plural forms");
        Assert.SequenceEqual(new[] { "old" }, report.Unknown, "Keys English does not have");
        Assert.Equal(1, report.Broken.Count, "A text that uses an argument English does not pass");
        Assert.True(report.Broken[0].StartsWith("b:"), "The broken text is named");
        Assert.Equal(1, report.Lost.Count, "A text that drops an argument");
        Assert.False(report.IsComplete, "Not complete");
        Assert.Equal(6, report.Total, "Three plain texts and three plural forms");

        catalog.Add("ja", "a", "エー");
        var japanese = TranslationCheck.Compare(catalog, "ja");
        Assert.True(japanese.Missing.Contains("items.other") && !japanese.Missing.Contains("items.one"), "Japanese needs only the other form");

        catalog.Add("de", "a", "{");
        Assert.Equal(1, TranslationCheck.Compare(catalog, "de").Broken.Count, "A stray brace is found");

        var json = TranslationTools.ExportJson(catalog, "ru");
        context.Note(json.Replace("\n", " "));
        var exported = new TextCatalog("test.export");
        exported.LoadJson("ru", json);
        Assert.Equal("А", exported.Find("a", "ru"), "Export keeps translations");
        Assert.Equal("{0} items", exported.FindExact("items.many", "ru"), "Export fills missing plural forms with English");
        Assert.Null(exported.FindExact("old", "ru"), "Export leaves out texts English does not have");
        Assert.True(json.Contains("Still in English: items.many"), "Export lists what is still in English");
        var summary = TranslationTools.SummaryLines(catalog, new[] { "en", "ru", "de", "ja", "pt-br" });
        context.Note(string.Join(" | ", summary));
        Assert.True(summary[0].StartsWith("test.check translations: de 1, en 5, ja 1, ru 6"), "First line lists the languages");
        Assert.True(summary.Any(line => line.StartsWith("!") && line.Contains("ru misses 1 of 6")), "Missing texts are a warning");
        Assert.True(summary.Any(line => line.EndsWith("has no texts in pt-br, English is shown there")), "Game languages without texts are named");
        yield break;
    }
}
