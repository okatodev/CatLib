using System;
using System.Collections.Generic;
using System.Linq;
using CatLib.Events;
using CatLib.Logging;

namespace CatLib.Net;

public sealed class HostSession
{
    private readonly ISessionTransport _transport;
    private readonly LocalIdentity _identity;
    private readonly Func<IReadOnlyList<SessionSettingValue>> _snapshot;
    private readonly Func<double> _clock;
    private readonly double _helloTimeout;
    private readonly double _announceInterval;
    private readonly Func<bool> _disconnectIncompatible;
    private readonly byte[] _announce = MessageCodec.Encode(new AnnounceMessage());
    private readonly CatLogger _log;
    private readonly Dictionary<ulong, PeerState> _peers = new();

    public HostSession(ISessionTransport transport, LocalIdentity identity, Func<IReadOnlyList<SessionSettingValue>> snapshot, Func<double> clock, double helloTimeoutSeconds, CatLogger log, double announceIntervalSeconds = 1, Func<bool> disconnectIncompatible = null)
    {
        _announceInterval = announceIntervalSeconds;
        _disconnectIncompatible = disconnectIncompatible;
        _transport = transport;
        _identity = identity;
        _snapshot = snapshot;
        _clock = clock;
        _helloTimeout = helloTimeoutSeconds;
        _log = log;
    }

    public event Action<PeerReport> PeerEvaluated;

    public IReadOnlyList<PeerReport> Reports => _peers.Values.Where(peer => peer.Report != null).Select(peer => peer.Report).ToList();

    public int PeerCount => _peers.Count;

    public PeerReport ReportFor(ulong peer) => _peers.TryGetValue(peer, out var state) ? state.Report : null;

    public bool HasPeer(ulong peer) => _peers.ContainsKey(peer);

    public int RepeatedHellosFrom(ulong peer) => _peers.TryGetValue(peer, out var state) ? state.RepeatedHellos : 0;

    public IReadOnlyList<ulong> PendingPeers => _peers.Where(pair => pair.Value.Report == null).Select(pair => pair.Key).ToList();

    public void OnPeerConnected(ulong peer)
    {
        if (peer == _transport.LocalId || _peers.ContainsKey(peer))
        {
            return;
        }

        _peers[peer] = new PeerState(_clock());
    }

    public void OnPeerDisconnected(ulong peer) => _peers.Remove(peer);

    public void OnReceived(ulong peer, byte[] data)
    {
        if (peer == _transport.LocalId)
        {
            return;
        }

        DecodedMessage message;
        try
        {
            message = MessageCodec.Decode(data);
        }
        catch (WireFormatException exception)
        {
            _log?.Warning($"Ignored a malformed message from {peer}: {exception.Message}");
            return;
        }

        if (!_peers.TryGetValue(peer, out var state))
        {
            state = new PeerState(_clock());
            _peers[peer] = state;
        }

        if (message.IsProtocolMismatch)
        {
            var problems = new[] { new CompatibilityProblem(ProblemKind.ProtocolMismatch, "catlib", MessageCodec.ProtocolVersion.ToString(), message.Protocol.ToString()) };
            var disconnect = ShouldDisconnect();
            Complete(peer, state, new PeerReport(peer, SessionStatus.Rejected, problems, null, null, disconnect));
            Send(peer, MessageCodec.Encode(new VerdictMessage(false, problems, Array.Empty<SessionSettingValue>(), disconnect)));
            return;
        }

        if (message.Payload is not HelloMessage hello)
        {
            _log?.Warning($"Ignored an unexpected {message.Type} message from {peer}");
            return;
        }

        var found = Compatibility.Compare(_identity, hello.Identity);
        var accepted = found.Count == 0;
        var disconnecting = !accepted && ShouldDisconnect();
        var settings = accepted ? _snapshot() : Array.Empty<SessionSettingValue>();
        Send(peer, MessageCodec.Encode(new VerdictMessage(accepted, found, settings, disconnecting)));

        var status = accepted ? SessionStatus.Accepted : SessionStatus.Rejected;
        if (state.Report != null && state.Report.Status == status)
        {
            state.RepeatedHellos++;
            return;
        }

        Complete(peer, state, new PeerReport(peer, status, found, hello.Identity.CatLibVersion, hello.Identity.GameVersion, disconnecting));
    }

    public int AnnouncesSentTo(ulong peer) => _peers.TryGetValue(peer, out var state) ? state.Announces : 0;

    public void Update()
    {
        var now = _clock();
        foreach (var pair in _peers.ToList())
        {
            var state = pair.Value;
            if (state.Report != null)
            {
                continue;
            }

            if (now - state.LastAnnounce >= _announceInterval)
            {
                state.LastAnnounce = now;
                state.Announces++;
                Send(pair.Key, _announce);
            }

            if (now - state.ConnectedAt < _helloTimeout)
            {
                continue;
            }

            var problems = Compatibility.Compare(_identity, null);
            Complete(pair.Key, state, new PeerReport(pair.Key, SessionStatus.PeerWithoutCatLib, problems, null, null, problems.Count > 0 && ShouldDisconnect()));
        }
    }

    public int BroadcastSettings(IReadOnlyList<SessionSettingValue> values)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        var payload = MessageCodec.Encode(new SettingsUpdateMessage(values));
        var sent = 0;
        foreach (var pair in _peers)
        {
            if (pair.Value.Report?.Status == SessionStatus.Accepted)
            {
                Send(pair.Key, payload);
                sent++;
            }
        }

        return sent;
    }

    private bool ShouldDisconnect()
    {
        try
        {
            return _disconnectIncompatible?.Invoke() ?? false;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private void Complete(ulong peer, PeerState state, PeerReport report)
    {
        state.Report = report;
        SafeInvoker.Invoke(PeerEvaluated, report, "HostSession.PeerEvaluated", _log);
    }

    private void Send(ulong peer, byte[] payload)
    {
        try
        {
            _transport.Send(peer, payload);
        }
        catch (Exception exception)
        {
            _log?.Warning($"Sending to {peer} failed: {exception.Message}");
        }
    }

    private sealed class PeerState
    {
        public PeerState(double connectedAt)
        {
            ConnectedAt = connectedAt;
        }

        public double ConnectedAt { get; }

        public PeerReport Report { get; set; }

        public int RepeatedHellos { get; set; }

        public double LastAnnounce { get; set; } = double.NegativeInfinity;

        public int Announces { get; set; }
    }
}
