using System;
using CatLib.Core;
using CatLib.Events;

namespace CatLib.Game.Events;

public static class NetworkEvents
{
    public const string ServerStartedName = "Network.ServerStarted";
    public const string ClientConnectedName = "Network.ClientConnected";
    public const string OtherClientConnectedName = "Network.OtherClientConnected";
    public const string ClientDisconnectedName = "Network.ClientDisconnected";
    public const string ClientReadyName = "Network.ClientReady";
    public const string ClientConnectionAcknowledgedName = "Network.ClientConnectionAcknowledged";
    public const string ReceivedEntitySynchronizationName = "Network.ReceivedEntitySynchronization";
    public const string NetworkTickName = "Network.NetworkTick";

    public static event Action ServerStarted;
    public static event Action<ulong> ClientConnected;
    public static event Action<ulong> OtherClientConnected;
    public static event Action<ulong> ClientDisconnected;
    public static event Action<ulong> ClientReady;
    public static event Action<ulong> ClientConnectionAcknowledged;
    public static event Action ReceivedEntitySynchronization;
    public static event Action<ulong> NetworkTick;

    internal static void RaiseServerStarted() => Raise(ServerStarted, ServerStartedName);

    internal static void RaiseClientConnected(ulong clientId) => Raise(ClientConnected, clientId, ClientConnectedName);

    internal static void RaiseOtherClientConnected(ulong clientId) => Raise(OtherClientConnected, clientId, OtherClientConnectedName);

    internal static void RaiseClientDisconnected(ulong clientId) => Raise(ClientDisconnected, clientId, ClientDisconnectedName);

    internal static void RaiseClientReady(ulong clientId) => Raise(ClientReady, clientId, ClientReadyName);

    internal static void RaiseClientConnectionAcknowledged(ulong clientId) =>
        Raise(ClientConnectionAcknowledged, clientId, ClientConnectionAcknowledgedName);

    internal static void RaiseReceivedEntitySynchronization() =>
        Raise(ReceivedEntitySynchronization, ReceivedEntitySynchronizationName);

    internal static void RaiseNetworkTick(ulong tick)
    {
        GameEventStream.Publish(NetworkTickName, GameEventStream.HasListeners ? $"tick={tick}" : string.Empty);
        SafeInvoker.Invoke(NetworkTick, tick, NetworkTickName, CatLibRuntime.Log);
    }

    private static void Raise(Action handlers, string eventName)
    {
        GameEventStream.Publish(eventName);
        SafeInvoker.Invoke(handlers, eventName, CatLibRuntime.Log);
    }

    private static void Raise(Action<ulong> handlers, ulong clientId, string eventName)
    {
        GameEventStream.Publish(eventName, $"client={clientId}");
        SafeInvoker.Invoke(handlers, clientId, eventName, CatLibRuntime.Log);
    }
}
