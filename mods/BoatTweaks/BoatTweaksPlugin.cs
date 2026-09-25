using BepInEx;
using BepInEx.Unity.IL2CPP;
using CatLib.Config;
using CatLib.Core;
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

        _controller = new BoatController(log);
        _controller.Settings = new BoatSettings(settings, _controller.RequestApply);
        FrameLoop.Update += _controller.Update;
        log.Info($"Boat Tweaks {PluginMeta.Version} loaded with {translations} translated text(s) in {string.Join(", ", settings.Texts.Languages)}");
    }
}
