using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Unity.IL2CPP;

namespace CatLib.Localization;

public static class CatLocalization
{
    private static readonly object Sync = new();
    private static readonly Dictionary<string, TextCatalog> Catalogs = new(StringComparer.Ordinal);

    public static TextCatalog For(BasePlugin plugin)
    {
        var metadata = MetadataHelper.GetMetadata(plugin)
                       ?? throw new ArgumentException($"{plugin.GetType().FullName} has no BepInPlugin attribute", nameof(plugin));
        return For(metadata.GUID);
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
}
