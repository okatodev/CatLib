using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using CatLib.Config;
using CatLib.Game;
using CatLib.Game.Events;
using CatLib.Logging;
using CatLib.Threading;
using CatLib.UI;
using Steamworks;

namespace CatLib.Net;

internal static class SessionNetwork
{
    public const double HandshakeTimeoutSeconds = 12;
    public const double HelloResendSeconds = 2;
    public const double AcceptRetrySeconds = 1;
    public static readonly TimeSpan DepartedCloseSettle = TimeSpan.FromSeconds(5);
    public const string SteamShutdownPatchOwner = "catlib.core.steamshutdown";
    public const double KickGraceSeconds = 8;
    public const double ClientLeaveDelaySeconds = 4;
    public const string HostStartedEventName = "Net.HostStarted";
    public const string ClientStartedEventName = "Net.ClientStarted";
    public const string HostIdentifiedEventName = "Net.HostIdentified";
    public const string PeerEvaluatedEventName = "Net.PeerEvaluated";
    public const string HandshakeCompletedEventName = "Net.HandshakeCompleted";
    public const string StoppedEventName = "Net.Stopped";
    public const string RosterEventName = "Net.Roster";
    public const string ActiveModsEventName = "Net.ActiveMods";
    public const double RosterIntervalSeconds = 0.5;

    private static readonly Stopwatch Clock = new();
    private static readonly HashSet<ulong> HostCandidates = new();
    private static CatLib.Patching.CatPatches _steamShutdown;
    private static TimeSpan _departedClosedAt = TimeSpan.MinValue;
    private static readonly Dictionary<ulong, double> PendingKicks = new();
    private static CatLogger _log;
    private static Setting<bool> _enabled;
    private static Setting<IncompatiblePlayerAction> _onIncompatible;
    private static Setting<SteamApiBackend> _backend;
    private static double _lastHello;
    private static double _lastAccept;
    private static ulong _identifiedHost;
    private static double _leaveAt = double.PositiveInfinity;
    private static bool _restarting;
    private static ClientSession _heldSettings;
    private static double _nextRoster;
    private static byte[] _lastRosterBytes;
    private static HashSet<string> _active;
    private static bool _leaveFromRoster;

    public static SessionRoster Roster { get; private set; }

    public static event Action<SessionRoster> RosterChanged;

    public static event Action ActiveModsChanged;

    public static IncompatiblePlayerAction IncompatiblePolicy
    {
        get => _onIncompatible?.Value ?? IncompatiblePlayerAction.Warn;
        set
        {
            if (_onIncompatible != null && _onIncompatible.LocalValue != value)
            {
                _onIncompatible.LocalValue = value;
            }
        }
    }

    public static bool IsModActive(string modId)
    {
        var active = _active;
        if (active == null || string.IsNullOrEmpty(modId))
        {
            return true;
        }

        var declared = CatNetwork.DeclaredMods.FirstOrDefault(mod => string.Equals(mod.Id, modId, StringComparison.Ordinal));
        return declared == null || declared.Policy != SessionPolicy.RequiredOnAll || active.Contains(modId);
    }

    public static SteamMessagesTransport Transport { get; private set; }

    public static HostSession Host { get; private set; }

    public static ClientSession Client { get; private set; }

    public static SteamSelfCheck SelfCheck { get; private set; }

    public static ModMessenger Messenger { get; private set; }

    public static bool IsEnabled => _enabled == null || _enabled.Value;

    public static SteamApiBackend Backend => _backend?.Value ?? SteamApiBackend.Interop;

    private static double Now => Clock.Elapsed.TotalSeconds;

