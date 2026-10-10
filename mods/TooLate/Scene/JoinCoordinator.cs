using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CatLib.Core;
using CatLib.Game.Events;
using CatLib.Localization;
using CatLib.Logging;
using CatLib.Net;
using CatLib.Patching;
using CatLib.UI;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using TooLate.Logic;
using TooLate.Settings;
using Object = Il2CppSystem.Object;

namespace TooLate.Scene;

public sealed class JoinCoordinator
{
    public const double LoadingWarningSeconds = 120;
    public const double LoadingGiveUpSeconds = 360;
    public const int FlushPerFrame = 200;
    public const double AppearingGraceSeconds = 3;
    public const string EventPrefix = "TooLate.";

    private readonly CatLogger _log;
    private readonly TextCatalog _texts;
    private readonly Dictionary<ulong, JoinTicket<Il2CppReferenceArray<Object>>> _tickets = new();
    private readonly HashSet<ulong> _warned = new();
    private readonly StateLedger _state = new();
    private readonly LastingMessages<Il2CppReferenceArray<Object>> _lasting = new();
    private CodePatch _loading;
    private CodePatch _limit;
    private bool _loadingOpen;
    private bool _startWritten;
    private bool _inLevel;
    private int _limitWritten = PlayerLimit.GamePlayers;

    public JoinCoordinator(CatLogger log, TextCatalog texts)
    {
        _log = log;
        _texts = texts;
    }

    public JoinSettings Settings { get; set; }

    public IReadOnlyCollection<JoinTicket<Il2CppReferenceArray<Object>>> Tickets => _tickets.Values;

    public bool CanJoinInLevel => _loading is { IsFound: true };

    public bool CanRaiseLimit => _limit is { IsFound: true };

    public void UseCodePatches(CodePatch loading, CodePatch limit)
    {
        _loading = loading;
        _limit = limit;
    }

    public void Update()
    {
        var state = ReadHost();
        UpdateCodePatches(state);
        if (!state.Hosting)
        {
            if (_tickets.Count > 0)
            {
                _tickets.Clear();
            }

            return;
        }

        foreach (var ticket in _tickets.Values.ToList())
        {
            if (ticket.FailReason != null)
            {
                Fail(ticket, ticket.FailReason);
                continue;
            }

            if (ticket.Flushing)
            {
                Flush(ticket);
                continue;
            }

            switch (ticket.Stage)
            {
                case JoinStage.Waiting when JoinGate.ReadyToStart(state, FrameLoop.Realtime - ticket.ConnectedAt):
                    Start(ticket);
                    break;
                case JoinStage.Waiting when !ticket.WaitingNoticeShown && JoinGate.IsRecap(state.TimePeriod):
                    ticket.WaitingNoticeShown = true;
                    Notify("message.waiting", ticket.Name);
                    _log.Info($"{ticket.Name} waits in the lobby until the day's results are over");
                    break;
                case JoinStage.Appearing when IsSpawned(ticket.ClientId) && FrameLoop.Realtime - ticket.StageSince > AppearingGraceSeconds:
                    _log.Warning($"{ticket.Name} appeared without the game sending them the identifiers again, Too Late sends them itself");
                    GameProtocol.Send(ProtocolCodes.IdentifiersSynchronization, ticket.ClientId, GameProtocol.Identifiers(ticket.Places, NextIdentifier()));
                    StartFlush(ticket);
                    break;
                case JoinStage.Loading or JoinStage.Appearing when ticket.Overflowed > 0:
                    Fail(ticket, $"more than {JoinTicket<Object>.MostHeldMessages} game messages piled up while the player was loading");
                    break;
                case JoinStage.Loading or JoinStage.Appearing when FrameLoop.Realtime - ticket.StageSince > LoadingGiveUpSeconds:
                    Fail(ticket, $"the player did not finish loading in {LoadingGiveUpSeconds:0} seconds");
                    break;
                case JoinStage.Loading or JoinStage.Appearing when FrameLoop.Realtime - ticket.StageSince > LoadingWarningSeconds && _warned.Add(ticket.ClientId):
                    _log.Warning($"{ticket.Name} has been loading for {LoadingWarningSeconds:0} seconds, {ticket.Held.Count} game message(s) wait for them");
                    break;
                case JoinStage.Synced when IsSpawned(ticket.ClientId):
                    ticket.MoveTo(JoinStage.Playing, FrameLoop.Realtime);
                    Notify("message.joined", ticket.Name);
                    Publish("Joined", ticket);
                    _log.Info($"{ticket.Name} is in the game, {Seconds(FrameLoop.Realtime - ticket.ConnectedAt)} after connecting");
                    break;
            }
        }
    }

