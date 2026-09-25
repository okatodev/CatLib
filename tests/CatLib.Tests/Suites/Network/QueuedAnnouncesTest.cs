using System.Collections.Generic;
using CatLib.Net;
using CatLib.Tests.Framework;
using static CatLib.Tests.Suites.Network.NetworkFixture;

namespace CatLib.Tests.Suites.Network;

public sealed class QueuedAnnouncesTest : TestCase
{
    public override string Suite => "Network";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var fixture = new NetworkFixture(Identity(), Identity(), () => new[] { new SessionSettingValue("demo", "Demo", "SharedLimit", "3") });
        var evaluations = 0;
        fixture.Host.PeerEvaluated += _ => evaluations++;

        fixture.Host.OnPeerConnected(ClientId);
        fixture.Client.Start();
        for (var second = 0; second < 3; second++)
        {
            fixture.Network.Now = second;
            fixture.Host.Update();
        }

        Assert.Equal(3, fixture.Network.Queued, "Three announces waited while the Steam session was not accepted yet");

        fixture.Network.Pump();

        context.Note($"Hellos sent: {fixture.Client.HellosSent}, host messages: {fixture.HostTransport.Sent}");
        Assert.Equal(1, fixture.Client.HellosSent, "A burst of queued announces is answered with one Hello");
        Assert.Equal(1, evaluations, "One evaluation");
        Assert.Equal(SessionStatus.Accepted, fixture.Client.Status, "Client status");
        Assert.Equal(1, ((RecordingSink)fixture.Sink).Applied.Count, "Settings applied once");
        yield break;
    }
}
