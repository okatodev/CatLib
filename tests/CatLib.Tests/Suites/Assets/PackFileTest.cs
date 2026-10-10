using System.Collections.Generic;
using CatLib.Assets;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.Assets;

public sealed class PackFileTest : TestCase
{
    public override string Suite => "Assets";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var file = PackFile.Parse("﻿# A pack\r\nname: Котики\nVersion = 1.2.0\n// a note\nwebsite: https://example.com/a=b\ndescription: First line\n  second line\n\nauthor:\nnot a key\nname: Cats\n");
        Assert.Equal("Cats", file[PackFile.NameKey], "The last value of a repeated key wins");
        Assert.Equal("1.2.0", file.Get("version"), "Keys are found whatever their case, = works too");
        Assert.Equal("https://example.com/a=b", file.Get(PackFile.WebsiteKey), "A colon after the key does not split the value");
        Assert.Equal("First line\nsecond line", file.Get(PackFile.DescriptionKey), "An indented line goes on the value above");
        Assert.Null(file.Get(PackFile.AuthorKey), "An empty value counts as missing");
        Assert.Equal("nobody", file.Get(PackFile.AuthorKey, "nobody"), "Or as the fallback");
        Assert.Equal(2, file.Problems.Count, "A line without a key and a repeated key are problems");
        Assert.True(file.Problems[0].Contains("line 10"), "Problems name their line");
        Assert.Equal(0, PackFile.Parse(string.Empty).Values.Count, "An empty file has no values");
        yield break;
    }
}
