using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using CatLib.Assets;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.Assets;

public sealed class ThunderstorePackageTest : TestCase
{
    public override string Suite => "Assets";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        Assert.Equal("My_Stamps", ThunderstorePackage.PackageName("My Stamps!"), "Spaces and signs become one _");
        Assert.Equal("a_b", ThunderstorePackage.PackageName("  a--b  "), "No _ at the ends");
        Assert.Equal(string.Empty, ThunderstorePackage.PackageName("Котики"), "Letters other than Latin are left out");
        Assert.Equal("Cats2", ThunderstorePackage.PackageName("Cats2 котики"), "Mixed names keep the Latin part");
        Assert.True(ThunderstorePackage.IsValidName("Cat_Stamps_2"), "A valid package name");
        Assert.False(ThunderstorePackage.IsValidName("Cat Stamps"), "No spaces in a package name");
        Assert.True(ThunderstorePackage.IsValidVersion("1.10.0"), "A valid version");
        Assert.False(ThunderstorePackage.IsValidVersion("1.0"), "Three parts are needed");
        Assert.False(ThunderstorePackage.IsValidVersion("01.0.0"), "No leading zeros");
        var longText = new string('a', 300);
        Assert.Equal(ThunderstorePackage.MaxDescription, ThunderstorePackage.FitDescription(longText).Length, "A long description is cut to the limit");
        Assert.True(ThunderstorePackage.FitDescription(longText).EndsWith("…"), "and ends with an ellipsis");

        var manifest = ThunderstorePackage.Manifest("Cats", "1.0.0", "", "Quote \" and\nnew line", new[] { "CatLib-CustomStamps-0.1.0", " " });
        using (var document = JsonDocument.Parse(manifest))
        {
            var root = document.RootElement;
            Assert.Equal("Cats", root.GetProperty("name").GetString(), "Manifest name");
            Assert.Equal("1.0.0", root.GetProperty("version_number").GetString(), "Manifest version");
            Assert.Equal("Quote \" and new line", root.GetProperty("description").GetString(), "The description is one line, quotes escaped");
            Assert.Equal(1, root.GetProperty("dependencies").GetArrayLength(), "Blank dependencies are left out");
        }

        var folder = Path.Combine(Path.GetTempPath(), "CatLibPackageTest_" + Guid.NewGuid().ToString("N"));
        try
        {
            var source = Directory.CreateDirectory(Path.Combine(folder, "Cats")).FullName;
            Directory.CreateDirectory(Path.Combine(source, "decorative"));
            Directory.CreateDirectory(Path.Combine(source, ".git"));
            File.WriteAllText(Path.Combine(source, "stamps.txt"), "name: Cats");
            File.WriteAllBytes(Path.Combine(source, "decorative", "paw.png"), ImageData.Blank(2, 2).ToPng());
            File.WriteAllText(Path.Combine(source, "icon.png"), "old icon");
            File.WriteAllText(Path.Combine(source, "README.md"), "old readme");
            File.WriteAllText(Path.Combine(source, "old.zip"), "zip");
            File.WriteAllText(Path.Combine(source, ".git", "config"), "hidden");
            var icon = ImageData.Blank(ThunderstorePackage.IconSide, ThunderstorePackage.IconSide).ToPng();
            var request = new PackageRequest
            {
                Name = "Cats",
                Version = "1.0.0",
                Description = "Cat stamps.",
                Readme = "# Cats\n",
                IconPng = icon,
                SourceFolder = source,
                OutputPath = Path.Combine(source, "Cats-1.0.0.zip")
            };
            request.Dependencies.Add("CatLib-CustomStamps-0.1.0");
            var result = ThunderstorePackage.Build(request);
            Assert.True(result.Succeeded, "The package is built: " + string.Join("; ", result.Problems));
            using (var zip = ZipFile.OpenRead(result.Path))
            {
                var names = zip.Entries.Select(entry => entry.FullName).OrderBy(name => name, StringComparer.Ordinal).ToList();
                Assert.SequenceEqual(new[] { "README.md", "decorative/paw.png", "icon.png", "manifest.json", "stamps.txt" }, names, "The package has the pack and the Thunderstore files, nothing hidden, no old zips");
                using var reader = new StreamReader(zip.GetEntry("README.md")!.Open());
                Assert.Equal("# Cats\n", reader.ReadToEnd(), "The README of the request replaces the one in the folder");
                Assert.Equal(icon.Length, zip.GetEntry("icon.png")!.Length, "The icon of the request replaces the one in the folder");
            }

            request.IconPng = ImageData.Blank(100, 100).ToPng();
            request.Version = "1.0";
            var refused = ThunderstorePackage.Check(request);
            Assert.Equal(2, refused.Problems.Count, "A wrong icon size and version are refused");
        }
        finally
        {
            try
            {
                Directory.Delete(folder, true);
            }
            catch (IOException)
            {
            }
        }

        yield break;
    }
}
