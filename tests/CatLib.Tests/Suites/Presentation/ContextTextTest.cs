using System.Collections.Generic;
using BepInEx.Configuration;
using CatLib.Tests.Framework;
using CatLib.Tests.Suites.Settings;
using CatLib.UI;

namespace CatLib.Tests.Suites.Presentation;

public sealed class ContextTextTest : TestCase
{
    public override string Suite => "Presentation";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        using var sandbox = new ConfigSandbox("ContextText");
        var count = sandbox.Settings.Local("General", "Count", 5, "How many cats.", new AcceptableValueRange<int>(0, 10));
        var color = sandbox.Settings.Local("General", "Color", "Red", "Fur color.", new AcceptableValueList<string>("Red", "Green", "Blue"));
        var enabled = sandbox.Settings.Local("General", "Enabled", true, "");
        var fast = sandbox.Settings.Local("General", "Fast", false, "Fast mode.").RequiresRestart();

        Assert.Equal("How many cats.\nDefault: 5 · Range: 0 to 10", ContextText.For(count, "en"), "Slider context in English");
        Assert.Equal("How many cats.\nПо умолчанию: 5 · Диапазон: от 0 до 10", ContextText.For(count, "ru"), "Slider context in Russian");
        Assert.Equal("Fur color.\nDefault: Red · Options: Red, Green, Blue", ContextText.For(color, "en"), "Dropdown context");
        Assert.Equal("No description\nDefault: On", ContextText.For(enabled, "en"), "Toggle without a description");
        Assert.Equal("Fast mode.\nDefault: Off · Applies after a restart", ContextText.For(fast, "en"), "Restart-only setting");

        fast.Entry.Value = true;

        Assert.Equal("Fast mode.\nDefault: Off · After a restart: On", ContextText.For(fast, "en"), "Pending restart");
        Assert.Equal("4 settings · 1 waiting for a restart", ContextText.CardStatus(sandbox.Settings, "en"), "Card status in English");
        Assert.Equal("4 настройки · 1 ждёт перезапуска", ContextText.CardStatus(sandbox.Settings, "ru"), "Card status in Russian");
        yield break;
    }
}
