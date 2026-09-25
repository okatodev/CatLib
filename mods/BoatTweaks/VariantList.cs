using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace BoatTweaks;

public static class VariantList
{
    public static IReadOnlyList<int> Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<int>();
        }

        return text
            .Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(part => int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : 0)
            .Where(number => number > 0)
            .Distinct()
            .OrderBy(number => number)
            .ToList();
    }
}
