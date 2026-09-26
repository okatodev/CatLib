using System;
using System.Text;
using CatLib.Events;
using CatLib.Logging;

namespace CatLib.Net;

public sealed class ModChannel
{
    private readonly ModMessenger _messenger;

    internal ModChannel(ModMessenger messenger, string modId)
    {
        _messenger = messenger;
        ModId = modId;
    }

    public event Action<ModMessage> Received;

    public event Action<ulong> PeerJoined;

    public event Action<ulong> PeerLeft;

    public string ModId { get; }

    public bool CanSendToHost => _messenger.CanReachHost(ModId);

    public bool SendToHost(string name, byte[] data) => _messenger.SendToHost(ModId, Check(name), CheckData(data));

    public bool SendToHost(string name, string text) => SendToHost(name, Encode(text));

    public int Broadcast(string name, byte[] data) => _messenger.Broadcast(ModId, Check(name), CheckData(data));

    public int Broadcast(string name, string text) => Broadcast(name, Encode(text));

    public bool SendTo(ulong peer, string name, byte[] data) => _messenger.SendTo(peer, ModId, Check(name), CheckData(data));

    public bool SendTo(ulong peer, string name, string text) => SendTo(peer, name, Encode(text));

    internal void RaiseReceived(ModMessage message, CatLogger log) => SafeInvoker.Invoke(Received, message, ModId + ".Received", log);

    internal void RaisePeerJoined(ulong peer, CatLogger log) => SafeInvoker.Invoke(PeerJoined, peer, ModId + ".PeerJoined", log);

    internal void RaisePeerLeft(ulong peer, CatLogger log) => SafeInvoker.Invoke(PeerLeft, peer, ModId + ".PeerLeft", log);

    private static byte[] Encode(string text) => Encoding.UTF8.GetBytes(text ?? string.Empty);

    private static string Check(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("Message name must not be empty.", nameof(name));
        }

        if (Encoding.UTF8.GetByteCount(name) > MessageCodec.MaxModNameBytes)
        {
            throw new ArgumentException($"Message name must not exceed {MessageCodec.MaxModNameBytes} bytes.", nameof(name));
        }

        return name;
    }

    private static byte[] CheckData(byte[] data)
    {
        data ??= Array.Empty<byte>();
        if (data.Length > MessageCodec.MaxModDataBytes)
        {
            throw new ArgumentException($"Message data must not exceed {MessageCodec.MaxModDataBytes} bytes.", nameof(data));
        }

        return (byte[])data.Clone();
    }
}
