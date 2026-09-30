using BepInEx;
using BepInEx.Unity.IL2CPP;
using BetterRepair.Data;
using BetterRepair.Settings;
using BetterRepair.Sync;
using CatLib.Config;
using CatLib.Core;
using CatLib.DevTools;
using CatLib.Game.Events;
using CatLib.Logging;
using CatLib.Net;
using CatLib.Saves;

namespace BetterRepair;

[BepInPlugin(PluginMeta.Guid, PluginMeta.Name, PluginMeta.Version)]
[BepInDependency("catlib.core")]
public sealed class BetterRepairPlugin : BasePlugin
{
    public const string LanguageResourcePrefix = "BetterRepair.Lang.";
    public const int DataVersion = 1;

    private BetterRepairController _controller;
    private StockStore _store;

    public override void Load()
    {
        var log = CatLogger.From(Log);
        var settings = CatSettings.For(this);
        var translations = settings.Texts.LoadEmbedded(typeof(BetterRepairPlugin).Assembly, LanguageResourcePrefix);
        CatNetwork.Declare(this, SessionPolicy.RequiredOnAll);

        _controller = new BetterRepairController(log, settings.Texts);
        _controller.Settings = new RepairSettings(settings, _controller.OnSettingsChanged);
        _controller.Sync = new StockSync(CatNetwork.Channel(this), () => _controller.CurrentStock, log);
        _controller.Initialize();
        _store = new StockStore(CatSaves.For(this, DataVersion), () => _controller.CurrentStock, log);
        _store.Loaded += _controller.OnStoreLoaded;

        FrameLoop.Update += _controller.Update;
        BootstrapEvents.MainMenuLoaded += _controller.OnMainMenuLoaded;
        BootstrapEvents.LevelLoadStarted += _controller.Forget;
        BootstrapEvents.GameRestartStarted += _controller.Forget;
        DevMenu.Command("Better Repair", "Cardboard state", _controller.Describe,
            "Writes the cardboard stock, the time period and the state of every repair table to the log.");
        DevMenu.Command("Better Repair", "Refill now", _controller.RefillNow,
            "Refills the cardboard as a new day would. Host only.");
        log.Info($"Better Repair {PluginMeta.Version} loaded with {translations} translated text(s) in {string.Join(", ", settings.Texts.Languages)}");
    }
}
