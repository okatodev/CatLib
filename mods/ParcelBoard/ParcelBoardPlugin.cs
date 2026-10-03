using BepInEx;
using BepInEx.Unity.IL2CPP;
using CatLib.Config;
using CatLib.Core;
using CatLib.DevTools;
using CatLib.Game.Events;
using CatLib.Logging;
using CatLib.Net;
using ParcelBoard.Settings;

namespace ParcelBoard;

[BepInPlugin(PluginMeta.Guid, PluginMeta.Name, PluginMeta.Version)]
[BepInDependency("catlib.core")]
public sealed class ParcelBoardPlugin : BasePlugin
{
    public const string LanguageResourcePrefix = "ParcelBoard.Lang.";

    private ParcelBoardController _controller;

    public static ParcelBoardController Controller { get; private set; }

    public override void Load()
    {
        var log = CatLogger.From(Log);
        var settings = CatSettings.For(this);
        var translations = settings.Texts.Count;
        CatNetwork.Declare(this, SessionPolicy.ClientOnly);

        _controller = new ParcelBoardController(log, settings.Texts);
        _controller.Settings = new BoardSettings(settings, _controller.OnSettingsChanged);
        Controller = _controller;

        FrameLoop.Update += _controller.Update;
        BootstrapEvents.LevelLoadStarted += _controller.Forget;
        BootstrapEvents.GameRestartStarted += _controller.Forget;
        BootstrapEvents.MainMenuLoaded += _controller.Forget;
        DevMenu.Command("Parcel Board", "Parcels in the log", _controller.Describe,
            "Writes every parcel of the level with its destination, marks, missing stamps, size and place, and the counts of every list.");
        DevMenu.Command("Parcel Board", "Parcel sizes in the log", _controller.MeasureSizes,
            "Writes the shelf footprint in cells and the box size of one parcel of every size in the level, for drawing the size icons.");
        log.Info($"Parcel Board {PluginMeta.Version} loaded with {translations} translated text(s) in {string.Join(", ", settings.Texts.Languages)}");
    }
}
