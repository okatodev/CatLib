using System;

namespace BetterRepair.Logic;

public sealed class CardboardStock
{
    public const int SheetsOnTable = 3;
    public const int MinimumStock = 1;
    public const int MaximumStock = 99;

    public CardboardStock(int maximum)
    {
        Maximum = Clamp(maximum);
        Current = Maximum;
    }

    public int Maximum { get; private set; }

    public int Current { get; private set; }

    public bool Unlimited { get; set; }

    public bool IsEmpty => !Unlimited && Current == 0;

    public static int Clamp(int maximum) => Math.Max(MinimumStock, Math.Min(MaximumStock, maximum));

    public bool SetMaximum(int maximum)
    {
        var clamped = Clamp(maximum);
        var before = Current;
        Maximum = clamped;
        Current = Math.Min(Current, Maximum);
        return Current != before;
    }

    public bool SetCurrent(int current)
    {
        var value = Math.Max(0, Math.Min(Maximum, current));
        if (value == Current)
        {
            return false;
        }

        Current = value;
        return true;
    }

    public int Consume(int sheets)
    {
        if (Unlimited || sheets <= 0)
        {
            return 0;
        }

        var used = Math.Min(sheets, Current);
        Current -= used;
        return used;
    }

    public int Refill(RefillMode mode, int amount)
    {
        var before = Current;
        Current = mode == RefillMode.Full ? Maximum : Math.Min(Maximum, Current + Math.Max(0, amount));
        return Current - before;
    }

    public int VisibleSheets(int capacity = SheetsOnTable)
    {
        var sheets = Math.Max(0, capacity);
        return Unlimited ? sheets : Math.Min(Current, sheets);
    }

    public int GameIndex(int capacity = SheetsOnTable) => Math.Max(0, capacity) - VisibleSheets(capacity);

    public string Describe() => Unlimited ? "unlimited" : $"{Current} of {Maximum}";
}
