using System;
using System.Collections.Generic;
using CatLib.Tests.Framework;
using CatLib.Tests.Suites.Scheduling;

namespace CatLib.Tests.Suites.Settings;

public sealed class ApplyIsolationTest : TestCase
{
    public override string Suite => "Settings";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        using var sandbox = new ConfigSandbox("ApplyIsolation");
        var setting = sandbox.Settings.Local("General", "Speed", 1, "Speed.");
        var received = new List<int>();

        setting.Apply(_ => throw new InvalidOperationException(MainThreadExceptionIsolationTest.ExpectedExceptionMessage));
        setting.Apply(received.Add);

        sandbox.WriteValue("General", "Speed", "2");

        yield return Wait.Until(() => received.Count >= 2, 5, "the healthy applier to receive the edited value");

        context.Note($"Two error log lines containing \"{MainThreadExceptionIsolationTest.ExpectedExceptionMessage}\" are expected");
        Assert.SequenceEqual(new[] { 1, 2 }, received, "Values received by the healthy applier");
        Assert.Equal(2, setting.Value, "Value");
    }
}
