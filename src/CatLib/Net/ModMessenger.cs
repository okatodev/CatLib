using System;
using System.Collections.Generic;
using System.Linq;
using CatLib.Logging;

namespace CatLib.Net;

public sealed class ModMessenger
{
    public const int MessagesPerSecondPerPeer = 60;
    public const double RateWarningIntervalSeconds = 5;

    private readonly object _sync = new();
    private readonly Dictionary<string, ModChannel> _channels = new(StringComparer.Ordinal);
    private readonly Queue<ModMessage> _local = new();
    private readonly Dictionary<ulong, RateWindow> _rates = new();
    private readonly Dictionary<ulong, HashSet<string>> _joined = new();
    private readonly IModMessageLink _link;
    private readonly Func<double> _clock;
    private readonly CatLogger _log;

    public ModMessenger(IModMessageLink link, Func<double> clock, CatLogger log)
    {
        _link = link ?? throw new ArgumentNullException(nameof(link));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _log = log;
    }

    public SessionRole Role => _link.Role;

    public int Dropped { get; private set; }

    public ModChannel Channel(string modId)
    {
        if (string.IsNullOrWhiteSpace(modId))
        {
            throw new ArgumentException("Mod id must not be empty.", nameof(modId));
        }

        lock (_sync)
        {
            if (!_channels.TryGetValue(modId, out var channel))
            {
                channel = new ModChannel(this, modId);
                _channels[modId] = channel;
            }

            return channel;
        }
    }

    public int Update()
    {
        var delivered = 0;
        while (true)
        {
            ModMessage message;
            lock (_sync)
            {
                if (_local.Count == 0)
                {
                    return delivered;
                }

                message = _local.Dequeue();
            }

            Deliver(message);
            delivered++;
        }
    }

    public void OnHostReceived(ulong peer, ModMessageData data)
    {
        var channel = Find(data.ModId);
        if (channel == null)
        {
            _log?.Debug($"Ignored a message of {data.ModId} from {peer}, the mod has no channel here");
            return;
        }

        if (!AllowFrom(peer))
        {
            return;
        }

        Deliver(new ModMessage(data.ModId, data.Name, data.Data, peer, false, false));
    }

    public void OnClientReceived(ulong host, ModMessageData data)
    {
        if (Find(data.ModId) == null)
        {
            _log?.Debug($"Ignored a message of {data.ModId} from the host, the mod has no channel here");
            return;
        }

        Deliver(new ModMessage(data.ModId, data.Name, data.Data, host, true, false));
    }

    public void OnPeerEvaluated(ulong peer, IReadOnlyCollection<string> sharedMods)
    {
        List<ModChannel> joined;
        lock (_sync)
        {
            if (!_joined.TryGetValue(peer, out var known))
            {
                known = new HashSet<string>(StringComparer.Ordinal);
                _joined[peer] = known;
            }

            joined = sharedMods.Where(known.Add).Select(Find).Where(channel => channel != null).ToList();
        }

        foreach (var channel in joined)
        {
            channel.RaisePeerJoined(peer, _log);
        }
    }

    public void OnPeerLeft(ulong peer)
    {
        HashSet<string> known;
        lock (_sync)
        {
            _rates.Remove(peer);
            if (!_joined.Remove(peer, out known))
            {
                return;
            }
        }

        foreach (var channel in known.Select(Find).Where(channel => channel != null))
        {
            channel.RaisePeerLeft(peer, _log);
        }
    }

    public void Reset()
    {
        List<(ulong Peer, HashSet<string> Mods)> left;
        lock (_sync)
        {
            left = _joined.Select(pair => (pair.Key, pair.Value)).ToList();
            _joined.Clear();
            _rates.Clear();
            _local.Clear();
        }

        foreach (var (peer, mods) in left)
        {
            foreach (var channel in mods.Select(Find).Where(channel => channel != null))
            {
                channel.RaisePeerLeft(peer, _log);
            }
        }
    }

    internal bool SendToHost(string modId, string name, byte[] data)
    {
        var role = _link.Role;
        if (role != SessionRole.Client)
        {
            Enqueue(new ModMessage(modId, name, data, _link.LocalId, false, true));
            return true;
        }

        var sent = _link.SendToHost(new ModMessageData(modId, name, data));
        if (!sent)
        {
            _log?.Debug($"{modId}/{name} was not sent, the host does not share this mod or the handshake is not finished");
        }

        return sent;
    }

    internal int Broadcast(string modId, string name, byte[] data)
    {
        if (_link.Role == SessionRole.Client)
        {
            _log?.Warning($"{modId}/{name} was not broadcast: only the host broadcasts");
            return 0;
        }

        var message = new ModMessageData(modId, name, data);
        var sent = _link.PeersSharing(modId).Count(peer => _link.SendToPeer(peer, message));
        Enqueue(new ModMessage(modId, name, data, _link.LocalId, true, true));
        return sent + 1;
    }

    internal bool SendTo(ulong peer, string modId, string name, byte[] data)
    {
        if (_link.Role == SessionRole.Client)
        {
            _log?.Warning($"{modId}/{name} was not sent to {peer}: only the host sends to players");
            return false;
        }

        if (peer == _link.LocalId)
        {
            Enqueue(new ModMessage(modId, name, data, peer, true, true));
            return true;
        }

        return _link.SendToPeer(peer, new ModMessageData(modId, name, data));
    }

    internal bool CanReachHost(string modId) => _link.Role != SessionRole.Client || _link.HostShares(modId);

    private void Enqueue(ModMessage message)
    {
        lock (_sync)
        {
            _local.Enqueue(message);
        }
    }

    private void Deliver(ModMessage message)
    {
        Find(message.ModId)?.RaiseReceived(message, _log);
    }

    private ModChannel Find(string modId)
    {
        lock (_sync)
        {
            return modId != null && _channels.TryGetValue(modId, out var channel) ? channel : null;
        }
    }

    private bool AllowFrom(ulong peer)
    {
        var now = _clock();
        lock (_sync)
        {
            if (!_rates.TryGetValue(peer, out var window))
            {
                window = new RateWindow { Start = now };
                _rates[peer] = window;
            }

            if (now - window.Start >= 1)
            {
                window.Start = now;
                window.Count = 0;
            }

            window.Count++;
            if (window.Count <= MessagesPerSecondPerPeer)
            {
                return true;
            }

            Dropped++;
            if (now - window.LastWarning >= RateWarningIntervalSeconds)
            {
                window.LastWarning = now;
                _log?.Warning($"Player {peer} sends more than {MessagesPerSecondPerPeer} mod messages per second, extra messages are dropped");
            }

            return false;
        }
    }

    private sealed class RateWindow
    {
        public double Start { get; set; }

        public int Count { get; set; }

        public double LastWarning { get; set; } = double.NegativeInfinity;
    }
}
