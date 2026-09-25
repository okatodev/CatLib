using System;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Steamworks;

namespace CatLib.Net;

internal sealed class InteropSteamChannelApi : ISteamChannelApi
{
    public const int MaxBatch = 32;

    private readonly Il2CppStructArray<IntPtr> _buffer = new(MaxBatch);

    public string Name => "Interop";

    public unsafe int Send(ulong peer, byte[] payload, int flags, int channel)
    {
        var identity = Identity(peer);
        fixed (byte* data = payload)
        {
            return (int)SteamNetworkingMessages.SendMessageToUser(ref identity, (IntPtr)data, (uint)payload.Length, flags, channel);
        }
    }

    public bool Accept(ulong peer)
    {
        var identity = Identity(peer);
        return SteamNetworkingMessages.AcceptSessionWithUser(ref identity);
    }

    public int Receive(int channel, Action<ulong, byte[]> received)
    {
        var count = SteamNetworkingMessages.ReceiveMessagesOnChannel(channel, _buffer, MaxBatch);
        for (var index = 0; index < count; index++)
        {
            var pointer = _buffer[index];
            try
            {
                var message = SteamNetworkingMessage_t.FromIntPtr(pointer);
                var size = message.m_cbSize;
                var bytes = size > 0 && size <= MessageCodec.MaxMessageBytes ? new byte[size] : null;
                if (bytes != null)
                {
                    Marshal.Copy(message.m_pData, bytes, 0, size);
                }

                received(message.m_identityPeer.GetSteamID64(), bytes);
            }
            finally
            {
                SteamNetworkingMessage_t.Release(pointer);
            }
        }

        return Math.Max(count, 0);
    }

    private static SteamNetworkingIdentity Identity(ulong steamId)
    {
        var identity = new SteamNetworkingIdentity();
        identity.SetSteamID64(steamId);
        return identity;
    }
}
