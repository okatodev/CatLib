using BepInEx;
using BepInEx.Unity.IL2CPP;
using CatLib.Config;
using CatLib.Core;
using CatLib.Logging;
using CatLib.Net;
using CatLib.Saves;

namespace ShelfLabels;

[BepInPlugin(PluginMeta.Guid, PluginMeta.Name, PluginMeta.Version)]
[BepInDependency("catlib.core")]
public sealed class ShelfLabelsPlugin : BasePlugin
{
    public const string LanguageResourcePrefix = "ShelfLabels.Lang.";
    public const int DataVersion = 1;

    private LabelsController _controller;
    private LabelStore _store;

    public override void Load()
    {
        var log = CatLogger.From(Log);
        var settings = CatSettings.For(this);
        var translations = settings.Texts.LoadEmbedded(typeof(ShelfLabelsPlugin).Assembly, LanguageResourcePrefix);
        CatNetwork.Declare(this, SessionPolicy.RequiredOnAll);

        var board = new LabelBoard();
        var placements = new PlacementBoard();
        var stands = new StandBoard();
        var channel = CatNetwork.Channel(this);
        _controller = new LabelsController(log, channel, board, placements, stands, settings.Texts);
        _controller.Settings = new LabelSettings(settings, _controller.RequestLayout);
        _controller.Sync = new LabelSync(channel, board, placements, stands, _controller.SpriteCount, () => _controller.Settings.Slots.Value,
            () => _controller.Settings.Placement.Value, () => _controller.Settings.HideStands.Value, log);
        _store = new LabelStore(CatSaves.For(this, DataVersion), board, placements, stands, log);
        var patched = LabelClickPatch.Install(PluginMeta.Guid, _controller.HandleClick, log);
        FrameLoop.Update += _controller.Update;
        log.Info($"Shelf Labels {PluginMeta.Version} loaded with {translations} translated text(s), clicks {(patched ? "patched" : "not patched")}");
    }
}
