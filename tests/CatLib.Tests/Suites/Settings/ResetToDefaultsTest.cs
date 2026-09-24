using System.Collections.Generic;
using System.IO;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.Settings;

public sealed class ResetToDefaultsTest : TestCase
{
    public override string Suite => "Settings";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        using var sandbox = new ConfigSandbox("ResetDefaults");
        var speed = sandbox.Settings.Local("General", "Speed", 5, "Speed.");
        var name = sandbox.Settings.Local("General", "Name", "Cat", "Name.");
        var secret = sandbox.Settings.Local("General", "Secret", "hidden", "Secret.").HiddenInMenu();
        var applied = new List<int>();
        speed.Apply(applied.Add);

        speed.Entry.Value = 9;
        name.Entry.Value = "Tabby";
        secret.Entry.Value = "changed";

        var changed = sandbox.Settings.ResetToDefaults(setting => !setting.IsHiddenInMenu);

        Assert.Equal(2, changed, "Settings reset");
        Assert.Equal(5, speed.Value, "Speed after the reset");
        Assert.Equal("Cat", name.Value, "Name after the reset");
        Assert.Equal("changed", secret.Value, "Filtered settings must keep their value");
        Assert.SequenceEqual(new[] { 5, 9, 5 }, applied, "Applied values");
        Assert.Equal(0, sandbox.Settings.ResetToDefaults(setting => !setting.IsHiddenInMenu), "Second reset changes nothing");

        var file = File.ReadAllText(sandbox.FilePath);
        Assert.True(file.Contains("Speed = 5") && file.Contains("Name = Cat"), "The reset must be saved to the file");

        yield return Wait.Seconds(1);

        Assert.Equal(0, sandbox.Reports.Count, "Reloads caused by the reset save");
    }
}
