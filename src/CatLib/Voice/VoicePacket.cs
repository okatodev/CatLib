using System;

namespace CatLib.Voice;

public readonly struct VoicePacket
{
    public const byte Magic = 0x56;
    public const byte CurrentVersion = 1;
    public const int HeaderSize = 12;
    public const int MaxDataBytes = 8 * 1024;

    public VoicePacket(ulong speaker, ushort sequence, byte[] data)
    {
        Speaker = speaker;
        Sequence = sequence;
        Data = data ?? Array.Empty<byte>();
    }

    public ulong Speaker { get; }

    public ushort Sequence { get; }

    public byte[] Data { get; }

    public static byte[] Write(ulong speaker, ushort sequence, byte[] data, int count)
    {
        if (data == null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        if (count < 0 || count > data.Length || count > MaxDataBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        var bytes = new byte[HeaderSize + count];
        bytes[0] = Magic;
        bytes[1] = CurrentVersion;
        for (var index = 0; index < 8; index++)
        {
            bytes[2 + index] = (byte)(speaker >> (8 * index));
        }

        bytes[10] = (byte)sequence;
        bytes[11] = (byte)(sequence >> 8);
        Buffer.BlockCopy(data, 0, bytes, HeaderSize, count);
        return bytes;
    }

    public static byte[] WithSpeaker(byte[] bytes, ulong speaker)
    {
        if (!TryRead(bytes, out var packet))
        {
            return null;
        }

        return Write(speaker, packet.Sequence, packet.Data, packet.Data.Length);
    }

    public static bool TryRead(byte[] bytes, out VoicePacket packet)
    {
        packet = default;
        if (bytes == null || bytes.Length < HeaderSize || bytes.Length > HeaderSize + MaxDataBytes || bytes[0] != Magic || bytes[1] != CurrentVersion)
        {
            return false;
        }

        ulong speaker = 0;
        for (var index = 0; index < 8; index++)
        {
            speaker |= (ulong)bytes[2 + index] << (8 * index);
        }

        var sequence = (ushort)(bytes[10] | (bytes[11] << 8));
        var data = new byte[bytes.Length - HeaderSize];
        Buffer.BlockCopy(bytes, HeaderSize, data, 0, data.Length);
        packet = new VoicePacket(speaker, sequence, data);
        return true;
    }

    public static bool IsNewer(ushort sequence, ushort last) => (short)(sequence - last) > 0;
}
