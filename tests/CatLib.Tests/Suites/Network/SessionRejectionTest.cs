using System.Collections.Generic;
using System.Linq;
using CatLib.Net;
using CatLib.Tests.Framework;
using static CatLib.Tests.Suites.Network.NetworkFixture;

namespace CatLib.Tests.Suites.Network;

public sealed class SessionRejectionTest : TestCase
{
    public override string Suite => "Network";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var hostSettings = new[] { new SessionSettingValue("gameplay", "Rules", "Limit", "7") };
        var fixture = new NetworkFixture(
            Identity(Mod("gameplay", "1.2.0", SessionPolicy.RequiredOnAll)),
            Identity(Mod("gameplay", "1.3.0", SessionPolicy.RequiredOnAll)),
            () => hostSettings);

        fixture.Connect();

        var hostReport = fixture.Host.ReportFor(ClientId);
        Assert.Equal(SessionStatus.Rejected, hostReport.Status, "Host verdict");
        Assert.Equal(SessionStatus.Rejected, fixture.Client.Status, "Client status");
        Assert.SequenceEqual(new[] { ProblemKind.VersionMismatch }, fixture.Client.Report.Problems.Select(problem => problem.Kind), "Problems reported to the client");
        Assert.Equal(0, ((RecordingSink)fixture.Sink).Applied.Count, "A rejected client must not receive session settings");
        Assert.Equal(0, fixture.Host.BroadcastSettings(hostSettings), "Rejected peers receive no updates");

        var mismatch = new NetworkFixture(Identity(), Identity());
        mismatch.Host.OnPeerConnected(ClientId);
        var future = MessageCodec.Encode(new HelloMessage(Identity()));
        future[4] = 2;
        mismatch.Host.OnReceived(ClientId, future);
        mismatch.Network.Pump();
        Assert.SequenceEqual(new[] { ProblemKind.ProtocolMismatch }, mismatch.Host.ReportFor(ClientId).Problems.Select(problem => problem.Kind), "Protocol mismatch on the host");

        var garbage = new NetworkFixture(Identity(), Identity());
        garbage.Host.OnPeerConnected(ClientId);
        garbage.Host.OnReceived(ClientId, new byte[] { 1, 2, 3 });
        garbage.Client.OnReceived(HostId, new byte[] { 4, 5, 6, 7, 8, 9, 10 });
        Assert.Null(garbage.Host.ReportFor(ClientId), "Garbage must not complete an evaluation");
        Assert.Equal(SessionStatus.Waiting, garbage.Client.Status, "Garbage must not change the client state");
        context.Note("Malformed messages are logged as warnings and ignored");
        yield break;
    }
}
