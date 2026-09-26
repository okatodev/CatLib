using System.Collections.Generic;
using System.IO;
using System.Linq;
using CatLib.Game;
using CatLib.Game.Bridge;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.Platform;

public sealed class SaveInfoTest : TestCase
{
    public override string Suite => "Platform";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var directory = GameInfo.SaveDirectory;
        context.Note("Save directory: " + (directory ?? "null"));
        context.Note("Save file name: " + (GameInfo.SaveFileName ?? "null"));
        context.Note("Save file path: " + (GameInfo.SaveFilePath ?? "null"));
        context.Note($"New save: {GameInfo.IsNewSave?.ToString() ?? "null"}, loading: {GameInfo.IsLoadingSave?.ToString() ?? "null"}");
        if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
        {
            var files = Directory.GetFiles(directory).Select(path => Path.GetFileName(path) + " " + new FileInfo(path).Length + " B").ToList();
            context.Note("Files in the save directory: " + (files.Count == 0 ? "none" : string.Join(", ", files)));
        }

        Assert.False(string.IsNullOrEmpty(directory), "The game reports its save directory");
        Assert.True(ManagerRegistry.IsAttached(ManagerRegistry.SaveManagerName), "The bridge is attached to SaveManager");
        Assert.Equal(4, ManagerRegistry.BoundEventCount(ManagerRegistry.SaveManagerName), "All save events are bound");
        yield break;
    }
}
