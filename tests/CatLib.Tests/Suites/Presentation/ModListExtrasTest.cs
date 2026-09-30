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
