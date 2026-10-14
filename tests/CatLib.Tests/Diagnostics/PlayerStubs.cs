using System;
using System.Collections.Generic;
using System.Linq;
using CatLib.DevTools;
using CatLib.Game.Events;
using CatLib.Logging;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppInterop.Runtime.Runtime;
using UnityEngine;
using Object = Il2CppSystem.Object;

namespace CatLib.Tests.Diagnostics;

public sealed class PlayerStubs
{
    public const string Group = "Players";
    public const int MostPlayers = 8;
    public const uint FirstNetworkIdentifier = 11;
    public const ulong FirstStubClient = 990000;
    public const int PlayerSpawnedBroadcast = 20;
    public const int ClientDisconnectBroadcast = 6;

    private readonly CatLogger _log;
    private readonly Dictionary<int, ulong> _stubs = new();
    private readonly Dictionary<int, Vector3> _positions = new();

    public PlayerStubs(CatLogger log)
    {
        _log = log;
        BootstrapEvents.LevelLoadStarted += Forget;
        BootstrapEvents.MainMenuLoaded += Forget;
        PlayerEvents.PlayerGhostSpawned += OnGhostSpawned;
    }

    public IReadOnlyCollection<int> Slots => _stubs.Keys;

    public static uint NetworkIdentifierOf(int player) => FirstNetworkIdentifier + (uint)(player - 1);

    public static int PlayerOf(uint networkIdentifier) => (int)(networkIdentifier - FirstNetworkIdentifier) + 1;

    public static ulong StubClientOf(int player) => FirstStubClient + (ulong)player;

    public static string NameOf(int player) => "Stub " + player;

    public static bool IsStubClient(ulong client) => client > FirstStubClient && client <= FirstStubClient + MostPlayers;

    public IEnumerable<DevItem> Items()
    {
        for (var player = 2; player <= MostPlayers; player++)
        {
            var slot = player;
            yield return new DevItem(Group, $"Player {slot}", $"Puts a standing stub of player {slot} where the game spawns that player, for every player of the session, " +
                "or takes it away. It counts as a player in the game but has no game behind it. Host only, in a level; a slot of a real player cannot be used.",
                () => Toggle(slot), () => State(slot));
        }
    }

    public string State(int player)
    {
        if (_stubs.ContainsKey(player))
        {
            return "on";
        }

        return RealPlayerIn(player) != null ? "real" : "off";
    }

    public string Toggle(int player)
    {
        var problem = HostProblem();
        if (problem != null)
        {
            return problem;
        }

        return _stubs.ContainsKey(player) ? Remove(player) : Add(player);
    }

    private string Add(int player)
    {
        var real = RealPlayerIn(player);
        if (real != null)
        {
            return $"player {player} is {real}, a real player";
        }

        var manager = Singleton<PlayerManager>.Instance;
        var server = Singleton<NetworkManager>.Instance._server;
        var client = StubClientOf(player);
        var identifier = NetworkIdentifierOf(player);
        var used = manager._usedPlayerNetworkIdentifiers;
        used[client] = identifier;
        var point = manager.GetPlayerSpawnPoint(identifier);
        var position = point == null ? Vector3.zero : point.position;
        var name = NameOf(player);
        var reached = Broadcast(server, PlayerSpawnedBroadcast, () => SpawnPayload(player, position, true));
        _stubs[player] = client;
        _positions[player] = position;
        var spot = SpotOf(manager, identifier);
        _log.Info($"{name} added as client {client} with network identifier {identifier} at {position} (spawn point {spot}), sent to {reached} game(s)");
        return $"stub at spawn point {spot}, sent to {reached} game(s)";
    }

    private string Remove(int player)
    {
        var client = _stubs[player];
        _stubs.Remove(player);
        _positions.Remove(player);
        var reached = Broadcast(Singleton<NetworkManager>.Instance._server, ClientDisconnectBroadcast, () => Payload(Box(client)));
        var manager = Singleton<PlayerManager>.Instance;
        if (manager._usedPlayerNetworkIdentifiers.ContainsKey(client))
        {
            manager.ReleasePlayerIdentifier(client);
        }

        _log.Info($"{NameOf(player)} removed, sent to {reached} game(s)");
        return $"removed from {reached} game(s)";
    }

