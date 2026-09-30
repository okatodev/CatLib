using System.Collections.Generic;
using CatLib.Localization;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.Localization;

public sealed class PluralRulesTest : TestCase
{
    public override string Suite => "Localization";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        void Check(string language, long count, string expected) =>
            Assert.Equal(expected, PluralRules.Category(language, count), $"{language} {count}");

        Check("en", 1, "one");
        Check("en", 0, "other");
        Check("en", 2, "other");
        Check("ru", 1, "one");
        Check("ru", 21, "one");
        Check("ru", 11, "many");
        Check("ru", 3, "few");
        Check("ru", 14, "many");
        Check("ru", 22, "few");
        Check("ru", 0, "many");
        Check("uk", 2, "few");
        Check("pl", 1, "one");
        Check("pl", 21, "many");
        Check("pl", 22, "few");
        Check("cs", 3, "few");
        Check("cs", 5, "other");
        Check("fr", 0, "one");
        Check("fr", 1, "one");
        Check("fr", 2, "other");
        Check("fr", 1000000, "many");
        Check("pt-BR", 0, "one");
        Check("pt-PT", 0, "other");
        Check("es", 1, "one");
        Check("de", 1, "one");
        Check("de", 5, "other");
        Check("ja", 1, "other");
        Check("zh-CN", 1, "other");
        Check("ko", 5, "other");
        Check("ar", 0, "zero");
        Check("ar", 2, "two");
        Check("ar", 5, "few");
        Check("ar", 11, "many");
        Check("ar", 100, "other");
        Check("ru", -2, "few");

        Assert.SequenceEqual(new[] { "one", "few", "many" }, PluralRules.Categories("ru"), "Russian forms for whole numbers");
        Assert.SequenceEqual(new[] { "other" }, PluralRules.Categories("ja"), "Japanese has one form");
        Assert.SequenceEqual(new[] { "one", "other" }, PluralRules.Categories("de"), "German forms");
        Assert.SequenceEqual(new[] { "one", "other" }, PluralRules.Categories("fr"), "French many is only for millions and falls back to other");
        yield break;
    }
}
