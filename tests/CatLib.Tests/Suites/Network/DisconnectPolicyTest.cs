using System.Collections.Generic;
using CatLib.Net;
using CatLib.Tests.Framework;
using static CatLib.Tests.Suites.Network.NetworkFixture;

namespace CatLib.Tests.Suites.Network;

public sealed class DisconnectPolicyTest : TestCase
{
    public override string Suite => "Network";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var hostMods = Identity(Mod("gameplay", "1.0.0", SessionPolicy.RequiredOnAll));
        var newerClient = Identity(Mod("gameplay", "2.0.0", SessionPolicy.RequiredOnAll));

        foreach (var disconnect in new[] { true, false })
        {
            var fixture = Create(hostMods, newerClient, disconnect);
            fixture.Connect();
            Assert.Equal(SessionStatus.Rejected, fixture.Client.Status, "Client status");
            Assert.Equal(disconnect, fixture.Client.Report.Disconnecting, $"Client learns the host policy (disconnect {disconnect})");
            Assert.Equal(disconnect, fixture.Host.ReportFor(ClientId).Disconnecting, $"Host report carries the policy (disconnect {disconnect})");
        }

        var compatible = Create(hostMods, hostMods, true);
        compatible.Connect();
        Assert.Equal(SessionStatus.Accepted, compatible.Client.Status, "Compatible client");
        Assert.False(compatible.Client.Report.Disconnecting, "Compatible players are never disconnected");

        var vanilla = Create(hostMods, null, true);
        vanilla.Host.OnPeerConnected(ClientId);
        vanilla.Network.Now = HandshakeTimeout;
        vanilla.Host.Update();
        Assert.True(vanilla.Host.ReportFor(ClientId).Disconnecting, "A client without CatLib missing a required mod is disconnected");

        var relaxed = Create(Identity(Mod("server", "1.0.0", SessionPolicy.HostOnly)), null, true);
        relaxed.Host.OnPeerConnected(ClientId);
        relaxed.Network.Now = HandshakeTimeout;
        relaxed.Host.Update();
        Assert.False(relaxed.Host.ReportFor(ClientId).Disconnecting, "A client without CatLib is kept when nothing is required");
        yield break;
    }

    private static NetworkFixture Create(LocalIdentity host, LocalIdentity client, bool disconnect)
    {
        var fixture = new NetworkFixture(host, client);
        fixture.ReplaceHost(new HostSession(fixture.HostTransport, host, () => new SessionSettingValue[0], () => fixture.Network.Now, HandshakeTimeout, null, 1, () => disconnect));
        return fixture;
    }
}
