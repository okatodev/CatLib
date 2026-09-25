using System.Collections.Generic;
using System.Linq;

namespace BoatTweaks;

public static class LayoutFit
{
    public static bool Fits(DeckPattern pattern, ISet<(int Row, int Column)> arriving) =>
        arriving == null || !pattern.BlockedCells().Any(arriving.Contains);

    public static List<int> FittingVariants(DeckPattern pattern, IReadOnlyList<ISet<(int Row, int Column)>> arrivingPerVariant) =>
        Enumerable.Range(0, arrivingPerVariant.Count).Where(index => Fits(pattern, arrivingPerVariant[index])).ToList();
}
