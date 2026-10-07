using System.Collections.Generic;
using System.Linq;
using CatLib.Game;
using CatLib.Tests.Framework;
using ParcelBoard.Logic;
using ParcelBoard.Scene;

namespace CatLib.Tests.Suites.ParcelBoard;

public sealed class SizeIconsTest : TestCase
{
    public override string Suite => "ParcelBoard";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var resources = typeof(BoardView).Assembly.GetManifestResourceNames();
        foreach (var size in BoardCounter.SizeOrder)
        {
            Assert.True(resources.Contains(BoardView.IconPrefix + "size_" + size + ".png"), $"An icon is drawn for {size}");
        }

        Assert.True(resources.Contains(BoardView.IconPrefix + "sizes.png"), "The list of sizes has a header icon");
        foreach (var file in BoardView.OwnListIcons)
        {
            Assert.True(resources.Contains(BoardView.IconPrefix + file), $"The list icon {file} is in the mod");
        }

        Assert.Equal("2×2", new ParcelFootprint(2, 2).Text(), "A footprint reads as width times depth");
        Assert.Equal("5x4", new ParcelFootprint(5, 4).Text('x'), "The sign can be a plain x for fonts without the times sign");
        Assert.Equal(string.Empty, default(ParcelFootprint).Text(), "An unknown footprint has no text");
        Assert.False(new ParcelFootprint(0, 2).IsKnown, "A footprint needs both sides");

        var parcel = new ParcelInfo(1, ParcelRegion.Hazelton, StorageConstraint.None, BehaviorConstraint.None, PackageSize.Flat, ParcelWeightClass.Medium,
            false, ParcelStamps.None, ParcelPlace.Stored);
        Assert.False(parcel.Footprint.IsKnown, "Parcels made without a footprint still work");
        yield break;
    }
}
