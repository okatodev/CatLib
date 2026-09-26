using System.Collections.Generic;
using CatLib.Tests.Framework;
using ShelfLabels;

namespace CatLib.Tests.Suites.ShelfLabels;

public sealed class StandBoardTest : TestCase
{
    public override string Suite => "ShelfLabels";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var board = new StandBoard();
        Assert.True(board.IsHidden(390, true), "Without a choice the default applies");
        Assert.False(board.IsHidden(390, false), "Either way");
        Assert.True(board.Set(390, true), "Hide");
        Assert.False(board.Set(390, true), "Same choice is not a change");
        Assert.True(board.Set(12, false), "An explicit shown stand is stored");
        Assert.False(board.Set(0, true), "Invalid ids are refused");
        Assert.Equal("stand/390", StandBoard.SaveKey(390), "Save key");
        Assert.True(StandBoard.TryParseSaveKey("stand/12", out var id) && id == 12, "Save key round trip");
        Assert.False(StandBoard.TryParseSaveKey("place/12", out _), "Placement keys are not stands");
        Assert.Equal(2, StandBoard.Parse("12=0;390=1;5=2;x=1;7").Count, "Malformed entries are skipped");
        Assert.Equal("12=0;390=1", string.Join(";", board.Chunks(100)), "Chunks");
        yield break;
    }
}
