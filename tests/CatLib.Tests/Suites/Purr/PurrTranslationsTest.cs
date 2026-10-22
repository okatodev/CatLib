using System;
using System.Collections.Generic;
using CatLib.Config;
using CatLib.Localization;
using CatLib.Tests.Framework;
using CatLib.Tests.Suites.Settings;
using CatLib.Voice;
using Purr;
using Purr.Logic;
using Purr.Settings;
using UnityEngine;

namespace CatLib.Tests.Suites.Purr;

public sealed class PurrTranslationsTest : TestCase
{
    public override string Suite => "Purr";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        using var sandbox = new ConfigSandbox("VoiceTexts");
        var voice = new VoiceSettings(sandbox.Settings);
        var catalog = sandbox.Settings.Texts;
        catalog.LoadEmbedded(typeof(PurrPlugin).Assembly, PurrPlugin.LanguageResourcePrefix);
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

            foreach (var type in new[] { typeof(MicMode), typeof(VoiceSound), typeof(SpeakersCorner) })
            {
                foreach (Enum value in Enum.GetValues(type))
                {
                    Require(SettingTexts.EnumKey(value));
                }
            }

            foreach (MicNotice notice in Enum.GetValues(typeof(MicNotice)))
            {
                if (notice != MicNotice.None)
                {
                    Require("notice." + notice);
                }
            }

            Require("speakers.title");
            Require("speakers.you");
            var report = TranslationCheck.Compare(catalog, language);
            if (language != "en" && !report.IsComplete)
            {
                missing.Add(language + ": " + string.Join(", ", report.Missing) + " " + string.Join(", ", report.Broken));
            }
        }

        Assert.Equal(0, missing.Count, "Missing translations: " + string.Join("; ", missing));
        Assert.Equal(15, sandbox.Settings.Settings.Count, "Fifteen settings");
        foreach (var setting in sandbox.Settings.Settings)
        {
            Assert.Equal(SettingScope.Local, setting.Scope, $"{setting.Section}.{setting.Key} is personal");
            Assert.Equal(setting.EntryBase.Description.Description, catalog.Find(SettingTexts.DescriptionKey(setting), "en"), $"The English text of {setting.Section}.{setting.Key} matches the code");
        }

        Assert.True(voice.Enabled.Value, "Voice chat is on by default");
        Assert.Equal(MicMode.PushToTalk, voice.Mode.Value, "Push to talk by default");
        Assert.Equal(KeyCode.V, voice.PushToTalkKey.Value.MainKey, "V to talk");
        Assert.Equal(VoiceSound.Spatial, voice.Sound.Value, "Voices come from the cats by default");
        Assert.False(voice.Muted.Value || voice.Deafened.Value, "Nothing is turned off at the start");
        Assert.Equal("Purr!", SettingTexts.ModName(sandbox.Settings, "ru"), "The name stays Purr! in Russian");
        Assert.Equal("Наушники выключены: вы никого не слышите", catalog.Find("notice.HeadphonesOff", "ru"), "Russian notice");
        yield break;
    }
}
