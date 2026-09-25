using System.Collections.Generic;
using BepInEx.Configuration;
using CatLib.Localization;
using CatLib.Tests.Framework;
using CatLib.Tests.Suites.Presentation;
using CatLib.Tests.Suites.Settings;
using CatLib.UI;

namespace CatLib.Tests.Suites.Localization;

public sealed class SettingTextsTest : TestCase
{
    public override string Suite => "Localization";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        using var sandbox = new ConfigSandbox("SettingTexts");
        var mode = sandbox.Settings.Local("Layout", "Mode", SampleMode.FastMode, "Mode.");
        var scale = sandbox.Settings.Local("Height", "ApprovedHeightScale", 1f, "Scale.", new AcceptableValueRange<float>(0.5f, 4f));
        var plain = sandbox.Settings.Local("Height", "MaximumHeightScale", 1f, "Maximum.").Label("Code label");

        Assert.Equal("Approved Height Scale", SettingTexts.Label(scale, "ru"), "Without translations the key is prettified");
        Assert.Equal("Scale.", SettingTexts.Description(scale, "ru"), "Without translations the declared description is used");
        Assert.Equal("Fast Mode", SettingTexts.EnumValue(mode, SampleMode.FastMode, "ru"), "Enum names are prettified");

        var texts = sandbox.Settings.Texts;
        texts.LoadJson("ru", @"{
            ""mod.name"": ""Корабль"",
            ""section.Height"": ""Высота"",
            ""setting.Height.ApprovedHeightScale"": ""Одобренная высота"",
            ""setting.Height.ApprovedHeightScale.description"": ""Множитель к высоте игры."",
            ""enum.SampleMode.FastMode"": ""Быстро""
        }");

        Assert.Equal("Корабль", SettingTexts.ModName(sandbox.Settings, "ru"), "Translated mod name");
        Assert.Equal(sandbox.Settings.DisplayName, SettingTexts.ModName(sandbox.Settings, "en"), "Declared name without an English translation");
        Assert.Equal("Высота", SettingTexts.Section(sandbox.Settings, "Height", "ru"), "Translated section");
        Assert.Equal("Layout", SettingTexts.Section(sandbox.Settings, "Layout", "ru"), "Untranslated section");
        Assert.Equal("Одобренная высота", SettingTexts.Label(scale, "ru-RU"), "Translated label");
        Assert.Equal("Множитель к высоте игры.", SettingTexts.Description(scale, "ru"), "Translated description");
        Assert.Equal("Code label", SettingTexts.Label(plain, "ru"), "A label from code is used when there is no translation");
        Assert.Equal("Быстро", SettingTexts.EnumValue(mode, SampleMode.FastMode, "ru"), "Translated enum value");

        var presentation = SettingPresentation.For(mode, "ru");
        Assert.SequenceEqual(new[] { "Быстро", "Slow Mode", "HTTP Mode" }, presentation.ChoiceLabels, "Dropdown labels mix translated and prettified values");
        Assert.True(ContextText.For(scale, "ru").StartsWith("Множитель к высоте игры."), "The context line uses the translated description");
        yield break;
    }
}
