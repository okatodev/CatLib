using System;
using CatLib.Game.Events;
using CatLib.Il2Cpp;

namespace CatLib.Game.Bridge;

internal static class BootstrapBinder
{
    public const int EventCount = 5;

    public static void Bind(BootstrapManager manager, Il2CppEventBindings bindings)
    {
        bindings.Add<BootstrapManager.LoadHandler>(BootstrapEvents.MainMenuLoadedName,
            new Action(BootstrapEvents.RaiseMainMenuLoaded), manager.add_MainMenuLoaded, manager.remove_MainMenuLoaded);
        bindings.Add<BootstrapManager.LoadHandler>(BootstrapEvents.LevelLoadStartedName,
            new Action(BootstrapEvents.RaiseLevelLoadStarted), manager.add_LevelLoadStarted, manager.remove_LevelLoadStarted);
        bindings.Add<BootstrapManager.LoadHandler>(BootstrapEvents.LevelLoadFinalizedName,
            new Action(BootstrapEvents.RaiseLevelLoadFinalized), manager.add_LevelLoadFinalized, manager.remove_LevelLoadFinalized);
        bindings.Add<BootstrapManager.LoadHandler>(BootstrapEvents.GameRestartStartedName,
            new Action(BootstrapEvents.RaiseGameRestartStarted), manager.add_GameRestartStarted, manager.remove_GameRestartStarted);
        bindings.Add<BootstrapManager.StartedGameFromLobbyHandler>(BootstrapEvents.GameStartedFromLobbyName,
            new Action(BootstrapEvents.RaiseGameStartedFromLobby), manager.add_GameStartedFromLobby, manager.remove_GameStartedFromLobby);
    }
}
