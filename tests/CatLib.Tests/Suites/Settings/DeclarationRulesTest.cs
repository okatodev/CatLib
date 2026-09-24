using System;
using System.Collections.Generic;
using CatLib.Config;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.Settings;

public sealed class DeclarationRulesTest : TestCase
{
    public override string Suite => "Settings";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        using var sandbox = new ConfigSandbox("Declaration");
        var setting = sandbox.Settings.Local("General", "Speed", 1, "Speed.");

        Assert.True(ReferenceEquals(sandbox.Settings, CatSettings.For(sandbox.Config, sandbox.OwnerId)), "Same file must return the same CatSettings");
        Assert.True(ReferenceEquals(setting, sandbox.Settings.Local("General", "Speed", 1, "Speed.")), "Same declaration must return the same setting");
        Assert.Throws<InvalidOperationException>(() => sandbox.Settings.Local("General", "Speed", "text", "Speed."), "Redeclaring with another type");
        Assert.Throws<InvalidOperationException>(() => sandbox.Settings.Session("General", "Speed", 1, "Speed."), "Redeclaring with another scope");
        Assert.Throws<InvalidOperationException>(() => CatSettings.For(sandbox.Config, sandbox.OwnerId + ".other"), "Claiming the file for another owner");
        Assert.Equal(1, sandbox.Settings.Settings.Count, "Declared settings");
        yield break;
    }
}
