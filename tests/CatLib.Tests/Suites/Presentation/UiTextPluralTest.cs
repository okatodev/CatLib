using System.Collections.Generic;
using CatLib.Tests.Framework;
using CatLib.UI;

namespace CatLib.Tests.Suites.Presentation;

public sealed class UiTextPluralTest : TestCase
{
    public override string Suite => "Presentation";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var russian = new (int Count, string Expected)[]
        {
            (0, "0 настроек"), (1, "1 настройка"), (2, "2 настройки"), (4, "4 настройки"), (5, "5 настроек"),
            (11, "11 настроек"), (12, "12 настроек"), (14, "14 настроек"), (21, "21 настройка"), (22, "22 настройки"),
            (25, "25 настроек"), (101, "101 настройка"), (111, "111 настроек"), (112, "112 настроек")
        };

        foreach (var (count, expected) in russian)
        {
            Assert.Equal(expected, UiText.Plural(UiText.SettingsCount, count, "ru"), "Russian plural for " + count);
        }

        Assert.Equal("1 setting", UiText.Plural(UiText.SettingsCount, 1, "en"), "English singular");
        Assert.Equal("2 settings", UiText.Plural(UiText.SettingsCount, 2, "en"), "English plural");
        Assert.Equal("0 settings", UiText.Plural(UiText.SettingsCount, 0, "en"), "English zero");
        Assert.Equal("1 ждёт перезапуска", UiText.Plural(UiText.PendingRestart, 1, "ru"), "Russian pending singular");
        Assert.Equal("3 ждут перезапуска", UiText.Plural(UiText.PendingRestart, 3, "ru"), "Russian pending plural");
        Assert.Equal("Default: 5", UiText.Format(UiText.DefaultValue, "en", 5), "English format");
        Assert.Equal("По умолчанию: 5", UiText.Format(UiText.DefaultValue, "ru-RU", 5), "Russian format with a region");
        yield break;
    }
}
