using System.Linq;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using CatLib.Config;
using CatLib.Core;
using CatLib.DevTools;
using CatLib.Game.Events;
using CatLib.Logging;
using CatLib.Net;
using CatLib.Patching;
using CatLib.Threading;
using TooLate.Logic;
using TooLate.Patches;
using TooLate.Research;
using TooLate.Scene;
using TooLate.Settings;

namespace TooLate;

[BepInPlugin(PluginMeta.Guid, PluginMeta.Name, PluginMeta.Version)]
[BepInDependency("catlib.core")]
public sealed class TooLatePlugin : BasePlugin
{
    public const string LanguageResourcePrefix = "TooLate.Lang.";
    public const string DevGroup = "Too Late";

    public static JoinCoordinator Joins { get; private set; }

    public override void Load()
    {
        var log = CatLogger.From(Log);
        var settings = CatSettings.For(this);
        var translations = settings.Texts.Count;
        CatNetwork.Declare(this, SessionPolicy.HostOnly);

        var joins = new JoinCoordinator(log, settings.Texts);
        joins.Settings = new JoinSettings(settings, null);
        Joins = joins;
        var connect = typeof(Server).GetMethods().FirstOrDefault(method => method.Name == nameof(Server.HandleClientConnect)
            && method.GetParameters().Length == 2 && method.GetParameters()[0].ParameterType == typeof(ulong));
        var loading = CodePatch.Locate(PluginMeta.Guid, "joining a level", log, connect, PlayerLimit.LoadingPattern, PlayerLimit.LoadingOffset, PlayerLimit.LoadingLength);
        var limit = CodePatch.Locate(PluginMeta.Guid, "player limit", log, connect, PlayerLimit.CountPattern, PlayerLimit.CountOffset, 1);
        joins.UseCodePatches(loading, limit);

        var lobby = new LobbyOverflow(log, settings.Texts);
        var recorder = new MessageRecorder(log);
        var patches = TooLatePatches.Install(PluginMeta.Guid, joins, lobby, recorder, log);
        patches.TurnedOff += () => MainThread.Post(joins.ShutDown);

        FrameLoop.Update += () =>
        {
            patches.Run("Update", joins.Update);
            patches.Run("Recorder", recorder.Update);
        };
        NetworkEvents.ClientConnected += id => patches.Run("ClientConnected", () => joins.OnClientConnected(id));
        NetworkEvents.ClientDisconnected += id => patches.Run("ClientDisconnected", () => joins.OnClientDisconnected(id));
        BootstrapEvents.GameStartedFromLobby += () => patches.Run("GameStarted", joins.EnterLevel);
        BootstrapEvents.LevelLoadStarted += () => patches.Run("LevelLoadStarted", joins.EnterLevel);
        BootstrapEvents.GameRestartStarted += () => joins.LeaveLevel(true);
        BootstrapEvents.MainMenuLoaded += () =>
        {
            joins.LeaveLevel(false);
            lobby.Forget();
        };

        DevMenu.Toggle(DevGroup, "Record game messages", () => recorder.IsEnabled, recorder.SetEnabled,
            "Writes every network message of the game to the log: what the host sends to each player and what this game sends to the host.");
        DevMenu.Command(DevGroup, "Joining players in the log", joins.Describe,
            "Writes whether players can join your level now, the player limit and every player who is joining.");
        DevMenu.Command(DevGroup, "Snapshot in the log", joins.SnapshotProbe,
            "Writes the warehouse as it is now into a separate file in BepInEx/cache/TooLate, like for a joining player, and writes what it holds.");
        log.Info($"Too Late {PluginMeta.Version} loaded with {translations} translated text(s) in {string.Join(", ", settings.Texts.Languages)}, " +
                 (patches.IsActive && joins.CanJoinInLevel ? "players can join your levels" : "joining a level stays closed like in the game") +
                 (joins.CanRaiseLimit ? string.Empty : ", the player limit stays 4"));
    }
}
