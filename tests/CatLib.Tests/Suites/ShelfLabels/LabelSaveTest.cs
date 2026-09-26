using System.Collections.Generic;
using System.IO;
using CatLib.Saves;
using CatLib.Tests.Framework;
using CatLib.Tests.Suites.Saves;
using ShelfLabels;

namespace CatLib.Tests.Suites.ShelfLabels;

public sealed class LabelSaveTest : TestCase
{
    public override string Suite => "ShelfLabels";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        using var sandbox = new SaveSandbox("ShelfLabels");
        var board = new LabelBoard();
        var placements = new PlacementBoard();
        var stands = new StandBoard();
        var store = new LabelStore(sandbox.Session.Register("catlib.shelflabels", "0.1.0", ShelfLabelsPlugin.DataVersion), board, placements, stands, null);
        sandbox.Session.Select(SaveSandbox.SaveFile, false);

        stands.Set(390, true);
        stands.Set(12, false);
        placements.Set(390, Placement.None);
        placements.Set(12, Placement.Below);
        board.Set(new LabelSlot(390, 1), 2);
        board.Set(new LabelSlot(390, 2), 5);
        board.Set(new LabelSlot(390, 3), 8);
        sandbox.Save();
        Assert.True(File.ReadAllText(sandbox.PathOf("catlib.shelflabels")).Contains("label/390/3"), "Pictures are written with the game save");

        var reloaded = new LabelBoard();
        var reloadedPlacements = new PlacementBoard();
        var reloadedStands = new StandBoard();
        new LabelStore(sandbox.Restart().Register("catlib.shelflabels", "0.1.0", ShelfLabelsPlugin.DataVersion), reloaded, reloadedPlacements, reloadedStands, null);
        sandbox.Session.Select(SaveSandbox.SaveFile, false);
        Assert.Equal(8, reloaded.Get(new LabelSlot(390, 3)), "Pictures come back after a restart");
        Assert.Equal(Placement.None, reloadedPlacements.Get(390, Placement.Auto), "A shelf without copies stays without copies");
        Assert.Equal(Placement.Below, reloadedPlacements.Get(12, Placement.Auto), "Placements come back after a restart");
        Assert.True(reloadedStands.IsHidden(390, false), "A hidden stand stays hidden");
        Assert.False(reloadedStands.IsHidden(12, true), "An explicitly shown stand stays shown even when the default hides stands");
        Assert.Equal(8, reloaded.Get(new LabelSlot(390, 3)), "Pictures of a shelf without copies are still kept");
        Assert.Equal(3, reloaded.Count, "All of them");

        reloaded.Set(new LabelSlot(390, 1), 3);
        sandbox.Save();
        var afterHiding = new LabelBoard();
        var afterHidingPlacements = new PlacementBoard();
        new LabelStore(sandbox.Restart().Register("catlib.shelflabels", "0.1.0", ShelfLabelsPlugin.DataVersion), afterHiding, afterHidingPlacements, new StandBoard(), null);
        sandbox.Session.Select(SaveSandbox.SaveFile, false);
        Assert.Equal(5, afterHiding.Get(new LabelSlot(390, 2)), "Slots hidden in the meantime keep their pictures across saves");
        Assert.Equal(8, afterHiding.Get(new LabelSlot(390, 3)), "and come back when they are shown again");
        Assert.Equal(3, afterHiding.Get(new LabelSlot(390, 1)), "while the visible slot has its new picture");

        sandbox.Session.Select("GameSave_Other.bin", false);
        Assert.Equal(0, afterHiding.Count, "Another save starts without these pictures");
        Assert.Equal(0, afterHidingPlacements.Count, "and without these placements");
        yield break;
    }
}
