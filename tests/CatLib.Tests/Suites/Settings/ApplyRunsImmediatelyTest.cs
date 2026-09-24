using System.Collections.Generic;
using CatLib.Config;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.Settings;

public sealed class ApplyRunsImmediatelyTest : TestCase
{
    public override string Suite => "Settings";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        using var sandbox = new ConfigSandbox("ApplyImmediate");
        var setting = sandbox.Settings.Local("General", "Speed", 5, "Speed.");
        var received = new List<int>();

        setting.Apply(received.Add);

        Assert.SequenceEqual(new[] { 5 }, received, "Values passed to Apply right after subscribing");
        Assert.Equal(5, setting.Value, "Value");
        Assert.Equal(5, setting.LocalValue, "LocalValue");
        Assert.Equal(SettingScope.Local, setting.Scope, "Scope");
        Assert.Equal(sandbox.OwnerId + "/General/Speed", setting.Id, "Id");
        yield break;
    }
}