    public bool InLevel => _inLevel;

    public void OnClientConnected(ulong clientId)
    {
        if (ShouldTakeOver(clientId))
        {
            Ensure(clientId);
            return;
        }

        if (GameProtocol.IsHosting && CatNetwork.IsRemotePlayer(clientId) && !_tickets.ContainsKey(clientId))
        {
            _log.Info($"Player {clientId} connected and joins the usual way: {(_inLevel ? "joining a game in progress is off in the settings" : "you are in the lobby")}");
        }
    }

    public void OnClientDisconnected(ulong clientId)
    {
        _warned.Remove(clientId);
        if (_tickets.Remove(clientId, out var ticket))
        {
            Publish("Left", ticket);
            _log.Info($"{ticket.Name} left while {ticket.Stage.ToString().ToLowerInvariant()}");
        }
    }

    public bool TakesOverSave(ulong clientId)
    {
        if (!ShouldTakeOver(clientId))
        {
            return false;
        }

        Ensure(clientId);
        return true;
    }

    public void Observe(int code, Il2CppReferenceArray<Object> payload)
    {
        if (!_inLevel || !(code is ProtocolCodes.GenericMessage or ProtocolCodes.EntityDisposed || LastingMessages<Object>.IsLasting(code)) || !GameProtocol.IsHosting)
        {
            return;
        }

        if (code == ProtocolCodes.GenericMessage)
        {
            if (GameProtocol.TryReadGeneric(payload, out var entityId, out var text))
            {
                _state.Record(entityId, text);
            }

            return;
        }

        if (code != ProtocolCodes.EntityDisposed)
        {
            if (!LastingMessages<Object>.IsPerEntity(code))
            {
                _lasting.Record(code, 0, GameProtocol.Copy(payload));
            }
            else if (GameProtocol.TryReadId(payload, out var changed))
            {
                _lasting.Record(code, changed, GameProtocol.Copy(payload));
            }

            return;
        }

        if (GameProtocol.TryReadId(payload, out var disposed))
        {
            _state.Forget(disposed);
            _lasting.Forget(disposed);
        }
    }

    public bool RouteOutgoing(int code, bool reliable, ulong target, Il2CppReferenceArray<Object> payload)
    {
        if (_tickets.Count == 0 || !_tickets.TryGetValue(target, out var ticket))
        {
            return true;
        }

        var route = MessageRoute.For(ticket.Stage, code);
        switch (route)
        {
            case Route.Pass:
                return true;
            case Route.Replace:
                try
                {
                    SendIdentifiers(ticket);
                }
                catch (Exception exception)
                {
                    _log.Error($"Sending the identifiers to {ticket.Name} failed", exception);
                    ticket.FailReason = "the identifiers of the snapshot could not be sent";
                }

                return false;
            case Route.Hold:
            case Route.HoldLatest:
                ticket.Accept(code, reliable, GameProtocol.Copy(payload));
                return false;
            default:
                ticket.Accept(code, reliable, null);
                return false;
        }
    }

    public void ShutDown()
    {
        _log.Warning("Too Late turned itself off: players who are still joining are disconnected, joining a level and the player limit are like in the game again");
        foreach (var ticket in _tickets.Values.Where(ticket => ticket.Stage is JoinStage.Waiting or JoinStage.Loading or JoinStage.Appearing).ToList())
        {
            GameProtocol.Disconnect(ticket.ClientId);
        }

        _tickets.Clear();
        _warned.Clear();
        if (_loading is { IsFound: true })
        {
            _loading.Restore();
        }

        if (_limit is { IsFound: true })
        {
            _limit.Restore();
        }

        _loadingOpen = false;
        _limitWritten = PlayerLimit.GamePlayers;
    }

    public void EnterLevel()
    {
        if (!_inLevel)
        {
            _inLevel = true;
            _startWritten = false;
            ClearState();
            WriteStarted();
        }
    }

