using System;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using CatLib.Config;
using CatLib.Core;
using CatLib.DevTools;
using CatLib.Game.Events;
using CatLib.Logging;
using CatLib.Net;
using StackIt.Patches;
using StackIt.Research;
using StackIt.Settings;

namespace StackIt;

[BepInPlugin(PluginMeta.Guid, PluginMeta.Name, PluginMeta.Version)]
[BepInDependency("catlib.core")]
public sealed class StackItPlugin : BasePlugin
{
    public const string LanguageResourcePrefix = "StackIt.Lang.";
    public const string DevGroup = "Stack it!";

    public static StackController Controller { get; private set; }

    public override void Load()
    {
        var log = CatLogger.From(Log);
        var settings = CatSettings.For(this);
        var translations = settings.Texts.Count;
        CatNetwork.Declare(this, SessionPolicy.RequiredOnAll);

        var controller = new StackController(log);
        controller.Settings = new StackSettings(settings, null);
        Controller = controller;
        var patches = StackPatches.Install(PluginMeta.Guid, controller, log);
        patches.TurnedOff += controller.Forget;

        FrameLoop.Update += () => patches.Run("Update", controller.Update);
        BootstrapEvents.LevelLoadStarted += controller.Forget;
        BootstrapEvents.GameRestartStarted += controller.Forget;
        BootstrapEvents.MainMenuLoaded += controller.Forget;
        GameplayEvents.GameStarted += controller.OnGameStarted;

        var survey = new StackSurvey(log);
        DevMenu.Command(DevGroup, "Bridges in the log", controller.Describe,
            "Writes every parcel that stands across a joint, the parcels under it and the cells it holds on them.");
        DevMenu.Command(DevGroup, "Parcel grids in the log", survey.ParcelGrids,
            "Writes the grid on top of one parcel of every size next to its shelf footprint, and the height of its top.");
        DevMenu.Command(DevGroup, "Storages in the log", survey.Storages,
            "Writes every storage with its grid, limits and the tree of parcels standing on it.");
        DevMenu.Command(DevGroup, "Level tops in the log", survey.LevelTops,
            "Writes, for every storage, the parcels on it grouped by the height of their tops.");
        log.Info($"Stack it! {PluginMeta.Version} loaded with {translations} translated text(s) in {string.Join(", ", settings.Texts.Languages)}, " +
                 (patches.IsActive ? "the game is patched" : "the game could not be patched, parcels stack as without the mod"));
    }
}
