using System.Collections.Generic;
using System.Linq;
using CatLib.Net;
using CatLib.Tests.Framework;
using static CatLib.Tests.Suites.Network.NetworkFixture;

namespace CatLib.Tests.Suites.Network;

public sealed class RosterSessionTest : TestCase
{
    public override string Suite => "Network";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var hostSettings = new[]
        {
            new SessionSettingValue("boat", "Height", "Scale", "2"),
            new SessionSettingValue("labels", "Labels", "Slots", "3")
        };
        var host = Identity(Mod("boat", "0.2.0", SessionPolicy.RequiredOnAll), Mod("labels", "0.1.0", SessionPolicy.RequiredOnAll));
        var client = Identity(Mod("boat", "0.2.0", SessionPolicy.RequiredOnAll));
        var disconnect = false;
        var fixture = new NetworkFixture(host, client, () => hostSettings);
        fixture.ReplaceHost(new HostSession(fixture.HostTransport, host, () => hostSettings, () => fixture.Network.Now, HandshakeTimeout, null, 1, () => disconnect));
        fixture.Connect();

        Assert.Equal(SessionStatus.Rejected, fixture.Client.Status, "The client misses a mod");
        var applied = ((RecordingSink)fixture.Sink).Applied;
        Assert.SequenceEqual(new[] { "boat" }, applied.Select(value => value.OwnerId), "A limited player gets the settings of the mods it shares");
        Assert.Equal(1, fixture.Host.BroadcastSettings(hostSettings), "Updates reach a limited player");
        fixture.Network.Pump();
        Assert.Equal(2, applied.Count(value => value.OwnerId == "boat"), "Only the shared mod's update arrives");
        Assert.Equal(0, applied.Count(value => value.OwnerId == "labels"), "Settings of the missing mod never arrive");

        var roster = fixture.Host.BuildRoster(HostId, "Host", IncompatiblePlayerAction.Warn, id => id == ClientId ? "Friend" : "?");
        Assert.SequenceEqual(new[] { "boat" }, roster.ActiveMods, "The mod everyone has stays active, the missing one pauses");
        Assert.Equal(1, fixture.Host.SendRoster(roster), "The roster goes to the player");
        fixture.Network.Pump();
        Assert.NotNull(fixture.Client.Roster, "The player receives the roster");
        Assert.Equal("Friend", fixture.Client.Roster.Find(ClientId).Name, "Names come from the host");
        Assert.Equal(ModMark.Missing, fixture.Client.Roster.Find(ClientId).Find("labels").Mark, "The player sees what it misses");

        disconnect = true;
        var changed = fixture.Host.SetDisconnecting(true);
        Assert.SequenceEqual(new[] { ClientId }, changed, "Switching to disconnect affects the limited player");
        Assert.Equal(RosterStatus.Leaving, fixture.Host.BuildRoster(HostId, "Host", IncompatiblePlayerAction.Disconnect, null).Find(ClientId).Status, "The player is now leaving");
        Assert.Equal(0, fixture.Host.BroadcastSettings(hostSettings), "A leaving player gets no settings");
        Assert.Equal(1, fixture.Host.SetDisconnecting(false).Count, "Switching back lets the player stay");
        Assert.Equal(RosterStatus.Limited, fixture.Host.BuildRoster(HostId, "Host", IncompatiblePlayerAction.Warn, null).Find(ClientId).Status, "Limited again");

        var bytes = MessageCodec.Encode(new RosterMessage(roster));
        var decoded = (RosterMessage)MessageCodec.Decode(bytes).Payload;
        Assert.Equal(roster.Players.Count, decoded.Roster.Players.Count, "Roster players survive the wire");
        Assert.SequenceEqual(roster.ActiveMods, decoded.Roster.ActiveMods, "Active mods survive the wire");
        Assert.Equal(roster.Find(ClientId).Mods.Count, decoded.Roster.Find(ClientId).Mods.Count, "Mods survive the wire");
        Assert.Equal("0.1.0", decoded.Roster.Find(ClientId).Find("labels").HostVersion, "The host version survives the wire");
        Assert.Equal(ClientId, decoded.Roster.Find(ClientId).Id, "64-bit ids survive the wire");
        yield break;
    }
}
