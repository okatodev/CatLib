using System;
using System.Collections.Generic;
using System.Linq;

namespace CatLib.Net;

public sealed record SessionRoster(IncompatiblePlayerAction Policy, IReadOnlyList<string> ActiveMods, IReadOnlyList<RosterEntry> Players)
{
    public static readonly SessionRoster Empty = new(IncompatiblePlayerAction.Warn, Array.Empty<string>(), Array.Empty<RosterEntry>());

    public RosterEntry Find(ulong id) => Players.FirstOrDefault(player => player.Id == id);

    public RosterEntry HostEntry => Players.FirstOrDefault(player => player.IsHost);

    public bool IsActive(string modId) => ActiveMods.Contains(modId, StringComparer.Ordinal);

    public IReadOnlyList<string> PausedMods(IEnumerable<ModInfo> declared) =>
        declared.Where(mod => mod.Policy == SessionPolicy.RequiredOnAll && !IsActive(mod.Id)).Select(mod => mod.Id).ToList();
}
