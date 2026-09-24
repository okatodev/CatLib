using System;
using System.Collections.Generic;
using System.Linq;
using CatLib.Logging;

namespace CatLib.Game.Bridge;

public static class ManagerRegistry
{
    public const string AttachedEventName = "Bridge.Attached";
    public const string DetachedEventName = "Bridge.Detached";

    public const string BootstrapManagerName = nameof(BootstrapManager);
    public const string GameManagerName = nameof(GameManager);
    public const string PlayerManagerName = nameof(PlayerManager);
    public const string NetworkManagerName = nameof(NetworkManager);
    public const string NetworkClientName = "NetworkClient";

    private static readonly List<IManagerTracker> Trackers = new();

    public static IReadOnlyList<string> Names => Trackers.Select(tracker => tracker.Name).ToList();

    public static bool IsAttached(string managerName) => Find(managerName)?.IsAttached ?? false;

    public static int ExpectedEventCount(string managerName) => Find(managerName)?.ExpectedEventCount ?? 0;

    public static int BoundEventCount(string managerName) => Find(managerName)?.BoundEventCount ?? 0;

    public static string LastError(string managerName) => Find(managerName)?.LastError;

    internal static void Initialize(CatLogger log)
    {
        if (Trackers.Count > 0)
        {
            return;
        }

        Trackers.Add(new ManagerTracker<BootstrapManager>(BootstrapManagerName,
            Singleton<BootstrapManager>.HasInstance, () => Singleton<BootstrapManager>.Instance, BootstrapBinder.Bind, BootstrapBinder.EventCount, log));
        Trackers.Add(new ManagerTracker<GameManager>(GameManagerName,
            Singleton<GameManager>.HasInstance, () => Singleton<GameManager>.Instance, GameplayBinder.Bind, GameplayBinder.EventCount, log));
        Trackers.Add(new ManagerTracker<PlayerManager>(PlayerManagerName,
            Singleton<PlayerManager>.HasInstance, () => Singleton<PlayerManager>.Instance, PlayerBinder.Bind, PlayerBinder.EventCount, log));
        Trackers.Add(new ManagerTracker<NetworkManager>(NetworkManagerName,
            Singleton<NetworkManager>.HasInstance, () => Singleton<NetworkManager>.Instance, NetworkBinder.Bind, NetworkBinder.EventCount, log));
        Trackers.Add(new ManagerTracker<Client>(NetworkClientName,
            NetworkClientBinder.HasInstance, NetworkClientBinder.GetInstance, NetworkClientBinder.Bind, NetworkClientBinder.EventCount, log));
    }

    internal static void Update()
    {
        for (var index = 0; index < Trackers.Count; index++)
        {
            Trackers[index].Update();
        }
    }

    private static IManagerTracker Find(string managerName) =>
        Trackers.FirstOrDefault(tracker => string.Equals(tracker.Name, managerName, StringComparison.Ordinal));
}
