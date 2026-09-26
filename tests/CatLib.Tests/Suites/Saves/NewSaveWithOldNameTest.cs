using System.Collections.Generic;
using System.IO;
using System.Linq;
using CatLib.Saves;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.Saves;

public sealed class NewSaveWithOldNameTest : TestCase
{
    public override string Suite => "Saves";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        using var sandbox = new SaveSandbox("NewSaveOldName");
        var old = SaveSandbox.Document("test.labels", 1, "{\"old\":1}");
        sandbox.WriteRaw("test.labels", old);

        var mod = sandbox.Session.Register("test.labels", "0.1.0", 1);
        sandbox.Session.Select(SaveSandbox.SaveFile, true);
        Assert.False(mod.Has("old"), "A new save does not inherit data left under the same name");
        Assert.Equal(ModSaveState.Ready, mod.State, "The new save is usable");
        Assert.Equal(old, File.ReadAllText(sandbox.PathOf("test.labels")), "Old data stays in place until the game saves");

        var late = sandbox.Session.Register("test.late", "0.1.0", 1);
        Assert.Equal(SaveReadStatus.Missing, late.LastRead, "A mod registered later does not read old data either");

        mod.Set("new", 1);
        sandbox.Now = sandbox.Now.AddMinutes(5);
        var report = sandbox.Session.CommitSave();
        Assert.NotNull(report.ArchivedTo, "Old data is archived on the first save");
        Assert.Equal(old, File.ReadAllText(Path.Combine(report.ArchivedTo, "test.labels" + SaveFileStore.Extension)), "The archive keeps the old data");
        var current = File.ReadAllText(sandbox.PathOf("test.labels"));
        Assert.True(current.Contains("\"new\"") && !current.Contains("\"old\""), "The save folder holds only new data");

        mod.Set("new", 2);
        var second = sandbox.Session.CommitSave();
        Assert.Null(second.ArchivedTo, "Archiving happens once");
        Assert.Equal(1, Directory.GetDirectories(sandbox.Root).Count(folder => folder.Contains(SaveFileStore.ArchiveMarker)), "One archive");

        using var untouched = new SaveSandbox("NewSaveNoGameSave");
        untouched.WriteRaw("test.labels", old);
        untouched.Session.Register("test.labels", "0.1.0", 1);
        untouched.Session.Select(SaveSandbox.SaveFile, true);
        untouched.Session.Close();
        Assert.Equal(old, File.ReadAllText(untouched.PathOf("test.labels")), "Leaving without a game save changes nothing");
        yield break;
    }
}
