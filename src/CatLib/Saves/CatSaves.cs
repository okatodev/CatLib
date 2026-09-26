using System;
using System.IO;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using CatLib.Core;
using CatLib.Events;
using CatLib.Game;
using CatLib.Game.Events;
using CatLib.Logging;
using CatLib.Net;

namespace CatLib.Saves;

public static class CatSaves
{
    public const string FolderName = "CatLibSaves";

    private static SaveSession _session;
    private static CatLogger _log;

    public static event Action<SaveCommitReport> Committed;

    public static string CurrentSave => _session?.CurrentSave;

    public static string Root => _session?.Root;

    public static ModSave For(BasePlugin plugin, int dataVersion = 1)
    {
        if (plugin == null)
        {
            throw new ArgumentNullException(nameof(plugin));
        }

        var metadata = MetadataHelper.GetMetadata(plugin)
                       ?? throw new ArgumentException($"{plugin.GetType().FullName} has no BepInPlugin attribute", nameof(plugin));
        return For(metadata.GUID, metadata.Version?.ToString(), dataVersion);
    }

    public static ModSave For(string modId, string modVersion, int dataVersion = 1)
    {
        if (_session == null)
        {
            throw new InvalidOperationException("CatLib is not initialized yet; add [BepInDependency(\"catlib.core\")] to the plugin");
        }

        return _session.Register(modId, modVersion, dataVersion);
    }

    public static string ResolveRoot(string gameSaveDirectory)
    {
        if (string.IsNullOrWhiteSpace(gameSaveDirectory))
        {
            return null;
        }

        var parent = Path.GetDirectoryName(gameSaveDirectory.TrimEnd('/', '\\'));
        return string.IsNullOrWhiteSpace(parent) ? null : Path.Combine(parent, FolderName);
    }

    internal static void Initialize(CatLogger log)
    {
        if (_session != null)
        {
            return;
        }

        _log = log;
        _session = new SaveSession(() => ResolveRoot(GameInfo.SaveDirectory), IsAuthority, () => DateTime.Now, log);
        SaveEvents.SaveFileSelected += OnSaveFileSelected;
        SaveEvents.GameSavingStarted += _session.BeginSave;
        SaveEvents.SuccessfullySaved += OnSuccessfullySaved;
        SaveEvents.UnsuccessfullySaved += OnUnsuccessfullySaved;
        BootstrapEvents.MainMenuLoaded += _session.Close;
        FrameLoop.Update += DetachOnClient;
    }

    private static bool IsAuthority() => CatNetwork.IsAuthority && GameInfo.IsServer != false;

    private static void OnSaveFileSelected()
    {
        if (CatNetwork.Role == SessionRole.Client)
        {
            _session.Close();
            return;
        }

        _session.Select(GameInfo.SaveFileName, GameInfo.IsNewSave);
    }

    private static void OnSuccessfullySaved()
    {
        var report = _session.CommitSave();
        SafeInvoker.Invoke(Committed, report, "CatSaves.Committed", _log);
    }

    private static void OnUnsuccessfullySaved()
    {
        _session.FailSave();
        _log?.Warning("The game failed to save, mod data was not written either");
    }

    private static void DetachOnClient()
    {
        if (_session.IsAttached && CatNetwork.Role == SessionRole.Client)
        {
            _log?.Info($"Joined a session as a client, save data of {_session.CurrentSave} is detached");
            _session.Close();
        }
    }
}
