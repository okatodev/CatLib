using System;
using CatLib.Game.Events;
using CatLib.Il2Cpp;

namespace CatLib.Game.Bridge;

internal static class NetworkBinder
{
    public const int EventCount = 7;

    public static void Bind(NetworkManager manager, Il2CppEventBindings bindings)
    {
        bindings.Add<NetworkManager.ServerStartedHandler>(NetworkEvents.ServerStartedName,
            new Action(NetworkEvents.RaiseServerStarted), manager.add_ServerStarted, manager.remove_ServerStarted);
        bindings.Add<NetworkManager.ClientConnectedHandler>(NetworkEvents.ClientConnectedName,
            new Action<ulong>(NetworkEvents.RaiseClientConnected), manager.add_ClientConnected, manager.remove_ClientConnected);
        bindings.Add<NetworkManager.ClientConnectedHandler>(NetworkEvents.OtherClientConnectedName,
            new Action<ulong>(NetworkEvents.RaiseOtherClientConnected), manager.add_OtherClientConnected, manager.remove_OtherClientConnected);
        bindings.Add<NetworkManager.ClientConnectedHandler>(NetworkEvents.ClientDisconnectedName,
            new Action<ulong>(NetworkEvents.RaiseClientDisconnected), manager.add_ClientDisconnected, manager.remove_ClientDisconnected);
        bindings.Add<NetworkManager.ClientReadyHandler>(NetworkEvents.ClientReadyName,
            new Action<ulong>(NetworkEvents.RaiseClientReady), manager.add_ClientReady, manager.remove_ClientReady);
        bindings.Add<NetworkManager.ReceivedEntitySynchronizationHandler>(NetworkEvents.ReceivedEntitySynchronizationName,
            new Action(NetworkEvents.RaiseReceivedEntitySynchronization), manager.add_ReceivedEntitySynchronization, manager.remove_ReceivedEntitySynchronization);
        bindings.Add<NetworkManager.NetworkTickHandler>(NetworkEvents.NetworkTickName,
            new Action<ulong>(NetworkEvents.RaiseNetworkTick), manager.add_NetworkTick, manager.remove_NetworkTick);
    }
}
