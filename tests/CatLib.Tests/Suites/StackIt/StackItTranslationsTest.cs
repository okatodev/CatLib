using System.Collections.Generic;
using CatLib.Config;
using CatLib.Localization;
using CatLib.Net;
using CatLib.Tests.Framework;
using CatLib.Tests.Suites.Settings;
using StackIt;
using StackIt.Settings;

namespace CatLib.Tests.Suites.StackIt;

public sealed class StackItTranslationsTest : TestCase
{
    public override string Suite => "StackIt";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        using var sandbox = new ConfigSandbox("StackTexts");
        var changes = 0;
        var stack = new StackSettings(sandbox.Settings, () => changes++);
        var catalog = sandbox.Settings.Texts;
        catalog.LoadEmbedded(typeof(StackItPlugin).Assembly, StackItPlugin.LanguageResourcePrefix);
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
            foreach (var setting in sandbox.Settings.Settings)
            {
                Require(SettingTexts.LabelKey(setting));
                Require(SettingTexts.DescriptionKey(setting));
                Require(SettingTexts.SectionKey(setting.Section));
            }
        }

        Assert.Equal(0, missing.Count, "Missing translations: " + string.Join("; ", missing));
        Assert.Equal(2, sandbox.Settings.Settings.Count, "Two settings");
        foreach (var setting in sandbox.Settings.Settings)
        {
            Assert.Equal(SettingScope.Session, setting.Scope, $"{setting.Section}.{setting.Key} comes from the host, every player stacks by the same rules");
        }

        Assert.True(stack.Enabled.Value, "Bridges are on by default");
        Assert.False(stack.KeepBalanced.Value, "By default a parcel falls when a parcel under it is taken");
        stack.KeepBalanced.LocalValue = true;
        Assert.AtLeast(1, changes, "Changing a setting tells the mod");
        Assert.Equal("Stack it!", SettingTexts.ModName(sandbox.Settings, "ru"), "The name stays the same in every language");
        Assert.Equal("Держать равновесие", catalog.Find("setting.Bridges.KeepBalanced", "ru"), "Russian name of the balance setting");
        context.Note("Network policy of the mod: " + SessionPolicy.RequiredOnAll);
        yield break;
    }
}
