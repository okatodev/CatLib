using System;

namespace BoatTweaks;

public static class DeckDecor
{
    public const string BlockerLayerName = "StorageBlocker";
    public const string VisualsName = "Visuals";
    public const string PropPrefix = "prop_";

    public static bool IsProp(string parentName, string name) =>
        string.Equals(parentName, VisualsName, StringComparison.Ordinal) &&
        name != null && name.StartsWith(PropPrefix, StringComparison.OrdinalIgnoreCase);
}
