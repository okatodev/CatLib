using System.Collections.Generic;
using System.IO;
using CatLib.Config;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.Presentation;

public sealed class IconLocatorTest : TestCase
{
    public override string Suite => "Presentation";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var root = Path.Combine(Path.GetTempPath(), "CatLibIconTest_" + System.Guid.NewGuid().ToString("N"));
        try
        {
            var plugins = Path.Combine(root, "BepInEx", "plugins");
            var flat = Directory.CreateDirectory(Path.Combine(plugins, "BoatTweaks")).FullName;
            File.WriteAllBytes(Path.Combine(flat, IconLocator.FileName), new byte[] { 1 });
            Assert.Equal(Path.Combine(flat, IconLocator.FileName), IconLocator.Find(flat), "An icon next to the plugin");

            var package = Directory.CreateDirectory(Path.Combine(plugins, "Okato-ShelfLabels")).FullName;
            var nested = Directory.CreateDirectory(Path.Combine(package, "ShelfLabels")).FullName;
            File.WriteAllBytes(Path.Combine(package, IconLocator.FileName), new byte[] { 1 });
            Assert.Equal(Path.Combine(package, IconLocator.FileName), IconLocator.Find(nested), "A Thunderstore package icon one folder up");

            File.WriteAllBytes(Path.Combine(plugins, IconLocator.FileName), new byte[] { 1 });
            var bare = Directory.CreateDirectory(Path.Combine(plugins, "NoIcon")).FullName;
            Assert.Null(IconLocator.Find(bare), "An icon in the shared plugins folder belongs to no mod");
            Assert.Null(IconLocator.Find(plugins), "A plugin placed directly in plugins has no icon");
            Assert.Null(IconLocator.Find(null), "No folder, no icon");
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

        yield break;
    }
}
