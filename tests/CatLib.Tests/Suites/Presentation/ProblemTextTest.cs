using System.Collections.Generic;
using CatLib.Net;
using CatLib.Tests.Framework;
using CatLib.UI;

namespace CatLib.Tests.Suites.Presentation;

public sealed class ProblemTextTest : TestCase
{
    public override string Suite => "Presentation";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var problems = new[]
        {
            new CompatibilityProblem(ProblemKind.ProtocolMismatch, "catlib", "1", "2"),
            new CompatibilityProblem(ProblemKind.GameVersionMismatch, "game", "a", "b"),
            new CompatibilityProblem(ProblemKind.MissingOnClient, "gameplay", "1.0.0", null),
            new CompatibilityProblem(ProblemKind.MissingOnHost, "extra", null, "1.0.0"),
            new CompatibilityProblem(ProblemKind.VersionMismatch, "strict", "2.0.0", "2.1.0")
        };

        Assert.Equal("CatLib version, game version, gameplay missing on the client, +2", ProblemText.Summarize(problems, "en"), "English summary");
        Assert.Equal("strict 2.0.0 vs 2.1.0", ProblemText.Describe(problems[4], "en"), "Version mismatch text");
        Assert.Equal("extra missing on the host", ProblemText.Describe(problems[3], "en"), "Missing on host text");
        Assert.Equal("catlib, game, gameplay, +2", ProblemText.ModsOnly(problems), "Mods only summary");
        Assert.Equal("gameplay", ProblemText.Summarize(new[] { new CompatibilityProblem(ProblemKind.MissingOnHost, "gameplay", null, "1") }, "en").Replace(" missing on the host", string.Empty), "Single problem summary");
        Assert.True(ProblemText.Summarize(problems, "ru").Contains("версия игры"), "Russian summary");
        Assert.Equal("Strict Mod 2.0.0 vs 2.1.0", ProblemText.Describe(problems[4], "en", id => id == "strict" ? "Strict Mod" : null), "Mod name instead of the id");
        Assert.Equal("extra missing on the host", ProblemText.Describe(problems[3], "en", id => null), "Unknown names fall back to the id");
        yield break;
    }
}
