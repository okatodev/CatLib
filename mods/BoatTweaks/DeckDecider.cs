using System;
using System.Collections.Generic;
using System.Linq;

namespace BoatTweaks;

public sealed class DeckChoices
{
    public LayoutMode Mode { get; init; }

    public CustomOrder Order { get; init; }

    public string FixedFile { get; init; }

    public bool AvoidRepeats { get; init; }

    public float Density { get; init; }

    public bool EdgesOnly { get; init; }

    public int GameWeight { get; init; }

    public int EmptyWeight { get; init; }

    public int CustomWeight { get; init; }

    public int GeneratedWeight { get; init; }
}

public sealed record DeckDecision(DeckPlan Plan, int NextSequence, string Note);

public static class DeckDecider
{
    public static DeckDecision Decide(DeckChoices choices, IReadOnlyList<(string Name, DeckPattern Pattern)> layouts, string lastLayout, int sequence, Random random)
    {
        var mode = choices.Mode;
        if (mode == LayoutMode.Mixed)
        {
            mode = PickSource(choices, layouts.Count > 0, random);
        }

        return mode switch
        {
            LayoutMode.Empty => new DeckDecision(DeckPlan.Empty, sequence, null),
            LayoutMode.Custom => ChooseLayout(choices, layouts, lastLayout, sequence, random),
            LayoutMode.Generated => new DeckDecision(DeckPlan.FromGenerator(random.Next(1, int.MaxValue), choices.Density, choices.EdgesOnly), sequence, null),
            _ => new DeckDecision(DeckPlan.Game, sequence, null)
        };
    }

    public static LayoutMode PickSource(DeckChoices choices, bool hasLayouts, Random random)
    {
        var sources = new List<(LayoutMode Mode, int Weight)>
        {
            (LayoutMode.Game, Math.Max(0, choices.GameWeight)),
            (LayoutMode.Empty, Math.Max(0, choices.EmptyWeight)),
            (LayoutMode.Custom, hasLayouts ? Math.Max(0, choices.CustomWeight) : 0),
            (LayoutMode.Generated, Math.Max(0, choices.GeneratedWeight))
        };

        var total = sources.Sum(source => source.Weight);
        if (total == 0)
        {
            return LayoutMode.Game;
        }

        var roll = random.Next(total);
        foreach (var (mode, weight) in sources)
        {
            if (roll < weight)
            {
                return mode;
            }

            roll -= weight;
        }

        return LayoutMode.Game;
    }

    private static DeckDecision ChooseLayout(DeckChoices choices, IReadOnlyList<(string Name, DeckPattern Pattern)> layouts, string lastLayout, int sequence, Random random)
    {
        if (layouts.Count == 0)
        {
            return new DeckDecision(DeckPlan.Game, sequence, "no own layouts were found, the game deck is kept");
        }

        switch (choices.Order)
        {
            case CustomOrder.Fixed:
                var wanted = (choices.FixedFile ?? string.Empty).Trim();
                if (wanted.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                {
                    wanted = wanted.Substring(0, wanted.Length - 4);
                }

                var match = layouts.FirstOrDefault(layout => string.Equals(layout.Name, wanted, StringComparison.OrdinalIgnoreCase));
                return match.Pattern == null
                    ? new DeckDecision(DeckPlan.Game, sequence, $"the layout \"{wanted}\" was not found, the game deck is kept")
                    : new DeckDecision(DeckPlan.FromPattern(match.Name, match.Pattern), sequence, null);
            case CustomOrder.Sequence:
                var index = ((sequence % layouts.Count) + layouts.Count) % layouts.Count;
                return new DeckDecision(DeckPlan.FromPattern(layouts[index].Name, layouts[index].Pattern), sequence + 1, null);
            default:
                var candidates = layouts.ToList();
                if (choices.AvoidRepeats && candidates.Count > 1)
                {
                    candidates.RemoveAll(layout => string.Equals(layout.Name, lastLayout, StringComparison.OrdinalIgnoreCase));
                }

                var chosen = candidates[random.Next(candidates.Count)];
                return new DeckDecision(DeckPlan.FromPattern(chosen.Name, chosen.Pattern), sequence, null);
        }
    }
}
