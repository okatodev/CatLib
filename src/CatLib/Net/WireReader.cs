using System;
using System.Buffers.Binary;
using System.Text;

namespace CatLib.Net;

public sealed class WireReader
{
    private readonly byte[] _data;
    private int _position;

    public WireReader(byte[] data, int offset)
    {
        _data = data ?? throw new WireFormatException("Message is null");
        if (_data.Length > MessageCodec.MaxMessageBytes)
        {
            throw new WireFormatException($"Message of {_data.Length} bytes exceeds the limit of {MessageCodec.MaxMessageBytes}");
        }

        _position = offset;
    }

    public int Remaining => _data.Length - _position;

    public byte ReadByte()
    {
        Require(1);
        return _data[_position++];
    }

    public bool ReadBool()
    {
        var value = ReadByte();
        if (value > 1)
        {
            throw new WireFormatException($"Invalid boolean value {value} at offset {_position - 1}");
        }

        return value == 1;
    }

    public ushort ReadUInt16()
    {
        Require(2);
        var value = BinaryPrimitives.ReadUInt16LittleEndian(_data.AsSpan(_position, 2));
        _position += 2;
        return value;
    }

    public uint ReadUInt32()
    {
        Require(4);
        var value = BinaryPrimitives.ReadUInt32LittleEndian(_data.AsSpan(_position, 4));
        _position += 4;
        return value;
    }

    public int ReadCount()
    {
        var count = ReadUInt16();
        if (count > MessageCodec.MaxItems)
        {
            throw new WireFormatException($"Item count {count} exceeds the limit of {MessageCodec.MaxItems}");
        }

        return count;
    }

    public string ReadString()
    {
        var present = ReadBool();
        var length = ReadUInt16();
        if (length > MessageCodec.MaxStringBytes)
        {
            throw new WireFormatException($"String of {length} bytes exceeds the limit of {MessageCodec.MaxStringBytes}");
        }

        Require(length);
        var value = Encoding.UTF8.GetString(_data, _position, length);
        _position += length;
        return present ? value : null;
    }

    public T ReadEnum<T>(T min, T max) where T : struct, Enum
    {
        var raw = ReadByte();
        var minValue = Convert.ToByte(min);
        var maxValue = Convert.ToByte(max);
        if (raw < minValue || raw > maxValue)
        {
            throw new WireFormatException($"Value {raw} is not a valid {typeof(T).Name}");
        }

        return (T)Enum.ToObject(typeof(T), raw);
    }

    public void EnsureEnd()
    {
        if (Remaining != 0)
        {
            throw new WireFormatException($"{Remaining} unexpected trailing bytes");
        }
    }

    private void Require(int count)
    {
        if (count < 0 || _position + count > _data.Length)
        {
            throw new WireFormatException($"Message ended early: needed {count} bytes at offset {_position}, {Remaining} left");
        }
    }
}
