using System.Collections.Generic;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.Settings;

public sealed class IndependentFilesTest : TestCase
{
    public override string Suite => "Settings";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        using var first = new ConfigSandbox("IndependentA");
        using var second = new ConfigSandbox("IndependentB");
        var firstSetting = first.Settings.Local("General", "Speed", 1, "Speed.");
        var secondSetting = second.Settings.Local("General", "Speed", 1, "Speed.");
        var firstReceived = new List<int>();
        var secondReceived = new List<int>();
        firstSetting.Apply(firstReceived.Add);
        secondSetting.Apply(secondReceived.Add);

        first.WriteValue("General", "Speed", "3");

        yield return Wait.Until(() => first.Reports.Count >= 1, 5, "the first file to be reloaded");
        yield return Wait.Seconds(0.6);

        Assert.SequenceEqual(new[] { 1, 3 }, firstReceived, "Values applied from the edited file");
        Assert.SequenceEqual(new[] { 1 }, secondReceived, "Values applied from the untouched file");
        Assert.Equal(0, second.Reports.Count, "Reloads of the untouched file");
    }
}
