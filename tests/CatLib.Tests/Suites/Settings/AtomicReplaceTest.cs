using System.Collections.Generic;
using System.IO;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.Settings;

public sealed class AtomicReplaceTest : TestCase
{
    public override string Suite => "Settings";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        using var sandbox = new ConfigSandbox("AtomicReplace");
        var setting = sandbox.Settings.Local("General", "Speed", 1, "Speed.");
        var received = new List<int>();
        setting.Apply(received.Add);

        var temporary = sandbox.FilePath + ".tmp";
        File.WriteAllText(temporary, sandbox.ContentWith("General", "Speed", "9"));
        File.Move(temporary, sandbox.FilePath, true);

        yield return Wait.Until(() => received.Count >= 2, 5, "the value saved through a rename to be applied");

        Assert.SequenceEqual(new[] { 1, 9 }, received, "Applied values");
        Assert.False(File.Exists(temporary), "Temporary file must be gone");
    }
}
