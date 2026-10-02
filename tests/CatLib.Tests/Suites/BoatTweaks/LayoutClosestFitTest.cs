using System.Collections.Generic;
using BoatTweaks.Logic;
using BoatTweaks;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.BoatTweaks;

public sealed class LayoutClosestFitTest : TestCase
{
    public override string Suite => "BoatTweaks";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        DeckPattern.TryParse("##......\n##......\n........\n......##", out var layout, out _);

        var oneFits = new List<ISet<(int Row, int Column)>>
        {
            new HashSet<(int, int)> { (0, 0) },
            new HashSet<(int, int)> { (2, 2) },
            new HashSet<(int, int)> { (3, 7), (1, 1) },
            new HashSet<(int, int)>()
        };
        var fit = LayoutFit.ClosestVariants(layout, oneFits);
        Assert.SequenceEqual(new[] { 1, 3 }, fit.Variants, "Variants whose parcels miss the layout are the closest");
        Assert.Equal(0, fit.FreedCells, "A fitting variant frees no cell");

        var noneFit = new List<ISet<(int Row, int Column)>>
        {
            new HashSet<(int, int)> { (0, 0), (0, 1), (3, 6) },
            new HashSet<(int, int)> { (1, 1), (1, 0), (4, 4) },
            new HashSet<(int, int)> { (3, 7) },
            new HashSet<(int, int)> { (0, 1) },
            new HashSet<(int, int)> { (3, 7), (5, 5) }
        };
        var closest = LayoutFit.ClosestVariants(layout, noneFit);
        Assert.Equal(2, closest.FreedCells, "The closest variant frees the fewest cells, a touched piece is freed whole");
        Assert.SequenceEqual(new[] { 2, 4 }, closest.Variants, "Only variants that free the same cells come, so the deck looks the same every time");

        DeckPattern.TryParse("##......\n##......\n........\n........", out var square, out _);
        var corner = LayoutFit.ClosestVariants(square, new List<ISet<(int Row, int Column)>> { new HashSet<(int, int)> { (1, 1) } });
        Assert.Equal(4, corner.FreedCells, "A parcel on one cell of a 2x2 square frees the whole square");

        var again = LayoutFit.ClosestVariants(layout, noneFit);
        Assert.SequenceEqual(closest.Variants, again.Variants, "The choice does not depend on chance");

        var withoutParcels = LayoutFit.ClosestVariants(layout, new List<ISet<(int Row, int Column)>> { null, new HashSet<(int, int)> { (0, 0) } });
        Assert.SequenceEqual(new[] { 0 }, withoutParcels.Variants, "A variant without arriving parcels always fits");
        Assert.Equal(0, LayoutFit.ClosestVariants(layout, new List<ISet<(int Row, int Column)>>()).Variants.Count, "An empty pool gives no variants");
        yield break;
    }
}
