using System.Collections.Generic;
using BepInEx.Configuration;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.Settings;

public sealed class OutOfRangeAdjustedTest : TestCase
{
    public override string Suite => "Settings";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        using var sandbox = new ConfigSandbox("OutOfRange");
        var setting = sandbox.Settings.Local("General", "Limit", 3, "Limit.", new AcceptableValueRange<int>(1, 10));
        var received = new List<int>();
        setting.Apply(received.Add);

        sandbox.WriteValue("General", "Limit", "50");

        yield return Wait.Until(() => sandbox.Adjusted.Count >= 1, 5, "the out of range value to be adjusted");

        var problem = sandbox.Adjusted[0];
        context.Note("One warning log line about \"50\" is expected");
        Assert.Equal(10, setting.Value, "Value after an out of range edit");
        Assert.SequenceEqual(new[] { 3, 10 }, received, "Applied values");
        Assert.Equal("50", problem.RawValue, "Adjusted raw value");
        Assert.Equal("10", problem.EffectiveValue, "Effective value in the report");
        Assert.Equal(0, sandbox.Rejected.Count, "Rejected values");
    }
}
