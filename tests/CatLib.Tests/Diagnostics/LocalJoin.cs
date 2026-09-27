using System;
using System.Collections.Generic;
using System.Text;
using CatLib.Logging;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Steamworks;

namespace CatLib.Tests.Diagnostics;

public sealed class LocalJoin
{
    public const string ConnectKey = "connect";
    public const int ConnectBytes = 256;

    private readonly CatLogger _log;

    public LocalJoin(CatLogger log)
    {
        _log = log;
    }

    public string ListPlayers()
    {
        var state = SteamState();
        _log.Info(state);
        if (!SteamManagerBase.Initialized)
        {
            return "Steam is not initialized in this game copy, see the log";
        }

        var players = Players();
        if (players.Count == 0)
        {
            _log.Info("No other players are visible through Steam");
            return "no players visible";
        }

        foreach (var player in players)
        {
            _log.Info($"Player {player.Id} \"{player.Name}\": {player.Presence}");
        }

        var hosts = players.FindAll(player => player.Connect.Length > 0).Count;
        return $"{players.Count} player(s), {hosts} can be joined, see the log";
    }

    public string JoinFirst()
    {
        if (!SteamManagerBase.Initialized)
        {
            _log.Warning(SteamState());
            return "Steam is not initialized in this game copy, see the log";
        }

        var target = Players().Find(player => player.Connect.Length > 0);
        if (target == null)
        {
            _log.Warning("No visible player offers a game to join; the host must be in its lobby or level");
            return "nobody to join";
        }

        if (SteamManager.Instance == null)
        {
            return "the game's Steam manager is not ready";
        }

        var request = new GameRichPresenceJoinRequested_t
        {
            m_steamIDFriend = new CSteamID(target.Id),
            m_rgchConnect_ = new Il2CppStructArray<byte>(ConnectBytes)
        };
        request.m_rgchConnect = target.Connect;
        _log.Info($"Joining {target.Id} \"{target.Name}\" with connect string \"{target.Connect}\" the way a Steam join request does");
        SteamManager.Instance.OnRichPresenceJoinRequestedReceived(request);
        return $"joining {target.Name}";
    }

    public static string SteamState()
    {
        if (!SteamManagerBase.Initialized)
        {
            return "The game did not initialize Steam: Steam calls, joining and the CatLib channel are unavailable in this copy";
        }

        var manager = SteamManager.Instance;
        return manager == null
            ? "Steam is initialized, the game's Steam manager is missing"
            : $"Steam is initialized as {manager.GetLocalSteamID()} \"{manager.GetLocalPlayerName()}\", relay network {(manager.RelayNetworkAvailable ? "available" : "unavailable")}, network {(manager.NetworkNotAvailable ? "not available" : "available")}";
    }

    private static List<Player> Players()
    {
        var players = new List<Player>();
        var count = SteamFriends.GetFriendCount(EFriendFlags.k_EFriendFlagAll);
        for (var index = 0; index < count; index++)
        {
            var id = SteamFriends.GetFriendByIndex(index, EFriendFlags.k_EFriendFlagAll);
            var presence = new StringBuilder();
            var keys = SteamFriends.GetFriendRichPresenceKeyCount(id);
            for (var key = 0; key < keys; key++)
            {
                var name = SteamFriends.GetFriendRichPresenceKeyByIndex(id, key);
                if (key > 0)
                {
                    presence.Append(", ");
                }

                presence.Append(name).Append('=').Append(SteamFriends.GetFriendRichPresence(id, name));
            }

            players.Add(new Player(id.m_SteamID, SteamFriends.GetFriendPersonaName(id) ?? string.Empty,
                keys == 0 ? "no rich presence" : presence.ToString(), SteamFriends.GetFriendRichPresence(id, ConnectKey) ?? string.Empty));
        }

        return players;
    }

    private sealed record Player(ulong Id, string Name, string Presence, string Connect);
}
