using System;

namespace BoatTweaks.Logic;

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

    public const float FlatShare = 0.5f;

    public static readonly string[] UnusedMarkers = { "Rope", "Paddle" };

    public static bool IsExcluded(string name) =>
        name != null && Array.Exists(UnusedMarkers, marker => name.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0);

    public static int Cells(float size, float cell) =>
        cell <= 0f ? 1 : Math.Max(1, (int)Math.Round(size / cell, MidpointRounding.AwayFromZero));

    public static (int Short, int Long) Footprint(float width, float depth, float cell)
    {
        var a = Cells(width, cell);
        var b = Cells(depth, cell);
        return (Math.Min(a, b), Math.Max(a, b));
    }

    public static bool IsFlat(float height, float cell) => height < cell * FlatShare;

    public static (int Short, int Long)? ByName(string name) =>
        IsLargeCrate(name) ? (2, 2) : IsSmallCrate(name) ? (1, 2) : IsSmallProp(name) ? (1, 1) : null;

    public static bool IsProp(string parentName, string name) =>
        string.Equals(parentName, VisualsName, StringComparison.Ordinal) &&
        name != null && name.StartsWith(PropPrefix, StringComparison.OrdinalIgnoreCase);
}
