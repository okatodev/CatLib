using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using CatLib.Logging;

namespace CatLib.Localization;

public static class CatLocalization
{
    public const string EmbeddedFolder = "Lang";
    public const string OverridesFolder = "Translations";

    private static readonly object Sync = new();
    private static readonly Dictionary<string, TextCatalog> Catalogs = new(StringComparer.Ordinal);
    private static readonly HashSet<string> AutoLoaded = new(StringComparer.Ordinal);
    private static int _revision;

    internal static CatLogger Log { get; set; }

    public static int Revision => Volatile.Read(ref _revision);

    public static string TranslationsDirectory
    {
        get
        {
            try
            {
                return Path.Combine(Paths.ConfigPath, "CatLib", OverridesFolder);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    public static IReadOnlyList<TextCatalog> All
    {
        get
        {
            lock (Sync)
            {
                return Catalogs.Values.OrderBy(catalog => catalog.OwnerId, StringComparer.Ordinal).ToList();
            }
        }
    }

    public static TextCatalog For(BasePlugin plugin)
    {
        if (plugin == null)
        {
            throw new ArgumentNullException(nameof(plugin));
        }

        var metadata = MetadataHelper.GetMetadata(plugin)
                       ?? throw new ArgumentException($"{plugin.GetType().FullName} has no BepInPlugin attribute", nameof(plugin));
        var catalog = For(metadata.GUID);
        bool first;
        lock (Sync)
        {
            first = AutoLoaded.Add(metadata.GUID);
        }

        if (first)
        {
            AutoLoad(catalog, plugin.GetType().Assembly, metadata.Name);
        }

        return catalog;
    }

    public static TextCatalog For(string ownerId)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
        {
            throw new ArgumentException("Owner id must not be empty.", nameof(ownerId));
        }

        lock (Sync)
        {
            if (!Catalogs.TryGetValue(ownerId, out var catalog))
            {
                catalog = new TextCatalog(ownerId);
                Catalogs[ownerId] = catalog;
            }

            return catalog;
        }
    }

    public static TextCatalog Find(string ownerId)
    {
        if (ownerId == null)
        {
            return null;
        }

        lock (Sync)
        {
            return Catalogs.TryGetValue(ownerId, out var catalog) ? catalog : null;
        }
    }

    public static string OverridesFor(string ownerId)
    {
        var root = TranslationsDirectory;
        return root == null || string.IsNullOrWhiteSpace(ownerId) ? null : Path.Combine(root, ownerId);
    }

    public static int ReloadAll()
    {
        var total = 0;
        foreach (var catalog in All)
        {
            var directory = catalog.OverridesDirectory ?? OverridesFor(catalog.OwnerId);
            var load = catalog.LoadOverrides(directory);
            total += load.Texts;
            Report(catalog, load, directory);
        }

        return total;
    }

    internal static void Touch() => Interlocked.Increment(ref _revision);

    internal static string NameOf(TextCatalog catalog)
    {
        var name = catalog.FindExact("mod.name", CatLanguage.Fallback);
        if (!string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        var settings = CatLib.Config.CatConfig.All.FirstOrDefault(entry => entry.OwnerId == catalog.OwnerId);
        return settings?.DisplayName ?? catalog.OwnerId;
    }

    private static void AutoLoad(TextCatalog catalog, System.Reflection.Assembly assembly, string name)
    {
        try
        {
            catalog.LoadEmbedded(assembly, assembly.GetName().Name + "." + EmbeddedFolder + ".");
        }
        catch (Exception exception)
        {
            Log?.Error($"The translations built into {name} could not be read", exception);
        }

        var directory = OverridesFor(catalog.OwnerId);
        try
        {
            Report(catalog, catalog.LoadOverrides(directory), directory);
        }
        catch (Exception exception)
        {
            Log?.Error($"The translation files of {name} in {directory} could not be read", exception);
        }
    }

    private static void Report(TextCatalog catalog, TranslationLoad load, string directory)
    {
        foreach (var error in load.Errors)
        {
            Log?.Warning($"A translation file of {NameOf(catalog)} was skipped, {error}");
        }

        if (load.Texts > 0)
        {
            Log?.Info($"{NameOf(catalog)}: {load.Texts} text(s) in {string.Join(", ", load.Languages)} from {directory}");
        }
    }
}
