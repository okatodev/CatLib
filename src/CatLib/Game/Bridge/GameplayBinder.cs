using System;
using CatLib.Game.Events;
using CatLib.Il2Cpp;

namespace CatLib.Game.Bridge;

internal static class GameplayBinder
{
    public const int EventCount = 3;

    public static void Bind(GameManager manager, Il2CppEventBindings bindings)
    {
        bindings.Add<GameManager.InitializingGameHandler>(GameplayEvents.InitializingParcelsName,
            new Action(GameplayEvents.RaiseInitializingParcels), manager.add_InitializingParcels, manager.remove_InitializingParcels);
        bindings.Add<GameManager.GameStartedHandler>(GameplayEvents.GameStartedName,
            new Action(GameplayEvents.RaiseGameStarted), manager.add_GameStarted, manager.remove_GameStarted);
        bindings.Add<GameManager.GameStartedPhase2Handler>(GameplayEvents.GameStartedPhase2Name,
            new Action(GameplayEvents.RaiseGameStartedPhase2), manager.add_GameStartedPhase2, manager.remove_GameStartedPhase2);
    }
}
