using System;
using CatLib.Core;
using CatLib.Events;

namespace CatLib.Game.Events;

public static class PlayerEvents
{
    public const string LocalPlayerSpawnedName = "Player.LocalPlayerSpawned";
    public const string ClientNameChangedName = "Player.ClientNameChanged";
    public const string PlayerGhostSpawnedName = "Player.PlayerGhostSpawned";
    public const string PlayerDisconnectedName = "Player.PlayerDisconnected";
    public const string PlayerAmountChangedName = "Player.PlayerAmountChanged";

    public static event Action LocalPlayerSpawned;
    public static event Action<ulong, string> ClientNameChanged;
    public static event Action<PlayerGhostEntity> PlayerGhostSpawned;
    public static event Action<ulong> PlayerDisconnected;
    public static event Action<int> PlayerAmountChanged;

    internal static void RaiseLocalPlayerSpawned()
    {
        GameEventStream.Publish(LocalPlayerSpawnedName);
        SafeInvoker.Invoke(LocalPlayerSpawned, LocalPlayerSpawnedName, CatLibRuntime.Log);
    }

    internal static void RaiseClientNameChanged(ulong clientId, string clientName)
    {
        GameEventStream.Publish(ClientNameChangedName, $"client={clientId} name={clientName}");
        SafeInvoker.Invoke(ClientNameChanged, clientId, clientName, ClientNameChangedName, CatLibRuntime.Log);
    }

    internal static void RaisePlayerGhostSpawned(PlayerGhostEntity ghost)
    {
        GameEventStream.Publish(PlayerGhostSpawnedName, ghost == null ? "ghost=null" : $"ghost=0x{ghost.Pointer.ToInt64():X}");
        SafeInvoker.Invoke(PlayerGhostSpawned, ghost, PlayerGhostSpawnedName, CatLibRuntime.Log);
    }

    internal static void RaisePlayerDisconnected(ulong clientId)
    {
        GameEventStream.Publish(PlayerDisconnectedName, $"client={clientId}");
        SafeInvoker.Invoke(PlayerDisconnected, clientId, PlayerDisconnectedName, CatLibRuntime.Log);
    }

    internal static void RaisePlayerAmountChanged(int amount)
    {
        GameEventStream.Publish(PlayerAmountChangedName, $"amount={amount}");
        SafeInvoker.Invoke(PlayerAmountChanged, amount, PlayerAmountChangedName, CatLibRuntime.Log);
    }
}