    private void OnGhostSpawned(PlayerGhostEntity ghost)
    {
        try
        {
            if (ghost == null || _stubs.Count == 0 || IsStubClient(ghost.GhostClientId) || HostProblem() != null)
            {
                return;
            }

            var target = ghost.GhostClientId;
            var server = Singleton<NetworkManager>.Instance._server;
            foreach (var player in _stubs.Keys.ToList())
            {
                server.SendMessage((ProtocolCode)PlayerSpawnedBroadcast, true, target, SpawnPayload(player, _positions[player], false));
            }

            _log.Info($"{_stubs.Count} player stub(s) sent to client {target}, who appeared after them");
        }
        catch (Exception exception)
        {
            _log.Warning("Sending the player stubs to a new player failed: " + exception.Message);
        }
    }

    private static Il2CppReferenceArray<Object> SpawnPayload(int player, Vector3 position, bool fresh)
    {
        var client = StubClientOf(player);
        return Payload(Box(client), Box(client), Box(NetworkIdentifierOf(player)), Box(position), (Object)(Il2CppSystem.String)NameOf(player), Box(fresh));
    }

    private static int SpotOf(PlayerManager manager, uint identifier)
    {
        var points = manager._spawnPoints;
        var count = points == null ? 0 : points.Length;
        return count == 0 ? 0 : (int)((identifier - FirstNetworkIdentifier) % count) + 1;
    }

    private static string RealPlayerIn(int player)
    {
        if (!Singleton<PlayerManager>.HasInstance())
        {
            return null;
        }

        var manager = Singleton<PlayerManager>.Instance;
        var used = manager._usedPlayerNetworkIdentifiers;
        if (used == null)
        {
            return null;
        }

        var identifier = NetworkIdentifierOf(player);
        foreach (var pair in used)
        {
            if (pair.Value == identifier && !IsStubClient(pair.Key))
            {
                return manager.TryGetPlayerName(pair.Key, out var name) && !string.IsNullOrEmpty(name) ? name : "client " + pair.Key;
            }
        }

        return null;
    }

    private int Broadcast(Server server, int code, Func<Il2CppReferenceArray<Object>> payload)
    {
        var clients = new List<ulong>();
        foreach (var pair in server._clients)
        {
            clients.Add(pair.Key);
        }

        var reached = 0;
        foreach (var client in clients)
        {
            try
            {
                server.SendMessage((ProtocolCode)code, true, client, payload());
                reached++;
            }
            catch (Exception exception)
            {
                _log.Warning($"Sending {code} to client {client} failed: {exception.Message}");
            }
        }

        return reached;
    }

    private void Forget()
    {
        if (_stubs.Count > 0)
        {
            _log.Info($"{_stubs.Count} player stub(s) are gone with the level");
            _stubs.Clear();
            _positions.Clear();
        }
    }

    private static string HostProblem()
    {
        var network = Singleton<NetworkManager>.HasInstance() ? Singleton<NetworkManager>.Instance : null;
        if (network == null || !network.IsServer || network._server == null)
        {
            return "only the host can do this";
        }

        var boot = Singleton<BootstrapManager>.HasInstance() ? Singleton<BootstrapManager>.Instance : null;
        if (boot == null || !boot.LoadFinalized || !Singleton<PlayerManager>.HasInstance())
        {
            return "only in a level";
        }

        return null;
    }

    private static Il2CppReferenceArray<Object> Payload(params Object[] values)
    {
        var payload = new Il2CppReferenceArray<Object>(values.Length);
        for (var index = 0; index < values.Length; index++)
        {
            payload[index] = values[index];
        }

        return payload;
    }

    private static Object Box(ulong value) => Box(value, ClassOf<ulong, Il2CppSystem.UInt64>());

    private static Object Box(uint value) => Box(value, ClassOf<uint, Il2CppSystem.UInt32>());

    private static Object Box(bool value) => Box(value, ClassOf<bool, Il2CppSystem.Boolean>());

    private static Object Box(Vector3 value) => Box(value, ClassOf<Vector3, Vector3>());

    private static unsafe Object Box<T>(T value, IntPtr type) where T : unmanaged
    {
        if (type == IntPtr.Zero)
        {
            throw new InvalidOperationException($"The game has no class for {typeof(T).Name}");
        }

        return new Object(IL2CPP.il2cpp_value_box(type, (IntPtr)(&value)));
    }

    private static IntPtr ClassOf<TManaged, TGame>()
    {
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(TGame).TypeHandle);
        var type = Il2CppClassPointerStore<TManaged>.NativeClassPtr;
        return type != IntPtr.Zero ? type : Il2CppClassPointerStore<TGame>.NativeClassPtr;
    }
}
