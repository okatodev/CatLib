using System.Globalization;
using CatLib.Game.Events;
using CatLib.Logging;
using CatLib.Net;

namespace CatLib.Tests.Diagnostics;

internal sealed class MessageProbe
{
    public const string ModId = "catlib.tests.demo-network";
    public const string ReceivedEventName = "Probe.Received";

    private readonly CatLogger _log;
    private readonly ModChannel _channel;
    private int _sent;

    public MessageProbe(CatLogger log)
    {
        _log = log;
        _channel = CatNetwork.Channel(ModId);
        _channel.Received += OnReceived;
        _channel.PeerJoined += OnPeerJoined;
        _channel.PeerLeft += peer => _log.Message($"Message probe: player {peer} left");
    }

    public void SendPing()
    {
        _sent++;
        var text = $"ping {_sent} from {CatNetwork.Role}";
        var sent = _channel.SendToHost("ping", text);
        _log.Message(sent ? $"Message probe: sent \"{text}\" to the host" : $"Message probe: \"{text}\" was not sent, role {CatNetwork.Role}, the host does not share {ModId} or the handshake is not finished");
    }

    private void OnPeerJoined(ulong peer)
    {
        var sent = _channel.SendTo(peer, "welcome", "welcome from the host");
        _log.Message($"Message probe: player {peer} joined with {ModId}, welcome {(sent ? "sent" : "not sent")}");
    }

    private void OnReceived(ModMessage message)
    {
        _log.Message($"Message probe: received {message.Name} \"{message.Text}\" as {CatNetwork.Role}, {message}");
        GameEventStream.Publish(ReceivedEventName, $"name={message.Name} fromHost={(message.FromHost ? "yes" : "no")} local={(message.IsLocal ? "yes" : "no")} sender={message.Sender.ToString(CultureInfo.InvariantCulture)}");
        if (message.FromHost)
        {
            return;
        }

        var reached = _channel.Broadcast("pong", $"{message.Text} -> seen by the host");
        _log.Message($"Message probe: answered with pong to {reached} receiver(s) including this game");
    }
}
