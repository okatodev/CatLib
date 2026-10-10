using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CatLib.Assets;
using CatLib.Config;
using CatLib.Localization;
using CatLib.Tests.Framework;
using CatLib.Tests.Suites.Settings;
using CatLib.UI;

namespace CatLib.Tests.Suites.Assets;

public sealed class ContentPacksTest : TestCase
{
    public override string Suite => "Assets";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var kind = new ContentPackKind("catlib.tests.packs", "stamps.txt", "teststamps") { MaxDepth = 2, Template = "name: {name}\nversion: 1.0.0\n" };
        kind.Folders.Add("decorative");
        var root = Path.Combine(Path.GetTempPath(), "CatLibPacksTest_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            var created = ContentPacks.Create(kind, "My Cats", root);
            Assert.Equal("My_Cats", Path.GetFileName(created), "A new pack is a folder named after it");
            Assert.True(Directory.Exists(Path.Combine(created, "decorative")), "with the folders of its kind");
            Assert.Equal("name: My Cats\nversion: 1.0.0\n", File.ReadAllText(Path.Combine(created, "stamps.txt")), "and the template with its name");
            Assert.Equal("My_Cats_2", Path.GetFileName(ContentPacks.Create(kind, "My Cats", root)), "A second pack of the same name gets a number");
            Assert.Equal("MyPack", Path.GetFileName(ContentPacks.Create(kind, "Котики", root)), "A name without Latin letters gets a plain folder name");

            var deep = Directory.CreateDirectory(Path.Combine(root, "Team-Pack", "Pack", "Inner")).FullName;
            File.WriteAllText(Path.Combine(root, "Team-Pack", "Pack", "STAMPS.TXT"), "name: Котики\nversion: 2.0\n");
            File.WriteAllText(Path.Combine(deep, "stamps.txt"), "name: Too deep");
            var files = ContentPacks.FindFiles(kind, root);
            Assert.Equal(4, files.Count, "Packs are found up to the depth of their kind, whatever the case of the file name");
            Assert.False(files.Any(path => path.Contains("Inner")), "Deeper folders are not searched");

            var cyrillic = ContentPacks.Describe(kind, files.First(path => path.Contains("Team-Pack")));
            Assert.Equal("Котики", cyrillic.Name, "The name comes from the file");
            Assert.Equal("Pack", cyrillic.PackageName, "Without Latin letters the package name comes from the folder");
            Assert.Equal("teststamps.pack", cyrillic.Id, "The id is the prefix of the kind and the package name");
            Assert.Equal("2.0", cyrillic.Version, "The version as written");
            Assert.Equal(1, cyrillic.Problems.Count, "A version without three parts is a problem");
            var mine = ContentPacks.Describe(kind, Path.Combine(created, "stamps.txt"));
            Assert.Equal("teststamps.my_cats", mine.Id, "Ids are lower case");
            Assert.Equal(0, mine.Problems.Count, "A good pack has no problems");
            Assert.True(ContentPacks.Readme(mine).StartsWith("# My Cats\n"), "The README starts with the name");
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

        using (var sandbox = new ConfigSandbox("MenuItems"))
        {
            var settings = sandbox.Settings;
            Assert.False(ModsPanel.IsListed(settings), "A card without settings and items is not listed");
            var clicks = 0;
            var button = settings.Button("Pack", "Build", () => clicks++);
            var gallery = settings.Gallery("Stamps", "Decorative");
            Assert.True(ModsPanel.IsListed(settings), "Menu items alone list a card");
            Assert.Equal(2, settings.MenuItems.Count, "Both items are kept in order");
            Assert.Equal("Build", SettingTexts.ItemLabel(button, "en"), "Without a translation the key is the label");
            button.Label = language => language == "ru" ? "Собрать" : "Build it";
            Assert.Equal("Собрать", SettingTexts.ItemLabel(button, "ru"), "A label of the item is used next");
            CatLocalization.For(settings.OwnerId).Add("en", "setting.Pack.Build", "Build the package");
            Assert.Equal("Build the package", SettingTexts.ItemLabel(button, "en"), "A translation of the owner wins");
            var revision = gallery.Revision;
            gallery.SetImages(null);
            Assert.True(gallery.Revision > revision, "Changing the images changes the revision");
            button.Clicked();
            Assert.Equal(1, clicks, "The button runs its action");
            Assert.Throws<ArgumentException>(() => settings.Gallery(" ", "x"), "A section is needed");
            Assert.True(settings.RemoveMenuItem(gallery), "An item can be removed");
            Assert.Equal(1, settings.MenuItems.Count, "One item is left");

            using var child = new ConfigSandbox("ChildPack");
            using var other = new ConfigSandbox("AAA");
            child.Settings.ParentId = settings.OwnerId;
            var ordered = ModsPanel.Ordered(new[] { child.Settings, other.Settings, settings }).Select(each => each.OwnerId).ToList();
            Assert.Equal(ordered.IndexOf(settings.OwnerId) + 1, ordered.IndexOf(child.Settings.OwnerId), "A pack follows the mod that reads it");

            var before = ModsPanel.ListSignature(new[] { child.Settings });
            child.Settings.Dispose();
            using var again = CatSettings.For(new BepInEx.Configuration.ConfigFile(child.FilePath, true), child.OwnerId);
            Assert.False(ReferenceEquals(child.Settings, again), "A card made again after its pack was read again is a new one");
            Assert.False(before == ModsPanel.ListSignature(new[] { again }), "and the Mods tab rebuilds its list for it, even with the same id and rows");
        }

        yield break;
    }
}