    internal static void Initialize(CatLogger log, CatSettings settings)
    {
        _log = log;
        Clock.Start();
        SelfCheck = new SteamSelfCheck(log.Scope("SelfCheck"));
        Messenger = new ModMessenger(new SessionLink(() => Host, () => Client, () => Transport), () => Now, log.Scope("Messages"));
        _enabled = settings.Local("Network", "Enabled", true,
            "Exchange mod lists and session settings with other players who use CatLib. Takes effect from the next session.");
        _onIncompatible = settings.Local("Network", "OnIncompatiblePlayer", IncompatiblePlayerAction.Warn,
            "What the host does when a joining player's mods do not match: let the player in and pause the mods that not everyone has, or disconnect the player.");
        _onIncompatible.Changed += (_, value) => MainThread.RunOrPost(() => OnPolicyChanged(value));
        _backend = settings.Local("Network", "SteamApi", SteamApiBackend.Interop,
            "How CatLib calls Steam. Change it only when asked to while diagnosing a problem. Takes effect from the next session.");

        NetworkEvents.ServerStarted += OnServerStarted;
        NetworkEvents.ClientConnected += OnClientConnected;
        NetworkEvents.OtherClientConnected += OnOtherClientConnected;
        NetworkEvents.ClientDisconnected += OnClientDisconnected;
        NetworkEvents.ClientConnectionAcknowledged += OnConnectionAcknowledged;
        BootstrapEvents.GameRestartStarted += () =>
        {
            _restarting = true;
            CloseChannelSessions();
            MainThread.Post(Stop);
        };
        _steamShutdown = new CatLib.Patching.CatPatches(SteamShutdownPatchOwner, log)
            .Prefix(typeof(SteamAPI), nameof(SteamAPI.Shutdown), Type.EmptyTypes, typeof(SessionNetwork), nameof(BeforeSteamShutdown));
        _steamShutdown.Apply();
        BootstrapEvents.MainMenuLoaded += EndRestart;
        BootstrapEvents.LevelLoadStarted += EndRestart;
        CatConfig.EffectiveValueChanged += OnEffectiveValueChanged;
    }

    internal static void RunSelfCheck()
    {
        var self = SteamUserId();
        if (!SteamIds.IsIndividual(self))
        {
            _log.Warning($"Steam self check: the Steam user id {self} is not valid, is Steam running?");
            return;
        }

        _log.Info($"Steam self check as Steam user {self}");
        SelfCheck.Start(self, Backend);
    }

    internal static void Update()
    {
        if (_log == null)
        {
            return;
        }

        try
        {
            SelfCheck.Update();
        }
        catch (Exception exception)
        {
            _log.Error("Steam self check failed", exception);
        }

        try
        {
            Messenger.Update();
        }
        catch (Exception exception)
        {
            _log.Error("Delivering local mod messages failed", exception);
        }

        if (Transport == null)
        {
            return;
        }

        try
        {
            var now = Now;
            if (now - _lastAccept >= AcceptRetrySeconds)
            {
                _lastAccept = now;
                AcceptPending();
            }

            Transport.Poll(Route);
            Host?.Update();
            ProcessPendingKicks(now);
            if (Host != null && now >= _nextRoster)
            {
                _nextRoster = now + RosterIntervalSeconds;
                PublishHostRoster();
            }

            if (Client != null)
            {
                if (Client.HostId != 0 && _identifiedHost != Client.HostId)
                {
                    _identifiedHost = Client.HostId;
                    _log.Info($"The host introduced itself as {_identifiedHost}");
                    GameEventStream.Publish(HostIdentifiedEventName, $"host={_identifiedHost}");
                }

                if (Client.Status == SessionStatus.Waiting && Client.HostId != 0 && now - _lastHello >= HelloResendSeconds)
                {
                    _lastHello = now;
                    Client.ResendHello();
                }

                Client.Update();
            }

            if (now >= _leaveAt)
            {
                _leaveAt = double.PositiveInfinity;
                LeaveLobby();
            }
        }
        catch (Exception exception)
        {
            _log.Error("Network update failed", exception);
        }
    }

