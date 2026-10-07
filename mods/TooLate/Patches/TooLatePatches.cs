using System;
using CatLib.Logging;
using CatLib.Patching;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using TooLate.Research;
using TooLate.Scene;
using Object = Il2CppSystem.Object;

namespace TooLate.Patches;

public static class TooLatePatches
{
    private static JoinCoordinator _joins;
    private static LobbyOverflow _lobby;
    private static MessageRecorder _recorder;

    public static CatPatches Group { get; private set; }

    public static CatPatches Install(string ownerId, JoinCoordinator joins, LobbyOverflow lobby, MessageRecorder recorder, CatLogger log)
    {
        _joins = joins;
        _lobby = lobby;
        _recorder = recorder;
        Group = new CatPatches(ownerId, log)
            .Prefix(typeof(Server), nameof(Server.SendMessage),
                new[] { typeof(ProtocolCode), typeof(bool), typeof(ulong), typeof(Il2CppReferenceArray<Object>) }, typeof(TooLatePatches), nameof(ServerSendPrefix))
            .Prefix(typeof(Server), nameof(Server.BroadcastMessage),
                new[] { typeof(ProtocolCode), typeof(bool), typeof(Il2CppSystem.Nullable<ulong>), typeof(Il2CppReferenceArray<Object>) }, typeof(TooLatePatches), nameof(ServerBroadcastPrefix))
            .Prefix(typeof(Client), nameof(Client.SendMessage),
                new[] { typeof(ProtocolCode), typeof(bool), typeof(Il2CppReferenceArray<Object>) }, typeof(TooLatePatches), nameof(ClientSendPrefix))
            .Prefix(typeof(SaveManager), nameof(SaveManager.NetworkManager_OnClientConnected),
                new[] { typeof(ulong) }, typeof(TooLatePatches), nameof(SaveOnConnectPrefix))
            .Postfix(typeof(MultiplayerLobbyInterface), nameof(MultiplayerLobbyInterface.ProcessClientJoin),
                new[] { typeof(ulong) }, typeof(TooLatePatches), nameof(LobbyJoinPostfix))
            .Postfix(typeof(MultiplayerLobbyInterface), nameof(MultiplayerLobbyInterface.NetworkManager_OnClientDisconnected),
                new[] { typeof(ulong) }, typeof(TooLatePatches), nameof(LobbyLeavePostfix));
        Group.Apply();
        return Group;
    }

    private static bool ServerSendPrefix(ProtocolCode __0, bool __1, ulong __2, Il2CppReferenceArray<Object> __3)
    {
        if (Group == null || !Group.IsActive)
        {
            return true;
        }

        try
        {
            var code = (int)__0;
            if (_recorder.IsEnabled)
            {
                _recorder.HostSent(code, __1, __2, __3);
            }

            if (GameProtocol.IsSending)
            {
                return true;
            }

            _joins.Observe(code, __3);
            return _joins.RouteOutgoing(code, __1, __2, __3);
        }
        catch (Exception exception)
        {
            Group.Fault(nameof(ServerSendPrefix), exception);
            return true;
        }
    }

    private static void ServerBroadcastPrefix(ProtocolCode __0, Il2CppReferenceArray<Object> __3)
    {
        if (Group == null || !Group.IsActive || GameProtocol.IsSending)
        {
            return;
        }

        try
        {
            _joins.Observe((int)__0, __3);
        }
        catch (Exception exception)
        {
            Group.Fault(nameof(ServerBroadcastPrefix), exception);
        }
    }

    private static void ClientSendPrefix(ProtocolCode __0, bool __1, Il2CppReferenceArray<Object> __2)
    {
        if (Group == null || !Group.IsActive)
        {
            return;
        }

        try
        {
            if (_recorder.IsEnabled)
            {
                _recorder.ClientSent((int)__0, __1, __2);
            }

            _joins.Observe((int)__0, __2);
        }
        catch (Exception exception)
        {
            Group.Fault(nameof(ClientSendPrefix), exception);
        }
    }

    private static bool SaveOnConnectPrefix(ulong __0)
    {
        if (Group == null || !Group.IsActive)
        {
            return true;
        }

        try
        {
            return !_joins.TakesOverSave(__0);
        }
        catch (Exception exception)
        {
            Group.Fault(nameof(SaveOnConnectPrefix), exception);
            return true;
        }
    }

    private static void LobbyJoinPostfix(MultiplayerLobbyInterface __instance, ulong __0)
    {
        if (Group == null || !Group.IsActive)
        {
            return;
        }

        try
        {
            _lobby.OnJoined(__instance, __0);
        }
        catch (Exception exception)
        {
            Group.Fault(nameof(LobbyJoinPostfix), exception);
        }
    }

    private static void LobbyLeavePostfix(MultiplayerLobbyInterface __instance, ulong __0)
    {
        if (Group == null || !Group.IsActive)
        {
            return;
        }

        try
        {
            _lobby.OnLeft(__instance, __0);
        }
        catch (Exception exception)
        {
            Group.Fault(nameof(LobbyLeavePostfix), exception);
        }
    }
}
