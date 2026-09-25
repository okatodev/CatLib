using System;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP.Configuration;
using CatLib.Config;
using UnityEngine;

namespace BoatTweaks;

public sealed class BoatSettings
{
    public BoatSettings(CatSettings settings, Action changed, Action planChanged)
    {
        var scale = new AcceptableValueRange<float>(HeightRule.MinimumScale, HeightRule.MaximumScale);
        var weight = new AcceptableValueRange<int>(0, 100);

        Mode = settings.Session("Layout", "Mode", LayoutMode.Game,
            "How the fixed crates, baskets and other blockers on the boat deck are chosen. Arriving parcels never change. The sections below only work in their own mode.");
        AllowedVariants = settings.Session("GameVariants", "Allowed", "1,2,3,4",
            "Numbers of the game boat variants that may come, for example 1,2,4. Empty means all. Used in the Game layouts mode.");
        AvoidRepeats = settings.Session("GameVariants", "AvoidRepeats", true,
            "The same boat variant never comes twice in a row. Used in the Game layouts mode.");
        FixedVariant = settings.Session("FixedVariant", "Variant", 1,
            "The game boat variant that comes every time. If a level has fewer variants, its last one is used. Used in the One game layout mode.",
            new AcceptableValueRange<int>(1, 9));
        CustomOrder = settings.Session("CustomLayouts", "Order", BoatTweaks.CustomOrder.Random,
            "How own layouts are picked: at random, one after another by file name, or always the file named below. Used in the Own layouts and Mixed modes.");
        CustomFile = settings.Session("CustomLayouts", "File", "",
            "File name of the layout used when the order is Always one file, without the folder. Used in the Own layouts mode.");
        CustomAvoidRepeats = settings.Session("CustomLayouts", "AvoidRepeats", true,
            "The same own layout never comes twice in a row when picked at random.");
        Density = settings.Session("Generated", "Density", 0.2f,
            "Share of the deck taken by generated blockers. Used in the Generated and Mixed modes.",
            new AcceptableValueRange<float>(PatternGenerator.MinimumDensity, PatternGenerator.MaximumDensity));
        EdgesOnly = settings.Session("Generated", "EdgesOnly", true,
            "Generated blockers stay near the rails, like in the game, and keep the middle of the deck free.");
        GameWeight = settings.Session("Mixed", "GameWeight", 40, "Chance weight of the game's own deck in the Mixed mode.", weight);
        EmptyWeight = settings.Session("Mixed", "EmptyWeight", 10, "Chance weight of an empty deck in the Mixed mode.", weight);
        CustomWeight = settings.Session("Mixed", "CustomWeight", 25, "Chance weight of own layouts in the Mixed mode. Ignored when there are none.", weight);
        GeneratedWeight = settings.Session("Mixed", "GeneratedWeight", 25, "Chance weight of a generated deck in the Mixed mode.", weight);
        ApprovedHeightScale = settings.Session("Height", "ApprovedHeightScale", 1f,
            "Multiplier for the height above which a stack on the boat turns red. 1 keeps the game value.", scale);
        MaximumHeightScale = settings.Session("Height", "MaximumHeightScale", 1f,
            "Multiplier for the absolute height limit of stacks on the boat. The approved height never exceeds it.", scale);
        SaveHotkey = settings.Local("Saving", "SaveHotkey", new KeyboardShortcut(KeyCode.B, KeyCode.LeftControl),
            "Saves the deck of the boat at the dock as an own layout. Cells of the parcels that arrived with the boat are left out.");
        Plan = settings.Session("Sync", "Plan", "game", "The deck planned by the host for the next boat. Written by the mod.").HiddenInMenu();

        foreach (var setting in new ISettingApply[]
                 {
                     new Hook<LayoutMode>(Mode), new Hook<string>(AllowedVariants), new Hook<bool>(AvoidRepeats), new Hook<int>(FixedVariant),
                     new Hook<CustomOrder>(CustomOrder), new Hook<string>(CustomFile), new Hook<bool>(CustomAvoidRepeats),
                     new Hook<float>(Density), new Hook<bool>(EdgesOnly),
                     new Hook<int>(GameWeight), new Hook<int>(EmptyWeight), new Hook<int>(CustomWeight), new Hook<int>(GeneratedWeight),
                     new Hook<float>(ApprovedHeightScale), new Hook<float>(MaximumHeightScale)
                 })
        {
            setting.OnChange(changed);
        }

        Plan.Apply(_ => planChanged());
    }

    public Setting<LayoutMode> Mode { get; }

    public Setting<string> AllowedVariants { get; }

    public Setting<bool> AvoidRepeats { get; }

    public Setting<int> FixedVariant { get; }

    public Setting<CustomOrder> CustomOrder { get; }

    public Setting<string> CustomFile { get; }

    public Setting<bool> CustomAvoidRepeats { get; }

    public Setting<float> Density { get; }

    public Setting<bool> EdgesOnly { get; }

    public Setting<int> GameWeight { get; }

    public Setting<int> EmptyWeight { get; }

    public Setting<int> CustomWeight { get; }

    public Setting<int> GeneratedWeight { get; }

    public Setting<float> ApprovedHeightScale { get; }

    public Setting<float> MaximumHeightScale { get; }

    public Setting<KeyboardShortcut> SaveHotkey { get; }

    public Setting<string> Plan { get; }

    public DeckChoices Choices() => new()
    {
        Mode = Mode.Value,
        Order = CustomOrder.Value,
        FixedFile = CustomFile.Value,
        AvoidRepeats = CustomAvoidRepeats.Value,
        Density = Density.Value,
        EdgesOnly = EdgesOnly.Value,
        GameWeight = GameWeight.Value,
        EmptyWeight = EmptyWeight.Value,
        CustomWeight = CustomWeight.Value,
        GeneratedWeight = GeneratedWeight.Value
    };

    private interface ISettingApply
    {
        void OnChange(Action changed);
    }

    private sealed class Hook<T> : ISettingApply
    {
        private readonly Setting<T> _setting;

        public Hook(Setting<T> setting)
        {
            _setting = setting;
        }

        public void OnChange(Action changed) => _setting.Apply(_ => changed());
    }
}