    internal static void Stop()
    {
        HostCandidates.Clear();
        PendingKicks.Clear();
        MenuNotices.ClearPlayers();
        _leaveAt = double.PositiveInfinity;
        _identifiedHost = 0;
        if (Transport == null && Host == null && Client == null)
        {
            return;
        }

        CloseChannelSessions();
        var holdSettings = _restarting && Client != null;
        try
        {
            Client?.Stop(!holdSettings);
        }
        catch (Exception exception)
        {
            _log.Error("Stopping the client session failed", exception);
        }

        if (holdSettings)
        {
            _heldSettings = Client;
        }

        Host = null;
        Client = null;
        Transport = null;
        Messenger.Reset();
        _lastRosterBytes = null;
        _leaveFromRoster = false;
        SetRoster(null);
        SetActive(null);
        _log.Info(holdSettings
            ? "Network session stopped, session overrides are kept until the level is unloaded"
            : "Network session stopped, session overrides cleared");
        GameEventStream.Publish(StoppedEventName);
    }

    internal static void OnQuitting() => CloseChannelSessions();

    private static void BeforeSteamShutdown()
    {
        if (_steamShutdown == null || !_steamShutdown.IsActive)
        {
            return;
        }

        try
        {
            CloseChannelSessions();
            var since = _departedClosedAt == TimeSpan.MinValue ? TimeSpan.MaxValue : Clock.Elapsed - _departedClosedAt;
            var delay = ShutdownDelay(since);
            if (delay > TimeSpan.Zero)
            {
                _log.Info($"A player left {since.TotalSeconds:0.0} s ago, waiting {delay.TotalSeconds:0.0} s so Steam finishes closing the CatLib session with that player before it shuts down");
                System.Threading.Thread.Sleep(delay);
            }
        }
        catch (Exception exception)
        {
            _steamShutdown.Fault(nameof(BeforeSteamShutdown), exception);
        }
    }

    internal static TimeSpan ShutdownDelay(TimeSpan sinceDepartedClose) =>
        sinceDepartedClose < TimeSpan.Zero || sinceDepartedClose >= DepartedCloseSettle ? TimeSpan.Zero : DepartedCloseSettle - sinceDepartedClose;

    private static void CloseChannelSessions()
    {
        var transport = Transport;
        if (transport == null || transport.Peers.Count == 0)
        {
            return;
        }

        var closed = transport.CloseAll();
        _log.Info($"Closed {closed} CatLib channel session(s) before the game shuts its network down");
    }

    private static void EndRestart()
    {
        _restarting = false;
        ReleaseHeldSettings();
    }

    private static void ReleaseHeldSettings()
    {
        var held = _heldSettings;
        _heldSettings = null;
        if (held == null)
        {
            return;
        }

        try
        {
            held.ReleaseSettings();
            _log.Info("Session overrides cleared after the level was unloaded");
        }
        catch (Exception exception)
        {
            _log.Error("Clearing the session overrides failed", exception);
        }
    }

    private static void AcceptPending()
    {
        var peers = PeersToAccept(Host?.Peers, Client?.HostId ?? 0, Client != null && Client.Status == SessionStatus.Waiting, HostCandidates);
        foreach (var peer in peers)
        {
            Transport.Accept(peer);
        }
    }

    internal static IReadOnlyList<ulong> PeersToAccept(IEnumerable<ulong> hostPeers, ulong hostId, bool clientWaiting, IEnumerable<ulong> candidates)
    {
        var result = new List<ulong>();
        if (hostPeers != null)
        {
            result.AddRange(hostPeers);
        }

        if (hostId != 0)
        {
            result.Add(hostId);
        }
        else if (clientWaiting && candidates != null)
        {
            result.AddRange(candidates);
        }

        return result.Where(peer => peer != 0).Distinct().ToList();
    }

    private static void OnServerStarted() => MainThread.Post(StartHost);

