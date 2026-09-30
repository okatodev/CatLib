using System.Collections.Generic;
using System.Linq;
using CatLib.Localization;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.Localization;

public sealed class AllLanguagesTest : TestCase
{
    public static readonly string[] GameLanguages = { "en", "fr", "it", "de", "es", "pt-br", "pl", "zh", "zh-tw", "ja", "ko", "uk", "ru" };

    public override string Suite => "Localization";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var catalogs = new List<TextCatalog> { CatLib.UI.UiText.Catalog };
        foreach (var (owner, assembly, prefix) in new[]
                 {
                     ("test.all.betterrepair", typeof(global::BetterRepair.BetterRepairPlugin).Assembly, global::BetterRepair.BetterRepairPlugin.LanguageResourcePrefix),
                     ("test.all.boattweaks", typeof(global::BoatTweaks.BoatTweaksPlugin).Assembly, global::BoatTweaks.BoatTweaksPlugin.LanguageResourcePrefix),
                     ("test.all.shelflabels", typeof(global::ShelfLabels.ShelfLabelsPlugin).Assembly, global::ShelfLabels.ShelfLabelsPlugin.LanguageResourcePrefix)
                 })
        {
            var catalog = new TextCatalog(owner);
            catalog.LoadEmbedded(assembly, prefix);
            catalogs.Add(catalog);
        }

        var problems = new List<string>();
        foreach (var catalog in catalogs)
        {
            var name = catalog.FindExact("mod.name", "en");
            foreach (var language in GameLanguages)
            {
                var report = TranslationCheck.Compare(catalog, language);
                problems.AddRange(report.Missing.Select(key => $"{name} {language}: missing {key}"));
                problems.AddRange(report.Unknown.Select(key => $"{name} {language}: unknown {key}"));
                problems.AddRange(report.Broken.Select(entry => $"{name} {language}: {entry}"));
                problems.AddRange(report.Lost.Select(entry => $"{name} {language}: {entry}"));
            }

            context.Note($"{name}: {string.Join(", ", catalog.Languages.Select(language => $"{language} {catalog.CountFor(language)}"))}");
        }

        Assert.Equal(0, problems.Count, string.Join("; ", problems.Take(20)));
        yield break;
    }
}
