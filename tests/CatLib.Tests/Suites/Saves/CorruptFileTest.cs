using System.Collections.Generic;
using System.IO;
using System.Linq;
using CatLib.Saves;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.Saves;

public sealed class CorruptFileTest : TestCase
{
    public override string Suite => "Saves";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        using var sandbox = new SaveSandbox("Corrupt");
        const string garbage = "{\"format\":1,\"values\":{\"cut";
        sandbox.WriteRaw("test.labels", garbage);
        File.WriteAllText(sandbox.BackupOf("test.labels"), SaveSandbox.Document("test.labels", 1, "{\"value\":5}"));

        var mod = sandbox.Session.Register("test.labels", "0.1.0", 1);
        sandbox.Session.Select(SaveSandbox.SaveFile, false);
        Assert.Equal(SaveReadStatus.LoadedFromBackup, mod.LastRead, "A damaged file falls back to the backup");
        Assert.Equal(5, mod.Get("value", 0), "Values come from the backup");
        Assert.Equal(ModSaveState.Ready, mod.State, "The mod keeps working");
        var kept = Directory.GetFiles(sandbox.Folder).Where(file => file.Contains(SaveFileStore.CorruptMarker)).ToList();
        Assert.Equal(1, kept.Count, "The damaged file is kept aside");
        Assert.Equal(garbage, File.ReadAllText(kept[0]), "The damaged file is kept byte for byte");
        Assert.True(mod.HasUnsavedChanges, "The main file is rebuilt on the next save");

        sandbox.Save();
        Assert.True(File.ReadAllText(sandbox.PathOf("test.labels")).Contains("\"value\": 5"), "The main file is repaired from the backup");

        using var both = new SaveSandbox("CorruptBoth");
        both.WriteRaw("test.labels", garbage);
        File.WriteAllText(both.BackupOf("test.labels"), "not json");
        var lost = both.Session.Register("test.labels", "0.1.0", 1);
        both.Session.Select(SaveSandbox.SaveFile, false);
        Assert.Equal(SaveReadStatus.Corrupt, lost.LastRead, "Both copies damaged");
        Assert.Equal(ModSaveState.Ready, lost.State, "The mod starts empty");
        Assert.Equal(0, lost.Keys.Count, "No values");
        Assert.Equal("not json", File.ReadAllText(both.BackupOf("test.labels")), "The damaged backup is not overwritten by reading");
        Assert.True(Directory.GetFiles(both.Folder).Any(file => file.Contains(SaveFileStore.CorruptMarker)), "The damaged file is kept aside");

        using var wrongShape = new SaveSandbox("WrongShape");
        wrongShape.WriteRaw("test.labels", "[1,2,3]");
        var shaped = wrongShape.Session.Register("test.labels", "0.1.0", 1);
        wrongShape.Session.Select(SaveSandbox.SaveFile, false);
        Assert.Equal(SaveReadStatus.Corrupt, shaped.LastRead, "Valid JSON of the wrong shape is damaged too");
        yield break;
    }
}
