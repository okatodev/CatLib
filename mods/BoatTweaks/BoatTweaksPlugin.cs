using System.IO;
using BepInEx.Unity.IL2CPP;
using BepInEx;
using BoatTweaks.Logic;
using BoatTweaks.Settings;
using CatLib.Config;
using CatLib.Core;
using CatLib.DevTools;
using CatLib.Game.Events;
using CatLib.Logging;
using CatLib.Net;

namespace BoatTweaks;

[BepInPlugin(PluginMeta.Guid, PluginMeta.Name, PluginMeta.Version)]
[BepInDependency("catlib.core")]
public sealed class BoatTweaksPlugin : BasePlugin
{
    public const string LanguageResourcePrefix = "BoatTweaks.Lang.";

    private BoatController _controller;

    public override void Load()
    {
        var log = CatLogger.From(Log);
        var settings = CatSettings.For(this);
        var translations = settings.Texts.LoadEmbedded(typeof(BoatTweaksPlugin).Assembly, LanguageResourcePrefix);
        CatNetwork.Declare(this, SessionPolicy.RequiredOnAll);

        var library = new PatternLibrary(Path.Combine(Paths.ConfigPath, "BoatTweaks", "layouts"));
        _controller = new BoatController(log, settings.Texts, library);
        _controller.Settings = new BoatSettings(settings, _controller.RequestDecision, _controller.RequestApply);
        FrameLoop.Update += _controller.Update;
        BootstrapEvents.GameRestartStarted += _controller.SuspendForRestart;
        BootstrapEvents.LevelLoadStarted += _controller.OnLevelLoadStarted;
        CatNetwork.ActiveModsChanged += _controller.OnActiveModsChanged;
        DevMenu.Command("Boat Tweaks", "Boat heights", _controller.DescribeBoats,
            "Writes the height limits of every boat in the scene and whether its stack is approved.");
        log.Info($"Boat Tweaks {PluginMeta.Version} loaded with {translations} translated text(s) in {string.Join(", ", settings.Texts.Languages)}, own layouts in {library.Directory}");
    }
}
