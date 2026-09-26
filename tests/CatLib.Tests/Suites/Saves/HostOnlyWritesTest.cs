using System.Collections.Generic;
using System.IO;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.Saves;

public sealed class HostOnlyWritesTest : TestCase
{
    public override string Suite => "Saves";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        using var sandbox = new SaveSandbox("HostOnly");
        var mod = sandbox.Session.Register("test.labels", "0.1.0", 1);
        sandbox.Session.Select(SaveSandbox.SaveFile, false);
        mod.Set("value", 1);

        sandbox.Authority = false;
        sandbox.Session.BeginSave();
        var report = sandbox.Session.CommitSave();
        Assert.True(report.Skipped, "A client does not write");
        Assert.False(Directory.Exists(sandbox.Folder), "No folder is created on a client");
        Assert.True(mod.HasUnsavedChanges, "The change is kept");

        sandbox.Authority = true;
        sandbox.Save();
        Assert.True(File.Exists(sandbox.PathOf("test.labels")), "The host writes");
        yield break;
    }
}
