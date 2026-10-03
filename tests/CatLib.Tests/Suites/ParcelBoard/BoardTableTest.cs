using System.Collections.Generic;
using System.Linq;
using CatLib.Game;
using CatLib.Tests.Framework;
using ParcelBoard.Logic;

namespace CatLib.Tests.Suites.ParcelBoard;

public sealed class BoardTableTest : TestCase
{
    public override string Suite => "ParcelBoard";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var parcels = new List<ParcelInfo>
        {
            Parcel(ParcelRegion.Hazelton, BehaviorConstraint.Fragile),
            Parcel(ParcelRegion.Hazelton, BehaviorConstraint.None),
            Parcel(ParcelRegion.CatsIsland, BehaviorConstraint.Heavy),
            Parcel(ParcelRegion.TropicalDunes, BehaviorConstraint.Heavy),
            Parcel(ParcelRegion.TropicalDunes, BehaviorConstraint.Heavy),
            Parcel(ParcelRegion.TropicalDunes, BehaviorConstraint.None)
        };
        var options = new BoardOptions { Visibility = list => list is BoardList.All or BoardList.Heavy or BoardList.Fragile or BoardList.Sizes ? ListVisibility.Always : ListVisibility.Never };
        var table = BoardTable.Build(BoardCounter.Build(parcels, options), RegionOrder.ByCount);

        Assert.SequenceEqual(new[] { BoardList.All, BoardList.Heavy, BoardList.Fragile }, table.Columns.Select(column => column.List), "Destination lists are the columns");
        Assert.NotNull(table.Sizes, "Sizes are a table of their own");
        Assert.SequenceEqual(new[] { ParcelRegion.CatsIsland, ParcelRegion.TropicalDunes, ParcelRegion.Hazelton }, table.Lines.Select(line => line.Region),
            "One line per destination: Cat's Island, then by all parcels");
        Assert.SequenceEqual(new[] { 1, 1, 0 }, table.Lines[0].Counts, "Cat's Island: all, heavy, fragile");
        Assert.SequenceEqual(new[] { 3, 2, 0 }, table.Lines[1].Counts, "Tropical Dunes");
        Assert.SequenceEqual(new[] { 2, 0, 1 }, table.Lines[2].Counts, "Hazelton");
        Assert.True(table.Lines[1].Separated && !table.Lines[0].Separated && !table.Lines[2].Separated, "One line under Cat's Island");

        var gameOrder = BoardTable.Build(BoardCounter.Build(parcels, options), RegionOrder.GameOrder);
        Assert.SequenceEqual(new[] { ParcelRegion.CatsIsland, ParcelRegion.Hazelton, ParcelRegion.TropicalDunes }, gameOrder.Lines.Select(line => line.Region), "The game's order");

        Assert.Equal(6, BoardTable.SizeRowsPerBlock(11, 6), "Eleven sizes next to six destinations: two columns of six and five");
        Assert.Equal(0, BoardTable.SizeRowsPerBlock(5, 6), "Sizes that fit the height of the destinations stay in one column");
        Assert.Equal(4, BoardTable.SizeRowsPerBlock(11, 2), "Next to a short table a column still has four sizes");
        Assert.Equal(0, BoardTable.SizeRowsPerBlock(11, 0), "Without destinations the sizes are one column");

        options.Visibility = list => list == BoardList.Heavy ? ListVisibility.Always : ListVisibility.Never;
        var heavy = BoardTable.Build(BoardCounter.Build(parcels, options), RegionOrder.ByCount);
        Assert.SequenceEqual(new[] { ParcelRegion.CatsIsland, ParcelRegion.TropicalDunes }, heavy.Lines.Select(line => line.Region), "Without the list of all parcels, only destinations with counts");
        Assert.Null(heavy.Sizes, "No sizes table when sizes are off");
        yield break;
    }

    private static ParcelInfo Parcel(ParcelRegion region, BehaviorConstraint behavior) =>
        new(1, region, StorageConstraint.None, behavior, PackageSize.Standard, ParcelWeightClass.Medium, false, ParcelStamps.None, ParcelPlace.Stored);
}
