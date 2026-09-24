using System.Collections.Generic;
using System.IO;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.Settings;

public sealed class OwnSaveTest : TestCase
{
    public override string Suite => "Settings";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        using var sandbox = new ConfigSandbox("OwnSave");
        var setting = sandbox.Settings.Local("General", "Speed", 1, "Speed.");
        var received = new List<int>();
        setting.Apply(received.Add);

        setting.Entry.Value = 4;

        Assert.SequenceEqual(new[] { 1, 4 }, received, "Applied values right after a change from code");

        yield return Wait.Seconds(1);

        Assert.SequenceEqual(new[] { 1, 4 }, received, "Applied values after the own save settled");
        Assert.Equal(0, sandbox.Reports.Count, "Reloads caused by the own save");
        Assert.True(File.ReadAllText(sandbox.FilePath).Contains("Speed = 4"), "Saved file must contain the new value");
    }
}
