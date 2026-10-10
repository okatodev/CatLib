using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CatLib.Assets;
using CatLib.Tests.Framework;
using CustomStamps.Packs;

namespace CatLib.Tests.Suites.CustomStamps;

public sealed class StampFilesTest : TestCase
{
    public override string Suite => "CustomStamps";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var root = Path.Combine(Path.GetTempPath(), "CatLibStampsTest_" + Guid.NewGuid().ToString("N"));
        try
        {
            var pack = Directory.CreateDirectory(Path.Combine(root, "Cats")).FullName;
            var decorative = Directory.CreateDirectory(Path.Combine(pack, StampFiles.DecorativeFolder)).FullName;
            var weight = Directory.CreateDirectory(Path.Combine(pack, StampFiles.WeightFolder)).FullName;
            var png = ImageData.Blank(4, 4).ToPng();
            File.WriteAllBytes(Path.Combine(pack, "icon.png"), png);
            File.WriteAllBytes(Path.Combine(pack, "loose.png"), png);
            File.WriteAllBytes(Path.Combine(decorative, "cat10.png"), png);
            File.WriteAllBytes(Path.Combine(decorative, "cat2.PNG"), png);
            File.WriteAllBytes(Path.Combine(decorative, "photo.jpg"), png);
            File.WriteAllBytes(Path.Combine(decorative, "anim.gif"), png);
            File.WriteAllText(Path.Combine(decorative, "notes.txt"), "not an image");
            for (var index = 0; index < StampFiles.MaxPerKind + 1; index++)
            {
                File.WriteAllBytes(Path.Combine(weight, $"w{index}.png"), png);
            }

            var skipped = new List<(string File, string Reason)>();
            var found = StampFiles.Find(pack, skipped);
            var names = found.Where(entry => entry.Kind == StampKind.Decorative).Select(entry => Path.GetFileName(entry.Path)).ToList();
            Assert.SequenceEqual(new[] { "loose.png", "cat2.PNG", "cat10.png", "photo.jpg" }, names, "Loose images and the decorative folder, PNG and JPG, numbers in order, icon.png left out");
            Assert.Equal(StampFiles.MaxPerKind, found.Count(entry => entry.Kind == StampKind.Weight), "At most the limit of weight stamps");
            Assert.True(skipped.Contains(("decorative/anim.gif", StampFiles.NotSupported)), "A GIF is named as not read");
            Assert.True(skipped.Any(entry => entry.Reason == StampFiles.TooMany), "Images over the limit are named");
            Assert.Equal(1f, StampLibrary.ClearShare(ImageData.Blank(4, 4)), "A clear image is all clear");
            var opaque = ImageData.Blank(4, 4);
            for (var index = 3; index < opaque.Rgba.Length; index += 4)
            {
                opaque.Rgba[index] = 255;
            }

            Assert.Equal(0f, StampLibrary.ClearShare(opaque), "A square of paper has no clear pixels and is not taken as a sample");
            Assert.False(skipped.Any(entry => entry.File.EndsWith("notes.txt")), "Other files are not mentioned");
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

        Assert.Equal("CustomStamps/cats/weight/fish_1", StampFiles.Key("customstamps.cats", StampKind.Weight, "C:/x/weight/fish 1.png"), "A key names the pack, the kind and the image");
        Assert.True(StampFiles.IsCustomKey("CustomStamps/cats/decorative/paw"), "Keys of custom stamps are told apart");
        Assert.False(StampFiles.IsCustomKey("StampData_Duck"), "from the game's stamps");
        Assert.True(StampFiles.Compare("a9.png", "a10.png") < 0, "Numbers sort by value");
        Assert.True(StampFiles.Compare("B.png", "a.png") > 0, "Letters sort without case");

        Assert.True(StampLibrary.HasOutline(null), "The outline is on by default");
        Assert.False(StampLibrary.HasOutline("No"), "outline: no turns it off");
        var art = ImageData.Blank(300, 100);
        for (var y = 20; y < 80; y++)
        {
            for (var x = 10; x < 290; x++)
            {
                art.Rgba[(y * 300 + x) * 4 + 3] = 255;
            }
        }

        var prepared = StampLibrary.Prepare(art, true);
        Assert.Equal(prepared.Width, prepared.Height, "A prepared stamp is square");
        Assert.True(prepared.Width <= StampLibrary.MaxSide, "and not larger than the limit");
        var bounds = prepared.VisibleBounds();
        Assert.True(bounds.Width > 290 && bounds.Width < 320, "The trimmed drawing gets a border a few percent of its size");
        var plain = StampLibrary.Prepare(art, false).VisibleBounds();
        Assert.True(plain.Width < bounds.Width, "Without the outline the drawing is narrower");
        yield break;
    }
}
