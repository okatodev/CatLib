using UnityEngine;

namespace CatLib.Game;

public static class GameInfo
{
    public static string UnityVersion => Application.unityVersion;

    public static string ApplicationVersion => Application.version;

    public static string GameVersion => Singleton<BootstrapManager>.HasInstance() ? Singleton<BootstrapManager>.Instance.GameVersion : null;

    public static bool? IsDemo => Singleton<BootstrapManager>.HasInstance() ? Singleton<BootstrapManager>.Instance.IsDemo : null;

    public static bool? IsSingleplayer => Singleton<BootstrapManager>.HasInstance() ? Singleton<BootstrapManager>.Instance.IsSingleplayer : null;

    public static string LoadedLevelKey => Singleton<BootstrapManager>.HasInstance() ? Singleton<BootstrapManager>.Instance.LoadedLevelKey : null;

    public static bool? IsServer => Singleton<NetworkManager>.HasInstance() ? Singleton<NetworkManager>.Instance.IsServer : null;

    public static string SaveDirectory => SaveManager.GameSaveDirectoryPath;

    public static string SaveFileName => Singleton<SaveManager>.HasInstance() ? Singleton<SaveManager>.Instance.GameSaveFileName : null;

    public static string SaveFilePath => Singleton<SaveManager>.HasInstance() ? Singleton<SaveManager>.Instance.CurrentGameSaveFullPath : null;

    public static bool? IsNewSave => Singleton<SaveManager>.HasInstance() ? Singleton<SaveManager>.Instance.IsNewSave : null;

    public static bool? IsLoadingSave => Singleton<SaveManager>.HasInstance() ? Singleton<SaveManager>.Instance.IsLoadingSave : null;
}
