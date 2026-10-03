using System;

namespace CatLib.Game;

[Flags]
public enum ParcelStamps
{
    None = 0,
    Destination = 1,
    Weight = 2,
    Storage = 4,
    Behavior = 8
}

public enum ParcelPlace
{
    Loose,
    Carried,
    Stored,
    Boat,
    Customer
}

public readonly record struct ParcelFootprint(int Width, int Depth)
{
    public bool IsKnown => Width > 0 && Depth > 0;

    public string Text(char times = '×') => IsKnown ? $"{Width}{times}{Depth}" : string.Empty;

    public override string ToString() => Text();
}

public sealed record ParcelInfo(
    uint NetworkId,
    ParcelRegion Region,
    StorageConstraint Storage,
    BehaviorConstraint Behavior,
    PackageSize Size,
    ParcelWeightClass Weight,
    bool IsDamaged,
    ParcelStamps MissingStamps,
    ParcelPlace Place,
    ParcelFootprint Footprint = default)
{
    public const ParcelRegion HomeRegion = ParcelRegion.CatsIsland;

    public bool IsInbound => Region == HomeRegion;

    public bool IsOutbound => Region != ParcelRegion.None && Region != HomeRegion;

    public bool NeedsStamps => MissingStamps != ParcelStamps.None;

    public bool Has(StorageConstraint constraint) => constraint != StorageConstraint.None && (Storage & constraint) == constraint;

    public bool Has(BehaviorConstraint constraint) => constraint != BehaviorConstraint.None && (Behavior & constraint) == constraint;
}
