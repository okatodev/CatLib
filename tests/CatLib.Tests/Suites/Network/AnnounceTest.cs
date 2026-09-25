using System.Collections.Generic;
using System.Linq;
using CatLib.Net;
using CatLib.Tests.Framework;
using static CatLib.Tests.Suites.Network.NetworkFixture;

namespace CatLib.Tests.Suites.Network;

public sealed class AnnounceTest : TestCase
{
    public const ulong StrangerId = 76561190000000099UL;

    public override string Suite => "Network";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var host = new NetworkFixture(Identity(), null);
        host.Host.OnPeerConnected(ClientId);
        host.Host.Update();
        host.Network.Now = 0.5;
        host.Host.Update();
        Assert.Equal(1, host.Host.AnnouncesSentTo(ClientId), "Announces within one interval");
        host.Network.Now = 1.0;
        host.Host.Update();
        Assert.Equal(2, host.Host.AnnouncesSentTo(ClientId), "Announces repeat every interval while the peer is silent");
        host.Host.OnReceived(ClientId, MessageCodec.Encode(new HelloMessage(Identity())));
        host.Network.Now = 5.0;
        host.Host.Update();
        Assert.Equal(2, host.Host.AnnouncesSentTo(ClientId), "No announces after the peer was evaluated");

        var client = new NetworkFixture(Identity(), Identity());
        client.Client.Start();
        client.Client.OnReceived(StrangerId, MessageCodec.Encode(new AnnounceMessage()));
        Assert.Equal(0UL, client.Client.HostId, "An announce from someone the game did not report is ignored");
        Assert.Equal(0, client.ClientTransport.Sent, "Nothing is sent to a stranger");

        client.Client.OnReceived(HostId, MessageCodec.Encode(new AnnounceMessage()));
        Assert.Equal(HostId, client.Client.HostId, "An announce from a known player identifies the host");
        Assert.Equal(1, client.ClientTransport.Sent, "Hello right after the announce");

        client.Client.OnReceived(HostId, MessageCodec.Encode(new AnnounceMessage()));
        Assert.Equal(1, client.ClientTransport.Sent, "Announces that arrive together are answered once");

        client.Network.Now = ClientSession.MinAnnounceResponseSeconds;
        client.Client.OnReceived(HostId, MessageCodec.Encode(new AnnounceMessage()));
        Assert.Equal(2, client.ClientTransport.Sent, "A later announce while waiting resends Hello");

        client.Client.OnReceived(StrangerId, MessageCodec.Encode(new VerdictMessage(true, new CompatibilityProblem[0], new SessionSettingValue[0])));
        Assert.Equal(SessionStatus.Waiting, client.Client.Status, "A verdict from someone else than the host is ignored");

        var future = MessageCodec.Encode(new AnnounceMessage());
        future[4] = (byte)(MessageCodec.ProtocolVersion + 1);
        var newer = new NetworkFixture(Identity(), Identity());
        newer.Client.Start();
        newer.Client.OnReceived(HostId, future);
        Assert.Equal(SessionStatus.Rejected, newer.Client.Status, "An announce with another protocol rejects the session");
        Assert.SequenceEqual(new[] { ProblemKind.ProtocolMismatch }, newer.Client.Report.Problems.Select(problem => problem.Kind), "Protocol mismatch reported");
        yield break;
    }
}
