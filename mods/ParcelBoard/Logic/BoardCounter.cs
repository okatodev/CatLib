using System;
using System.Collections.Generic;
using System.Linq;
using CatLib.Game;

namespace ParcelBoard.Logic;

public sealed record BoardRow(ParcelRegion Region, PackageSize Size, int Count, bool Separated)
{
    public bool IsSize => Region == ParcelRegion.None;
}

public sealed record BoardColumn(BoardList List, int Total, IReadOnlyList<BoardRow> Rows);

public sealed class BoardOptions
{
    public IReadOnlyList<BoardList> Lists { get; set; } = BoardCounter.DefaultOrder;

    public Func<BoardList, ListVisibility> Visibility { get; set; } = _ => ListVisibility.Always;

    public bool HideEmptyRows { get; set; } = true;

    public RegionOrder Order { get; set; } = RegionOrder.ByCount;

    public CountScope Scope { get; set; } = CountScope.Everything;

    public IReadOnlyCollection<ParcelRegion> KnownRegions { get; set; } = CatParcels.Regions.ToArray();

    public IReadOnlyCollection<PackageSize> KnownSizes { get; set; } = Array.Empty<PackageSize>();
}

public static class BoardCounter
{
    public static readonly IReadOnlyList<BoardList> DefaultOrder = (BoardList[])Enum.GetValues(typeof(BoardList));

    public static readonly IReadOnlyList<PackageSize> SizeOrder = new[]
    {
        PackageSize.Small, PackageSize.FlatLetter, PackageSize.Flat, PackageSize.LongFlat, PackageSize.Standard, PackageSize.StandardLong,
        PackageSize.Medium, PackageSize.Tall, PackageSize.VeryLong, PackageSize.OddCube, PackageSize.Giant, PackageSize.GiantPlus
    };

    public static bool Matches(BoardList list, ParcelInfo parcel) => list switch
    {
        BoardList.All => true,
        BoardList.Heavy => parcel.Has(BehaviorConstraint.Heavy),
        BoardList.Fragile => parcel.Has(BehaviorConstraint.Fragile),
        BoardList.ContactForbidden => parcel.Has(BehaviorConstraint.ContactForbidden),
        BoardList.Lover => parcel.Has(BehaviorConstraint.Lover),
        BoardList.Corrupted => parcel.Has(BehaviorConstraint.Corrupted),
        BoardList.Dark => parcel.Has(StorageConstraint.Dark),
        BoardList.Frozen => parcel.Has(StorageConstraint.Frozen),
        BoardList.Hot => parcel.Has(StorageConstraint.Hot),
        BoardList.Cold => parcel.Has(StorageConstraint.Cold),
        BoardList.Bright => parcel.Has(StorageConstraint.Bright),
        BoardList.Damaged => parcel.IsDamaged,
        BoardList.NeedsStamps => parcel.NeedsStamps,
        BoardList.Sizes => parcel.Size != PackageSize.None,
        _ => false
    };

    public static bool Counted(ParcelInfo parcel, CountScope scope) =>
        scope == CountScope.Everything || (parcel.Place != ParcelPlace.Boat && parcel.Place != ParcelPlace.Customer);

    public static IReadOnlyList<BoardColumn> Build(IEnumerable<ParcelInfo> parcels, BoardOptions options)
    {
        var counted = (parcels ?? Enumerable.Empty<ParcelInfo>()).Where(parcel => parcel != null && Counted(parcel, options.Scope)).ToList();
        var columns = new List<BoardColumn>();
        foreach (var list in options.Lists)
        {
            var visibility = options.Visibility(list);
            if (visibility == ListVisibility.Never)
            {
                continue;
            }

            var matching = counted.Where(parcel => Matches(list, parcel)).ToList();
            if (matching.Count == 0 && visibility == ListVisibility.WhenNotEmpty)
            {
                continue;
            }

            var rows = list == BoardList.Sizes ? SizeRows(matching, options) : RegionRows(matching, options);
            columns.Add(new BoardColumn(list, matching.Count, rows));
        }

        return columns;
    }

    public static IReadOnlyList<BoardRow> RegionRows(IReadOnlyCollection<ParcelInfo> parcels, BoardOptions options)
    {
        var counts = parcels.GroupBy(parcel => parcel.Region).ToDictionary(group => group.Key, group => group.Count());
        var regions = new HashSet<ParcelRegion>(counts.Keys.Where(region => region != ParcelRegion.None));
        if (!options.HideEmptyRows)
        {
            regions.UnionWith(options.KnownRegions.Where(region => region != ParcelRegion.None));
        }

        var rows = new List<BoardRow>();
        if (regions.Remove(ParcelInfo.HomeRegion))
        {
            rows.Add(new BoardRow(ParcelInfo.HomeRegion, PackageSize.None, Count(counts, ParcelInfo.HomeRegion), false));
        }

        var outbound = Ordered(regions, region => Count(counts, region), region => IndexOf(CatParcels.Regions, region), options.Order);
        for (var index = 0; index < outbound.Count; index++)
        {
            rows.Add(new BoardRow(outbound[index], PackageSize.None, Count(counts, outbound[index]), index == 0 && rows.Count > 0));
        }

        return rows;
    }

    public static IReadOnlyList<BoardRow> SizeRows(IReadOnlyCollection<ParcelInfo> parcels, BoardOptions options)
    {
        var counts = parcels.GroupBy(parcel => parcel.Size).ToDictionary(group => group.Key, group => group.Count());
        var sizes = new HashSet<PackageSize>(counts.Keys.Where(size => size != PackageSize.None));
        if (!options.HideEmptyRows)
        {
            sizes.UnionWith(options.KnownSizes.Where(size => size != PackageSize.None));
        }

        return Ordered(sizes, size => Count(counts, size), size => IndexOf(SizeOrder, size), options.Order)
            .Select(size => new BoardRow(ParcelRegion.None, size, Count(counts, size), false))
            .ToList();
    }

    private static List<T> Ordered<T>(IEnumerable<T> items, Func<T, int> count, Func<T, int> gameIndex, RegionOrder order) =>
        order == RegionOrder.ByCount
            ? items.OrderByDescending(count).ThenBy(gameIndex).ToList()
            : items.OrderBy(gameIndex).ToList();

    private static int Count<T>(Dictionary<T, int> counts, T key) => counts.TryGetValue(key, out var value) ? value : 0;

    private static int IndexOf<T>(IReadOnlyList<T> list, T item)
    {
        for (var index = 0; index < list.Count; index++)
        {
            if (EqualityComparer<T>.Default.Equals(list[index], item))
            {
                return index;
            }
        }

        return int.MaxValue;
    }
}
