using System.Collections.Generic;
using CatLib.Net;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.Network;

public sealed class SteamIdsTest : TestCase
{
    public override string Suite => "Network";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        Assert.True(SteamIds.IsIndividual(76561199689020186UL), "A real Steam user id from the sessions");
        Assert.True(SteamIds.IsIndividual(76561199592750406UL), "The second real Steam user id");
        Assert.False(SteamIds.IsIndividual(0UL), "Zero");
        Assert.False(SteamIds.IsIndividual(ulong.MaxValue), "The game's placeholder while not connected");
        Assert.False(SteamIds.IsIndividual(SteamIds.IndividualBase), "The base itself is account zero");
        Assert.False(SteamIds.IsIndividual(12345UL), "A small number such as a network identifier");
        yield break;
    }
}
