using System.Collections.Generic;
using System.IO;
using CatLib.Saves;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.Saves;

public sealed class RoundTripTest : TestCase
{
    public override string Suite => "Saves";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        using var sandbox = new SaveSandbox("RoundTrip");
        var mod = sandbox.Session.Register("test.labels", "0.1.0", 1);
        Assert.Equal(ModSaveState.NoSave, mod.State, "No save before the game selects one");
        Assert.False(mod.Set("a", 1), "Writing without a save is refused");

        sandbox.Session.Select(SaveSandbox.SaveFile, false);
        Assert.Equal(ModSaveState.Ready, mod.State, "Ready after selection");
        Assert.Equal(SaveReadStatus.Missing, mod.LastRead, "Nothing stored yet");
        Assert.True(mod.Set("count", 3), "Set int");
        Assert.True(mod.Set("name", "Кот"), "Set text");
        Assert.True(mod.Set("slots", new Dictionary<string, int> { ["390/1"] = 4, ["390/2"] = 0 }), "Set dictionary");
        sandbox.Save();
        Assert.True(File.Exists(sandbox.PathOf("test.labels")), "File written after the game saved");
        Assert.False(mod.HasUnsavedChanges, "Nothing left to write");

        var reloaded = sandbox.Restart().Register("test.labels", "0.1.0", 1);
        var loadedEvents = 0;
        reloaded.Loaded += () => loadedEvents++;
        sandbox.Session.Select(SaveSandbox.SaveFile, false);
        Assert.Equal(1, loadedEvents, "Loaded is raised once");
        Assert.Equal(SaveReadStatus.Loaded, reloaded.LastRead, "Read status");
        Assert.Equal(1, reloaded.StoredDataVersion, "Stored data version");
        Assert.Equal(3, reloaded.Get("count", 0), "Int value");
        Assert.Equal("Кот", reloaded.Get<string>("name"), "Text value");
        Assert.Equal(4, reloaded.Get<Dictionary<string, int>>("slots")["390/1"], "Dictionary value");
        Assert.Equal(7, reloaded.Get("missing", 7), "Fallback for a missing key");
        Assert.Equal(-1, reloaded.Get("name", -1), "Fallback for a value of another type");
        Assert.SequenceEqual(new[] { "count", "name", "slots" }, reloaded.Keys, "Keys");
        Assert.False(reloaded.HasUnsavedChanges, "Loaded data is not a change");
        Assert.True(reloaded.Set("count", 3), "Setting the same value");
        Assert.False(reloaded.HasUnsavedChanges, "The same value is not a change");
        yield break;
    }
}