    private static void StartHost()
    {
        ReleaseHeldSettings();
        if (!IsEnabled)
        {
            _log.Info("Network is disabled in the CatLib settings, the host will not check mods");
            return;
        }

        var localId = LocalId();
        if (localId == 0)
        {
            _log.Warning("The local Steam id is unknown, mod checks are unavailable for this session");
            return;
        }

        Transport = new SteamMessagesTransport(localId, SteamMessagesTransport.CreateApi(Backend, _log), _log.Scope("Channel"));
        Host = new HostSession(Transport, Identity(), SessionSettings.Snapshot, () => Now, HandshakeTimeoutSeconds, _log.Scope("Host"), 1,
            () => _onIncompatible.Value == IncompatiblePlayerAction.Disconnect);
        var host = Host;
        host.PeerEvaluated += OnPeerEvaluated;
        host.PeerEvaluated += report => Messenger.OnPeerEvaluated(report.PeerId, host.SharedModsOf(report.PeerId));
        host.ModMessageReceived += Messenger.OnHostReceived;
        _nextRoster = 0;
        _lastRosterBytes = null;
        _log.Info($"Hosting as {localId} through the {Transport.ApiName} Steam API, declared mods: {CatNetwork.DeclaredMods.Count}");
        GameEventStream.Publish(HostStartedEventName, $"id={localId} api={Transport.ApiName}");
    }

    private static void OnClientConnected(ulong clientId)
    {
        MainThread.Post(() =>
        {
            if (Host != null && Transport != null && clientId != Transport.LocalId)
            {
                Host.OnPeerConnected(clientId);
            }
        });
    }

    private static void OnOtherClientConnected(ulong clientId) => MainThread.Post(() =>
    {
        if (SteamIds.IsIndividual(clientId))
        {
            HostCandidates.Add(clientId);
        }
    });

    private static void OnClientDisconnected(ulong clientId)
    {
        MainThread.Post(() =>
        {
            if (Transport == null)
            {
                return;
            }

            if (clientId == Transport.LocalId)
            {
                Stop();
                return;
            }

            PendingKicks.Remove(clientId);
            if (Transport.Close(clientId))
            {
                _departedClosedAt = Clock.Elapsed;
            }

            MenuNotices.ClearPlayerNotice(clientId);
            Host?.OnPeerDisconnected(clientId);
            Messenger.OnPeerLeft(clientId);
        });
    }

    private static void OnConnectionAcknowledged(ulong clientId) => MainThread.Post(() => StartClient(clientId));

    private static void StartClient(ulong clientId)
    {
        ReleaseHeldSettings();
        if (!IsEnabled || Host != null || IsServer())
        {
            return;
        }

        if (!SteamIds.IsIndividual(clientId))
        {
            _log.Warning($"The client id {clientId} is not a Steam user id, mod checks are unavailable for this connection");
            return;
        }

        Transport = new SteamMessagesTransport(clientId, SteamMessagesTransport.CreateApi(Backend, _log), _log.Scope("Channel"));
        Client = new ClientSession(Transport, Identity(), SessionSettings.Instance, () => Now, HandshakeTimeoutSeconds, _log.Scope("Client"), HostCandidates.Contains);
        Client.Completed += OnHandshakeCompleted;
        Client.SettingsApplied += OnSettingsApplied;
        var client = Client;
        client.ModMessageReceived += data => Messenger.OnClientReceived(client.HostId, data);
        client.RosterReceived += OnClientRoster;
        Client.Start();
        MenuNotices.ClearLobby();
        _lastAccept = double.NegativeInfinity;
        _log.Info($"Joined as {clientId} through the {Transport.ApiName} Steam API, waiting for the host to introduce itself");
        GameEventStream.Publish(ClientStartedEventName, $"self={clientId} api={Transport.ApiName}");
    }

    private static void Route(ulong sender, byte[] payload)
    {
        if (Host != null)
        {
            Host.OnReceived(sender, payload);
        }
        else
        {
            Client?.OnReceived(sender, payload);
        }
    }

