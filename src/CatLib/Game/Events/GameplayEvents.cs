using System;
using CatLib.Core;
using CatLib.Events;

namespace CatLib.Game.Events;

public static class GameplayEvents
{
    public const string InitializingParcelsName = "Gameplay.InitializingParcels";
    public const string GameStartedName = "Gameplay.GameStarted";
    public const string GameStartedPhase2Name = "Gameplay.GameStartedPhase2";

    public static event Action InitializingParcels;
    public static event Action GameStarted;
    public static event Action GameStartedPhase2;

    internal static void RaiseInitializingParcels() => Raise(InitializingParcels, InitializingParcelsName);

    internal static void RaiseGameStarted() => Raise(GameStarted, GameStartedName);

    internal static void RaiseGameStartedPhase2() => Raise(GameStartedPhase2, GameStartedPhase2Name);

    private static void Raise(Action handlers, string eventName)
    {
        GameEventStream.Publish(eventName);
        SafeInvoker.Invoke(handlers, eventName, CatLibRuntime.Log);
    }
}
