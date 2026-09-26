using System;
using System.Collections.Generic;
using System.Linq;
using CatLib.Localization;
using CatLib.Tests.Framework;
using CatLib.Tests.Suites.Settings;
using ShelfLabels;

namespace CatLib.Tests.Suites.ShelfLabels;

public sealed class ShelfLabelsTranslationsTest : TestCase
{
    public override string Suite => "ShelfLabels";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        using var sandbox = new ConfigSandbox("LabelTexts");
        var labels = new LabelSettings(sandbox.Settings, () => { });
        var catalog = sandbox.Settings.Texts;
        catalog.LoadEmbedded(typeof(ShelfLabelsPlugin).Assembly, ShelfLabelsPlugin.LanguageResourcePrefix);
        var missing = new List<string>();
        foreach (var language in new[] { "en", "ru" })
        {
            void Require(string key)
            {
                if (!catalog.Has(key, language) || string.IsNullOrWhiteSpace(catalog.Find(key, language)))
                {
                    missing.Add(language + ": " + key);
                }
            }

            Require(SettingTexts.ModNameKey);
            foreach (var setting in sandbox.Settings.Settings)
            {
                Require(SettingTexts.LabelKey(setting));
                Require(SettingTexts.DescriptionKey(setting));
                Require(SettingTexts.SectionKey(setting.Section));
            }

            foreach (Placement placement in Enum.GetValues(typeof(Placement)))
            {
                Require(SettingTexts.EnumKey(placement));
            }

            foreach (var message in new[] { "message.placement", "message.lookAtLabel", "message.noHost", "message.standHidden", "message.standShown", "message.standUnsupported" })
            {
                Require(message);
            }
        }

        Assert.Equal(0, missing.Count, "Missing translations: " + string.Join("; ", missing));
        Assert.Equal(CatLib.Config.SettingScope.Session, labels.Slots.Scope, "The host decides how many labels exist");
        Assert.Equal(CatLib.Config.SettingScope.Session, labels.Placement.Scope, "The host decides the default placement");
        Assert.Equal(CatLib.Config.SettingScope.Local, labels.PlacementHotkey.Scope, "The hotkey is personal");
        Assert.Equal(CatLib.Config.SettingScope.Local, labels.Spacing.Scope, "The gap is personal");
        Assert.Equal(CatLib.Config.SettingScope.Session, labels.HideStands.Scope, "The host decides whether stands are hidden by default");
        Assert.False(labels.HideStands.Value, "Stands are kept by default, like in the game");
        Assert.Equal(CatLib.Config.SettingScope.Local, labels.StandHotkey.Scope, "The stand hotkey is personal");
        Assert.Equal(1, labels.Slots.Value, "One extra label by default");
        Assert.Equal("Метки полок", SettingTexts.ModName(sandbox.Settings, "ru"), "Russian mod name");
        yield break;
    }
}
