using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BoatTweaks;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.BoatTweaks;

public sealed class DeckDeciderTest : TestCase
{
    public override string Suite => "BoatTweaks";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        DeckPattern.TryParse("#.\n..", out var a, out _);
        DeckPattern.TryParse(".#\n..", out var b, out _);
        DeckPattern.TryParse("..\n#.", out var c, out _);
        var layouts = new List<(string Name, DeckPattern Pattern)> { ("alpha", a), ("beta", b), ("gamma", c) };
        var none = new List<(string Name, DeckPattern Pattern)>();
        var random = new Random(3);

        DeckDecision Decide(LayoutMode mode, CustomOrder order = CustomOrder.Random, string file = null, string last = null, int sequence = 0, bool avoid = true, IReadOnlyList<(string, DeckPattern)> list = null) =>
            DeckDecider.Decide(new DeckChoices { Mode = mode, Order = order, FixedFile = file, AvoidRepeats = avoid, Density = 0.2f, EdgesOnly = true, GameWeight = 1, EmptyWeight = 1, CustomWeight = 1, GeneratedWeight = 1 },
                list ?? layouts, last, sequence, random);

        foreach (var mode in new[] { LayoutMode.Game, LayoutMode.GameVariants, LayoutMode.FixedVariant })
        {
            Assert.Equal(DeckPlanKind.Game, Decide(mode).Plan.Kind, mode + " keeps the deck of the chosen game variant");
        }

        Assert.Equal(DeckPlanKind.Empty, Decide(LayoutMode.Empty).Plan.Kind, "Empty");
        var generated = Decide(LayoutMode.Generated).Plan;
        Assert.True(generated.Kind == DeckPlanKind.Generated && generated.Seed > 0 && generated.EdgesOnly, "Generated plans carry a seed and the options");

        Assert.Equal("beta", Decide(LayoutMode.Custom, CustomOrder.Fixed, "BETA.txt").Plan.Name, "Always one file, by name with any case and extension");
        var missing = Decide(LayoutMode.Custom, CustomOrder.Fixed, "delta");
        Assert.True(missing.Plan.Kind == DeckPlanKind.Game && missing.Note.Contains("delta"), "A missing file keeps the game deck and says why");
        var empty = Decide(LayoutMode.Custom, list: none);
        Assert.True(empty.Plan.Kind == DeckPlanKind.Game && empty.Note != null, "No layouts keeps the game deck and says why");

        var names = new List<string>();
        var sequence = 0;
        for (var boat = 0; boat < 4; boat++)
        {
            var decision = Decide(LayoutMode.Custom, CustomOrder.Sequence, sequence: sequence);
            names.Add(decision.Plan.Name);
            sequence = decision.NextSequence;
        }

        Assert.SequenceEqual(new[] { "alpha", "beta", "gamma", "alpha" }, names, "One after another, then from the start");

        var last = "alpha";
        for (var boat = 0; boat < 100; boat++)
        {
            var name = Decide(LayoutMode.Custom, last: last).Plan.Name;
            Assert.True(name != last, "Random own layouts do not repeat in a row");
            last = name;
        }

        var sources = Enumerable.Range(0, 400).Select(_ => Decide(LayoutMode.Mixed).Plan.Kind).GroupBy(kind => kind).ToDictionary(group => group.Key, group => group.Count());
        context.Note("Mixed with equal weights: " + string.Join(", ", sources.Select(pair => $"{pair.Key} {pair.Value}")));
        Assert.Equal(4, sources.Count, "Every source is used with equal weights");
        Assert.True(sources.Values.All(count => count > 60), "Equal weights give similar shares");
        Assert.True(Enumerable.Range(0, 100).All(_ => Decide(LayoutMode.Mixed, list: none).Plan.Kind != DeckPlanKind.Pattern), "Without layouts the Mixed mode never picks them");
        Assert.Equal(LayoutMode.Game, DeckDecider.PickSource(new DeckChoices(), true, random), "All weights at zero keep the game deck");

        var directory = Path.Combine(Path.GetTempPath(), "BoatLayouts_" + Guid.NewGuid().ToString("N"));
        try
        {
            var library = new PatternLibrary(directory);
            Assert.Equal(0, library.Load(null).Count, "A new folder is created empty");
            var saved = library.Save(a, new[] { "Boat Tweaks layout", "#.#" }, new DateTime(2026, 9, 25, 23, 59, 1));
            var second = library.Save(b, null, new DateTime(2026, 9, 25, 23, 59, 1));
            File.WriteAllText(Path.Combine(directory, "broken.txt"), "##\n#");
            var warnings = new List<string>();
            var loaded = library.Load(warnings.Add);
            Assert.Equal("deck_20260925_235901", saved, "Saved file name");
            Assert.Equal("deck_20260925_235901_2", second, "A second save in the same second gets a suffix");
            Assert.SequenceEqual(new[] { "deck_20260925_235901", "deck_20260925_235901_2" }, loaded.Select(layout => layout.Name), "Both saved layouts load back");
            Assert.SequenceEqual(a.RowTexts(), loaded[0].Pattern.RowTexts(), "A note that looks like a grid row is not written");
            Assert.True(warnings.Count == 1 && warnings[0].Contains("broken.txt"), "Broken files are skipped with a warning");
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }

        yield break;
    }
}
