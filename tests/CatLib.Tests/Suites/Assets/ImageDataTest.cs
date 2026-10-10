using System;
using System.Collections.Generic;
using CatLib.Assets;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.Assets;

public sealed class ImageDataTest : TestCase
{
    public const string InterlacedPng =
        "iVBORw0KGgoAAAANSUhEUgAAAAUAAAADCAYAAAEsMfVuAAAAO0lEQVR4nA3KMREAMAwDsQdREAFREAZREBkNxUzTrDoBDEGDqIHUKJqkh+K4uaZy3XkGHZe2aEHb9PwBC3QYXdcfZQ0AAAAASUVORK5CYII=";

    public override string Suite => "Assets";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var image = Pattern(6, 4);
        Assert.True(ImageData.TryDecode(image.ToPng(), out var decoded), "An encoded image decodes again");
        Assert.Equal(6, decoded.Width, "Width after a round trip");
        Assert.Equal(4, decoded.Height, "Height after a round trip");
        Assert.SequenceEqual(image.Rgba, decoded.Rgba, "Every pixel after a round trip");

        Assert.True(ImageData.TryDecode(Convert.FromBase64String(InterlacedPng), out var interlaced), "An interlaced PNG decodes");
        Assert.Equal(5, interlaced.Width, "Width of the interlaced image");
        for (var y = 0; y < 3; y++)
        {
            for (var x = 0; x < 5; x++)
            {
                var index = (y * 5 + x) * 4;
                Assert.Equal((byte)(x * 40), interlaced.Rgba[index], $"Red of the interlaced pixel {x},{y}");
                Assert.Equal((byte)(y * 80), interlaced.Rgba[index + 1], $"Green of the interlaced pixel {x},{y}");
                Assert.Equal((byte)((x + y) % 2 == 0 ? 255 : 128), interlaced.Rgba[index + 3], $"Alpha of the interlaced pixel {x},{y}");
            }
        }

        var framed = ImageData.Blank(10, 8);
        Fill(framed, 3, 2, 4, 3, 200);
        var bounds = framed.VisibleBounds();
        Assert.Equal("4x3 at 3,2", bounds.ToString(), "The visible part of an image with clear edges");
        var trimmed = framed.Trimmed();
        Assert.Equal(4, trimmed.Width, "Trimmed to the drawing, width");
        Assert.Equal(3, trimmed.Height, "Trimmed to the drawing, height");
        Assert.Equal(6, framed.Trimmed(1).Width, "A margin is kept around the drawing");
        Assert.True(ImageData.Blank(3, 3).Trimmed().Width == 3, "A clear image stays as it is");

        var padded = trimmed.Padded(8, 5);
        Assert.Equal(0, padded.Rgba[3], "Padding is clear");
        Assert.Equal(200, padded.Rgba[(1 * 8 + 2) * 4 + 3], "The drawing is in the middle of the padding");
        var square = trimmed.PaddedToAspect(1f);
        Assert.Equal(square.Width, square.Height, "Padded to a square");
        Assert.Equal(4, square.Width, "The square is as wide as the longer side");

        var big = Pattern(1000, 500);
        var scaled = big.Scaled(256);
        Assert.Equal(256, scaled.Width, "The longer side is scaled to the limit");
        Assert.Equal(128, scaled.Height, "The other side keeps the proportion");
        Assert.True(ReferenceEquals(scaled, scaled.Scaled(512)), "A small image is not scaled up");
        var fitted = trimmed.Fitted(16, 16);
        Assert.Equal(16, fitted.Width, "Fitted into a box, width");
        Assert.Equal(16, fitted.Height, "Fitted into a box, height");

        var dot = ImageData.Blank(20, 20);
        Fill(dot, 8, 8, 4, 4, 255);
        var outlined = dot.Trimmed().Outlined(3);
        Assert.Equal(10, outlined.Width, "The outline adds its thickness on each side");
        Assert.Equal(255, outlined.Rgba[(5 * 10 + 5) * 4 + 3], "Inside stays solid");
        Assert.Equal(255, outlined.Rgba[(5 * 10 + 1) * 4 + 3], "The outline is solid near the drawing");
        Assert.Equal(255, outlined.Rgba[(5 * 10 + 1) * 4], "and white");
        Assert.Equal(0, outlined.Rgba[(0 * 10 + 0) * 4 + 3], "Beyond the outline the image stays clear");

        var collage = ImageData.Collage(new List<ImageData> { dot, square, padded, big }, 64, 4);
        Assert.Equal(64, collage.Width, "A collage has the given size");
        Assert.True(collage.VisibleBounds().Width > 32, "A collage of four images covers most of the width");
        Assert.Throws<ArgumentException>(() => new ImageData(2, 2, new byte[3]), "Wrong pixel data is refused");
        yield break;
    }

    private static ImageData Pattern(int width, int height)
    {
        var image = ImageData.Blank(width, height);
        for (var index = 0; index < width * height; index++)
        {
            image.Rgba[index * 4] = (byte)(index * 7);
            image.Rgba[index * 4 + 1] = (byte)(index * 13);
            image.Rgba[index * 4 + 2] = (byte)(index * 29);
            image.Rgba[index * 4 + 3] = 255;
        }

        return image;
    }

    private static void Fill(ImageData image, int left, int top, int width, int height, byte alpha)
    {
        for (var y = top; y < top + height; y++)
        {
            for (var x = left; x < left + width; x++)
            {
                var index = (y * image.Width + x) * 4;
                image.Rgba[index] = 10;
                image.Rgba[index + 1] = 20;
                image.Rgba[index + 2] = 30;
                image.Rgba[index + 3] = alpha;
            }
        }
    }
}
