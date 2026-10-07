using System.Collections.Generic;
using CatLib.Config;
using CatLib.Localization;
using CatLib.Tests.Framework;
using CatLib.Tests.Suites.Settings;
using TooLate;
using TooLate.Logic;
using TooLate.Settings;

namespace CatLib.Tests.Suites.TooLate;

public sealed class TooLateTranslationsTest : TestCase
{
    public override string Suite => "TooLate";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        using var sandbox = new ConfigSandbox("LateTexts");
        var join = new JoinSettings(sandbox.Settings, null);
        var catalog = sandbox.Settings.Texts;
        catalog.LoadEmbedded(typeof(TooLatePlugin).Assembly, TooLatePlugin.LanguageResourcePrefix);
        var missing = new List<string>();
        foreach (var language in new[] { "en", "ru", "de", "fr", "es", "it", "pt-br", "pl", "uk", "ja", "ko", "zh", "zh-tw" })
        {
            void Require(string key)
            {
                if (!catalog.Has(key, language) || string.IsNullOrWhiteSpace(catalog.Find(key, language)))
                {
                    missing.Add(language + ": " + key);
                }
            }

            Require(SettingTexts.ModNameKey);
            Require(SettingTexts.ModDescriptionKey);
            foreach (var setting in sandbox.Settings.Settings)
            {
                Require(SettingTexts.LabelKey(setting));
                Require(SettingTexts.DescriptionKey(setting));
                Require(SettingTexts.SectionKey(setting.Section));
            }

            foreach (var key in new[] { "message.joining", "message.waiting", "message.joined", "message.failed" })
            {
                Require(key);
            }

            var report = TranslationCheck.Compare(catalog, language);
            if (language != "en" && !report.IsComplete)
            {
                missing.Add(language + ": " + string.Join(", ", report.Missing) + " " + string.Join(", ", report.Broken));
            }
        }

        Assert.Equal(0, missing.Count, "Missing translations: " + string.Join("; ", missing));
        Assert.Equal(3, sandbox.Settings.Settings.Count, "Three settings");
        foreach (var setting in sandbox.Settings.Settings)
        {
            Assert.Equal(SettingScope.Local, setting.Scope, $"{setting.Section}.{setting.Key} is the host's own setting");
        }

        Assert.True(join.Enabled.Value, "Joining a running game is on by default");
        Assert.Equal(PlayerLimit.GamePlayers, join.MaxPlayers.Value, "Four players like in the game by default");
        Assert.Equal("Опоздавшие", SettingTexts.ModName(sandbox.Settings, "ru"), "Russian name");
        Assert.Equal("+3 игрока", catalog.PluralFor("ru", "lobby.more", 3, 3), "Russian count of more players");
        Assert.Equal("+5 игроков", catalog.PluralFor("ru", "lobby.more", 5, 5), "Russian count for five");
        Assert.Equal("Friend is joining the game", catalog.FormatFor("en", "message.joining", "Friend"), "English joining message");
        yield break;
    }
}
