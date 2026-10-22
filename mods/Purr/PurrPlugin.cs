using BepInEx;
using BepInEx.Unity.IL2CPP;
using CatLib.Config;
using CatLib.Core;
using CatLib.Game.Events;
using CatLib.Logging;
using CatLib.Net;
using CatLib.Voice;
using Purr.Settings;

namespace Purr;

[BepInPlugin(PluginMeta.Guid, PluginMeta.Name, PluginMeta.Version)]
[BepInDependency("catlib.core")]
public sealed class PurrPlugin : BasePlugin
{
    public const string LanguageResourcePrefix = "Purr.Lang.";

    public static PurrController Controller { get; private set; }

    public override void Load()
    {
        var log = CatLogger.From(Log);
        var settings = CatSettings.For(this);
        var translations = settings.Texts.Count;
        CatNetwork.Declare(this, SessionPolicy.ClientOnly);
        CatVoice.Enable(this);

        Controller = new PurrController(log, settings.Texts, new VoiceSettings(settings));
        FrameLoop.Update += Controller.Update;
        BootstrapEvents.LevelLoadStarted += Controller.Forget;
        BootstrapEvents.GameRestartStarted += Controller.Forget;
        BootstrapEvents.MainMenuLoaded += Controller.Forget;
        log.Info($"Purr! {PluginMeta.Version} loaded with {translations} translated text(s) in {string.Join(", ", settings.Texts.Languages)}");
    }
}
