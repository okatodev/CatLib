using System.Collections.Generic;
using System.IO;
using CatLib.Saves;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.Saves;

public sealed class NewerDataUntouchedTest : TestCase
{
    public override string Suite => "Saves";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        using var sandbox = new SaveSandbox("Newer");
        var newer = SaveSandbox.Document("test.labels", 3, "{\"value\":9}");
        sandbox.WriteRaw("test.labels", newer);

        var mod = sandbox.Session.Register("test.labels", "0.1.0", 2);
        sandbox.Session.Select(SaveSandbox.SaveFile, false);
        Assert.Equal(ModSaveState.ReadOnly, mod.State, "Data from a newer mod version is read only");
        Assert.Equal(3, mod.StoredDataVersion, "The stored version is reported");
        Assert.Equal(0, mod.Get("value", 0), "Values the mod cannot understand are not given to it");
        Assert.False(mod.Set("value", 1), "Writing is refused");
        sandbox.Save();
        Assert.Equal(newer, File.ReadAllText(sandbox.PathOf("test.labels")), "The file is left as it was");
        Assert.False(File.Exists(sandbox.BackupOf("test.labels")), "Not even a backup is made");

        using var format = new SaveSandbox("NewerFormat");
        var future = SaveSandbox.Document("test.labels", 1, "{\"value\":9}", SaveDocument.CurrentFormat + 1);
        format.WriteRaw("test.labels", future);
        var reader = format.Session.Register("test.labels", "0.1.0", 1);
        format.Session.Select(SaveSandbox.SaveFile, false);
        Assert.Equal(SaveReadStatus.TooNew, reader.LastRead, "A file from a newer CatLib");
        Assert.Equal(ModSaveState.ReadOnly, reader.State, "Read only");
        format.Save();
        Assert.Equal(future, File.ReadAllText(format.PathOf("test.labels")), "The file is left as it was");

        using var older = new SaveSandbox("Older");
        older.WriteRaw("test.labels", SaveSandbox.Document("test.labels", 1, "{\"value\":4}"));
        var upgraded = older.Session.Register("test.labels", "0.2.0", 2);
        older.Session.Select(SaveSandbox.SaveFile, false);
        Assert.Equal(ModSaveState.Ready, upgraded.State, "Older data is usable");
        Assert.Equal(1, upgraded.StoredDataVersion, "The mod sees which version it reads and can convert");
        upgraded.Set("value", 8);
        older.Save();
        Assert.True(File.ReadAllText(older.PathOf("test.labels")).Contains("\"dataVersion\": 2"), "Written with the current version");
        yield break;
    }
}
