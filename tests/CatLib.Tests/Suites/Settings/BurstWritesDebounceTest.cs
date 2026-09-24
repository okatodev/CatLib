using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.Settings;

public sealed class BurstWritesDebounceTest : TestCase
{
    private const int Writes = 10;

    public override string Suite => "Settings";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        using var sandbox = new ConfigSandbox("BurstWrites");
        var setting = sandbox.Settings.Local("General", "Speed", 0, "Speed.");
        var received = new List<int>();
        setting.Apply(received.Add);

        for (var value = 1; value <= Writes; value++)
        {
            sandbox.WriteValue("General", "Speed", value.ToString(CultureInfo.InvariantCulture));
            Thread.Sleep(5);
        }

        yield return Wait.Until(() => sandbox.Reports.Count >= 1, 5, "the burst to be reloaded");
        yield return Wait.Seconds(1);

        Assert.Equal(1, sandbox.Reports.Count, "Reloads caused by the burst");
        Assert.SequenceEqual(new[] { 0, Writes }, received, "Applied values");
        Assert.Equal(Writes, setting.Value, "Value after the burst");
    }
}