    private void WriteStarted()
    {
        if (_startWritten || !_inLevel || !GameProtocol.IsHosting)
        {
            return;
        }

        _startWritten = true;
        _log.Info(CatNetwork.IsWithoutSteamNetwork
            ? "Your game started without the Steam network, it runs on this computer only and nobody can join it"
            : "Your game started, players who connect from now on join the game in progress");
    }

    public void LeaveLevel(bool disconnectWaiting)
    {
        _inLevel = false;
        ClearState();
        Forget(disconnectWaiting);
    }

    private void Forget(bool disconnectWaiting)
    {
        if (disconnectWaiting)
        {
            foreach (var ticket in _tickets.Values.Where(ticket => ticket.Stage is JoinStage.Waiting or JoinStage.Loading or JoinStage.Appearing).ToList())
            {
                _log.Info($"{ticket.Name} is disconnected, the game restarts before they could join");
                GameProtocol.Disconnect(ticket.ClientId);
            }
        }

        _tickets.Clear();
        _warned.Clear();
        UpdateCodePatches(default);
    }

    public string Describe()
    {
        var state = ReadHost();
        var lines = new List<string>
        {
            $"Hosting {state.Hosting}, in a level {_inLevel}, state messages {_state.Count} + {_lasting.Count}, level ready {state.LevelReady}, restarting {state.Restarting}, time period {(GameTimePeriod)state.TimePeriod}, " +
            $"joining in a level {(_loadingOpen && _loading is { IsFound: true } ? "open" : "closed")}, player limit {_limitWritten}"
        };
        lines.AddRange(_tickets.Values.Select(ticket =>
            $"{ticket.Name} ({ticket.ClientId}): {ticket.Stage} for {Seconds(FrameLoop.Realtime - ticket.StageSince)}, held {ticket.Held.Count}, dropped {ticket.Dropped}, places {ticket.Places?.Count ?? 0}"));
        if (_tickets.Count == 0)
        {
            lines.Add("Nobody is joining");
        }

        foreach (var line in lines)
        {
            _log.Info("[Joins] " + line);
        }

        return lines[0];
    }

    public string SnapshotProbe()
    {
        var result = SaveSnapshot.Write();
        var text = "Snapshot written to " + SaveSnapshot.Folder + ": " + result;
        _log.Info(text);
        return text;
    }

    private bool ShouldTakeOver(ulong clientId) =>
        _inLevel && Settings != null && Settings.Enabled.Value && GameProtocol.IsHosting && CatNetwork.IsRemotePlayer(clientId);

    private void Ensure(ulong clientId)
    {
        if (_tickets.ContainsKey(clientId))
        {
            return;
        }

        var ticket = new JoinTicket<Il2CppReferenceArray<Object>>(clientId, CatNetwork.PlayerName(clientId), FrameLoop.Realtime);
        _tickets[clientId] = ticket;
        Notify("message.joining", ticket.Name);
        Publish("Connected", ticket);
        _log.Info($"{ticket.Name} ({clientId}) connected while you are in a level, they wait in the lobby until the game lets them in");
    }

    private void Start(JoinTicket<Il2CppReferenceArray<Object>> ticket)
    {
        try
        {
            var snapshot = SaveSnapshot.Write();
            ticket.Places = snapshot.Places;
            ticket.MoveTo(JoinStage.Loading, FrameLoop.Realtime);
            GameProtocol.SendSave(ticket.ClientId, snapshot.SaveName, snapshot.Data);
            GameProtocol.Send(ProtocolCodes.ServerGameStart, ticket.ClientId, GameProtocol.Empty());
            Publish("Loading", ticket, snapshot.ToString());
            _log.Info($"{ticket.Name} gets the warehouse as it is now and starts loading. Snapshot {snapshot}");
        }
        catch (Exception exception)
        {
            _log.Error($"The snapshot for {ticket.Name} could not be made", exception);
            Fail(ticket, "the snapshot of the game could not be made");
        }
    }

