using System;
using System.Linq;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using CatLib.Assets;
using CatLib.Config;
using CatLib.DevTools;
using CatLib.Localization;
using CatLib.Logging;
using CatLib.Net;
using CatLib.Threading;
using CatLib.UI;
using CustomStamps.Game;
using CustomStamps.Packs;
using CustomStamps.Patches;

namespace CustomStamps;

[BepInPlugin(PluginMeta.Guid, PluginMeta.Name, PluginMeta.Version)]
[BepInDependency("catlib.core")]
public sealed class CustomStampsPlugin : BasePlugin
{
    public const string PacksSection = "Packs";
    public const string DevGroup = "Custom Stamps";

    public static StampLibrary Library { get; private set; }

    public static StampFactory Factory { get; private set; }

    public static StampSelection Selection { get; private set; }

    public override void Load()
    {
        var log = CatLogger.From(Log);
        var settings = CatSettings.For(this);
        var texts = settings.Texts;
        CatNetwork.Declare(this, SessionPolicy.ClientOnly);

        var library = new StampLibrary(log, texts, PluginMeta.Version);
        var factory = new StampFactory(log);
        var selection = new StampSelection(library, factory, log);
        Library = library;
        Factory = factory;
        Selection = selection;
        var patches = StampPatches.Install(PluginMeta.Guid, selection, log);

        void Refresh() => MainThread.Post(() => patches.Run("Refresh", selection.Refresh));
        library.Changed += Refresh;
        CatNetwork.ActiveModsChanged += Refresh;

        settings.Button(PacksSection, "Create", () =>
        {
            var folder = library.CreatePack();
            library.Load();
            ContentPacks.OpenFolder(folder);
            Notifications.Show(texts.FormatFor(CatLanguage.Current, "stamps.Created", System.IO.Path.GetFileName(folder)));
        });
        settings.Button(PacksSection, "Reload", () =>
        {
            library.Load();
            Notifications.Show(texts.PluralFor(CatLanguage.Current, "stamps.Reloaded", library.Packs.Count));
        });
        settings.Button(PacksSection, "Folder", () => ContentPacks.OpenFolder(ContentPacks.PluginsFolder));

        library.Load();

        DevMenu.Command(DevGroup, "Game stamps in the log", () =>
        {
            var text = factory.DescribeBase(StampKind.Decorative) + "\n" + factory.DescribeBase(StampKind.Weight);
            log.Info("[Stamps] " + text);
            return text.Split('\n')[0];
        }, "Writes how the game's decorative and weight stamps are made: their prefab, materials and textures. Open a level first.");
        DevMenu.Command(DevGroup, "Custom stamps in the log", () =>
        {
            var lines = library.Stamps.Select(stamp => $"{stamp.Key}: {stamp.Image.Width}x{stamp.Image.Height}, {(stamp.IsActive ? "active" : "paused")}").ToList();
            log.Info($"[Stamps] {library.Packs.Count} pack(s), {library.Stamps.Count} stamp(s), {factory.Built} made for the game" +
                     (lines.Count == 0 ? string.Empty : ":\n" + string.Join("\n", lines)));
            return $"{library.Packs.Count} pack(s), {library.Stamps.Count} stamp(s)";
        }, "Writes every custom stamp, its size and whether it is used in this session.");

        log.Info($"Custom Stamps {PluginMeta.Version} loaded with {texts.Count} translated text(s) in {string.Join(", ", texts.Languages)}, " +
                 $"{library.Packs.Count} stamp pack(s) in {ContentPacks.PluginsFolder}" + (patches.IsActive ? string.Empty : ", the game's stamps are left as they are"));
    }
}
