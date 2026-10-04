using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using CatLib.Config;
using CatLib.Tests.Framework;
using CatLib.UI;

namespace CatLib.Tests.Suites.Presentation;

public sealed class ModListExtrasTest : TestCase
{
    public override string Suite => "Presentation";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        Assert.Equal(ModBadgeKind.Paused, ModBadge.Kind(true, true, true), "A paused mod shows that first");
        Assert.Equal(ModBadgeKind.Restart, ModBadge.Kind(false, true, true), "Then a pending restart");
        Assert.Equal(ModBadgeKind.Host, ModBadge.Kind(false, false, true), "Then settings set by the host");
        Assert.Equal(ModBadgeKind.None, ModBadge.Kind(false, false, false), "Nothing to show");
        Assert.Equal("на паузе в этой игре", ModBadge.Text(ModBadgeKind.Paused, "ru"), "Russian paused mark");
        Assert.Equal("needs a restart", ModBadge.Text(ModBadgeKind.Restart, "en"), "English restart mark");
        Assert.Equal(string.Empty, ModBadge.Text(ModBadgeKind.None, "en"), "No mark, no text");
        Assert.Equal("v0.1.0 · 7 настроек", ModListMeta.Text("0.1.0", 7, ModBadgeKind.None, "ru"), "Second line: version and settings");
        Assert.Equal("2 settings", ModListMeta.Text("", 2, ModBadgeKind.None, "en"), "Without a version only the settings");
        Assert.Equal("v1.0 · set by the host", ModListMeta.Text("1.0", 2, ModBadgeKind.Host, "en"), "A mark takes the place of the settings");
        Assert.Equal("v1.0 · <color=#AD6E1A>needs a restart</color>", ModListMeta.Text("1.0", 2, ModBadgeKind.Restart, "en"), "Restart and pause marks are colored");
        Assert.Equal("v1.0 · needs a restart", ModListMeta.Text("1.0", 2, ModBadgeKind.Restart, "en", false), "Or plain");

        var alpha = new byte[10 * 8];
        for (var y = 2; y < 6; y++)
        {
            for (var x = 3; x < 9; x++)
            {
                alpha[y * 10 + x] = 255;
            }
        }

        alpha[2 * 10 + 3] = 10;
        var bounds = IconFit.Measure(alpha, 10, 8);
        Assert.Equal(3, bounds.X, "Left edge of the drawing");
        Assert.Equal(2, bounds.Y, "Bottom edge of the drawing");
        Assert.Equal(6, bounds.Width, "Width of the drawing");
        Assert.Equal(4, bounds.Height, "Height of the drawing");
        Assert.True(bounds.Coverage > 0.95f && bounds.Coverage < 1f, "Nearly clear pixels do not count");
        Assert.True(IconFit.Measure(new byte[16], 4, 4).IsEmpty, "A clear image has no drawing");
        Assert.Equal(1f, IconFit.Scale(100, 40, 1f), "A thin icon keeps its full width");
        Assert.True(IconFit.Scale(100, 100, 1f) < 0.8f, "A full square is drawn smaller");
        Assert.Equal(IconFit.MinScale, IconFit.Scale(100, 100, 1f, 0.01f), "Never smaller than the limit");

        var root = Path.Combine(Path.GetTempPath(), "CatLibAuthorTest_" + System.Guid.NewGuid().ToString("N"));
        try
        {
            var plugins = Path.Combine(root, "BepInEx", "plugins");
            var package = Directory.CreateDirectory(Path.Combine(plugins, "Okato-BoatTweaks")).FullName;
            var nested = Directory.CreateDirectory(Path.Combine(package, "BoatTweaks")).FullName;
            var plain = Directory.CreateDirectory(Path.Combine(plugins, "BoatTweaks")).FullName;
            Assert.Equal("Okato", AuthorLocator.Find(package, "Someone", "BoatTweaks"), "The Thunderstore package folder names the author");
            Assert.Equal("Okato", AuthorLocator.Find(nested, null, "BoatTweaks"), "Also from a folder inside the package");
            Assert.Equal("Someone", AuthorLocator.Find(plain, "Someone", "BoatTweaks"), "Otherwise the assembly company");
            Assert.Null(AuthorLocator.Find(plain, "BoatTweaks", "BoatTweaks"), "A company equal to the assembly name is no author");
            Assert.Null(AuthorLocator.Find(plain, " ", "BoatTweaks"), "An empty company is no author");
        }
        finally
        {
            try
            {
                Directory.Delete(root, true);
            }
            catch (IOException)
            {
            }
        }

        var png = Png(2, 2, new byte[] { 255, 0, 0, 255, 0, 255, 0, 128, 0, 0, 255, 0, 10, 20, 30, 40 });
        Assert.True(PngDecoder.TryDecode(png, out var width, out var height, out var rgba), "A plain RGBA PNG decodes");
        Assert.Equal(2, width, "PNG width");
        Assert.Equal(2, height, "PNG height");
        Assert.Equal(128, (int)rgba[7], "Alpha of the second pixel");
        Assert.Equal(30, (int)rgba[14], "Blue of the last pixel");
        Assert.False(PngDecoder.TryDecode(new byte[] { 1, 2, 3 }, out _, out _, out _), "Garbage is not a PNG");
        yield break;
    }

    private static byte[] Png(int width, int height, byte[] rgba)
    {
        var raw = new MemoryStream();
        for (var y = 0; y < height; y++)
        {
            raw.WriteByte(y == 0 ? (byte)0 : (byte)2);
            for (var x = 0; x < width * 4; x++)
            {
                var value = rgba[y * width * 4 + x];
                var up = y == 0 ? 0 : rgba[(y - 1) * width * 4 + x];
                raw.WriteByte(y == 0 ? value : (byte)(value - up));
            }
        }

        var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, true))
        {
            raw.Position = 0;
            raw.CopyTo(zlib);
        }

        var output = new MemoryStream();
        output.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        Chunk(output, "IHDR", new byte[] { 0, 0, 0, (byte)width, 0, 0, 0, (byte)height, 8, 6, 0, 0, 0 });
        Chunk(output, "IDAT", compressed.ToArray());
        Chunk(output, "IEND", new byte[0]);
        return output.ToArray();
    }

    private static void Chunk(Stream stream, string type, byte[] data)
    {
        stream.Write(new[] { (byte)(data.Length >> 24), (byte)(data.Length >> 16), (byte)(data.Length >> 8), (byte)data.Length });
        stream.Write(System.Text.Encoding.ASCII.GetBytes(type));
        stream.Write(data);
        stream.Write(new byte[4]);
    }
}
