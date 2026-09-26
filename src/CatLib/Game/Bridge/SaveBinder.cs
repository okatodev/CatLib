using System;
using CatLib.Game.Events;
using CatLib.Il2Cpp;

namespace CatLib.Game.Bridge;

internal static class SaveBinder
{
    public const int EventCount = 4;

    public static void Bind(SaveManager manager, Il2CppEventBindings bindings)
    {
        bindings.Add<SaveManager.SaveFileSelectedHandler>(SaveEvents.SaveFileSelectedName,
            new Action(SaveEvents.RaiseSaveFileSelected), manager.add_SaveFileSelected, manager.remove_SaveFileSelected);
        bindings.Add<SaveManager.GameSavingStartedHandler>(SaveEvents.GameSavingStartedName,
            new Action(SaveEvents.RaiseGameSavingStarted), manager.add_GameSavingStarted, manager.remove_GameSavingStarted);
        bindings.Add<SaveManager.SuccessfullySavedHandler>(SaveEvents.SuccessfullySavedName,
            new Action(SaveEvents.RaiseSuccessfullySaved), manager.add_SuccessfullySaved, manager.remove_SuccessfullySaved);
        bindings.Add<SaveManager.UnsuccessfullySavedHandler>(SaveEvents.UnsuccessfullySavedName,
            new Action(SaveEvents.RaiseUnsuccessfullySaved), manager.add_UnsuccessfullySaved, manager.remove_UnsuccessfullySaved);
    }
}
