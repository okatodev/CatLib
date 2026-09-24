using System.Collections.Generic;
using System.Linq;
using CatLib.Net;
using CatLib.Tests.Framework;
using static CatLib.Tests.Suites.Network.NetworkFixture;

namespace CatLib.Tests.Suites.Network;

public sealed class CompatibilityTest : TestCase
{
    public override string Suite => "Network";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var host = Identity(
            Mod("gameplay", "1.2.0", SessionPolicy.RequiredOnAll),
            Mod("strict", "2.0.0", SessionPolicy.RequiredOnAll, VersionRule.Exact),
            Mod("server", "1.0.0", SessionPolicy.HostOnly),
            Mod("visual", "1.0.0", SessionPolicy.ClientOnly));

        var same = Identity(
            Mod("gameplay", "1.2.5", SessionPolicy.RequiredOnAll),
            Mod("strict", "2.0.0", SessionPolicy.RequiredOnAll, VersionRule.Exact));
        Assert.Equal(0, Compatibility.Compare(host, same).Count, "Compatible client without host-only and client-only mods");

        var client = Identity(
            Mod("gameplay", "1.3.0", SessionPolicy.RequiredOnAll),
            Mod("strict", "2.0.1", SessionPolicy.RequiredOnAll, VersionRule.SameMajor),
            Mod("extra", "1.0.0", SessionPolicy.RequiredOnAll),
            Mod("cosmetic", "1.0.0", SessionPolicy.ClientOnly),
            Mod("server", "0.1.0", SessionPolicy.HostOnly)) with { GameVersion = "CMC 1.02" };

        var problems = Compatibility.Compare(host, client).Select(problem => problem.Kind + ":" + problem.Subject).ToList();
        context.Note("Problems: " + string.Join(", ", problems));
        Assert.SequenceEqual(new[]
        {
            "GameVersionMismatch:game",
            "MissingOnHost:extra",
            "VersionMismatch:gameplay",
            "VersionMismatch:strict"
        }, problems, "Problems found for a mismatched client");

        var missing = Identity(Mod("gameplay", "1.2.0", SessionPolicy.RequiredOnAll));
        Assert.SequenceEqual(new[] { "MissingOnClient:strict" }, Compatibility.Compare(host, missing).Select(problem => problem.Kind + ":" + problem.Subject), "Required mod missing on the client");

        Assert.SequenceEqual(new[] { "MissingOnClient:gameplay", "MissingOnClient:strict" },
            Compatibility.Compare(host, null).Select(problem => problem.Kind + ":" + problem.Subject), "Client without CatLib");
        Assert.SequenceEqual(new[] { "MissingOnHost:gameplay", "MissingOnHost:strict" },
            Compatibility.CompareWithoutHost(host).Select(problem => problem.Kind + ":" + problem.Subject), "Host without CatLib");
        Assert.Equal(0, Compatibility.CompareWithoutHost(Identity(Mod("visual", "1.0.0", SessionPolicy.ClientOnly))).Count, "Client-only mods never block");
        yield break;
    }
}
