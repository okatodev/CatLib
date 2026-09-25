using System.Collections.Generic;
using CatLib.Net;
using CatLib.Tests.Framework;
using static CatLib.Tests.Suites.Network.NetworkFixture;

namespace CatLib.Tests.Suites.Network;

public sealed class RepeatedHelloTest : TestCase
{
    public override string Suite => "Network";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var fixture = new NetworkFixture(Identity(Mod("gameplay", "1.2.0", SessionPolicy.RequiredOnAll)), Identity(Mod("gameplay", "1.2.0", SessionPolicy.RequiredOnAll)),
            () => new[] { new SessionSettingValue("gameplay", "Rules", "Limit", "7") });
        var evaluations = 0;
        fixture.Host.PeerEvaluated += _ => evaluations++;

        fixture.Host.OnPeerConnected(ClientId);
        fixture.Client.Start();
        fixture.Client.ResendHello();
        Assert.Equal(0, fixture.ClientTransport.Sent, "A client that does not know the host yet sends nothing");

        fixture.Host.Update();
        Assert.True(fixture.Network.PumpOne(), "The host announces itself");
        Assert.Equal(HostId, fixture.Client.HostId, "The client learns the host from the announce");
        fixture.Client.ResendHello();
        fixture.Client.ResendHello();
        Assert.Equal(3, fixture.ClientTransport.Sent, "Hello after the announce plus two resends");

        fixture.Network.Pump();

        Assert.Equal(1, evaluations, "Hellos that crossed with the verdict are evaluated once");
        Assert.Equal(2, fixture.Host.RepeatedHellosFrom(ClientId), "Repeated Hellos seen by the host");
        Assert.Equal(4, fixture.HostTransport.Sent, "One announce plus a verdict for every Hello");
        Assert.Equal(SessionStatus.Accepted, fixture.Client.Status, "Client status");
        Assert.Equal(1, ((RecordingSink)fixture.Sink).Applied.Count, "Only the first verdict applies settings");
        yield break;
    }
}
