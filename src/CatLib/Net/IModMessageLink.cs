using System.Collections.Generic;

namespace CatLib.Net;

public interface IModMessageLink
{
    SessionRole Role { get; }

    ulong LocalId { get; }

    bool HostShares(string modId);

    bool SendToHost(ModMessageData message);

    bool SendToPeer(ulong peer, ModMessageData message);

    IReadOnlyList<ulong> PeersSharing(string modId);
}
