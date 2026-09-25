using System;
using BepInEx.Configuration;
using CatLib.Config;

namespace BoatTweaks;

public sealed class BoatSettings
{
    public BoatSettings(CatSettings settings, Action changed)
    {
        var scale = new AcceptableValueRange<float>(HeightRule.MinimumScale, HeightRule.MaximumScale);

        Mode = settings.Session("Layout", "Mode", LayoutMode.Game,
            "How the fixed crates, baskets and other blockers on the boat deck are chosen. Arriving parcels never change. The sections below only work in their own mode.");
        AllowedVariants = settings.Session("GameVariants", "Allowed", "1,2,3,4",
            "Numbers of the game boat variants that may come, for example 1,2,4. Empty means all. Used in the Game layouts mode.");
        AvoidRepeats = settings.Session("GameVariants", "AvoidRepeats", true,
            "The same boat variant never comes twice in a row. Used in the Game layouts mode.");
        FixedVariant = settings.Session("FixedVariant", "Variant", 1,
            "The game boat variant that comes every time. If a level has fewer variants, its last one is used. Used in the One game layout mode.",
            new AcceptableValueRange<int>(1, 9));
        ApprovedHeightScale = settings.Session("Height", "ApprovedHeightScale", 1f,
            "Multiplier for the height above which a stack on the boat turns red. 1 keeps the game value.", scale);
        MaximumHeightScale = settings.Session("Height", "MaximumHeightScale", 1f,
            "Multiplier for the absolute height limit of stacks on the boat. The approved height never exceeds it.", scale);

        Mode.Apply(_ => changed());
        AllowedVariants.Apply(_ => changed());
        AvoidRepeats.Apply(_ => changed());
        FixedVariant.Apply(_ => changed());
        ApprovedHeightScale.Apply(_ => changed());
        MaximumHeightScale.Apply(_ => changed());
    }

    public Setting<LayoutMode> Mode { get; }

    public Setting<string> AllowedVariants { get; }

    public Setting<bool> AvoidRepeats { get; }

    public Setting<int> FixedVariant { get; }

    public Setting<float> ApprovedHeightScale { get; }

    public Setting<float> MaximumHeightScale { get; }
}