    private static void OnEffectiveValueChanged(ISetting setting)
    {
        if (Host == null || setting.Scope != SettingScope.Session)
        {
            return;
        }

        var sent = Host.BroadcastSettings(new[] { SessionSettings.ToValue(setting) });
        if (sent > 0)
        {
            _log.Info($"Sent {setting.Id} to {sent} player(s)");
        }
    }

    private static void OnPeerEvaluated(PeerReport report)
    {
        var problems = string.Join("; ", report.Problems);
        _log.Info($"Player {report.PeerId}: {report.Status}{(problems.Length > 0 ? ", " + problems : string.Empty)}");
        GameEventStream.Publish(PeerEvaluatedEventName, $"peer={report.PeerId} status={report.Status} problems={report.Problems.Count}");

        if (report.IsCompatible)
        {
            return;
        }

        var language = UiText.LanguageCode;
        var summary = ProblemText.Summarize(report.Problems, language, ModName);
        var mods = ProblemText.ModsOnly(report.Problems, ModName);
        var name = PlayerName(report.PeerId);
        string message;
        if (report.Disconnecting)
        {
            PendingKicks[report.PeerId] = Now + KickGraceSeconds;
            _log.Info($"Player {report.PeerId} will be disconnected in {KickGraceSeconds} seconds unless it leaves first");
            message = UiText.Format(UiText.NetPlayerDisconnected, language, name, summary);
            PlayerMessages.Post(message, UiText.Format(UiText.NetPlayerDisconnectedBrief, language, name, mods));
        }
        else
        {
            message = UiText.Format(UiText.NetPlayerIncompatible, language, name, summary);
            PlayerMessages.Post(message, UiText.Format(UiText.NetPlayerIncompatibleBrief, language, name, mods));
        }

        MenuNotices.SetPlayerNotice(report.PeerId, UiText.Get(UiText.NetCardOtherMods, language));
    }

    private static void ProcessPendingKicks(double now)
    {
        if (PendingKicks.Count == 0 || Host == null)
        {
            return;
        }

        foreach (var pair in PendingKicks.ToList())
        {
            if (now < pair.Value)
            {
                continue;
            }

            PendingKicks.Remove(pair.Key);
            if (Host.HasPeer(pair.Key))
            {
                _log.Info($"Player {pair.Key} is still connected on the host side after {KickGraceSeconds} seconds, disconnecting it");
                TryDisconnect(pair.Key);
            }
        }
    }

    private static void OnHandshakeCompleted(PeerReport report)
    {
        var problems = string.Join("; ", report.Problems);
        _log.Info($"Handshake with the host: {report.Status}{(problems.Length > 0 ? ", " + problems : string.Empty)}");
        GameEventStream.Publish(HandshakeCompletedEventName, $"status={report.Status} problems={report.Problems.Count}");

        if (report.Status == SessionStatus.PeerWithoutCatLib || report.Problems.Any(problem => problem.Kind == ProblemKind.ProtocolMismatch))
        {
            var client = Client;
            var roster = RosterBuilder.WithoutHostCatLib(Identity(), Transport?.LocalId ?? 0, OwnName(), client?.HostId ?? 0,
                client == null || client.HostId == 0 ? string.Empty : PlayerName(client.HostId), report.Problems);
            SetRoster(roster);
            SetActive(roster.ActiveMods);
        }

        if (report.IsCompatible)
        {
            return;
        }

        var language = UiText.LanguageCode;
        var message = report.Status == SessionStatus.PeerWithoutCatLib
            ? UiText.Format(UiText.NetHostWithoutCatLib, language, ProblemText.ModsOnly(report.Problems, ModName))
            : UiText.Format(UiText.NetHostIncompatible, language, ProblemText.Summarize(report.Problems, language, ModName));

        if (report.Disconnecting)
        {
            message = UiText.Format(UiText.NetYouWillBeDisconnected, language, ProblemText.Summarize(report.Problems, language, ModName));
            _leaveAt = Now + ClientLeaveDelaySeconds;
            _log.Info($"The host will disconnect this player because of incompatible mods, leaving the lobby in {ClientLeaveDelaySeconds} seconds");
        }

        PlayerMessages.Post(message);
        MenuNotices.ShowInLobby(message);
    }

