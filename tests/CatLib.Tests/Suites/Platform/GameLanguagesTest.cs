using System.Collections.Generic;
using System.Linq;
using CatLib.Localization;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.Platform;

public sealed class GameLanguagesTest : TestCase
{
    public override string Suite => "Platform";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var languages = CatLanguage.GameLanguages;
        context.Note($"Game languages ({languages.Count}): {string.Join(", ", languages)}");
        context.Note($"Current: {CatLanguage.Current}");
        Assert.True(languages.Count > 1, "The game lists its languages");
        Assert.True(languages.Any(language => language == CatLanguage.Fallback || language.StartsWith(CatLanguage.Fallback + "-")), "English is one of them");

        var revert = CatLanguage.Game("Settings/Revert");
        context.Note($"Game text Settings/Revert: {revert ?? "none"}, in English: {CatLanguage.Game("Settings/Revert", "en") ?? "none"}");
        Assert.NotNull(revert, "A game text is found by its term");
        Assert.Null(CatLanguage.Game("CatLib/NoSuchTerm"), "An unknown term gives null");

        foreach (var catalog in CatLocalization.All.Where(catalog => catalog.OwnerId.StartsWith("catlib.") && !catalog.OwnerId.StartsWith("catlib.tests")))
        {
            context.Note($"{catalog.OwnerId}: {string.Join(", ", catalog.Languages.Select(language => $"{language} {catalog.CountFor(language)}"))}");
        }

        var repair = CatLocalization.Find("catlib.betterrepair");
        Assert.True(repair != null && repair.CountFor("en") > 0 && repair.CountFor("ru") > 0, "Better Repair texts are loaded without code, from its Lang folder");
        yield break;
    }
}
