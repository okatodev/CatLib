using System;
using System.Collections.Generic;

namespace CatLib.Net;

public enum RosterPeerState
{
    Checking,
    Evaluated,
    WithoutCatLib
}

public sealed record RosterPeer(ulong Id, string Name, RosterPeerState State, LocalIdentity Identity, IReadOnlyList<CompatibilityProblem> Problems, bool Disconnecting)
{
    public IReadOnlyList<CompatibilityProblem> ProblemList => Problems ?? Array.Empty<CompatibilityProblem>();
}