    private void SendIdentifiers(JoinTicket<Il2CppReferenceArray<Object>> ticket)
    {
        if (ticket.Places == null)
        {
            ticket.Accept(ProtocolCodes.IdentifiersSynchronization, true, null);
            return;
        }

        if (!ticket.PlacesMerged)
        {
            var saved = new HashSet<uint>(ticket.Places.Select(place => place.Id));
            ticket.Places = EntityPlaces.Merge(SceneEntities(), ticket.Places);
            ticket.ScenePlaces = ticket.Places.Where(place => !saved.Contains(place.Id)).ToList();
            ticket.PlacesMerged = true;
        }

        var first = ticket.Stage == JoinStage.Loading;
        var places = first ? ticket.ScenePlaces : ticket.Places;
        GameProtocol.Send(ProtocolCodes.IdentifiersSynchronization, ticket.ClientId, GameProtocol.Identifiers(places, NextIdentifier()));
        if (ticket.Stage == JoinStage.Appearing && !ticket.Flushing)
        {
            _log.Info($"{ticket.Name} appeared and has the identifiers of the saved entities now ({ticket.Places.Count}), the state of the warehouse and {ticket.Held.Count} held message(s) follow");
            StartFlush(ticket);
            return;
        }

        if (ticket.Stage != JoinStage.Loading)
        {
            _log.Info($"Sent {ticket.Name} the identifiers of the snapshot again ({ticket.Places.Count})");
            return;
        }

        var codes = string.Join(", ", ticket.Held.GroupBy(message => message.Code).OrderBy(group => group.Key)
            .Select(group => $"{Research.MessageRecorder.Name(group.Key)} x{group.Count()}"));
        GameProtocol.Send(ProtocolCodes.ServerFinalizeLoad, ticket.ClientId, GameProtocol.Empty());
        ticket.MoveTo(JoinStage.Appearing, FrameLoop.Realtime);
        _log.Info($"{ticket.Name} loaded the level: sent the {places.Count} identifiers of the level's own entities, like the game does at this point, and the go to finish loading; {ticket.Held.Count} held message(s) wait until they appear ({(codes.Length == 0 ? "none" : codes)}), {ticket.Dropped} were dropped");
    }

    private void StartFlush(JoinTicket<Il2CppReferenceArray<Object>> ticket)
    {
        SendState(ticket);
        ticket.Flushing = true;
    }

    private void SendState(JoinTicket<Il2CppReferenceArray<Object>> ticket)
    {
        if (ticket.StateSent)
        {
            return;
        }

        ticket.StateSent = true;
        var time = Singleton<GameTimeManager>.HasInstance() ? Singleton<GameTimeManager>.Instance : null;
        var period = time == null ? -1 : (int)time.CurrentTimePeriod;
        if (period >= 0)
        {
            GameProtocol.Send(ProtocolCodes.GameTimePeriod, ticket.ClientId, GameProtocol.TimePeriod(period));
        }

        var known = new HashSet<uint>((ticket.Places ?? Array.Empty<EntityPlace>()).Select(place => place.Id));
        var lasting = _lasting.For(known.Contains);
        foreach (var message in lasting)
        {
            GameProtocol.Send(message.Code, ticket.ClientId, message.Payload);
        }

        var messages = _state.For(known.Contains);
        foreach (var message in messages)
        {
            GameProtocol.Send(ProtocolCodes.GenericMessage, ticket.ClientId, GameProtocol.Generic(message.EntityId, message.Text));
        }

        var kinds = string.Join(", ", messages.GroupBy(message => message.Kind).OrderByDescending(group => group.Count()).ThenBy(group => group.Key)
            .Select(group => $"{group.Key} x{group.Count()}"));
        var lastingKinds = string.Join(", ", lasting.GroupBy(message => message.Code).OrderBy(group => group.Key)
            .Select(group => $"{Research.MessageRecorder.Name(group.Key)} x{group.Count()}"));
        _log.Info($"Sent {ticket.Name} the time of day ({(period >= 0 ? ((GameTimePeriod)period).ToString() : "unknown")}) and the state of the warehouse the game sent before they joined: " +
                  $"{lasting.Count} of {_lasting.Count} recorded game message(s){(lastingKinds.Length == 0 ? string.Empty : $" ({lastingKinds})")}, " +
                  $"{messages.Count} of {_state.Count} entity message(s)" +
                  (kinds.Length == 0 ? string.Empty : $" ({kinds})"));
    }

