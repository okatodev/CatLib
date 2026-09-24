using System.Collections.Generic;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.Settings;

public sealed class InvalidValueRejectedTest : TestCase
{
    public override string Suite => "Settings";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        using var sandbox = new ConfigSandbox("InvalidValue");
        var setting = sandbox.Settings.Local("General", "Speed", 5, "Speed.");
        var received = new List<int>();
        setting.Apply(received.Add);

        sandbox.WriteValue("General", "Speed", "not-a-number");

        yield return Wait.Until(() => sandbox.Rejected.Count >= 1, 5, "the invalid value to be rejected");

        var problem = sandbox.Rejected[0];
        context.Note("One warning log line about \"not-a-number\" is expected");
        Assert.Equal(5, setting.Value, "Value after an invalid edit");
        Assert.SequenceEqual(new[] { 5 }, received, "Applied values");
        Assert.Equal("not-a-number", problem.RawValue, "Rejected raw value");
        Assert.Equal("5", problem.EffectiveValue, "Effective value in the report");
        Assert.True(ReferenceEquals(setting, problem.Setting), "Problem must reference the setting");
        Assert.Equal(0, sandbox.Adjusted.Count, "Adjusted values");
    }
}
