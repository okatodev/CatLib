using System.Collections.Generic;
using CatLib.Saves;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.Saves;

public sealed class SaveSwitchTest : TestCase
{
    public override string Suite => "Saves";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        using var sandbox = new SaveSandbox("Switch");
        var mod = sandbox.Session.Register("test.labels", "0.1.0", 1);
        sandbox.Session.Select("GameSave_A.bin", false);
        mod.Set("value", 1);
        sandbox.Save();
        mod.Set("value", 2);

        sandbox.Session.Select("GameSave_B.bin", false);
        Assert.Equal("GameSave_B", mod.SaveName, "Attached to the new save");
        Assert.False(mod.Has("value"), "Data of another save is not visible");
        mod.Set("other", 1);

        sandbox.Session.Close();
        Assert.Equal(ModSaveState.NoSave, mod.State, "Detached in the main menu");
        Assert.False(mod.Set("value", 3), "Writing without a save is refused");

        sandbox.Session.Select("GameSave_A.bin", false);
        Assert.Equal(1, mod.Get("value", 0), "A change made after the last game save is lost with the game's progress");
        Assert.False(mod.Has("other"), "Unsaved data of the other save is gone");

        sandbox.Session.Select(".bin", false);
        Assert.False(sandbox.Session.IsAttached, "Nothing is attached for an empty save name");
        yield break;
    }
}
