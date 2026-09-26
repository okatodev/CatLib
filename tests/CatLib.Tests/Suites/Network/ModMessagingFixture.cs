using System.Collections.Generic;
using CatLib.Net;
using static CatLib.Tests.Suites.Network.NetworkFixture;

namespace CatLib.Tests.Suites.Network;

internal sealed class ModMessagingFixture
{
    public ModMessagingFixture(LocalIdentity host, LocalIdentity client)
    {
        Sessions = new NetworkFixture(host, client);
        HostMessenger = new ModMessenger(new SessionLink(() => Sessions.Host, () => null, () => Sessions.HostTransport), () => Sessions.Network.Now, null);
        ClientMessenger = new ModMessenger(new SessionLink(() => null, () => Sessions.Client, () => Sessions.ClientTransport), () => Sessions.Network.Now, null);
        Sessions.Host.ModMessageReceived += HostMessenger.OnHostReceived;
        Sessions.Host.PeerEvaluated += report => HostMessenger.OnPeerEvaluated(report.PeerId, Sessions.Host.SharedModsOf(report.PeerId));
        Sessions.Client.ModMessageReceived += data => ClientMessenger.OnClientReceived(Sessions.Client.HostId, data);
    }

    public NetworkFixture Sessions { get; }

    public ModMessenger HostMessenger { get; }

    public ModMessenger ClientMessenger { get; }

    public void Connect() => Sessions.Connect();

    public void Pump()
    {
        Sessions.Network.Pump();
        HostMessenger.Update();
        ClientMessenger.Update();
        Sessions.Network.Pump();
    }

    public static List<ModMessage> Record(ModChannel channel)
    {
        var received = new List<ModMessage>();
        channel.Received += received.Add;
        return received;
    }
}
