using System;

namespace BoatTweaks;

public static class DeckDecor
{
    public const string BlockerLayerName = "StorageBlocker";
    public const string VisualsName = "Visuals";
    public const string PropPrefix = "prop_";

    public static readonly string[] SmallPropMarkers = { "Bottle", "Lamp" };
    public const string LargeCrateMarker = "Crate_Standard";
    public const string SmallCrateMarker = "Crate_Small";

    public static bool IsLargeCrate(string name) => name != null && name.IndexOf(LargeCrateMarker, StringComparison.OrdinalIgnoreCase) >= 0;

    public static bool IsSmallCrate(string name) => name != null && name.IndexOf(SmallCrateMarker, StringComparison.OrdinalIgnoreCase) >= 0;

    public static bool IsSmallProp(string name) =>
        name != null && Array.Exists(SmallPropMarkers, marker => name.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0);

    public static bool IsProp(string parentName, string name) =>
        string.Equals(parentName, VisualsName, StringComparison.Ordinal) &&
        name != null && name.StartsWith(PropPrefix, StringComparison.OrdinalIgnoreCase);
}
