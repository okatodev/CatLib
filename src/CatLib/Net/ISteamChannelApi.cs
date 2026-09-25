using System;

namespace CatLib.Net;

internal interface ISteamChannelApi
{
    string Name { get; }

    int Send(ulong peer, byte[] payload, int flags, int channel);

    bool Accept(ulong peer);

    int Receive(int channel, Action<ulong, byte[]> received);
}
