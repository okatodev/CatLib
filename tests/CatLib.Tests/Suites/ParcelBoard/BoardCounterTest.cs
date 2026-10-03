using System.Collections.Generic;
using System.Linq;
using CatLib.Game;
using CatLib.Tests.Framework;
using ParcelBoard.Logic;

namespace CatLib.Tests.Suites.ParcelBoard;

public sealed class BoardCounterTest : TestCase
{
    public override string Suite => "ParcelBoard";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var parcels = new List<ParcelInfo>
        {
            Parcel(ParcelRegion.CatsIsland),
            Parcel(ParcelRegion.CatsIsland, behavior: BehaviorConstraint.Fragile),
            Parcel(ParcelRegion.Hazelton, behavior: BehaviorConstraint.Heavy, size: PackageSize.Giant),
            Parcel(ParcelRegion.Hazelton, behavior: BehaviorConstraint.Fragile, needsStamps: true),
            Parcel(ParcelRegion.Hazelton, storage: StorageConstraint.Dark, damaged: true),
            Parcel(ParcelRegion.PortWindy, storage: StorageConstraint.Frozen, place: ParcelPlace.Boat),
            Parcel(ParcelRegion.PortWindy, storage: StorageConstraint.Frozen | StorageConstraint.Dark, size: PackageSize.Flat, place: ParcelPlace.Customer),
            Parcel(ParcelRegion.SunnyShores, storage: StorageConstraint.Hot, behavior: BehaviorConstraint.Heavy | BehaviorConstraint.Fragile)
        };

        var options = new BoardOptions { Visibility = list => list == BoardList.All ? ListVisibility.Always : ListVisibility.WhenNotEmpty };
        var columns = BoardCounter.Build(parcels, options).ToDictionary(column => column.List);
        Assert.Equal(8, columns[BoardList.All].Total, "All counts every parcel");
        Assert.Equal(2, columns[BoardList.Heavy].Total, "Heavy counts the Heavy mark only");
        Assert.Equal(3, columns[BoardList.Fragile].Total, "Fragile, also together with Heavy");
        Assert.Equal(2, columns[BoardList.Dark].Total, "Dark, also together with Frozen");
        Assert.Equal(2, columns[BoardList.Frozen].Total, "Frozen");
        Assert.Equal(1, columns[BoardList.Hot].Total, "Hot");
        Assert.Equal(1, columns[BoardList.Damaged].Total, "Damaged");
        Assert.Equal(1, columns[BoardList.NeedsStamps].Total, "Needs stamps");
        Assert.False(columns.ContainsKey(BoardList.Cold), "An empty list shown only when not empty is left out");
        Assert.SequenceEqual(new[] { BoardList.All, BoardList.Heavy, BoardList.Fragile, BoardList.Dark, BoardList.Frozen, BoardList.Hot, BoardList.Damaged, BoardList.NeedsStamps, BoardList.Sizes },
            columns.Keys, "Lists keep their order");

        var all = columns[BoardList.All].Rows;
        Assert.Equal(ParcelRegion.CatsIsland, all[0].Region, "Cat's Island comes first");
        Assert.Equal(2, all[0].Count, "Cat's Island count");
        Assert.False(all[0].Separated, "No line above Cat's Island");
        Assert.Equal(ParcelRegion.Hazelton, all[1].Region, "Then the destination with the most parcels");
        Assert.True(all[1].Separated, "A line parts arrivals from parcels to send");
        Assert.Equal(ParcelRegion.PortWindy, all[2].Region, "Then two parcels");
        Assert.Equal(ParcelRegion.SunnyShores, all[3].Region, "Then one parcel");
        Assert.Equal(4, all.Count, "Destinations without parcels are hidden");

        var fragile = columns[BoardList.Fragile].Rows;
        Assert.SequenceEqual(new[] { ParcelRegion.CatsIsland, ParcelRegion.SunnyShores, ParcelRegion.Hazelton }, fragile.Select(row => row.Region), "A list has only the destinations it counts, equal counts in the game's order");
        Assert.True(fragile.All(row => row.Count == 1), "One fragile parcel each");

        options.Order = RegionOrder.GameOrder;
        var gameOrder = BoardCounter.Build(parcels, options).First().Rows.Select(row => row.Region).ToList();
        Assert.SequenceEqual(new[] { ParcelRegion.CatsIsland, ParcelRegion.SunnyShores, ParcelRegion.PortWindy, ParcelRegion.Hazelton }, gameOrder, "The game's order of destinations");

        options.HideEmptyRows = false;
        options.KnownRegions = new[] { ParcelRegion.CatsIsland, ParcelRegion.Hazelton, ParcelRegion.CrescentBay };
        var frozen = BoardCounter.Build(parcels, options).First(column => column.List == BoardList.Frozen).Rows;
        Assert.SequenceEqual(new[] { ParcelRegion.CatsIsland, ParcelRegion.PortWindy, ParcelRegion.Hazelton, ParcelRegion.CrescentBay }, frozen.Select(row => row.Region),
            "Empty rows of known destinations stay when asked");
        Assert.Equal(0, frozen[0].Count, "An empty row counts zero");
        Assert.True(frozen[1].Separated, "The line stays under Cat's Island");

        options.HideEmptyRows = true;
        options.Scope = CountScope.WithoutBoatAndCustomers;
        var scoped = BoardCounter.Build(parcels, options).ToDictionary(column => column.List);
        Assert.Equal(6, scoped[BoardList.All].Total, "Parcels on the boat and at customers can be left out");
        Assert.False(scoped.ContainsKey(BoardList.Frozen), "Frozen parcels were all on the boat or at a customer");

        options.Scope = CountScope.Everything;
        options.Order = RegionOrder.ByCount;
        options.KnownSizes = new[] { PackageSize.Standard, PackageSize.Giant, PackageSize.Flat, PackageSize.Tall };
        var sizes = BoardCounter.Build(parcels, options).First(column => column.List == BoardList.Sizes);
        Assert.Equal(8, sizes.Total, "Sizes count every parcel");
        Assert.SequenceEqual(new[] { PackageSize.Standard, PackageSize.Flat, PackageSize.Giant }, sizes.Rows.Select(row => row.Size), "Sizes by count, then in size order");
        Assert.True(sizes.Rows.All(row => row.IsSize), "Size rows have no destination");

        options.Visibility = list => list == BoardList.All ? ListVisibility.Never : ListVisibility.Always;
        var always = BoardCounter.Build(new List<ParcelInfo>(), options);
        Assert.False(always.Any(column => column.List == BoardList.All), "A list set to never is never shown");
        Assert.Equal(BoardCounter.DefaultOrder.Count - 1, always.Count, "Lists set to always show even without parcels");
        Assert.True(always.All(column => column.Total == 0 && column.Rows.Count == 0), "Empty lists have no rows");
        yield break;
    }

    private static ParcelInfo Parcel(ParcelRegion region, StorageConstraint storage = StorageConstraint.None, BehaviorConstraint behavior = BehaviorConstraint.None,
        PackageSize size = PackageSize.Standard, bool damaged = false, bool needsStamps = false, ParcelPlace place = ParcelPlace.Stored) =>
        new(1, region, storage, behavior, size, ParcelWeightClass.Medium, damaged, needsStamps ? ParcelStamps.Destination : ParcelStamps.None, place);
}
