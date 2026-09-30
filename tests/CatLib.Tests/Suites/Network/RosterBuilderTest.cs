using System.Collections.Generic;
using System.Linq;
using CatLib.Net;
using CatLib.Tests.Framework;
using static CatLib.Tests.Suites.Network.NetworkFixture;

namespace CatLib.Tests.Suites.Network;

public sealed class RosterBuilderTest : TestCase
{
    public override string Suite => "Network";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var host = Identity(Mod("boat", "0.2.0", SessionPolicy.RequiredOnAll), Mod("labels", "0.1.0", SessionPolicy.RequiredOnAll), Mod("look", "1.0.0", SessionPolicy.ClientOnly));
        var same = Identity(Mod("boat", "0.2.3", SessionPolicy.RequiredOnAll), Mod("labels", "0.1.0", SessionPolicy.RequiredOnAll));

        var alone = RosterBuilder.Build(host, HostId, "Host", IncompatiblePlayerAction.Warn, new RosterPeer[0]);
        Assert.SequenceEqual(new[] { "boat", "labels" }, alone.ActiveMods, "Alone every required mod is active");
        Assert.True(alone.Players.Single().IsHost, "The host is listed");
        Assert.Equal(3, alone.Players.Single().ModCount, "The host lists all its mods");
        Assert.Equal(ModMark.Local, alone.Players.Single().Find("look").Mark, "Mods that are not checked are marked local");

        var friends = RosterBuilder.Build(host, HostId, "Host", IncompatiblePlayerAction.Warn, new[]
        {
            new RosterPeer(ClientId, "Friend", RosterPeerState.Evaluated, same, Compatibility.Compare(host, same), false)
        });
        Assert.SequenceEqual(new[] { "boat", "labels" }, friends.ActiveMods, "A patch version difference keeps mods active");
        Assert.Equal(RosterStatus.Compatible, friends.Find(ClientId).Status, "The friend is compatible");
        Assert.Equal(ModMark.Same, friends.Find(ClientId).Find("boat").Mark, "Same mod");
        Assert.Equal("0.2.3", friends.Find(ClientId).Find("boat").Version, "The friend's own version is listed");

        var older = Identity(Mod("boat", "0.1.0", SessionPolicy.RequiredOnAll), Mod("extra", "1.0.0", SessionPolicy.RequiredOnAll));
        var mixed = RosterBuilder.Build(host, HostId, "Host", IncompatiblePlayerAction.Warn, new[]
        {
            new RosterPeer(ClientId, "Older", RosterPeerState.Evaluated, older, Compatibility.Compare(host, older), false),
            new RosterPeer(ClientId + 1, "Joining", RosterPeerState.Checking, null, null, false)
        });
        var olderEntry = mixed.Find(ClientId);
        Assert.Equal(RosterStatus.Limited, olderEntry.Status, "A player with other mods stays limited");
        Assert.Equal(ModMark.OtherVersion, olderEntry.Find("boat").Mark, "Other version");
        Assert.Equal("0.2.0", olderEntry.Find("boat").HostVersion, "The host's version is kept for advice");
        Assert.Equal(ModMark.Missing, olderEntry.Find("labels").Mark, "Missing mod");
        Assert.Equal(ModMark.NotOnHost, olderEntry.Find("extra").Mark, "A required mod the host does not have");
        Assert.Equal(2, olderEntry.ModCount, "Missing mods are not counted as the player's mods");
        Assert.Equal(3, olderEntry.ProblemCount, "Three problems");
        Assert.Equal(0, mixed.ActiveMods.Count, "Mods the older player does not have in a compatible version are paused");
        Assert.Equal(RosterStatus.Checking, mixed.Find(ClientId + 1).Status, "A joining player is being checked");

        var checking = RosterBuilder.Build(host, HostId, "Host", IncompatiblePlayerAction.Warn, new[]
        {
            new RosterPeer(ClientId, "Joining", RosterPeerState.Checking, null, null, false)
        });
        Assert.Equal(2, checking.ActiveMods.Count, "A player still being checked does not pause anything yet");

        var leaving = RosterBuilder.Build(host, HostId, "Host", IncompatiblePlayerAction.Disconnect, new[]
        {
            new RosterPeer(ClientId, "Older", RosterPeerState.Evaluated, older, Compatibility.Compare(host, older), true)
        });
        Assert.Equal(RosterStatus.Leaving, leaving.Find(ClientId).Status, "A disconnected player is leaving");
        Assert.Equal(2, leaving.ActiveMods.Count, "A leaving player does not pause mods");
        Assert.Equal(IncompatiblePlayerAction.Disconnect, leaving.Policy, "The policy is part of the roster");

        var vanilla = RosterBuilder.Build(host, HostId, "Host", IncompatiblePlayerAction.Warn, new[]
        {
            new RosterPeer(ClientId, "Vanilla", RosterPeerState.WithoutCatLib, null, Compatibility.Compare(host, null), false)
        });
        Assert.Equal(RosterStatus.WithoutCatLib, vanilla.Find(ClientId).Status, "A player without CatLib");
        Assert.Equal(0, vanilla.ActiveMods.Count, "A player without CatLib pauses every required mod");
        Assert.True(vanilla.Find(ClientId).Mods.All(mod => mod.Mark == ModMark.Missing), "Required mods are listed as missing");

        var protocol = new[] { new CompatibilityProblem(ProblemKind.ProtocolMismatch, "catlib", "5", "4") };
        var oldCatLib = RosterBuilder.Build(host, HostId, "Host", IncompatiblePlayerAction.Warn, new[]
        {
            new RosterPeer(ClientId, "Old CatLib", RosterPeerState.Evaluated, null, protocol, false)
        });
        Assert.Equal(RosterStatus.Limited, oldCatLib.Find(ClientId).Status, "A player with another CatLib protocol is limited");
        Assert.Equal(0, oldCatLib.ActiveMods.Count, "and pauses every required mod");

        var self = Identity(Mod("boat", "0.2.0", SessionPolicy.RequiredOnAll), Mod("look", "1.0.0", SessionPolicy.ClientOnly));
        var noHost = RosterBuilder.WithoutHostCatLib(self, ClientId, "Me", HostId, "Vanilla host", new CompatibilityProblem[0]);
        Assert.Equal(RosterStatus.WithoutCatLib, noHost.HostEntry.Status, "A host without CatLib");
        Assert.Equal(ModMark.NotOnHost, noHost.Find(ClientId).Find("boat").Mark, "Own required mods are not on the host");
        Assert.Equal(ModMark.Local, noHost.Find(ClientId).Find("look").Mark, "Own local mods stay local");
        Assert.Equal(0, noHost.ActiveMods.Count, "Nothing required is active without the host's CatLib");
        yield break;
    }
}
