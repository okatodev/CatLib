namespace BoatTweaks;

public readonly struct LayoutPlan
{
    private LayoutPlan(LayoutPlanKind kind, int variantIndex)
    {
        Kind = kind;
        VariantIndex = variantIndex;
    }

    public LayoutPlanKind Kind { get; }

    public int VariantIndex { get; }

    public static LayoutPlan KeepGame => new(LayoutPlanKind.KeepGame, -1);

    public static LayoutPlan Empty => new(LayoutPlanKind.Empty, -1);

    public static LayoutPlan Variant(int index) => new(LayoutPlanKind.Variant, index);

    public override string ToString() => Kind == LayoutPlanKind.Variant ? "variant " + (VariantIndex + 1) : Kind.ToString();
}

public enum LayoutPlanKind
{
    KeepGame,
    Empty,
    Variant
}
