using System.Collections.Generic;
using System.Linq;

namespace BoatTweaks.Logic;

public sealed record LayoutFitResult(List<int> Variants, int FreedCells);

public static class LayoutFit
{
    public static bool Fits(DeckPattern pattern, ISet<(int Row, int Column)> arriving) =>
        arriving == null || !pattern.BlockedCells().Any(arriving.Contains);

    public static List<int> FittingVariants(DeckPattern pattern, IReadOnlyList<ISet<(int Row, int Column)>> arrivingPerVariant) =>
        Enumerable.Range(0, arrivingPerVariant.Count).Where(index => Fits(pattern, arrivingPerVariant[index])).ToList();

    public static LayoutFitResult ClosestVariants(DeckPattern pattern, IReadOnlyList<ISet<(int Row, int Column)>> arrivingPerVariant)
    {
        var pieces = pattern.Pieces();
        var overlaps = arrivingPerVariant
            .Select(arriving => DeckPieces.FreedBy(pieces, arriving))
            .ToList();
        if (overlaps.Count == 0)
        {
            return new LayoutFitResult(new List<int>(), 0);
        }

        var fewest = overlaps.Min(overlap => overlap.Count);
        var best = overlaps.First(overlap => overlap.Count == fewest);
        var variants = Enumerable.Range(0, overlaps.Count).Where(index => overlaps[index].SetEquals(best)).ToList();
        return new LayoutFitResult(variants, fewest);
    }
}
