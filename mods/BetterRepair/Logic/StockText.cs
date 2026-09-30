using System.Globalization;

namespace BetterRepair.Logic;

public static class StockText
{
    public static string Format(int current) => current.ToString(CultureInfo.InvariantCulture);

    public static bool TryParse(string text, out int current) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out current) && current >= 0 && current <= CardboardStock.MaximumStock;
}
