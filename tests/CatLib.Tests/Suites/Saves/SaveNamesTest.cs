using System.Collections.Generic;
using System.IO;
using CatLib.Saves;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.Saves;

public sealed class SaveNamesTest : TestCase
{
    public override string Suite => "Saves";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        Assert.Null(SaveNames.FromFileName(".bin"), "The name the game reports before a save is selected");
        Assert.Null(SaveNames.FromFileName(""), "Empty name");
        Assert.Null(SaveNames.FromFileName(null), "Missing name");
        Assert.Equal("GameSave_20260923_201740_Cat-Mail-Co", SaveNames.FromFileName("GameSave_20260923_201740_Cat-Mail-Co.bin"), "Save name from the game file");
        Assert.False(SaveNames.ForFile("catlib/labels:1").Contains("/"), "Mod ids cannot leave the save folder");
        Assert.Equal("/users/cat/LocalLow/Maracas Studio/CatMailCo/" + CatSaves.FolderName, Slashes(CatSaves.ResolveRoot("/users/cat/LocalLow/Maracas Studio/CatMailCo/GameSaves/")), "Mod data lives next to the game saves, not inside");
        Assert.Equal("C:/Users/Cat/LocalLow/Maracas Studio/CatMailCo/" + CatSaves.FolderName, Slashes(CatSaves.ResolveRoot(@"C:/Users/Cat/LocalLow/Maracas Studio/CatMailCo/GameSaves")), "The path form the game reports on Windows");
        Assert.Null(CatSaves.ResolveRoot(null), "No folder without the game folder");
        yield break;
    }

    private static string Slashes(string path) => path?.Replace('\\', '/');
}
