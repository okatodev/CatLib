using System.Collections.Generic;
using System.IO;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.Settings;

public sealed class LocalValueWriteTest : TestCase
{
    public override string Suite => "Settings";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        using var sandbox = new ConfigSandbox("LocalValueWrite");
        var applied = new List<string>();
        var plan = sandbox.Settings.Session("Sync", "Plan", "game", "Written by the mod.").HiddenInMenu();
        plan.Apply(applied.Add);

        plan.LocalValue = "empty";
        Assert.Equal("empty", plan.Value, "Writing the local value changes the effective value without a host");
        Assert.Equal("empty", plan.LocalValue, "Local value");
        Assert.SequenceEqual(new[] { "game", "empty" }, applied, "Appliers run for values written from code");
        Assert.True(File.ReadAllText(sandbox.FilePath).Contains("Plan = empty"), "The value is saved to the file");
        yield break;
    }
}
