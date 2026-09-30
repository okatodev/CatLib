using System.Collections.Generic;
using BetterRepair.Logic;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.BetterRepair;

public sealed class CardboardStockTest : TestCase
{
    public override string Suite => "BetterRepair";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var game = new CardboardStock(3);
        Assert.Equal(3, game.Current, "A new stock is full");
        Assert.Equal(0, game.GameIndex(), "Three sheets on the table is game index 0, like a fresh table");
        Assert.Equal(1, game.Consume(1), "A repair uses one sheet");
        Assert.Equal(1, game.GameIndex(), "Two sheets left is game index 1");
        game.Consume(2);
        Assert.True(game.IsEmpty, "Three repairs empty the game's stock");
        Assert.Equal(3, game.GameIndex(), "No sheets is game index 3, the table cannot repair");
        Assert.Equal(0, game.Consume(1), "Nothing is used from an empty stock");
        Assert.Equal(3, game.Refill(RefillMode.Full, 0), "A full refill brings everything back");

        var large = new CardboardStock(10);
        large.Consume(4);
        Assert.Equal(6, large.Current, "Six of ten left");
        Assert.Equal(3, large.VisibleSheets(), "The table shows at most three sheets");
        Assert.Equal(0, large.GameIndex(), "and stays at game index 0 while more than three are left");
        large.Consume(4);
        Assert.Equal(2, large.VisibleSheets(), "Two left shows two sheets");
        Assert.Equal(1, large.GameIndex(), "Two left is game index 1");
        Assert.Equal(3, large.Refill(RefillMode.Amount, 3), "A refill by three adds three");
        Assert.Equal(5, large.Current, "Five of ten after the refill");
        Assert.Equal(5, large.Refill(RefillMode.Amount, 50), "A refill never goes above the stock size");
        Assert.Equal(10, large.Current, "Full after a large refill");

        Assert.True(large.SetMaximum(4), "Lowering the stock size cuts the current stock");
        Assert.Equal(4, large.Current, "Four of four");
        Assert.False(large.SetMaximum(8), "Raising the stock size does not add sheets");
        Assert.Equal(4, large.Current, "Still four, the next day fills it");
        Assert.Equal(CardboardStock.MaximumStock, new CardboardStock(1000).Maximum, "The stock size has an upper limit");
        Assert.Equal(CardboardStock.MinimumStock, new CardboardStock(0).Maximum, "and a lower one");

        var unlimited = new CardboardStock(3) { Unlimited = true };
        unlimited.Consume(1);
        unlimited.Consume(1);
        unlimited.Consume(1);
        unlimited.Consume(1);
        Assert.Equal(3, unlimited.Current, "Unlimited cardboard is never used up");
        Assert.False(unlimited.IsEmpty, "Unlimited cardboard is never empty");
        Assert.Equal(0, unlimited.GameIndex(), "Unlimited keeps all sheets on the table");
        Assert.Equal("unlimited", unlimited.Describe(), "Described as unlimited");

        Assert.False(new CardboardStock(5).SetCurrent(5), "Setting the same value changes nothing");
        var fromSave = new CardboardStock(5);
        fromSave.SetCurrent(99);
        Assert.Equal(5, fromSave.Current, "A stored value above the stock size is cut");
        fromSave.SetCurrent(-3);
        Assert.Equal(0, fromSave.Current, "A negative value becomes empty");

        Assert.True(StockText.TryParse("7", out var parsed) && parsed == 7, "Stock text is parsed");
        Assert.False(StockText.TryParse("-1", out _), "A negative stock is rejected");
        Assert.False(StockText.TryParse("lots", out _), "Text that is not a number is rejected");
        Assert.Equal("12", StockText.Format(12), "Stock text is written with the invariant culture");
        yield break;
    }
}
