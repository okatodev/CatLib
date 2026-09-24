using System;
using CatLib.Core;
using CatLib.Events;

namespace CatLib.Game.Events;

public static class BootstrapEvents
{
    public const string MainMenuLoadedName = "Bootstrap.MainMenuLoaded";
    public const string LevelLoadStartedName = "Bootstrap.LevelLoadStarted";
    public const string LevelLoadFinalizedName = "Bootstrap.LevelLoadFinalized";
    public const string GameRestartStartedName = "Bootstrap.GameRestartStarted";
    public const string GameStartedFromLobbyName = "Bootstrap.GameStartedFromLobby";

    public static event Action MainMenuLoaded;
    public static event Action LevelLoadStarted;
    public static event Action LevelLoadFinalized;
    public static event Action GameRestartStarted;
    public static event Action GameStartedFromLobby;

    internal static void RaiseMainMenuLoaded() => Raise(MainMenuLoaded, MainMenuLoadedName);

    internal static void RaiseLevelLoadStarted() => Raise(LevelLoadStarted, LevelLoadStartedName);

    internal static void RaiseLevelLoadFinalized() => Raise(LevelLoadFinalized, LevelLoadFinalizedName);

    internal static void RaiseGameRestartStarted() => Raise(GameRestartStarted, GameRestartStartedName);

    internal static void RaiseGameStartedFromLobby() => Raise(GameStartedFromLobby, GameStartedFromLobbyName);

    private static void Raise(Action handlers, string eventName)
    {
        GameEventStream.Publish(eventName);
        SafeInvoker.Invoke(handlers, eventName, CatLibRuntime.Log);
    }
}
