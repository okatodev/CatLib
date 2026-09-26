using System.Collections.Generic;
using System.Linq;
using CatLib.Net;
using CatLib.Tests.Framework;
using static CatLib.Tests.Suites.Network.NetworkFixture;

namespace CatLib.Tests.Suites.Network;

public sealed class ModMessagesSharingTest : TestCase
{
    public override string Suite => "Network";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var rejected = new ModMessagingFixture(
            Identity(Mod("labels", "1.0.0", SessionPolicy.RequiredOnAll), Mod("boat", "0.3.0", SessionPolicy.RequiredOnAll)),
            Identity(Mod("labels", "1.0.0", SessionPolicy.RequiredOnAll), Mod("boat", "0.2.0", SessionPolicy.RequiredOnAll)));
        var atHost = ModMessagingFixture.Record(rejected.HostMessenger.Channel("labels"));
        rejected.Connect();
        Assert.Equal(SessionStatus.Rejected, rejected.Sessions.Client.Status, "Another mod differs, the player is rejected but stays with the default Warn action");
        Assert.SequenceEqual(new[] { "labels" }, rejected.Sessions.Client.SharedMods.ToList(), "The matching mod is still shared");
        Assert.True(rejected.ClientMessenger.Channel("labels").SendToHost("click", "1"), "The matching mod keeps working");
        Assert.False(rejected.ClientMessenger.Channel("boat").SendToHost("click", "1"), "The mismatched mod does not talk");
        rejected.Pump();
        Assert.Equal(1, atHost.Count, "Delivered");

        var versions = new ModMessagingFixture(
            Identity(Mod("labels", "1.1.0", SessionPolicy.RequiredOnAll)),
            Identity(Mod("labels", "1.0.0", SessionPolicy.RequiredOnAll)));
        versions.Connect();
        Assert.Equal(0, versions.Sessions.Host.PeersSharing("labels").Count, "Different minor versions do not exchange messages");
        Assert.Equal(1, versions.HostMessenger.Channel("labels").Broadcast("state", "x"), "Only the host's own copy");

        var offlineLink = new OfflineLink();
        var offline = new ModMessenger(offlineLink, () => 0, null);
        var channel = offline.Channel("labels");
        var received = ModMessagingFixture.Record(channel);
        Assert.True(channel.CanSendToHost, "Without a session this game is the authority");
        Assert.True(channel.SendToHost("click", "1"), "A request without a session");
        Assert.Equal(1, channel.Broadcast("state", "2"), "A broadcast without a session");
        Assert.Equal(0, received.Count, "Nothing is delivered inside the call");
        Assert.Equal(2, offline.Update(), "Both are delivered by the next update");
        Assert.True(!received[0].FromHost && received[1].FromHost, "The request and the state keep their roles and order");

        channel.SendToHost("click", "3");
        offline.Reset();
        Assert.Equal(0, offline.Update(), "Stopping a session drops undelivered local messages");

        offlineLink.Role = SessionRole.Client;
        Assert.False(channel.CanSendToHost, "A client without a shared host cannot send");
        yield break;
    }
}
