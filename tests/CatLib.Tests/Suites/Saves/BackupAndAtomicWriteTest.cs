using System.Collections.Generic;
using System.IO;
using System.Linq;
using CatLib.Saves;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.Saves;

public sealed class BackupAndAtomicWriteTest : TestCase
{
    public override string Suite => "Saves";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        using var sandbox = new SaveSandbox("Backup");
        var mod = sandbox.Session.Register("test.labels", "0.1.0", 1);
        sandbox.Session.Select(SaveSandbox.SaveFile, false);

        mod.Set("value", 1);
        sandbox.Save();
        Assert.False(File.Exists(sandbox.BackupOf("test.labels")), "No backup after the first write");

        mod.Set("value", 2);
        sandbox.Save();
        Assert.True(File.ReadAllText(sandbox.BackupOf("test.labels")).Contains("\"value\": 1"), "The backup holds the previous version");
        Assert.True(File.ReadAllText(sandbox.PathOf("test.labels")).Contains("\"value\": 2"), "The file holds the new version");
        Assert.False(Directory.GetFiles(sandbox.Folder).Any(file => file.EndsWith(SaveFileStore.TemporaryExtension)), "No temporary file is left");

        File.WriteAllText(Path.Combine(sandbox.Folder, "test.labels" + SaveFileStore.TemporaryExtension), "{ half written");
        var reloaded = sandbox.Restart().Register("test.labels", "0.1.0", 1);
        sandbox.Session.Select(SaveSandbox.SaveFile, false);
        Assert.Equal(2, reloaded.Get("value", 0), "A temporary file left by a crash is ignored");
        reloaded.Set("value", 3);
        sandbox.Save();
        var third = sandbox.Restart().Register("test.labels", "0.1.0", 1);
        Assert.Equal(0, third.Get("value", 0), "Nothing is attached before selection");
        sandbox.Session.Select(SaveSandbox.SaveFile, false);
        Assert.Equal(3, third.Get("value", 0), "Writing over a leftover temporary file works");
        yield break;
    }
}
