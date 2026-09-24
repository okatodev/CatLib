using System;
using System.Collections.Generic;
using CatLib.Tests.Framework;
using CatLib.Threading;

namespace CatLib.Tests.Suites.Settings;

public sealed class FileEditAppliesTest : TestCase
{
    public override string Suite => "Settings";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        using var sandbox = new ConfigSandbox("FileEdit");
        var setting = sandbox.Settings.Local("General", "Speed", 5, "Speed.");
        var received = new List<int>();
        var threads = new List<int>();
        setting.Apply(value =>
        {
            received.Add(value);
            threads.Add(Environment.CurrentManagedThreadId);
        });

        sandbox.WriteValue("General", "Speed", "7");

        yield return Wait.Until(() => received.Count >= 2, 5, "the edited value to be applied");

        Assert.SequenceEqual(new[] { 5, 7 }, received, "Applied values");
        Assert.Equal(MainThread.ManagedThreadId, threads[1], "Thread that applied the edited value");
        Assert.Equal(7, setting.Value, "Value");
        Assert.Equal(1, sandbox.Reports.Count, "Reload reports");
        Assert.Equal(1, sandbox.Reports[0].Changed, "Changed entries in the report");
    }
}
