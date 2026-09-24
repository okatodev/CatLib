using System;
using CatLib.Game.Events;
using CatLib.Il2Cpp;

namespace CatLib.Game.Bridge;

internal static class PlayerBinder
{
    public const int EventCount = 5;

    public static void Bind(PlayerManager manager, Il2CppEventBindings bindings)
    {
        bindings.Add<PlayerManager.LocalPlayerSpawnedHandler>(PlayerEvents.LocalPlayerSpawnedName,
            new Action(PlayerEvents.RaiseLocalPlayerSpawned), manager.add_LocalPlayerSpawned, manager.remove_LocalPlayerSpawned);
        bindings.Add<PlayerManager.ClientNameChangedHandler>(PlayerEvents.ClientNameChangedName,
            new Action<ulong, string>(PlayerEvents.RaiseClientNameChanged), manager.add_ClientNameChanged, manager.remove_ClientNameChanged);
        bindings.Add<PlayerManager.PlayerGhostSpawnedHandler>(PlayerEvents.PlayerGhostSpawnedName,
            new Action<PlayerGhostEntity>(PlayerEvents.RaisePlayerGhostSpawned), manager.add_PlayerGhostSpawned, manager.remove_PlayerGhostSpawned);
        bindings.Add<PlayerManager.PlayerDisconnectedHandler>(PlayerEvents.PlayerDisconnectedName,
            new Action<ulong>(PlayerEvents.RaisePlayerDisconnected), manager.add_PlayerDisconnected, manager.remove_PlayerDisconnected);
        bindings.Add<PlayerManager.PlayerAmountChangedHandler>(PlayerEvents.PlayerAmountChangedName,
            new Action<int>(PlayerEvents.RaisePlayerAmountChanged), manager.add_PlayerAmountChanged, manager.remove_PlayerAmountChanged);
    }
}
