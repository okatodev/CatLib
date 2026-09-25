using System.Collections.Generic;

namespace CatLib.Net;

public sealed record PeerReport(ulong PeerId, SessionStatus Status, IReadOnlyList<CompatibilityProblem> Problems, string CatLibVersion, string GameVersion, bool Disconnecting = false)
{
    public bool IsCompatible => Problems.Count == 0 && Status != SessionStatus.Rejected;
}
