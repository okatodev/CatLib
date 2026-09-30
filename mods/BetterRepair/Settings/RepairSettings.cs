using System;
using BepInEx.Configuration;
using BetterRepair.Logic;
using CatLib.Config;

namespace BetterRepair.Settings;

public sealed class RepairSettings
{
    public RepairSettings(CatSettings settings, Action changed)
    {
        var stock = new AcceptableValueRange<int>(CardboardStock.MinimumStock, CardboardStock.MaximumStock);

        Unlimited = settings.Session("Cardboard", "Unlimited", false,
            "The repair table never runs out of cardboard. The host decides for everyone.");
        Stock = settings.Session("Cardboard", "Stock", CardboardStock.SheetsOnTable,
            "How many sheets of cardboard the repair table holds. The table shows up to three, the rest waits in stock. 3 is the game's amount. The host decides for everyone.", stock);
        Refill = settings.Session("Cardboard", "Refill", RefillMode.Full,
            "What a new day brings: the full stock, like in the game, or a set number of sheets. The host decides for everyone.");
        RefillAmount = settings.Session("Cardboard", "RefillAmount", CardboardStock.SheetsOnTable,
            "Sheets added every new day when a set number is chosen above. The stock never grows above its size. The host decides for everyone.", stock);
        Messages = settings.Local("Messages", "Enabled", true,
            "Shows how much cardboard is left after a repair and how much a new day brought. Only for you.");

        Unlimited.Changed += (_, _) => changed();
        Stock.Changed += (_, _) => changed();
        Refill.Changed += (_, _) => changed();
        RefillAmount.Changed += (_, _) => changed();
    }

    public Setting<bool> Unlimited { get; }

    public Setting<int> Stock { get; }

    public Setting<RefillMode> Refill { get; }

    public Setting<int> RefillAmount { get; }

    public Setting<bool> Messages { get; }
}
