using System;
using System.Collections.Generic;

namespace CatLib.Net;

public sealed class SessionLink : IModMessageLink
{
    private readonly Func<HostSession> _host;
    private readonly Func<ClientSession> _client;
    private readonly Func<ISessionTransport> _transport;

    public SessionLink(Func<HostSession> host, Func<ClientSession> client, Func<ISessionTransport> transport)
    {
        _host = host;
        _client = client;
        _transport = transport;
    }

    public SessionRole Role => CatNetwork.ResolveRole(_host() != null, _client() != null);

    public ulong LocalId => _transport()?.LocalId ?? 0;

    public bool HostShares(string modId) => _client()?.SharesWithHost(modId) ?? false;

    public bool SendToHost(ModMessageData message) => _client()?.Send(message) ?? false;

    public bool SendToPeer(ulong peer, ModMessageData message) => _host()?.Send(peer, message) ?? false;

    public IReadOnlyList<ulong> PeersSharing(string modId) => _host()?.PeersSharing(modId) ?? (IReadOnlyList<ulong>)Array.Empty<ulong>();
}
