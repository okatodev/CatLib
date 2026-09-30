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

    public event Action<ulong, ModMessageData> ModMessageReceived;

    public IReadOnlyList<PeerReport> Reports => _peers.Values.Where(peer => peer.Report != null).Select(peer => peer.Report).ToList();

    public int PeerCount => _peers.Count;

    public PeerReport ReportFor(ulong peer) => _peers.TryGetValue(peer, out var state) ? state.Report : null;

    public bool HasPeer(ulong peer) => _peers.ContainsKey(peer);

    public int RepeatedHellosFrom(ulong peer) => _peers.TryGetValue(peer, out var state) ? state.RepeatedHellos : 0;

    public IReadOnlyCollection<string> SharedModsOf(ulong peer) => _peers.TryGetValue(peer, out var state) ? state.SharedMods : Array.Empty<string>();

    public bool PeerShares(ulong peer, string modId) => _peers.TryGetValue(peer, out var state) && state.SharedMods.Contains(modId);

    public IReadOnlyList<ulong> PeersSharing(string modId) => _peers.Where(pair => pair.Value.SharedMods.Contains(modId)).Select(pair => pair.Key).OrderBy(peer => peer).ToList();

    public bool Send(ulong peer, ModMessageData message)
    {
        if (!PeerShares(peer, message.ModId))
        {
            return false;
        }

        Send(peer, MessageCodec.Encode(message));
        return true;
    }

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
            state.Identity = null;
            Complete(peer, state, new PeerReport(peer, SessionStatus.Rejected, problems, null, null, disconnect));
            Send(peer, MessageCodec.Encode(new VerdictMessage(false, problems, Array.Empty<SessionSettingValue>(), disconnect)));
            return;
        }

        if (message.Payload is ModMessageData modMessage)
        {
            if (state.Report == null)
            {
                _log?.Warning($"Ignored a message of {modMessage.ModId} from {peer} before its mods were checked");
                return;
            }

            SafeInvoker.Invoke(ModMessageReceived, peer, modMessage, "HostSession.ModMessageReceived", _log);
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
        state.Identity = hello.Identity;
        state.Excluded = ExcludedOwners(found);
        var settings = disconnecting ? Array.Empty<SessionSettingValue>() : Filter(_snapshot(), state.Excluded);
        var shared = disconnecting ? new List<string>() : SharedMods(_identity, hello.Identity, found);
        state.SharedMods = new HashSet<string>(shared, StringComparer.Ordinal);
        Send(peer, MessageCodec.Encode(new VerdictMessage(accepted, found, settings, disconnecting, shared)));

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

        var sent = 0;
        foreach (var pair in _peers)
        {
            if (!ReceivesSettings(pair.Value))
            {
                continue;
            }

            var filtered = Filter(values, pair.Value.Excluded);
            if (filtered.Count == 0)
            {
                continue;
            }

            Send(pair.Key, MessageCodec.Encode(new SettingsUpdateMessage(filtered)));
            sent++;
        }

        return sent;
    }

    public SessionRoster BuildRoster(ulong hostId, string hostName, IncompatiblePlayerAction policy, Func<ulong, string> nameOf)
    {
        var peers = _peers
            .OrderBy(pair => pair.Value.ConnectedAt)
            .ThenBy(pair => pair.Key)
            .Select(pair => new RosterPeer(pair.Key, nameOf?.Invoke(pair.Key) ?? pair.Key.ToString(System.Globalization.CultureInfo.InvariantCulture),
                PeerStateOf(pair.Value), pair.Value.Identity, pair.Value.Report?.Problems, pair.Value.Report?.Disconnecting ?? false))
            .ToList();
        return RosterBuilder.Build(_identity, hostId, hostName, policy, peers);
    }

    public int SendRoster(SessionRoster roster)
    {
        var payload = MessageCodec.Encode(new RosterMessage(roster));
        var sent = 0;
        foreach (var pair in _peers)
        {
            if (pair.Value.Report != null && pair.Value.Identity != null)
            {
                Send(pair.Key, payload);
                sent++;
            }
        }

        return sent;
    }

    public IReadOnlyList<ulong> SetDisconnecting(bool disconnect)
    {
        var changed = new List<ulong>();
        foreach (var pair in _peers)
        {
            var report = pair.Value.Report;
            if (report == null || report.IsCompatible || report.Disconnecting == disconnect)
            {
                continue;
            }

            pair.Value.Report = report with { Disconnecting = disconnect };
            if (disconnect)
            {
                pair.Value.SharedMods = new HashSet<string>(StringComparer.Ordinal);
            }
            else if (pair.Value.Identity != null)
            {
                pair.Value.SharedMods = new HashSet<string>(SharedMods(_identity, pair.Value.Identity, report.Problems), StringComparer.Ordinal);
            }

            changed.Add(pair.Key);
        }

        return changed;
    }

    public static IReadOnlyList<SessionSettingValue> Filter(IReadOnlyList<SessionSettingValue> values, ISet<string> excludedOwners)
    {
        if (excludedOwners == null || excludedOwners.Count == 0)
        {
            return values;
        }

        return values.Where(value => !excludedOwners.Contains(value.OwnerId)).ToList();
    }

    private static HashSet<string> ExcludedOwners(IReadOnlyList<CompatibilityProblem> problems) =>
        new(problems.Where(problem => problem.Kind is ProblemKind.MissingOnClient or ProblemKind.MissingOnHost or ProblemKind.VersionMismatch)
            .Select(problem => problem.Subject), StringComparer.Ordinal);

    private static bool ReceivesSettings(PeerState state) =>
        state.Report != null && !state.Report.Disconnecting && state.Identity != null &&
        state.Report.Status is SessionStatus.Accepted or SessionStatus.Rejected;

    private static RosterPeerState PeerStateOf(PeerState state) =>
        state.Report == null
            ? RosterPeerState.Checking
            : state.Report.Status == SessionStatus.PeerWithoutCatLib ? RosterPeerState.WithoutCatLib : RosterPeerState.Evaluated;

    public static List<string> SharedMods(LocalIdentity host, LocalIdentity client, IReadOnlyList<CompatibilityProblem> problems) =>
        host.Mods
            .Where(mod => client.Find(mod.Id) != null && problems.All(problem => problem.Subject != mod.Id))
            .Select(mod => mod.Id)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

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

        public HashSet<string> SharedMods { get; set; } = new(StringComparer.Ordinal);

        public LocalIdentity Identity { get; set; }

        public HashSet<string> Excluded { get; set; } = new(StringComparer.Ordinal);
    }
}
