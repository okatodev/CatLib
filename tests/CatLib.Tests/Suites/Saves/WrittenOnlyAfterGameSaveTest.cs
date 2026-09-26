using System.Collections.Generic;
using System.IO;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.Saves;

public sealed class WrittenOnlyAfterGameSaveTest : TestCase
{
    public override string Suite => "Saves";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        using var sandbox = new SaveSandbox("AfterGameSave");
        var mod = sandbox.Session.Register("test.labels", "0.1.0", 1);
        sandbox.Session.Select(SaveSandbox.SaveFile, false);
        var path = sandbox.PathOf("test.labels");

        mod.Set("value", 1);
        Assert.False(File.Exists(path), "Changes stay in memory until the game saves");

        sandbox.Session.BeginSave();
        sandbox.Session.FailSave();
        Assert.False(File.Exists(path), "A failed game save writes nothing");
        Assert.True(mod.HasUnsavedChanges, "The change waits for the next save");

        sandbox.Session.BeginSave();
        mod.Set("value", 2);
        var report = sandbox.Session.CommitSave();
        Assert.SequenceEqual(new[] { "test.labels" }, report.Written, "Written mods");
        Assert.True(mod.HasUnsavedChanges, "A change made during the save waits for the next one");
        Assert.True(File.ReadAllText(path).Contains("\"value\": 1"), "The file holds the value from the moment the game started saving");

        sandbox.Save();
        Assert.True(File.ReadAllText(path).Contains("\"value\": 2"), "The later change is written by the next save");

        sandbox.Session.Register("test.untouched", "0.1.0", 1);
        sandbox.Save();
        Assert.False(File.Exists(sandbox.PathOf("test.untouched")), "Mods without data leave no files");
        yield break;
    }
}
