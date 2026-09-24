using System.Collections.Generic;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.Settings;

public sealed class ApplyDisposeTest : TestCase
{
    public override string Suite => "Settings";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        using var sandbox = new ConfigSandbox("ApplyDispose");
        var setting = sandbox.Settings.Local("General", "Speed", 1, "Speed.");
        var received = new List<int>();

        var subscription = setting.Apply(received.Add);
        subscription.Dispose();
        subscription.Dispose();

        sandbox.WriteValue("General", "Speed", "2");

        yield return Wait.Until(() => sandbox.Reports.Count >= 1, 5, "the edit to be reloaded");

        Assert.SequenceEqual(new[] { 1 }, received, "Values received after disposing the subscription");
        Assert.Equal(2, setting.Value, "Value");
    }
}
