using System;
using CatLib.Core;
using CatLib.Events;

namespace CatLib.Game.Events;

public static class SaveEvents
{
    public const string SaveFileSelectedName = "Save.SaveFileSelected";
    public const string GameSavingStartedName = "Save.GameSavingStarted";
    public const string SuccessfullySavedName = "Save.SuccessfullySaved";
    public const string UnsuccessfullySavedName = "Save.UnsuccessfullySaved";

    public static event Action SaveFileSelected;
    public static event Action GameSavingStarted;
    public static event Action SuccessfullySaved;
    public static event Action UnsuccessfullySaved;

    internal static void RaiseSaveFileSelected() => Raise(SaveFileSelected, SaveFileSelectedName);

    internal static void RaiseGameSavingStarted() => Raise(GameSavingStarted, GameSavingStartedName);

    internal static void RaiseSuccessfullySaved() => Raise(SuccessfullySaved, SuccessfullySavedName);

    internal static void RaiseUnsuccessfullySaved() => Raise(UnsuccessfullySaved, UnsuccessfullySavedName);

    private static void Raise(Action handlers, string name)
    {
        GameEventStream.Publish(name, Describe());
        SafeInvoker.Invoke(handlers, name, CatLibRuntime.Log);
    }

    private static string Describe()
    {
        try
        {
            return $"file={GameInfo.SaveFileName ?? "none"} new={Flag(GameInfo.IsNewSave)} loading={Flag(GameInfo.IsLoadingSave)} host={Flag(GameInfo.IsServer)}";
        }
        catch (Exception exception)
        {
            return "details unavailable: " + exception.GetType().Name;
        }
    }

    private static string Flag(bool? value) => value == null ? "?" : value.Value ? "yes" : "no";
}
