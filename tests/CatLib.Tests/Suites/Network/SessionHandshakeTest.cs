using System.Collections.Generic;
using System.Linq;
using CatLib.Net;
using CatLib.Tests.Framework;
using static CatLib.Tests.Suites.Network.NetworkFixture;

namespace CatLib.Tests.Suites.Network;

public sealed class SessionHandshakeTest : TestCase
{
    public override string Suite => "Network";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var hostSettings = new[] { new SessionSettingValue("gameplay", "Rules", "Limit", "7") };
        var fixture = new NetworkFixture(
            Identity(Mod("gameplay", "1.2.0", SessionPolicy.RequiredOnAll), Mod("server", "1.0.0", SessionPolicy.HostOnly)),
            Identity(Mod("gameplay", "1.2.3", SessionPolicy.RequiredOnAll), Mod("visual", "1.0.0", SessionPolicy.ClientOnly)),
            () => hostSettings);
        var hostReports = new List<PeerReport>();
        fixture.Host.PeerEvaluated += hostReports.Add;

        fixture.Host.OnPeerConnected(HostId);
        fixture.Connect();

        Assert.Equal(1, fixture.Host.PeerCount, "The host must ignore its own client id");
        Assert.Equal(1, hostReports.Count, "Host evaluations");
        Assert.Equal(SessionStatus.Accepted, hostReports[0].Status, "Host verdict");
        Assert.Equal("0.4.0", hostReports[0].CatLibVersion, "Client CatLib version seen by the host");
        Assert.Equal(SessionStatus.Accepted, fixture.Client.Status, "Client status");
        Assert.True(fixture.Client.Report.IsCompatible, "Client report");
        Assert.SequenceEqual(hostSettings, ((RecordingSink)fixture.Sink).Applied, "Session settings delivered with the verdict");

        var update = new[] { new SessionSettingValue("gameplay", "Rules", "Limit", "9") };
        Assert.Equal(1, fixture.Host.BroadcastSettings(update), "Accepted peers receiving an update");
        fixture.Network.Pump();
        Assert.Equal("9", ((RecordingSink)fixture.Sink).Applied.Last().Value, "Session settings update delivered");

        fixture.Network.Now = 60;
        fixture.Host.Update();
        fixture.Client.Update();
        Assert.Equal(1, hostReports.Count, "Timeouts must not fire after a completed handshake");

        fixture.Client.Stop();
        Assert.Equal(1, ((RecordingSink)fixture.Sink).Clears, "Stopping must clear session overrides");
        fixture.Host.OnPeerDisconnected(ClientId);
        Assert.Equal(0, fixture.Host.BroadcastSettings(update), "Disconnected peers receive nothing");
        yield break;
    }
}
