using System.Collections.Generic;
using CatLib.Config;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.Settings;

public sealed class SessionScopeTest : TestCase
{
    public override string Suite => "Settings";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        using var sandbox = new ConfigSandbox("SessionScope");
        var setting = sandbox.Settings.Session("Gameplay", "CarryLimit", 3, "Carry limit.");
        var received = new List<int>();
        setting.Apply(received.Add);

        sandbox.WriteValue("Gameplay", "CarryLimit", "5");

        yield return Wait.Until(() => received.Count >= 2, 5, "the session setting edit to be applied");

        context.Note("Without a network session a session setting behaves like a local one");
        Assert.Equal(SettingScope.Session, setting.Scope, "Scope");
        Assert.SequenceEqual(new[] { 3, 5 }, received, "Applied values");
        Assert.Equal(5, setting.Value, "Value");
        Assert.Equal(5, setting.LocalValue, "LocalValue");
    }
}
