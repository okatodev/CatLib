using System;
using System.Collections.Generic;
using System.Linq;
using BoatTweaks;
using CatLib.Localization;
using CatLib.Tests.Framework;
using CatLib.Tests.Suites.Settings;

namespace CatLib.Tests.Suites.BoatTweaks;

public sealed class BoatTranslationsTest : TestCase
{
    public static readonly string[] Languages = { "en", "ru" };

    public override string Suite => "BoatTweaks";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        using var sandbox = new ConfigSandbox("BoatTexts");
        var boat = new BoatSettings(sandbox.Settings, () => { });
        var catalog = sandbox.Settings.Texts;
        var loaded = catalog.LoadEmbedded(typeof(BoatTweaksPlugin).Assembly, BoatTweaksPlugin.LanguageResourcePrefix);
        context.Note($"Translations loaded: {loaded}, languages: {string.Join(", ", catalog.Languages)}");

        var missing = new List<string>();
        foreach (var language in Languages)
        {
            void Require(string key)
            {
                if (!catalog.Has(key, language) || string.IsNullOrWhiteSpace(catalog.Find(key, language)))
                {
                    missing.Add(language + ": " + key);
                }
            }

            if (catalog.CountFor(language) == 0)
            {
                missing.Add(language + ": no translations at all");
                continue;
            }

            Require(SettingTexts.ModNameKey);
            foreach (var setting in sandbox.Settings.Settings)
            {
                Require(SettingTexts.LabelKey(setting));
                Require(SettingTexts.DescriptionKey(setting));
            }

            foreach (var section in sandbox.Settings.Settings.Select(setting => setting.Section).Distinct())
            {
                Require(SettingTexts.SectionKey(section));
            }

            foreach (LayoutMode mode in Enum.GetValues(typeof(LayoutMode)))
            {
                Require(SettingTexts.EnumKey(mode));
            }
        }

        Assert.Equal(6, sandbox.Settings.Settings.Count, "Declared settings");
        Assert.True(sandbox.Settings.Settings.All(setting => setting.Scope == CatLib.Config.SettingScope.Session), "Every boat setting is a session setting so the host decides");
        Assert.Equal(0, missing.Count, "Missing translations: " + string.Join("; ", missing));
        Assert.Equal("Узор палубы", SettingTexts.Section(sandbox.Settings, "Layout", "ru"), "Russian section name");
        Assert.Equal("Пусто", SettingTexts.EnumValue(boat.Mode, LayoutMode.Empty, "ru"), "Russian mode name");
        yield break;
    }
}
