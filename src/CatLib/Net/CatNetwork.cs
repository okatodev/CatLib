using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Unity.IL2CPP;

namespace CatLib.Net;

public static class CatNetwork
{
    private static readonly object Sync = new();
    private static readonly Dictionary<string, ModInfo> Declared = new(StringComparer.Ordinal);

    public static IReadOnlyList<ModInfo> DeclaredMods
    {
        get
        {
            lock (Sync)
            {
                return Declared.Values.OrderBy(mod => mod.Id, StringComparer.Ordinal).ToList();
            }
        }
    }

    public static ModInfo Declare(BasePlugin plugin, SessionPolicy policy, VersionRule rule = VersionRule.SameMinor)
    {
        if (plugin == null)
        {
            throw new ArgumentNullException(nameof(plugin));
        }

        var metadata = MetadataHelper.GetMetadata(plugin)
                       ?? throw new ArgumentException($"{plugin.GetType().FullName} has no BepInPlugin attribute", nameof(plugin));
        return Declare(metadata.GUID, metadata.Name, metadata.Version?.ToString(), policy, rule);
    }

    public static ModInfo Declare(string id, string name, string version, SessionPolicy policy, VersionRule rule = VersionRule.SameMinor)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Mod id must not be empty.", nameof(id));
        }

        var info = new ModInfo(id, string.IsNullOrWhiteSpace(name) ? id : name, version ?? string.Empty, policy, rule);
        lock (Sync)
        {
            Declared[id] = info;
        }

        return info;
    }

    public static bool Undeclare(string id)
    {
        lock (Sync)
        {
            return Declared.Remove(id);
        }
    }

    public static LocalIdentity CreateIdentity(string catLibVersion, string gameVersion) =>
        new(catLibVersion, gameVersion ?? string.Empty, DeclaredMods);
}
