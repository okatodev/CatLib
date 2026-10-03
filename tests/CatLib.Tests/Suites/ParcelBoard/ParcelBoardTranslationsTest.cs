using System;
using System.Collections.Generic;
using CatLib.Config;
using CatLib.Localization;
using CatLib.Net;
using CatLib.Tests.Framework;
using CatLib.Tests.Suites.Settings;
using ParcelBoard;
using ParcelBoard.Logic;
using ParcelBoard.Settings;

namespace CatLib.Tests.Suites.ParcelBoard;

public sealed class ParcelBoardTranslationsTest : TestCase
{
    public override string Suite => "ParcelBoard";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        using var sandbox = new ConfigSandbox("BoardTexts");
        var changes = 0;
        var board = new BoardSettings(sandbox.Settings, () => changes++);
        var catalog = sandbox.Settings.Texts;
        catalog.LoadEmbedded(typeof(ParcelBoardPlugin).Assembly, ParcelBoardPlugin.LanguageResourcePrefix);
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

            foreach (var type in new[] { typeof(ListVisibility), typeof(RegionOrder), typeof(BoardCorner), typeof(CountScope) })
            {
                foreach (Enum value in Enum.GetValues(type))
                {
                    Require(SettingTexts.EnumKey(value));
                }
            }

            foreach (BoardList list in Enum.GetValues(typeof(BoardList)))
            {
                Require("list." + list);
            }

            foreach (var size in BoardCounter.SizeOrder)
            {
                Require("size." + size);
            }
        }

        Assert.Equal(0, missing.Count, "Missing translations: " + string.Join("; ", missing));
        Assert.Equal(11 + BoardCounter.DefaultOrder.Count, sandbox.Settings.Settings.Count, "Board settings and one per list");
        foreach (var setting in sandbox.Settings.Settings)
        {
            Assert.Equal(SettingScope.Local, setting.Scope, $"{setting.Section}.{setting.Key} is personal, the mod is only for this player");
        }

        Assert.Equal(ListVisibility.Always, board.Visibility(BoardList.All), "The list of all parcels is always there");
        Assert.Equal(ListVisibility.WhenNotEmpty, board.Visibility(BoardList.Fragile), "Mark lists show when they have parcels");
        Assert.False(board.StartOpen.Value, "Lists start closed");
        Assert.True(board.HideEmptyRows.Value, "Empty rows are hidden by default");
        Assert.True(board.Opacity.Value <= 0.2f, "The background is faint by default");
        board.Opacity.LocalValue = 0.3f;
        Assert.AtLeast(1, changes, "Changing a setting tells the mod");
        Assert.Equal("Табло посылок", SettingTexts.ModName(sandbox.Settings, "ru"), "Russian mod name");
        Assert.Equal("Ночное", catalog.Find("list.Dark", "ru"), "Dark is the night list in Russian");
        Assert.Equal("Сломанное", catalog.Find("list.Corrupted", "ru"), "Corrupted parcels are broken ones");

        context.Note("Network policy of the mod: " + SessionPolicy.ClientOnly);
        yield break;
    }
}
