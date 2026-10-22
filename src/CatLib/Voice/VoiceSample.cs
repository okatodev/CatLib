using System;
using System.Collections.Generic;
using System.IO;

namespace CatLib.Voice;

public sealed class VoiceSample
{
    public const uint Magic = 0x31535643;
    public const int MaxPackets = 100000;

    private readonly List<(double At, byte[] Data)> _packets = new();
    private int _next;

    public IReadOnlyList<(double At, byte[] Data)> Packets => _packets;

    public int Count => _packets.Count;

    public double Duration => _packets.Count == 0 ? 0 : _packets[_packets.Count - 1].At;

    public bool IsFinished => _next >= _packets.Count;

    public void Add(double at, byte[] data)
    {
        if (data == null || data.Length == 0 || _packets.Count >= MaxPackets)
        {
            return;
        }

        var last = _packets.Count == 0 ? 0 : _packets[_packets.Count - 1].At;
        _packets.Add((Math.Max(last, at), (byte[])data.Clone()));
    }

    public void Rewind() => _next = 0;

    public IReadOnlyList<byte[]> Due(double elapsed)
    {
        var due = new List<byte[]>();
        while (_next < _packets.Count && _packets[_next].At <= elapsed)
        {
            due.Add(_packets[_next].Data);
            _next++;
        }

        return due;
    }

    public byte[] ToBytes()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(Magic);
        writer.Write(_packets.Count);
        foreach (var (at, data) in _packets)
        {
            writer.Write(at);
            writer.Write(data.Length);
            writer.Write(data);
        }

        writer.Flush();
        return stream.ToArray();
    }

    public static VoiceSample FromBytes(byte[] bytes)
    {
        if (bytes == null)
        {
            throw new InvalidDataException("No voice sample data.");
        }

        using var reader = new BinaryReader(new MemoryStream(bytes));
        if (bytes.Length < 8 || reader.ReadUInt32() != Magic)
        {
            throw new InvalidDataException("This is not a CatLib voice sample.");
        }

        var count = reader.ReadInt32();
        if (count < 0 || count > MaxPackets)
        {
            throw new InvalidDataException($"A voice sample cannot hold {count} packets.");
        }

        var sample = new VoiceSample();
        for (var index = 0; index < count; index++)
        {
            var at = reader.ReadDouble();
            var length = reader.ReadInt32();
            if (length <= 0 || length > VoicePacket.MaxDataBytes || reader.BaseStream.Position + length > bytes.Length)
            {
                throw new InvalidDataException($"Packet {index} of the voice sample is damaged.");
            }

            sample.Add(at, reader.ReadBytes(length));
        }

        return sample;
    }
}
