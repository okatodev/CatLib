using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace CatLib.Net;

public sealed class WireWriter
{
    private readonly MemoryStream _stream = new();
    private readonly byte[] _buffer = new byte[8];

    public void WriteByte(byte value) => _stream.WriteByte(value);

    public void WriteBool(bool value) => _stream.WriteByte(value ? (byte)1 : (byte)0);

    public void WriteUInt16(ushort value)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(_buffer, value);
        _stream.Write(_buffer, 0, 2);
    }

    public void WriteUInt32(uint value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(_buffer, value);
        _stream.Write(_buffer, 0, 4);
    }

    public void WriteCount(int count)
    {
        if (count < 0 || count > MessageCodec.MaxItems)
        {
            throw new WireFormatException($"Item count {count} exceeds the limit of {MessageCodec.MaxItems}");
        }

        WriteUInt16((ushort)count);
    }

    public void WriteString(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        if (bytes.Length > MessageCodec.MaxStringBytes)
        {
            throw new WireFormatException($"String of {bytes.Length} bytes exceeds the limit of {MessageCodec.MaxStringBytes}");
        }

        WriteBool(value != null);
        WriteUInt16((ushort)bytes.Length);
        _stream.Write(bytes, 0, bytes.Length);
    }

    public byte[] ToArray()
    {
        if (_stream.Length > MessageCodec.MaxMessageBytes)
        {
            throw new WireFormatException($"Message of {_stream.Length} bytes exceeds the limit of {MessageCodec.MaxMessageBytes}");
        }

        return _stream.ToArray();
    }
}
