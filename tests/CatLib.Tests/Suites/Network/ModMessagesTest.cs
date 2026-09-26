using System;
using System.Collections.Generic;
using System.Linq;
using CatLib.Net;
using CatLib.Tests.Framework;
using static CatLib.Tests.Suites.Network.NetworkFixture;

namespace CatLib.Tests.Suites.Network;

public sealed class ModMessagesTest : TestCase
{
    public override string Suite => "Network";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var fixture = new ModMessagingFixture(
            Identity(Mod("labels", "1.0.0", SessionPolicy.RequiredOnAll), Mod("server", "1.0.0", SessionPolicy.HostOnly)),
            Identity(Mod("labels", "1.0.1", SessionPolicy.RequiredOnAll), Mod("visual", "1.0.0", SessionPolicy.ClientOnly)));
        var hostLabels = fixture.HostMessenger.Channel("labels");
        var clientLabels = fixture.ClientMessenger.Channel("labels");
        var clientVisual = fixture.ClientMessenger.Channel("visual");
        var atHost = ModMessagingFixture.Record(hostLabels);
        var atClient = ModMessagingFixture.Record(clientLabels);
        var joined = new List<ulong>();
        hostLabels.PeerJoined += joined.Add;

        Assert.False(clientLabels.CanSendToHost, "Nothing can be sent before the handshake");
        Assert.False(clientLabels.SendToHost("click", "390/1"), "Sending before the handshake is refused");

        fixture.Connect();
        Assert.SequenceEqual(new[] { "labels" }, fixture.Sessions.Host.SharedModsOf(ClientId).OrderBy(id => id), "The host shares only mods both sides have");
        Assert.SequenceEqual(new[] { "labels" }, fixture.Sessions.Client.SharedMods.OrderBy(id => id), "The client learns the shared mods from the verdict");
        Assert.SequenceEqual(new[] { ClientId }, joined, "PeerJoined names the new player once");
        Assert.True(clientLabels.CanSendToHost, "A shared mod can reach the host");
        Assert.False(clientVisual.SendToHost("hello", "x"), "A mod the host lacks cannot send");

        Assert.True(clientLabels.SendToHost("click", "390/1"), "Client request sent");
        fixture.Pump();
        Assert.Equal(1, atHost.Count, "The host received the request");
        Assert.Equal("click", atHost[0].Name, "Name");
        Assert.Equal("390/1", atHost[0].Text, "Text");
        Assert.Equal(ClientId, atHost[0].Sender, "Sender is the player");
        Assert.False(atHost[0].FromHost, "A request is not from the host");

        Assert.Equal(2, hostLabels.Broadcast("state", "390/1=4"), "Broadcast reaches the player and the host itself");
        Assert.Equal(1, atHost.Count, "The host's own copy arrives with the next update, not inside the call");
        fixture.Pump();
        Assert.Equal(1, atClient.Count, "The player received the state");
        Assert.True(atClient[0].FromHost && !atClient[0].IsLocal, "From the host over the network");
        Assert.Equal(2, atHost.Count, "The host received its own broadcast");
        Assert.True(atHost[1].FromHost && atHost[1].IsLocal, "The host's copy is local and marked as from the host");

        Assert.True(hostLabels.SendTo(ClientId, "full", "all"), "Sending to one player");
        Assert.False(hostLabels.SendTo(12345UL, "full", "all"), "Unknown players are not reached");
        Assert.Equal(0, clientLabels.Broadcast("state", "x"), "A client cannot broadcast");
        Assert.False(clientLabels.SendTo(HostId, "full", "x"), "A client cannot address players");
        fixture.Pump();
        Assert.Equal("full", atClient.Last().Name, "Direct message delivered");

        var healthy = 0;
        clientLabels.Received += _ => throw new InvalidOperationException("Expected exception thrown by CatLib.Tests");
        clientLabels.Received += _ => healthy++;
        hostLabels.Broadcast("state", "again");
        fixture.Pump();
        Assert.Equal(1, healthy, "A failing handler does not stop the others");

        fixture.Sessions.Network.Now += 1.5;
        var before = atHost.Count;
        for (var index = 0; index < ModMessenger.MessagesPerSecondPerPeer + 40; index++)
        {
            clientLabels.SendToHost("spam", index.ToString());
        }

        fixture.Pump();
        Assert.Equal(ModMessenger.MessagesPerSecondPerPeer, atHost.Count - before, "The host accepts a limited number of messages per second from one player");
        Assert.Equal(40, fixture.HostMessenger.Dropped, "Extra messages are dropped");
        fixture.Sessions.Network.Now += 1.5;
        clientLabels.SendToHost("click", "later");
        fixture.Pump();
        Assert.Equal("later", atHost.Last().Text, "The limit resets after a second");

        fixture.Sessions.Host.OnReceived(76561190000000009UL, MessageCodec.Encode(new ModMessageData("labels", "click", new byte[1])));
        fixture.HostMessenger.Update();
        Assert.Equal("later", atHost.Last().Text, "Messages from a player whose mods were not checked are ignored");

        var left = new List<ulong>();
        hostLabels.PeerLeft += left.Add;
        fixture.Sessions.Host.OnPeerDisconnected(ClientId);
        fixture.HostMessenger.OnPeerLeft(ClientId);
        Assert.SequenceEqual(new[] { ClientId }, left, "PeerLeft names the player");
        Assert.Equal(1, hostLabels.Broadcast("state", "x"), "After the player left only the host receives");

        fixture.Sessions.Client.Stop();
        Assert.False(clientLabels.CanSendToHost, "A stopped session cannot send");
        yield break;
    }
}
