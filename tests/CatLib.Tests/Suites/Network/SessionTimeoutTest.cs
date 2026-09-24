using System.Collections.Generic;
using System.Linq;
using CatLib.Net;
using CatLib.Tests.Framework;
using static CatLib.Tests.Suites.Network.NetworkFixture;

namespace CatLib.Tests.Suites.Network;

public sealed class SessionTimeoutTest : TestCase
{
    public override string Suite => "Network";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var strictHost = new NetworkFixture(Identity(Mod("gameplay", "1.0.0", SessionPolicy.RequiredOnAll), Mod("server", "1.0.0", SessionPolicy.HostOnly)), null);
        strictHost.Host.OnPeerConnected(ClientId);
        strictHost.Network.Now = HandshakeTimeout - 0.1;
        strictHost.Host.Update();
        Assert.Null(strictHost.Host.ReportFor(ClientId), "No verdict before the timeout");
        strictHost.Network.Now = HandshakeTimeout;
        strictHost.Host.Update();
        var report = strictHost.Host.ReportFor(ClientId);
        Assert.Equal(SessionStatus.PeerWithoutCatLib, report.Status, "Silent client status");
        Assert.False(report.IsCompatible, "A vanilla client is incompatible with a required mod");
        Assert.SequenceEqual(new[] { "gameplay" }, report.Problems.Select(problem => problem.Subject), "Missing required mods");

        var relaxedHost = new NetworkFixture(Identity(Mod("server", "1.0.0", SessionPolicy.HostOnly)), null);
        relaxedHost.Host.OnPeerConnected(ClientId);
        relaxedHost.Network.Now = HandshakeTimeout;
        relaxedHost.Host.Update();
        Assert.True(relaxedHost.Host.ReportFor(ClientId).IsCompatible, "A vanilla client is fine when the host only has host-only mods");

        var silentHost = new NetworkFixture(Identity(), Identity(Mod("gameplay", "1.0.0", SessionPolicy.RequiredOnAll), Mod("visual", "1.0.0", SessionPolicy.ClientOnly)));
        silentHost.Network.Leave(HostId);
        silentHost.Client.Start();
        silentHost.Network.Pump();
        silentHost.Network.Now = HandshakeTimeout;
        silentHost.Client.Update();
        Assert.Equal(SessionStatus.PeerWithoutCatLib, silentHost.Client.Status, "Client status with a vanilla host");
        Assert.SequenceEqual(new[] { "MissingOnHost:gameplay" }, silentHost.Client.Report.Problems.Select(problem => problem.Kind + ":" + problem.Subject), "Required mods the vanilla host lacks");

        var late = new NetworkFixture(Identity(), Identity());
        late.Network.Leave(HostId);
        late.Client.Start();
        late.Network.Now = HandshakeTimeout;
        late.Client.Update();
        late.Client.OnReceived(HostId, MessageCodec.Encode(new VerdictMessage(true, new CompatibilityProblem[0], new[] { new SessionSettingValue("m", "s", "k", "v") })));
        Assert.Equal(SessionStatus.PeerWithoutCatLib, late.Client.Status, "A verdict after the timeout must not change the decision");
        Assert.Equal(0, ((RecordingSink)late.Sink).Applied.Count, "A late verdict must not apply settings");
        yield break;
    }
}