    private static void PublishHostRoster()
    {
        var host = Host;
        var roster = host.BuildRoster(Transport.LocalId, OwnName(), IncompatiblePolicy, PlayerName);
        var bytes = MessageCodec.Encode(new RosterMessage(roster));
        if (_lastRosterBytes != null && bytes.AsSpan().SequenceEqual(_lastRosterBytes))
        {
            return;
        }

        _lastRosterBytes = bytes;
        var sent = host.SendRoster(roster);
        SetRoster(roster);
        SetActive(roster.ActiveMods);
        _log.Info($"Session roster: {Describe(roster)}, sent to {sent} player(s)");
    }

    private static void OnClientRoster(SessionRoster roster)
    {
        SetRoster(roster);
        SetActive(roster.ActiveMods);
        _log.Info($"Session roster from the host: {Describe(roster)}");

        var self = Transport == null ? null : roster.Find(Transport.LocalId);
        var language = UiText.LanguageCode;
        if (self != null && self.Status == RosterStatus.Leaving && double.IsPositiveInfinity(_leaveAt))
        {
            _leaveFromRoster = true;
            _leaveAt = Now + ClientLeaveDelaySeconds;
            var message = UiText.Format(UiText.NetYouWillBeDisconnected, language, ProblemText.Summarize(self.Problems, language, ModName));
            _log.Info($"The host will disconnect this player because of incompatible mods, leaving the lobby in {ClientLeaveDelaySeconds} seconds");
            PlayerMessages.Post(message);
            MenuNotices.ShowInLobby(message);
        }
        else if (self != null && self.Status != RosterStatus.Leaving && _leaveFromRoster)
        {
            _leaveFromRoster = false;
            _leaveAt = double.PositiveInfinity;
            MenuNotices.ClearLobby();
            _log.Info("The host lets this player stay after all");
        }
    }

    private static void OnPolicyChanged(IncompatiblePlayerAction value)
    {
        var host = Host;
        if (host == null)
        {
            return;
        }

        var changed = host.SetDisconnecting(value == IncompatiblePlayerAction.Disconnect);
        foreach (var peer in changed)
        {
            if (value == IncompatiblePlayerAction.Disconnect)
            {
                PendingKicks[peer] = Now + KickGraceSeconds;
            }
            else
            {
                PendingKicks.Remove(peer);
            }
        }

        _nextRoster = 0;
        _log.Info($"Incompatible players: {value}, {changed.Count} player(s) affected");
    }

    private static void SetRoster(SessionRoster roster)
    {
        Roster = roster;
        if (roster != null)
        {
            GameEventStream.Publish(RosterEventName, $"players={roster.Players.Count} active={roster.ActiveMods.Count} policy={roster.Policy}");
        }

        Events.SafeInvoker.Invoke(RosterChanged, roster, "CatNetwork.RosterChanged", _log);
    }

    private static void SetActive(IReadOnlyList<string> active)
    {
        var next = active == null ? null : new HashSet<string>(active, StringComparer.Ordinal);
        var previous = _active;
        var same = previous == null ? next == null : next != null && previous.SetEquals(next);
        if (same)
        {
            return;
        }

        _active = next;
        var paused = CatNetwork.DeclaredMods.Where(mod => mod.Policy == SessionPolicy.RequiredOnAll && !IsModActive(mod.Id)).Select(mod => mod.Id).ToList();
        _log.Info(paused.Count == 0 ? "All mods are active in this session" : $"Paused in this session because not every player has them: {string.Join(", ", paused)}");
        GameEventStream.Publish(ActiveModsEventName, $"paused={(paused.Count == 0 ? "none" : string.Join(",", paused))}");
        Events.SafeInvoker.Invoke(ActiveModsChanged, "CatNetwork.ActiveModsChanged", _log);
    }

