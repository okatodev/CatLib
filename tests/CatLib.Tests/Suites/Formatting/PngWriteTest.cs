using System.Collections.Generic;
using CatLib.Tests.Diagnostics;
using CatLib.Tests.Framework;
using CatLib.UI;

namespace CatLib.Tests.Suites.Formatting;

public sealed class PngWriteTest : TestCase
{
    public override string Suite => "Formatting";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        const int width = 5;
        const int height = 3;
        var rgba = new byte[width * height * 4];
        for (var index = 0; index < rgba.Length; index++)
        {
            rgba[index] = (byte)(index * 7);
        }

        var png = SpriteExporter.Png(rgba, width, height);
        Assert.True(PngDecoder.TryDecode(png, out var decodedWidth, out var decodedHeight, out var decoded), "The exported PNG reads back");
        Assert.Equal(width, decodedWidth, "Width");
        Assert.Equal(height, decodedHeight, "Height");
        Assert.SequenceEqual(rgba, decoded, "Pixels survive the round trip");
        yield break;
    }
}
