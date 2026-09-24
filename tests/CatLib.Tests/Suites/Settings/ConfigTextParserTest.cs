using System.Collections.Generic;
using BepInEx.Configuration;
using CatLib.Config;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.Settings;

public sealed class ConfigTextParserTest : TestCase
{
    public override string Suite => "Settings";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var text = string.Join("\r\n",
            "RootKey = root",
            "## Settings file was created by plugin",
            "[General]",
            "# Speed = 99",
            "  Speed   =   5  ",
            "Url = https://example.com/?a=b",
            "Speed = 6",
            "not a setting line",
            "Bad[Key] = 1",
            "",
            "[Other Section]",
            "Speed = 7",
            "Empty =");

        var values = ConfigText.Parse(text);

        Assert.Equal("root", values[new ConfigDefinition("", "RootKey")], "Key before any section");
        Assert.Equal("6", values[new ConfigDefinition("General", "Speed")], "Last duplicate wins, comments ignored, whitespace trimmed");
        Assert.Equal("https://example.com/?a=b", values[new ConfigDefinition("General", "Url")], "Only the first equals sign splits");
        Assert.Equal("7", values[new ConfigDefinition("Other Section", "Speed")], "Section with a space");
        Assert.Equal("", values[new ConfigDefinition("Other Section", "Empty")], "Empty value");
        Assert.Equal(5, values.Count, "Parsed entries");
        yield break;
    }
}
