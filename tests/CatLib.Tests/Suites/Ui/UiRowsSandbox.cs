using System;
using BepInEx.Configuration;
using CatLib.Config;
using CatLib.Tests.Suites.Presentation;
using CatLib.Tests.Suites.Settings;

namespace CatLib.Tests.Suites.Ui;

internal sealed class UiRowsSandbox : IDisposable
{
    public UiRowsSandbox(string name)
    {
        Config = new ConfigSandbox(name);
        var settings = Config.Settings;
        Enabled = settings.Local("General", "Enabled", true, "Enabled.");
        Count = settings.Local("General", "Count", 5, "Count.", new AcceptableValueRange<int>(0, 10));
        Speed = settings.Local("General", "Speed", 1.5f, "Speed.", new AcceptableValueRange<float>(0f, 3f));
        Mode = settings.Local("Mode", "Mode", SampleMode.FastMode, "Mode.");
        Color = settings.Local("Mode", "Color", "Red", "Color.", new AcceptableValueList<string>("Red", "Green", "Blue"));
        Name = settings.Local("Text", "Name", "Cat", "Name.").Label("Custom label");
        Seed = settings.Local("Text", "Seed", 42, "Seed.");
        Secret = settings.Local("Text", "Secret", "hidden", "Secret.").HiddenInMenu();
        FastMode = settings.Local("Advanced", "FastMode", false, "Fast mode.").RequiresRestart();
        Difficulty = settings.Session("Session", "Difficulty", 2, "Difficulty.", new AcceptableValueRange<int>(1, 5));
    }

    public const int VisibleCount = 9;
    public const int SectionCount = 5;

    public ConfigSandbox Config { get; }

    public CatSettings Settings => Config.Settings;

    public Setting<bool> Enabled { get; }

    public Setting<int> Count { get; }

    public Setting<float> Speed { get; }

    public Setting<SampleMode> Mode { get; }

    public Setting<string> Color { get; }

    public Setting<string> Name { get; }

    public Setting<int> Seed { get; }

    public Setting<string> Secret { get; }

    public Setting<bool> FastMode { get; }

    public Setting<int> Difficulty { get; }

    public void Dispose() => Config.Dispose();
}
