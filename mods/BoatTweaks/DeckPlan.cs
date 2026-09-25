using System;
using System.Globalization;

namespace BoatTweaks;

public enum DeckPlanKind
{
    Game,
    Empty,
    Pattern,
    Generated
}

public sealed record DeckPlan(DeckPlanKind Kind, string Name = null, DeckPattern Pattern = null, int Seed = 0, float Density = 0f, bool EdgesOnly = false)
{
    public const char Separator = '|';

    public static DeckPlan Game { get; } = new(DeckPlanKind.Game);

    public static DeckPlan Empty { get; } = new(DeckPlanKind.Empty);

    public bool ClearsGameDeck => Kind != DeckPlanKind.Game;

    public bool BuildsDeck => Kind == DeckPlanKind.Pattern || Kind == DeckPlanKind.Generated;

    public static DeckPlan FromPattern(string name, DeckPattern pattern) => new(DeckPlanKind.Pattern, Clean(name), pattern);

    public static DeckPlan FromGenerator(int seed, float density, bool edgesOnly) => new(DeckPlanKind.Generated, "generated", null, seed, density, edgesOnly);

    public string Encode() => Kind switch
    {
        DeckPlanKind.Empty => "empty",
        DeckPlanKind.Pattern => string.Join(Separator.ToString(), "pattern", Clean(Name), Pattern.ToString()),
        DeckPlanKind.Generated => string.Join(Separator.ToString(), "generated",
            Seed.ToString(CultureInfo.InvariantCulture), Density.ToString("0.###", CultureInfo.InvariantCulture), EdgesOnly ? "1" : "0"),
        _ => "game"
    };

    public static DeckPlan Decode(string text)
    {
        var parts = (text ?? string.Empty).Split(Separator);
        switch (parts[0])
        {
            case "empty":
                return Empty;
            case "pattern" when parts.Length == 3 && DeckPattern.TryParse(parts[2].Replace('/', '\n'), out var pattern, out _):
                return FromPattern(parts[1], pattern);
            case "generated" when parts.Length == 4
                                  && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var seed)
                                  && float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var density):
                return FromGenerator(seed, density, parts[3] == "1");
            default:
                return Game;
        }
    }

    public string Describe() => Kind switch
    {
        DeckPlanKind.Pattern => $"layout \"{Name}\" ({Pattern.BlockedCount} blocked cell(s))",
        DeckPlanKind.Generated => $"generated deck (seed {Seed}, density {Density:0.##}, {(EdgesOnly ? "edges only" : "anywhere")})",
        _ => Kind.ToString()
    };

    private static string Clean(string name) => string.IsNullOrWhiteSpace(name) ? "layout" : name.Replace(Separator, '_').Trim();
}
