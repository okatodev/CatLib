namespace ParcelBoard.Logic;

public enum BoardList
{
    All,
    Heavy,
    Fragile,
    Dark,
    Frozen,
    Hot,
    Cold,
    Bright,
    ContactForbidden,
    Lover,
    Corrupted,
    Damaged,
    NeedsStamps,
    Sizes
}

public enum ListVisibility
{
    Always,
    WhenNotEmpty,
    Never
}

public enum RegionOrder
{
    ByCount,
    GameOrder
}

public enum BoardCorner
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight
}

public enum CountScope
{
    Everything,
    WithoutBoatAndCustomers
}
