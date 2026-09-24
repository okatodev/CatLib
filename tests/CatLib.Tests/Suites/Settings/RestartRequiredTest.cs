using System.Collections.Generic;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.Settings;

public sealed class RestartRequiredTest : TestCase
{
    public override string Suite => "Settings";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        using var sandbox = new ConfigSandbox("RestartRequired");
        var setting = sandbox.Settings.Local("General", "FastMode", true, "Fast mode.").RequiresRestart();
        var received = new List<bool>();
        setting.Apply(received.Add);

        sandbox.WriteValue("General", "FastMode", "false");

        yield return Wait.Until(() => sandbox.RestartRequired.Count >= 1, 5, "the restart requirement to be reported");

        Assert.True(setting.IsRestartRequired, "IsRestartRequired");
        Assert.True(setting.IsRestartPending, "IsRestartPending after an edit");
        Assert.True(setting.Value, "Value must keep the startup value");
        Assert.False(setting.LocalValue, "LocalValue must follow the file");
        Assert.SequenceEqual(new[] { true }, received, "Applied values");

        sandbox.WriteValue("General", "FastMode", "true");

        yield return Wait.Until(() => sandbox.Reports.Count >= 2, 5, "the revert to be reloaded");

        Assert.False(setting.IsRestartPending, "IsRestartPending after reverting the edit");
        Assert.Equal(1, sandbox.RestartRequired.Count, "Restart notifications");
        Assert.SequenceEqual(new[] { true }, received, "Applied values after the revert");
    }
}
