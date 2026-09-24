using System.Collections.Generic;
using System.IO;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.Settings;

public sealed class DeletedFileKeepsValuesTest : TestCase
{
    public override string Suite => "Settings";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        using var sandbox = new ConfigSandbox("DeletedFile");
        var setting = sandbox.Settings.Local("General", "Speed", 1, "Speed.");
        var received = new List<int>();
        setting.Apply(received.Add);

        sandbox.WriteValue("General", "Speed", "2");
        yield return Wait.Until(() => received.Count >= 2, 5, "the edit to be applied");

        File.Delete(sandbox.FilePath);
        yield return Wait.Seconds(1);

        Assert.Equal(2, setting.Value, "Value after the file was deleted");
        Assert.SequenceEqual(new[] { 1, 2 }, received, "Applied values");
        Assert.Equal(1, sandbox.Reports.Count, "Reloads");
        Assert.Equal(0, sandbox.Rejected.Count + sandbox.Adjusted.Count, "Reported problems");
    }
}
