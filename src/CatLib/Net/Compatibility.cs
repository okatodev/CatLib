using System;
using System.Collections.Generic;
using System.Linq;

namespace CatLib.Net;

public static class Compatibility
{
    public static IReadOnlyList<CompatibilityProblem> Compare(LocalIdentity host, LocalIdentity client)
    {
        var problems = new List<CompatibilityProblem>();

        if (client == null)
        {
            foreach (var mod in host.Mods.Where(mod => mod.Policy == SessionPolicy.RequiredOnAll))
            {
                problems.Add(new CompatibilityProblem(ProblemKind.MissingOnClient, mod.Id, mod.Version, null));
            }

            return problems;
        }

        if (!string.Equals(host.GameVersion, client.GameVersion, StringComparison.Ordinal))
        {
            problems.Add(new CompatibilityProblem(ProblemKind.GameVersionMismatch, "game", host.GameVersion, client.GameVersion));
        }

        var ids = host.Mods.Select(mod => mod.Id).Concat(client.Mods.Select(mod => mod.Id)).Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal);
        foreach (var id in ids)
        {
            var hostMod = host.Find(id);
            var clientMod = client.Find(id);
            var declaration = hostMod ?? clientMod;
            if (declaration.Policy != SessionPolicy.RequiredOnAll)
            {
                continue;
            }

            if (hostMod == null)
            {
                problems.Add(new CompatibilityProblem(ProblemKind.MissingOnHost, id, null, clientMod.Version));
            }
            else if (clientMod == null)
            {
                problems.Add(new CompatibilityProblem(ProblemKind.MissingOnClient, id, hostMod.Version, null));
            }
            else if (!VersionMatcher.Matches(hostMod.Version, clientMod.Version, hostMod.Rule))
            {
                problems.Add(new CompatibilityProblem(ProblemKind.VersionMismatch, id, hostMod.Version, clientMod.Version));
            }
        }

        return problems;
    }

    public static IReadOnlyList<CompatibilityProblem> CompareWithoutHost(LocalIdentity client) =>
        client.Mods
            .Where(mod => mod.Policy == SessionPolicy.RequiredOnAll)
            .Select(mod => new CompatibilityProblem(ProblemKind.MissingOnHost, mod.Id, null, mod.Version))
            .ToList();
}
