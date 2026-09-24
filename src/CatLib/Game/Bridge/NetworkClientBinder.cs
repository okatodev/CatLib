using System;
using CatLib.Game.Events;
using CatLib.Il2Cpp;

namespace CatLib.Game.Bridge;

internal static class NetworkClientBinder
{
    public const int EventCount = 1;

    public static bool HasInstance() => Singleton<NetworkManager>.HasInstance() && Singleton<NetworkManager>.Instance._client != null;

    public static Client GetInstance() => Singleton<NetworkManager>.Instance._client;

    public static void Bind(Client client, Il2CppEventBindings bindings)
    {
        bindings.Add<Client.ConnectionAcknowledgedHandler>(NetworkEvents.ClientConnectionAcknowledgedName,
            new Action<ulong>(NetworkEvents.RaiseClientConnectionAcknowledged), client.add_ConnectionAcknowledged, client.remove_ConnectionAcknowledged);
    }
}
