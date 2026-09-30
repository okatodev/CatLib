using System;
using System.Collections.Generic;
using BetterRepair;
using BetterRepair.Logic;
using BetterRepair.Settings;
using CatLib.Config;
using CatLib.Localization;
using CatLib.Tests.Framework;
using CatLib.Tests.Suites.Settings;

namespace CatLib.Tests.Suites.BetterRepair;

public sealed class BetterRepairTranslationsTest : TestCase
{
    public override string Suite => "BetterRepair";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        using var sandbox = new ConfigSandbox("RepairTexts");
        var changes = 0;
        var repair = new RepairSettings(sandbox.Settings, () => changes++);
        var catalog = sandbox.Settings.Texts;
        catalog.LoadEmbedded(typeof(BetterRepairPlugin).Assembly, BetterRepairPlugin.LanguageResourcePrefix);
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

            foreach (RefillMode mode in Enum.GetValues(typeof(RefillMode)))
            {
                Require(SettingTexts.EnumKey(mode));
            }

            foreach (var message in new[] { "message.left", "message.empty", "message.refilled" })
            {
                Require(message);
            }
        }

        Assert.Equal(0, missing.Count, "Missing translations: " + string.Join("; ", missing));
        Assert.Equal(5, sandbox.Settings.Settings.Count, "Five settings");
        Assert.Equal(SettingScope.Session, repair.Unlimited.Scope, "The host decides about unlimited cardboard");
        Assert.Equal(SettingScope.Session, repair.Stock.Scope, "The host decides the stock size");
        Assert.Equal(SettingScope.Session, repair.Refill.Scope, "The host decides the refill");
        Assert.Equal(SettingScope.Session, repair.RefillAmount.Scope, "The host decides the sheets per day");
        Assert.Equal(SettingScope.Local, repair.Messages.Scope, "Messages are personal");
        Assert.False(repair.Unlimited.Value, "Cardboard runs out by default, like in the game");
        Assert.Equal(3, repair.Stock.Value, "Three sheets by default, like in the game");
        Assert.Equal(RefillMode.Full, repair.Refill.Value, "A new day brings the full stock by default, like in the game");
        repair.Stock.LocalValue = 8;
        Assert.AtLeast(1, changes, "Changing a setting tells the mod");
        Assert.Equal("Удобный ремонт", SettingTexts.ModName(sandbox.Settings, "ru"), "Russian mod name");
        yield break;
    }
}
