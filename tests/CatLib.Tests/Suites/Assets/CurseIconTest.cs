using System;
using System.Collections.Generic;
using CatLib.Assets;
using CatLib.Game;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.Assets;

public sealed class CurseIconTest : TestCase
{
    public override string Suite => "Assets";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var swirl = ImageData.Blank(64, 64);
        for (var y = 0; y < 64; y++)
        {
            for (var x = 0; x < 64; x++)
            {
                var index = (y * 64 + x) * 4;
                var ring = Math.Abs(Math.Sqrt((x - 31.5) * (x - 31.5) + (y - 31.5) * (y - 31.5)) - 16) < 2;
                swirl.Rgba[index] = swirl.Rgba[index + 1] = swirl.Rgba[index + 2] = ring ? (byte)255 : (byte)0;
                swirl.Rgba[index + 3] = 255;
            }
        }

        var icon = CurseIcon.Draw(swirl);
        Assert.Equal(CurseIcon.Size, icon.Width, "The icon is square");
        Assert.Equal(CurseIcon.Size, icon.Height, "and of the icon size");
        Assert.Equal((byte)0, Pixel(icon, 1, 1)[3], "Outside the round sticker it is clear");

        var rim = Pixel(icon, 64, 4);
        Assert.True(rim[3] == 255 && rim[0] > 200 && rim[2] > 200, "The rim is light like the game's stickers");

        var disk = Pixel(icon, 64, 64);
        Assert.True(disk[3] > 200 && disk[0] < 120 && disk[2] > disk[1], "The middle is dark purple, the black of the swirl is not drawn");

        var line = Pixel(icon, 64, 64 - 35);
        Assert.True(line[2] > 200 && line[2] > line[1], "The swirl itself is a light purple line");

        var glow = Pixel(icon, 64, 64 - 43);
        Assert.True(glow[2] > disk[2] && glow[0] > disk[0], "and it glows around");
        yield break;
    }

    private static byte[] Pixel(ImageData image, int x, int y)
    {
        var index = (y * image.Width + x) * 4;
        return new[] { image.Rgba[index], image.Rgba[index + 1], image.Rgba[index + 2], image.Rgba[index + 3] };
    }
}
