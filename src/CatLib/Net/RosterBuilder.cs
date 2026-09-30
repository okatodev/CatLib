using System;
using System.Collections.Generic;
using System.Linq;

namespace CatLib.Net;

public static class RosterBuilder
{
    public static SessionRoster Build(LocalIdentity host, ulong hostId, string hostName, IncompatiblePlayerAction policy, IEnumerable<RosterPeer> peers)
    {
        var peerList = (peers ?? Array.Empty<RosterPeer>()).ToList();
        var entries = new List<RosterEntry> { HostEntry(host, hostId, hostName) };
        entries.AddRange(peerList.Select(peer => PeerEntry(host, peer)));
        return new SessionRoster(policy, ActiveMods(host, peerList), entries);
    }

    public static SessionRoster WithoutHostCatLib(LocalIdentity self, ulong selfId, string selfName, ulong hostId, string hostName,
        IReadOnlyList<CompatibilityProblem> problems)
    {
        var problemList = problems ?? Array.Empty<CompatibilityProblem>();
        var protocolDiffers = problemList.Any(problem => problem.Kind == ProblemKind.ProtocolMismatch);
        var hostEntry = new RosterEntry(hostId, hostName, true, protocolDiffers ? RosterStatus.Limited : RosterStatus.WithoutCatLib,
            string.Empty, string.Empty, problemList.Where(problem => problem.Kind == ProblemKind.ProtocolMismatch).ToList(), Array.Empty<RosterMod>());
        var selfMods = self.Mods
            .OrderBy(mod => mod.Name, StringComparer.OrdinalIgnoreCase)
            .Select(mod => new RosterMod(mod.Id, mod.Name, mod.Version, mod.Policy, mod.Policy == SessionPolicy.RequiredOnAll ? ModMark.NotOnHost : ModMark.Local))
            .ToList();
        var selfEntry = new RosterEntry(selfId, selfName, false, selfMods.Any(mod => mod.IsProblem) ? RosterStatus.Limited : RosterStatus.Compatible,
            self.CatLibVersion, self.GameVersion, problemList, selfMods);
        return new SessionRoster(IncompatiblePlayerAction.Warn, Array.Empty<string>(), new[] { hostEntry, selfEntry });
    }

    public static IReadOnlyList<string> ActiveMods(LocalIdentity host, IReadOnlyList<RosterPeer> peers)
    {
        var counted = peers.Where(peer => peer.State != RosterPeerState.Checking && !peer.Disconnecting).ToList();
        return host.Mods
            .Where(mod => mod.Policy == SessionPolicy.RequiredOnAll)
            .Where(mod => counted.All(peer => HasCompatible(peer.Identity, mod)))
            .Select(mod => mod.Id)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();
    }

    private static bool HasCompatible(LocalIdentity peer, ModInfo hostMod)
    {
        var peerMod = peer?.Find(hostMod.Id);
        return peerMod != null && VersionMatcher.Matches(hostMod.Version, peerMod.Version, hostMod.Rule);
    }

    private static RosterEntry HostEntry(LocalIdentity host, ulong hostId, string hostName)
    {
        var mods = host.Mods
            .OrderBy(mod => mod.Name, StringComparer.OrdinalIgnoreCase)
            .Select(mod => new RosterMod(mod.Id, mod.Name, mod.Version, mod.Policy, mod.Policy == SessionPolicy.RequiredOnAll ? ModMark.Same : ModMark.Local))
            .ToList();
        return new RosterEntry(hostId, hostName, true, RosterStatus.Compatible, host.CatLibVersion, host.GameVersion, Array.Empty<CompatibilityProblem>(), mods);
    }

    private static RosterEntry PeerEntry(LocalIdentity host, RosterPeer peer)
    {
        var problems = peer.ProblemList;
        if (peer.State == RosterPeerState.Checking)
        {
            return new RosterEntry(peer.Id, peer.Name, false, RosterStatus.Checking, string.Empty, string.Empty, Array.Empty<CompatibilityProblem>(), Array.Empty<RosterMod>());
        }

        var mods = new List<RosterMod>();
        var identity = peer.State == RosterPeerState.WithoutCatLib ? null : peer.Identity;
        if (identity != null || peer.State == RosterPeerState.WithoutCatLib)
        {
            foreach (var hostMod in host.Mods.Where(mod => mod.Policy == SessionPolicy.RequiredOnAll))
            {
                var peerMod = identity?.Find(hostMod.Id);
                if (peerMod == null)
                {
                    mods.Add(new RosterMod(hostMod.Id, hostMod.Name, hostMod.Version, hostMod.Policy, ModMark.Missing, hostMod.Version));
                }
                else
                {
                    var same = VersionMatcher.Matches(hostMod.Version, peerMod.Version, hostMod.Rule);
                    mods.Add(new RosterMod(hostMod.Id, peerMod.Name, peerMod.Version, hostMod.Policy, same ? ModMark.Same : ModMark.OtherVersion, hostMod.Version));
                }
            }

            foreach (var peerMod in identity?.Mods ?? Array.Empty<ModInfo>())
            {
                if (mods.Any(mod => mod.Id == peerMod.Id))
                {
                    continue;
                }

                var hostMod = host.Find(peerMod.Id);
                var notOnHost = hostMod == null && peerMod.Policy == SessionPolicy.RequiredOnAll;
                mods.Add(new RosterMod(peerMod.Id, peerMod.Name, peerMod.Version, hostMod?.Policy ?? peerMod.Policy,
                    notOnHost ? ModMark.NotOnHost : ModMark.Local, hostMod?.Version));
            }
        }

        mods = mods.OrderBy(mod => mod.Name, StringComparer.OrdinalIgnoreCase).ToList();
        var status = peer.Disconnecting
            ? RosterStatus.Leaving
            : peer.State == RosterPeerState.WithoutCatLib
                ? RosterStatus.WithoutCatLib
                : problems.Count == 0 ? RosterStatus.Compatible : RosterStatus.Limited;
        return new RosterEntry(peer.Id, peer.Name, false, status, identity?.CatLibVersion ?? string.Empty, identity?.GameVersion ?? string.Empty, problems, mods);
    }
}
