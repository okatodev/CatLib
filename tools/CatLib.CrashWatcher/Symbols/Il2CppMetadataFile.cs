using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CatLib.CrashWatcher.Symbols;

internal sealed class Il2CppMetadataFile
{
    public const uint Magic = 0xFAB11BAF;
    public const int OldestVersion = 29;
    public const int NewestVersion = 106;
    public const string FileName = "global-metadata.dat";

    private const int Strings = 2;
    private const int Methods = 5;
    private const int Parameters = 10;
    private const int Fields = 11;
    private const int GenericParameters = 12;
    private const int GenericContainers = 14;
    private const int NestedTypes = 15;
    private const int InterfaceOffsets = 18;
    private const int TypeDefinitions = 19;
    private const int Images = 21;
    private const int Events = 3;
    private const int Properties = 4;
    private const int DefaultValueData = 8;

    private static readonly int[] SectionsBeforeImages = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19 };

    private readonly byte[] _data;
    private readonly int[] _offset = new int[32];
    private readonly int[] _size = new int[32];
    private readonly int[] _count = new int[32];
    private int _typeWidth;
    private int _typeDefinitionWidth;
    private int _genericContainerWidth;
    private int _parameterWidth;
    private int _interfaceOffsetWidth;
    private int _eventWidth;
    private int _propertyWidth;
    private int _nestedTypeWidth;
    private int _methodWidth;
    private int _genericParameterWidth;
    private int _fieldWidth;

    private Il2CppMetadataFile(byte[] data)
    {
        _data = data;
    }

    public int Version { get; private set; }

    public int TypeDefinitionCount { get; private set; }

    public int MethodCount { get; private set; }

    public int ImageCount { get; private set; }

    public int TypeDefinitionSize { get; private set; }

    public int MethodSize { get; private set; }

    public int ParameterSize { get; private set; }

    public int GenericContainerSize { get; private set; }

    public int GenericParameterSize { get; private set; }

    public int ImageSize { get; private set; }

    public static Il2CppMetadataFile Open(string path, out string problem)
    {
        problem = null;
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            problem = $"{path} is missing";
            return null;
        }

        try
        {
            var metadata = new Il2CppMetadataFile(File.ReadAllBytes(path));
            problem = metadata.ReadHeader();
            return problem == null ? metadata : null;
        }
        catch (Exception exception)
        {
            problem = $"{path} could not be read: {exception.Message}";
            return null;
        }
    }

    public string String(int index)
    {
        if (index < 0 || index >= _size[Strings])
        {
            return string.Empty;
        }

        var start = _offset[Strings] + index;
        var end = start;
        var limit = _offset[Strings] + _size[Strings];
        while (end < limit && _data[end] != 0)
        {
            end++;
        }

        return Encoding.UTF8.GetString(_data, start, end - start);
    }

    public TypeDefinitionRecord TypeDefinition(int index)
    {
        if (index < 0 || index >= TypeDefinitionCount)
        {
            return null;
        }

        var reader = new RecordReader(this, _offset[TypeDefinitions] + index * TypeDefinitionSize);
        var record = new TypeDefinitionRecord
        {
            NameIndex = reader.Int32(),
            NamespaceIndex = reader.Int32()
        };
        record.ByvalTypeIndex = reader.Index(_typeWidth);
        record.DeclaringTypeIndex = reader.Index(_typeWidth);
        record.ParentIndex = reader.Index(_typeWidth);
        if (Version < 35)
        {
            reader.Int32();
        }

        record.GenericContainerIndex = reader.Index(_genericContainerWidth);
        record.Flags = reader.UInt32();
        reader.Index(_fieldWidth);
        record.FirstMethodIndex = reader.Index(_methodWidth);
        reader.Index(_eventWidth);
        reader.Index(_propertyWidth);
        reader.Index(_nestedTypeWidth);
        reader.Index(_interfaceOffsetWidth);
        reader.Int32();
        reader.Index(_interfaceOffsetWidth);
        record.MethodCount = reader.UInt16();
        return record;
    }

    public MethodRecord Method(int index)
    {
        if (index < 0 || index >= MethodCount)
        {
            return null;
        }

        var reader = new RecordReader(this, _offset[Methods] + index * MethodSize);
        var record = new MethodRecord { NameIndex = reader.Int32() };
        record.DeclaringType = reader.Index(_typeDefinitionWidth);
        record.ReturnType = reader.Index(_typeWidth);
        if (Version >= 31)
        {
            reader.UInt32();
        }

        record.ParameterStart = reader.Index(_parameterWidth);
        record.GenericContainerIndex = reader.Index(_genericContainerWidth);
        record.Token = reader.UInt32();
        reader.UInt16();
        reader.UInt16();
        reader.UInt16();
        record.ParameterCount = reader.UInt16();
        return record;
    }

    public int ParameterType(int index)
    {
        if (index < 0 || index >= _size[Parameters] / ParameterSize)
        {
            return -1;
        }

        var reader = new RecordReader(this, _offset[Parameters] + index * ParameterSize);
        reader.Int32();
        reader.UInt32();
        return reader.Index(_typeWidth);
    }

    public List<string> GenericParameterNames(int containerIndex)
    {
        var names = new List<string>();
        if (containerIndex < 0 || containerIndex >= _size[GenericContainers] / GenericContainerSize)
        {
            return names;
        }

        var reader = new RecordReader(this, _offset[GenericContainers] + containerIndex * GenericContainerSize);
        reader.Int32();
        int count;
        if (Version >= 106)
        {
            count = reader.UInt16();
            reader.Byte();
        }
        else
        {
            count = reader.Int32();
            reader.Int32();
        }

        var start = reader.Index(_genericParameterWidth);
        for (var index = 0; index < count && index < 64; index++)
        {
            names.Add(GenericParameterName(start + index));
        }

        return names;
    }

    public string GenericParameterName(int index)
    {
        var record = GenericParameter(index);
        return record == null ? "T" + index : String(record.NameIndex);
    }

    public GenericParameterRecord GenericParameter(int index)
    {
        if (index < 0 || index >= _size[GenericParameters] / GenericParameterSize)
        {
            return null;
        }

        var reader = new RecordReader(this, _offset[GenericParameters] + index * GenericParameterSize);
        var record = new GenericParameterRecord { Owner = reader.Index(_genericContainerWidth), NameIndex = reader.Int32() };
        reader.Int16();
        reader.Int16();
        record.Number = reader.UInt16();
        return record;
    }

    public ImageRecord Image(int index)
    {
        if (index < 0 || index >= ImageCount)
        {
            return null;
        }

        var reader = new RecordReader(this, _offset[Images] + index * ImageSize);
        var record = new ImageRecord { NameIndex = reader.Int32() };
        reader.Int32();
        record.FirstType = reader.Index(_typeDefinitionWidth);
        record.TypeCount = (int)reader.UInt32();
        return record;
    }

    private static int Width(int count) => count <= 0xFF ? 1 : count <= 0xFFFF ? 2 : 4;

    private string ReadHeader()
    {
        if (_data.Length < 16 || BitConverter.ToUInt32(_data, 0) != Magic)
        {
            return "global-metadata.dat has an unknown format";
        }

        Version = BitConverter.ToInt32(_data, 4);
        if (Version < OldestVersion || Version > NewestVersion)
        {
            return $"global-metadata.dat has version {Version}, the crash watcher reads versions {OldestVersion} to {NewestVersion}";
        }

        var withCount = Version >= 38;
        var headerSize = withCount ? 12 : 8;
        var at = 8;
        var order = new List<int>(SectionsBeforeImages);
        if (Version >= 104)
        {
            order.Add(-1);
        }

        order.Add(Images);
        order.Add(Images + 1);
        foreach (var section in order)
        {
            if (at + headerSize > _data.Length)
            {
                return "global-metadata.dat is cut short";
            }

            if (section >= 0)
            {
                _offset[section] = BitConverter.ToInt32(_data, at);
                _size[section] = BitConverter.ToInt32(_data, at + 4);
                _count[section] = withCount ? BitConverter.ToInt32(_data, at + 8) : 0;
                if (_offset[section] < 0 || _size[section] < 0 || (long)_offset[section] + _size[section] > _data.Length)
                {
                    return $"global-metadata.dat has a section outside the file at header offset {at}";
                }
            }

            at += headerSize;
        }

        var legacy = Version < 38;
        _typeWidth = legacy ? 4 : _size[InterfaceOffsets] / Math.Max(1, _count[InterfaceOffsets]) - 4;
        _typeDefinitionWidth = legacy ? 4 : Width(_count[TypeDefinitions]);
        _genericContainerWidth = legacy ? 4 : Width(_count[GenericContainers]);
        _parameterWidth = Version >= 39 ? Width(_count[Parameters]) : 4;
        _interfaceOffsetWidth = Version >= 104 ? Width(_count[InterfaceOffsets]) : 4;
        _eventWidth = Version >= 104 ? Width(_count[Events]) : 4;
        _propertyWidth = Version >= 104 ? Width(_count[Properties]) : 4;
        _nestedTypeWidth = Version >= 104 ? Width(_count[NestedTypes]) : 4;
        _methodWidth = Version >= 105 ? Width(_count[Methods]) : 4;
        _genericParameterWidth = Version >= 106 ? Width(_count[GenericParameters]) : 4;
        _fieldWidth = Version >= 106 ? Width(_count[Fields]) : 4;
        if (_typeWidth != 1 && _typeWidth != 2 && _typeWidth != 4)
        {
            return $"global-metadata.dat gives a type index width of {_typeWidth}";
        }

        TypeDefinitionSize = 4 + 4 + _typeWidth * 3 + (Version < 35 ? 4 : 0) + _genericContainerWidth + 4 + _fieldWidth + _methodWidth + _eventWidth + _propertyWidth +
                             _nestedTypeWidth + _interfaceOffsetWidth + 4 + _interfaceOffsetWidth + 2 * 8 + 4 + 4;
        MethodSize = 4 + _typeDefinitionWidth + _typeWidth + (Version >= 31 ? 4 : 0) + _parameterWidth + _genericContainerWidth + 4 + 2 * 4;
        ParameterSize = 4 + 4 + _typeWidth;
        GenericContainerSize = 4 + (Version >= 106 ? 3 : 8) + _genericParameterWidth;
        GenericParameterSize = _genericContainerWidth + 4 + 2 + 2 + 2 + 2;
        ImageSize = 4 + 4 + _typeDefinitionWidth + 4 + _typeDefinitionWidth + 4 + _methodWidth + 4 + 4 + 4;

        TypeDefinitionCount = _size[TypeDefinitions] / TypeDefinitionSize;
        MethodCount = _size[Methods] / MethodSize;
        ImageCount = _size[Images] / ImageSize;
        if (withCount && (TypeDefinitionCount != _count[TypeDefinitions] || MethodCount != _count[Methods] || ImageCount != _count[Images]))
        {
            return $"global-metadata.dat version {Version} does not match the record sizes the crash watcher expects " +
                   $"(type definitions {TypeDefinitionCount} of {_count[TypeDefinitions]}, methods {MethodCount} of {_count[Methods]}, images {ImageCount} of {_count[Images]})";
        }

        if (!withCount && (_size[TypeDefinitions] % TypeDefinitionSize != 0 || _size[Methods] % MethodSize != 0 || _size[Images] % ImageSize != 0))
        {
            return $"global-metadata.dat version {Version} does not match the record sizes the crash watcher expects";
        }

        return null;
    }

    internal sealed class TypeDefinitionRecord
    {
        public int NameIndex { get; set; }

        public int NamespaceIndex { get; set; }

        public int ByvalTypeIndex { get; set; }

        public int DeclaringTypeIndex { get; set; }

        public int ParentIndex { get; set; }

        public int GenericContainerIndex { get; set; }

        public uint Flags { get; set; }

        public int FirstMethodIndex { get; set; }

        public int MethodCount { get; set; }
    }

    internal sealed class MethodRecord
    {
        public int NameIndex { get; set; }

        public int DeclaringType { get; set; }

        public int ReturnType { get; set; }

        public int ParameterStart { get; set; }

        public int GenericContainerIndex { get; set; }

        public uint Token { get; set; }

        public int ParameterCount { get; set; }
    }

    internal sealed class GenericParameterRecord
    {
        public int Owner { get; set; }

        public int NameIndex { get; set; }

        public int Number { get; set; }
    }

    internal sealed class ImageRecord
    {
        public int NameIndex { get; set; }

        public int FirstType { get; set; }

        public int TypeCount { get; set; }
    }

    private struct RecordReader
    {
        private readonly Il2CppMetadataFile _owner;
        private int _at;

        public RecordReader(Il2CppMetadataFile owner, int at)
        {
            _owner = owner;
            _at = at;
        }

        public int Int32()
        {
            var value = BitConverter.ToInt32(_owner._data, _at);
            _at += 4;
            return value;
        }

        public uint UInt32()
        {
            var value = BitConverter.ToUInt32(_owner._data, _at);
            _at += 4;
            return value;
        }

        public int UInt16()
        {
            var value = BitConverter.ToUInt16(_owner._data, _at);
            _at += 2;
            return value;
        }

        public int Int16()
        {
            var value = BitConverter.ToInt16(_owner._data, _at);
            _at += 2;
            return value;
        }

        public int Byte() => _owner._data[_at++];

        public int Index(int width)
        {
            switch (width)
            {
                case 1:
                    var small = _owner._data[_at++];
                    return small == 0xFF ? -1 : small;
                case 2:
                    var medium = BitConverter.ToUInt16(_owner._data, _at);
                    _at += 2;
                    return medium == 0xFFFF ? -1 : medium;
                default:
                    return Int32();
            }
        }
    }
}
