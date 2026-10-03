using System.Collections.Generic;
using System.Linq;
using CatLib.Game;

namespace ParcelBoard.Logic;

public sealed record TableLine(ParcelRegion Region, IReadOnlyList<int> Counts, bool Separated);

public sealed record BoardTableModel(IReadOnlyList<BoardColumn> Columns, IReadOnlyList<TableLine> Lines, BoardColumn Sizes);

public static class BoardTable
{
    public const int MinSizeRows = 4;

    public static int SizeRowsPerBlock(int sizes, int lines)
    {
        if (sizes <= 0 || lines <= 0)
        {
            return 0;
        }

        var target = System.Math.Max(lines, MinSizeRows);
        if (sizes <= target)
        {
            return 0;
        }

        var blocks = (sizes + target - 1) / target;
        return (sizes + blocks - 1) / blocks;
    }

    public static BoardTableModel Build(IReadOnlyList<BoardColumn> columns, RegionOrder order)
    {
        var regionColumns = columns.Where(column => column.List != BoardList.Sizes).ToList();
        var sizes = columns.FirstOrDefault(column => column.List == BoardList.Sizes);
        var counts = regionColumns.Select(column => column.Rows.ToDictionary(row => row.Region, row => row.Count)).ToList();
        var regions = new HashSet<ParcelRegion>(regionColumns.SelectMany(column => column.Rows).Select(row => row.Region));
        var all = regionColumns.FindIndex(column => column.List == BoardList.All);

        int Weight(ParcelRegion region) => all >= 0
            ? Count(counts[all], region)
            : counts.Sum(map => Count(map, region));

        var lines = new List<TableLine>();
        if (regions.Remove(ParcelInfo.HomeRegion))
        {
            lines.Add(Line(ParcelInfo.HomeRegion, counts, false));
        }

        var rest = order == RegionOrder.ByCount
            ? regions.OrderByDescending(Weight).ThenBy(GameIndex).ToList()
            : regions.OrderBy(GameIndex).ToList();
        for (var index = 0; index < rest.Count; index++)
        {
            lines.Add(Line(rest[index], counts, index == 0 && lines.Count > 0));
        }

        return new BoardTableModel(regionColumns, lines, sizes);
    }

    private static TableLine Line(ParcelRegion region, List<Dictionary<ParcelRegion, int>> counts, bool separated) =>
        new(region, counts.Select(map => Count(map, region)).ToList(), separated);

    private static int Count(Dictionary<ParcelRegion, int> counts, ParcelRegion region) => counts.TryGetValue(region, out var value) ? value : 0;

    private static int GameIndex(ParcelRegion region)
    {
        for (var index = 0; index < CatParcels.Regions.Count; index++)
        {
            if (CatParcels.Regions[index] == region)
            {
                return index;
            }
        }

        return int.MaxValue;
    }
}
