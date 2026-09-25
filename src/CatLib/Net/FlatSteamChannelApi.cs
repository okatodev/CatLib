using System;
using System.Runtime.InteropServices;

namespace CatLib.Net;

internal sealed unsafe class FlatSteamChannelApi : ISteamChannelApi
{
    public const string LibraryName = "steam_api64";
    public const int IdentitySize = 136;
    public const int IdentityTypeSteamId = 16;
    public const int MessageDataOffset = 0;
    public const int MessageSizeOffset = 8;
    public const int MessagePeerSteamIdOffset = 16 + 8;
    public const int MaxBatch = 32;

    private static readonly string[] AccessorNames =
    {
        "SteamAPI_SteamNetworkingMessages_SteamAPI_v002",
        "SteamAPI_SteamNetworkingMessages_v002",
        "SteamAPI_SteamNetworkingMessages_SteamAPI_v001"
    };

    private readonly IntPtr _self;
    private readonly delegate* unmanaged[Cdecl]<IntPtr, byte*, byte*, uint, int, int, int> _send;
    private readonly delegate* unmanaged[Cdecl]<IntPtr, int, IntPtr*, int, int> _receive;
    private readonly delegate* unmanaged[Cdecl]<IntPtr, byte*, byte> _accept;
    private readonly delegate* unmanaged[Cdecl]<IntPtr, void> _release;

    private FlatSteamChannelApi(IntPtr self, IntPtr send, IntPtr receive, IntPtr accept, IntPtr release, string accessor)
    {
        _self = self;
        _send = (delegate* unmanaged[Cdecl]<IntPtr, byte*, byte*, uint, int, int, int>)send;
        _receive = (delegate* unmanaged[Cdecl]<IntPtr, int, IntPtr*, int, int>)receive;
        _accept = (delegate* unmanaged[Cdecl]<IntPtr, byte*, byte>)accept;
        _release = (delegate* unmanaged[Cdecl]<IntPtr, void>)release;
        Accessor = accessor;
    }

    public string Name => "Flat";

    public string Accessor { get; }

    public static FlatSteamChannelApi TryCreate(out string error)
    {
        if (!NativeLibrary.TryLoad(LibraryName, out var library))
        {
            error = $"{LibraryName} is not loaded";
            return null;
        }

        string accessorName = null;
        var accessor = IntPtr.Zero;
        foreach (var name in AccessorNames)
        {
            if (NativeLibrary.TryGetExport(library, name, out accessor))
            {
                accessorName = name;
                break;
            }
        }

        if (accessorName == null)
        {
            error = "No SteamNetworkingMessages accessor was found in " + LibraryName;
            return null;
        }

        if (!NativeLibrary.TryGetExport(library, "SteamAPI_ISteamNetworkingMessages_SendMessageToUser", out var send) ||
            !NativeLibrary.TryGetExport(library, "SteamAPI_ISteamNetworkingMessages_ReceiveMessagesOnChannel", out var receive) ||
            !NativeLibrary.TryGetExport(library, "SteamAPI_ISteamNetworkingMessages_AcceptSessionWithUser", out var accept) ||
            !NativeLibrary.TryGetExport(library, "SteamAPI_SteamNetworkingMessage_t_Release", out var release))
        {
            error = "A SteamNetworkingMessages function is missing in " + LibraryName;
            return null;
        }

        var self = ((delegate* unmanaged[Cdecl]<IntPtr>)accessor)();
        if (self == IntPtr.Zero)
        {
            error = accessorName + " returned no interface, Steam may not be initialized";
            return null;
        }

        error = null;
        return new FlatSteamChannelApi(self, send, receive, accept, release, accessorName);
    }

    public int Send(ulong peer, byte[] payload, int flags, int channel)
    {
        var identity = stackalloc byte[IdentitySize];
        WriteIdentity(identity, peer);
        fixed (byte* data = payload)
        {
            return _send(_self, identity, data, (uint)payload.Length, flags, channel);
        }
    }

    public bool Accept(ulong peer)
    {
        var identity = stackalloc byte[IdentitySize];
        WriteIdentity(identity, peer);
        return _accept(_self, identity) != 0;
    }

    public int Receive(int channel, Action<ulong, byte[]> received)
    {
        var buffer = stackalloc IntPtr[MaxBatch];
        var count = _receive(_self, channel, buffer, MaxBatch);
        for (var index = 0; index < count; index++)
        {
            var message = (byte*)buffer[index];
            try
            {
                var data = *(IntPtr*)(message + MessageDataOffset);
                var size = *(int*)(message + MessageSizeOffset);
                var sender = *(ulong*)(message + MessagePeerSteamIdOffset);
                var bytes = size > 0 && size <= MessageCodec.MaxMessageBytes ? new byte[size] : null;
                if (bytes != null)
                {
                    Marshal.Copy(data, bytes, 0, size);
                }

                received(sender, bytes);
            }
            finally
            {
                _release((IntPtr)message);
            }
        }

        return Math.Max(count, 0);
    }

    private static void WriteIdentity(byte* identity, ulong steamId)
    {
        for (var index = 0; index < IdentitySize; index++)
        {
            identity[index] = 0;
        }

        *(int*)identity = IdentityTypeSteamId;
        *(int*)(identity + 4) = sizeof(ulong);
        *(ulong*)(identity + 8) = steamId;
    }
}