    private void Flush(JoinTicket<Il2CppReferenceArray<Object>> ticket)
    {
        foreach (var message in ticket.TakeHeld(FlushPerFrame))
        {
            GameProtocol.Send(message.Code, ticket.ClientId, message.Payload, message.Reliable);
        }

        if (ticket.Held.Count > 0)
        {
            return;
        }

        ticket.Flushing = false;
        ticket.MoveTo(JoinStage.Synced, FrameLoop.Realtime);
        Publish("Synced", ticket, $"places={ticket.Places.Count} dropped={ticket.Dropped}");
        _log.Info($"{ticket.Name} got every held message, the game goes on for them as for everyone");
    }

    private void ClearState()
    {
        _state.Clear();
        _lasting.Clear();
    }

    private static List<EntityPlace> SceneEntities()
    {
        var result = new List<EntityPlace>();
        var manager = Singleton<NetworkedEntityManager>.HasInstance() ? Singleton<NetworkedEntityManager>.Instance : null;
        var list = manager?._initialNetworkedEntities;
        if (list == null)
        {
            return result;
        }

        for (var index = 0; index < list.Count; index++)
        {
            var network = list[index];
            if (network == null || network.WasCollected || !network)
            {
                continue;
            }

            var entity = network.LinkedEntity;
            if (entity != null && !entity.WasCollected && entity.IsDisposed)
            {
                continue;
            }

            var position = network.InitialPosition;
            result.Add(new EntityPlace(network.NetworkIdentifier, position.x, position.y, position.z));
        }

        return result;
    }

    private static uint NextIdentifier() =>
        Singleton<NetworkedEntityManager>.HasInstance() ? Singleton<NetworkedEntityManager>.Instance._nextIdentifier : 0;

    private void Fail(JoinTicket<Il2CppReferenceArray<Object>> ticket, string reason)
    {
        _tickets.Remove(ticket.ClientId);
        Notify("message.failed", ticket.Name);
        Publish("Failed", ticket, reason);
        _log.Error($"{ticket.Name} cannot join: {reason}; they are disconnected");
        GameProtocol.Disconnect(ticket.ClientId);
    }

    private void UpdateCodePatches(HostState state)
    {
        WriteStarted();
        var enabled = Settings != null && Settings.Enabled.Value;
        var open = enabled && JoinGate.CanAccept(state);
        if (_loading is { IsFound: true } && open != _loadingOpen)
        {
            if (open ? _loading.Write(PlayerLimit.SkipJump) : _loading.Restore())
            {
                _loadingOpen = open;
                _log.Info(!open ? "Joining your level is closed, like in the game"
                    : CatNetwork.IsWithoutSteamNetwork ? "Joining your level is open, but without the Steam network nobody can reach it"
                    : "Players can join your level now");
            }
        }

        var players = enabled && Settings != null ? PlayerLimit.Clamp(Settings.MaxPlayers.Value) : PlayerLimit.GamePlayers;
        if (_limit is { IsFound: true } && players != _limitWritten)
        {
            var done = players == PlayerLimit.GamePlayers ? _limit.Restore() : _limit.Write(new[] { PlayerLimit.ConnectionLimit(players) });
            if (done)
            {
                _limitWritten = players;
                _log.Info($"Up to {players} players can be in your game");
            }
        }
    }

    private static HostState ReadHost()
    {
        var boot = Singleton<BootstrapManager>.HasInstance() ? Singleton<BootstrapManager>.Instance : null;
        var time = Singleton<GameTimeManager>.HasInstance() ? Singleton<GameTimeManager>.Instance : null;
        return new HostState(GameProtocol.IsHosting, boot != null && boot.LoadFinalized, boot != null && boot.IsRestarting,
            time == null ? -1 : (int)time.CurrentTimePeriod);
    }

    private static bool IsSpawned(ulong clientId)
    {
        var players = Singleton<PlayerManager>.HasInstance() ? Singleton<PlayerManager>.Instance : null;
        return players != null && players.TryGetPlayer(clientId, out var player) && player != null;
    }

    private void Notify(string key, string name)
    {
        if (Settings == null || Settings.Messages.Value)
        {
            Notifications.Show(_texts.Format(key, name));
        }
    }

    private static void Publish(string step, JoinTicket<Il2CppReferenceArray<Object>> ticket, string details = null) =>
        GameEventStream.Publish(EventPrefix + step, $"client={ticket.ClientId}" + (string.IsNullOrEmpty(details) ? string.Empty : " " + details));

    private static string Seconds(double seconds) => seconds.ToString("0.0", CultureInfo.InvariantCulture) + " s";
}