    private static string Describe(SessionRoster roster) =>
        string.Join("; ", roster.Players.Select(player =>
            $"{player.Name} ({player.Id}){(player.IsHost ? " host" : string.Empty)} {player.Status}, {player.ModCount} mod(s)" +
            (player.ProblemCount > 0 ? $", {player.ProblemCount} problem(s)" : string.Empty))) +
        $"; active: {(roster.ActiveMods.Count == 0 ? "none" : string.Join(", ", roster.ActiveMods))}";

    private static string OwnName()
    {
        try
        {
            var name = SteamFriends.GetPersonaName();
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }
        }
        catch (Exception exception)
        {
            _log.Debug($"Reading the own Steam name failed: {exception.Message}");
        }

        var id = Transport?.LocalId ?? 0;
        return id == 0 ? string.Empty : PlayerName(id);
    }

    private static void LeaveLobby()
    {
        try
        {
            if (MenuNotices.LeaveLobby())
            {
                _log.Info("Left the lobby through its back button");
            }
            else
            {
                _log.Warning("The lobby is not shown, waiting for the host to disconnect this player");
            }
        }
        catch (Exception exception)
        {
            _log.Error("Leaving the lobby failed", exception);
        }
    }

    private static string ModName(string id) => CatNetwork.DeclaredMods.FirstOrDefault(mod => mod.Id == id)?.Name;

    internal static string PlayerName(ulong steamId)
    {
        try
        {
            var name = SteamFriends.GetFriendPersonaName(new CSteamID(steamId));
            if (!string.IsNullOrWhiteSpace(name) && name != "[unknown]")
            {
                return name;
            }
        }
        catch (Exception exception)
        {
            _log.Debug($"Reading the Steam name of {steamId} failed: {exception.Message}");
        }

        var lobbyName = MenuNotices.LobbyName(steamId);
        if (!string.IsNullOrWhiteSpace(lobbyName))
        {
            return lobbyName;
        }

        return steamId.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void OnSettingsApplied(SessionApplyResult result)
    {
        _log.Info($"Applied {result.Applied} host setting(s), unknown {result.Unknown.Count}, invalid {result.Invalid.Count}, need a restart {result.RestartBlocked.Count}");
    }

    private static LocalIdentity Identity() => CatNetwork.CreateIdentity(PluginMeta.Version, GameInfo.GameVersion);

    private static bool IsServer()
    {
        try
        {
            return Singleton<NetworkManager>.HasInstance() && Singleton<NetworkManager>.Instance.IsServer;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static ulong LocalId()
    {
        try
        {
            if (Singleton<NetworkManager>.HasInstance())
            {
                var id = Singleton<NetworkManager>.Instance.ClientId;
                if (SteamIds.IsIndividual(id))
                {
                    return id;
                }
            }
        }
        catch (Exception exception)
        {
            _log.Warning($"Reading the game client id failed: {exception.Message}");
        }

        var steamId = SteamUserId();
        return SteamIds.IsIndividual(steamId) ? steamId : 0;
    }

    private static ulong SteamUserId()
    {
        try
        {
            return SteamUser.GetSteamID().m_SteamID;
        }
        catch (Exception exception)
        {
            _log.Warning($"Reading the Steam user id failed: {exception.Message}");
            return 0;
        }
    }

    private static bool TryDisconnect(ulong peer)
    {
        try
        {
            var server = Singleton<NetworkManager>.HasInstance() ? Singleton<NetworkManager>.Instance._server : null;
            if (server == null)
            {
                return false;
            }

            server.DisconnectClient(peer);
            _log.Info($"Disconnected player {peer} because of incompatible mods");
            return true;
        }
        catch (Exception exception)
        {
            _log.Error($"Disconnecting player {peer} failed", exception);
            return false;
        }
    }
}
