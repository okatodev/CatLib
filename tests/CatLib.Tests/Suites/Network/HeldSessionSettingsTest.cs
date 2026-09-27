using System.Collections.Generic;
using CatLib.Net;
using CatLib.Tests.Framework;
using static CatLib.Tests.Suites.Network.NetworkFixture;

namespace CatLib.Tests.Suites.Network;

public sealed class HeldSessionSettingsTest : TestCase
{
    public override string Suite => "Network";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var hostSettings = new[] { new SessionSettingValue("gameplay", "Rules", "Limit", "7") };
        var fixture = new NetworkFixture(
            Identity(Mod("gameplay", "1.2.0", SessionPolicy.RequiredOnAll)),
            Identity(Mod("gameplay", "1.2.0", SessionPolicy.RequiredOnAll)),
            () => hostSettings);
        fixture.Host.OnPeerConnected(HostId);
        fixture.Connect();
        var sink = (RecordingSink)fixture.Sink;
        Assert.Equal(SessionStatus.Accepted, fixture.Client.Status, "Client status");

        fixture.Client.Stop(false);
        Assert.Equal(SessionStatus.Stopped, fixture.Client.Status, "The session stops");
        Assert.Equal(0, sink.Clears, "Host settings are kept while the level is unloaded");

        fixture.Client.ReleaseSettings();
        Assert.Equal(1, sink.Clears, "Host settings are cleared once the level is gone");

        fixture.Client.Stop();
        Assert.Equal(1, sink.Clears, "Stopping a stopped session clears nothing again");
        yield break;
    }
}
