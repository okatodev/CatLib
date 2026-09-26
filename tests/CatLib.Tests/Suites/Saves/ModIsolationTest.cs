using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CatLib.Saves;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.Saves;

public sealed class ModIsolationTest : TestCase
{
    public override string Suite => "Saves";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        using var sandbox = new SaveSandbox("Isolation");
        var broken = sandbox.Session.Register("test.broken", "0.1.0", 1);
        var blocked = sandbox.Session.Register("test.blocked", "0.1.0", 1);
        var healthy = sandbox.Session.Register("test.healthy", "0.1.0", 1);
        broken.Loaded += () => throw new InvalidOperationException("expected test failure");
        broken.Saving += () => throw new InvalidOperationException("expected test failure");
        healthy.Saving += () => healthy.Set("fromSaving", true);

        sandbox.Session.Select(SaveSandbox.SaveFile, false);
        Assert.Equal(ModSaveState.Ready, healthy.State, "A failing handler of one mod does not stop the others");
        broken.Set("value", 1);
        blocked.Set("value", 1);
        Directory.CreateDirectory(sandbox.PathOf("test.blocked"));

        sandbox.Session.BeginSave();
        var report = sandbox.Session.CommitSave();
        Assert.SequenceEqual(new[] { "test.blocked" }, report.Failed, "The blocked file is reported");
        Assert.True(report.Written.Contains("test.broken") && report.Written.Contains("test.healthy"), "Other mods are written");
        Assert.True(healthy.Get("fromSaving", false), "Values set in Saving are part of the save");
        Assert.True(File.ReadAllText(sandbox.PathOf("test.healthy")).Contains("fromSaving"), "Saving runs before the snapshot");
        Assert.True(blocked.HasUnsavedChanges, "The failed mod keeps its changes for the next save");
        yield break;
    }
}
