using System.Collections.Generic;
using System.Linq;
using CatLib.Tests.Framework;
using ShelfLabels;

namespace CatLib.Tests.Suites.ShelfLabels;

public sealed class LabelBoardTest : TestCase
{
    public override string Suite => "ShelfLabels";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        Assert.True(LabelSlot.TryParse("390/2", out var slot) && slot == new LabelSlot(390, 2), "Slot text");
        Assert.False(LabelSlot.TryParse("390/4", out _), "At most three extra labels");
        Assert.False(LabelSlot.TryParse("390/0", out _), "Slot 0 is the game's own label and is never addressed");
        Assert.False(LabelSlot.TryParse("-1/1", out _), "Negative ids are refused");
        Assert.False(LabelSlot.TryParse("390/1/2", out _), "Extra parts are refused");
        Assert.Equal("label/390/2", slot.SaveKey, "Save key");
        Assert.True(LabelSlot.TryParseSaveKey("label/390/2", out var fromKey) && fromKey == slot, "Save key round trip");
        Assert.False(LabelSlot.TryParseSaveKey("other/390/2", out _), "Foreign keys are skipped");

        var board = new LabelBoard();
        var changes = new List<IReadOnlyList<LabelSlot>>();
        board.Changed += changes.Add;
        Assert.Equal(0, board.Get(slot), "An unknown slot shows the empty picture");
        Assert.Equal(1, board.Step(slot, 1, 10), "Forward");
        Assert.Equal(0, board.Step(slot, -1, 10), "Back");
        Assert.Equal(9, board.Step(slot, -1, 10), "Back from the empty picture wraps to the last one");
        Assert.Equal(0, board.Step(slot, 1, 10), "Forward from the last one wraps to the empty picture");
        Assert.Equal(-1, board.Step(slot, 1, 0), "Unknown picture count changes nothing");
        Assert.Equal(4, changes.Count, "Every change is reported");
        Assert.False(board.Set(slot, 0), "Setting the same value is not a change");
        Assert.False(board.Set(slot, 300), "Out of range values are refused");
        Assert.True(board.Has(slot), "A slot set back to empty is still stored");

        board.Set(new LabelSlot(12, 3), 7);
        board.Set(new LabelSlot(390, 1), 2);
        var text = LabelBoard.Format(board.Entries);
        Assert.Equal("12/3=7;390/1=2;390/2=0", text, "Format is sorted");
        var parsed = LabelBoard.Parse(text + ";garbage;390/9=1;5/1=x;5/1=999");
        Assert.Equal(3, parsed.Count, "Malformed entries are skipped");

        var large = new LabelBoard();
        large.Apply(Enumerable.Range(1, 900).SelectMany(id => Enumerable.Range(1, 3).Select(number => new KeyValuePair<LabelSlot, int>(new LabelSlot(id, number), id % 10))));
        var chunks = large.Chunks(LabelSync.ChunkBytes);
        Assert.True(chunks.Count > 1, "A large board is split into several messages");
        Assert.True(chunks.All(chunk => System.Text.Encoding.UTF8.GetByteCount(chunk) <= LabelSync.ChunkBytes), "Every chunk fits");
        Assert.Equal(2700, chunks.Sum(chunk => LabelBoard.Parse(chunk).Count), "No entry is lost");

        board.Clear();
        Assert.Equal(0, board.Count, "Clear");
        yield break;
    }
}
