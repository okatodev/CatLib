namespace CatLib.Saves;

public enum SaveReadStatus
{
    Missing,
    Loaded,
    LoadedFromBackup,
    Corrupt,
    Unreadable,
    TooNew
}
