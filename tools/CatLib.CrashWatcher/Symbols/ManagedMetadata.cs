using System;
using System.Collections.Generic;
using System.Text;

namespace CatLib.CrashWatcher.Symbols;

internal sealed class ManagedMetadata
{
    public const uint Signature = 0x424A5342;
    public const int MethodDefTable = 0x06;
    public const int TypeRefTable = 0x01;
    public const int TypeDefTable = 0x02;
    public const int TypeSpecTable = 0x1B;
    public const int NestedClassTable = 0x29;
    public const int GenericParamTable = 0x2A;
    public const int MemberRefTable = 0x0A;
    public const int CustomAttributeTable = 0x0C;
    public const string OriginalNameAttribute = "ObfuscatedNameAttribute";
    public const int MaxDepth = 16;

    private const int U16 = -1;
    private const int U32 = -2;
    private const int Str = -3;
    private const int Guid = -4;
    private const int Blob = -5;
    private const int Coded = 100;

    private static readonly int[][] CodedTables =
    {
        new[] { 0x02, 0x01, 0x1B },
        new[] { 0x04, 0x08, 0x17 },
        new[] { 0x06, 0x04, 0x01, 0x02, 0x08, 0x09, 0x0A, 0x00, 0x0E, 0x17, 0x14, 0x11, 0x1A, 0x1B, 0x20, 0x23, 0x26, 0x27, 0x28, 0x2A, 0x2C, 0x2B },
        new[] { 0x04, 0x08 },
        new[] { 0x02, 0x06, 0x20 },
        new[] { 0x02, 0x01, 0x1A, 0x06, 0x1B },
        new[] { 0x14, 0x17 },
        new[] { 0x06, 0x0A },
        new[] { 0x04, 0x06 },
        new[] { 0x26, 0x23, 0x27 },
        new[] { -1, -1, 0x06, 0x0A, -1 },
        new[] { 0x00, 0x1A, 0x23, 0x01 },
        new[] { 0x02, 0x06 }
    };

    private static readonly int[][] Schema =
    {
        new[] { U16, Str, Guid, Guid, Guid },
        new[] { Coded + 11, Str, Str },
        new[] { U32, Str, Str, Coded + 0, 0x04, 0x06 },
        new[] { 0x04 },
        new[] { U16, Str, Blob },
        new[] { 0x06 },
        new[] { U32, U16, U16, Str, Blob, 0x08 },
        new[] { 0x08 },
        new[] { U16, U16, Str },
        new[] { 0x02, Coded + 0 },
        new[] { Coded + 5, Str, Blob },
        new[] { U16, Coded + 1, Blob },
        new[] { Coded + 2, Coded + 10, Blob },
        new[] { Coded + 3, Blob },
        new[] { U16, Coded + 4, Blob },
        new[] { U16, U32, 0x02 },
        new[] { U32, 0x04 },
        new[] { Blob },
        new[] { 0x02, 0x14 },
        new[] { 0x14 },
        new[] { U16, Str, Coded + 0 },
        new[] { 0x02, 0x17 },
        new[] { 0x17 },
        new[] { U16, Str, Blob },
        new[] { U16, 0x06, Coded + 6 },
        new[] { 0x02, Coded + 7, Coded + 7 },
        new[] { Str },
        new[] { Blob },
        new[] { U16, Coded + 8, Str, 0x1A },
        new[] { U32, 0x04 },
        new[] { U32, U32 },
        new[] { U32 },
        new[] { U32, U16, U16, U16, U16, U32, Blob, Str, Str },
        new[] { U32 },
        new[] { U32, U32, U32 },
        new[] { U16, U16, U16, U16, U32, Blob, Str, Str, Blob },
        new[] { U32, 0x23 },
        new[] { U32, U32, U32, 0x23 },
        new[] { U32, Str, Blob },
        new[] { U32, U32, Str, Str, Coded + 9 },
        new[] { U32, U32, Str, Coded + 9 },
        new[] { 0x02, 0x02 },
        new[] { U16, U16, Coded + 12, Str },
        new[] { Coded + 7, Blob },
        new[] { 0x2A, Coded + 0 }
    };

    private static readonly Dictionary<string, string> Aliases = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["System.Void"] = "void",
        ["System.Boolean"] = "bool",
        ["System.Char"] = "char",
        ["System.SByte"] = "sbyte",
        ["System.Byte"] = "byte",
        ["System.Int16"] = "short",
        ["System.UInt16"] = "ushort",
        ["System.Int32"] = "int",
        ["System.UInt32"] = "uint",
        ["System.Int64"] = "long",
        ["System.UInt64"] = "ulong",
        ["System.Single"] = "float",
        ["System.Double"] = "double",
        ["System.String"] = "string",
        ["System.Object"] = "object",
        ["System.IntPtr"] = "IntPtr",
        ["System.UIntPtr"] = "UIntPtr"
    };

    private static readonly string[] Primitives =
    {
        null, "void", "bool", "char", "sbyte", "byte", "short", "ushort", "int", "uint", "long", "ulong", "float", "double", "string"
    };

    private readonly byte[] _data;
    private readonly int[] _rows = new int[64];
    private readonly int[] _tableStart = new int[64];
    private readonly int[] _rowSize = new int[64];
    private readonly int[][] _columnOffset = new int[64][];
    private readonly int[][] _columnSize = new int[64][];
    private int _strings;
    private int _stringsSize;
    private int _blob;
    private int _blobSize;
    private int _stringIndexSize;
    private int _guidIndexSize;
    private int _blobIndexSize;
    private Dictionary<int, int> _enclosing;
    private Dictionary<long, List<string>> _genericNames;
    private Dictionary<long, string> _originalNames;

    private ManagedMetadata(byte[] data)
    {
        _data = data;
    }

    public static ManagedMetadata Read(byte[] metadata)
    {
        if (metadata == null || metadata.Length < 32 || BitConverter.ToUInt32(metadata, 0) != Signature)
        {
            return null;
        }

        var reader = new ManagedMetadata(metadata);
        return reader.ReadStreams() ? reader : null;
    }

    public int MethodCount => _rows[MethodDefTable];

    public string MethodName(int token)
    {
        if (token >> 24 != MethodDefTable)
        {
            return null;
        }

        var method = token & 0xFFFFFF;
        if (method < 1 || method > _rows[MethodDefTable])
        {
            return null;
        }

        var type = DeclaringType(method);
        var name = OriginalName(0, method) ?? String(Value(MethodDefTable, method, 3));
        var builder = new StringBuilder();
        if (type > 0)
        {
            builder.Append(TypeDefFullName(type, 0)).Append('.');
        }

        builder.Append(name);
        var generic = GenericNames(1, method);
        if (generic.Count > 0)
        {
            builder.Append('<').Append(string.Join(", ", generic)).Append('>');
        }

        builder.Append('(').Append(Parameters(Value(MethodDefTable, method, 4), type, method)).Append(')');
        return builder.ToString();
    }

    private bool ReadStreams()
    {
        var versionLength = BitConverter.ToInt32(_data, 12);
        var at = 16 + ((versionLength + 3) & ~3);
        if (at + 4 > _data.Length)
        {
            return false;
        }

        var streamCount = BitConverter.ToUInt16(_data, at + 2);
        at += 4;
        var tables = -1;
        for (var index = 0; index < streamCount && at + 8 < _data.Length; index++)
        {
            var offset = BitConverter.ToInt32(_data, at);
            var size = BitConverter.ToInt32(_data, at + 4);
            at += 8;
            var nameStart = at;
            while (at < _data.Length && _data[at] != 0)
            {
                at++;
            }

            var name = Encoding.ASCII.GetString(_data, nameStart, at - nameStart);
            at = nameStart + ((at - nameStart + 4) & ~3);
            switch (name)
            {
                case "#~":
                    tables = offset;
                    break;
                case "#Strings":
                    _strings = offset;
                    _stringsSize = size;
                    break;
                case "#Blob":
                    _blob = offset;
                    _blobSize = size;
                    break;
            }
        }

        return tables >= 0 && ReadTables(tables);
    }

    private bool ReadTables(int at)
    {
        var heapSizes = _data[at + 6];
        var valid = BitConverter.ToUInt64(_data, at + 8);
        _stringIndexSize = (heapSizes & 1) != 0 ? 4 : 2;
        _guidIndexSize = (heapSizes & 2) != 0 ? 4 : 2;
        _blobIndexSize = (heapSizes & 4) != 0 ? 4 : 2;
        var position = at + 24;
        for (var table = 0; table < 64; table++)
        {
            if ((valid >> table & 1) == 0)
            {
                continue;
            }

            if (table >= Schema.Length)
            {
                return false;
            }

            _rows[table] = BitConverter.ToInt32(_data, position);
            position += 4;
        }

        if ((heapSizes & 0x40) != 0)
        {
            position += 4;
        }

        for (var table = 0; table < Schema.Length; table++)
        {
            var columns = Schema[table];
            _columnOffset[table] = new int[columns.Length];
            _columnSize[table] = new int[columns.Length];
            var size = 0;
            for (var column = 0; column < columns.Length; column++)
            {
                _columnOffset[table][column] = size;
                _columnSize[table][column] = ColumnSize(columns[column]);
                size += _columnSize[table][column];
            }

            _rowSize[table] = size;
            _tableStart[table] = position;
            position += size * _rows[table];
        }

        return position <= _data.Length;
    }

    private int ColumnSize(int kind)
    {
        switch (kind)
        {
            case U16:
                return 2;
            case U32:
                return 4;
            case Str:
                return _stringIndexSize;
            case Guid:
                return _guidIndexSize;
            case Blob:
                return _blobIndexSize;
        }

        if (kind >= Coded)
        {
            var tables = CodedTables[kind - Coded];
            var bits = 0;
            while (1 << bits < tables.Length)
            {
                bits++;
            }

            var largest = 0;
            foreach (var table in tables)
            {
                if (table >= 0)
                {
                    largest = Math.Max(largest, _rows[table]);
                }
            }

            return largest < 1 << (16 - bits) ? 2 : 4;
        }

        return _rows[kind] < 65536 ? 2 : 4;
    }

    private int Value(int table, int row, int column)
    {
        var at = _tableStart[table] + (row - 1) * _rowSize[table] + _columnOffset[table][column];
        return _columnSize[table][column] == 2 ? BitConverter.ToUInt16(_data, at) : BitConverter.ToInt32(_data, at);
    }

    private string String(int index)
    {
        if (index <= 0 || index >= _stringsSize)
        {
            return string.Empty;
        }

        var start = _strings + index;
        var end = start;
        while (end < _strings + _stringsSize && _data[end] != 0)
        {
            end++;
        }

        return Encoding.UTF8.GetString(_data, start, end - start);
    }

    private int DeclaringType(int method)
    {
        var low = 1;
        var high = _rows[TypeDefTable];
        var found = 0;
        while (low <= high)
        {
            var middle = (low + high) / 2;
            if (Value(TypeDefTable, middle, 5) <= method)
            {
                found = middle;
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        return found;
    }

    private string TypeDefFullName(int type, int depth)
    {
        var original = OriginalName(3, type);
        if (original != null)
        {
            return original.Replace('+', '.');
        }

        var name = GenericDisplayName(String(Value(TypeDefTable, type, 1)), GenericNames(0, type));
        if (depth < MaxDepth && Enclosing().TryGetValue(type, out var outer))
        {
            return TypeDefFullName(outer, depth + 1) + "." + name;
        }

        var space = OriginalNamespace(String(Value(TypeDefTable, type, 2)));
        return space.Length == 0 ? name : space + "." + name;
    }

    private Dictionary<int, int> Enclosing()
    {
        if (_enclosing == null)
        {
            _enclosing = new Dictionary<int, int>();
            for (var row = 1; row <= _rows[NestedClassTable]; row++)
            {
                _enclosing[Value(NestedClassTable, row, 0)] = Value(NestedClassTable, row, 1);
            }
        }

        return _enclosing;
    }

    private List<string> GenericNames(int ownerTag, int owner)
    {
        if (_genericNames == null)
        {
            _genericNames = new Dictionary<long, List<string>>();
            var sorted = new Dictionary<long, SortedDictionary<int, string>>();
            for (var row = 1; row <= _rows[GenericParamTable]; row++)
            {
                var coded = Value(GenericParamTable, row, 2);
                var key = Key(coded & 1, coded >> 1);
                if (!sorted.TryGetValue(key, out var names))
                {
                    names = new SortedDictionary<int, string>();
                    sorted[key] = names;
                }

                names[Value(GenericParamTable, row, 0)] = String(Value(GenericParamTable, row, 3));
            }

            foreach (var pair in sorted)
            {
                _genericNames[pair.Key] = new List<string>(pair.Value.Values);
            }
        }

        return _genericNames.TryGetValue(Key(ownerTag, owner), out var found) ? found : new List<string>();
    }

    private string OriginalName(int parentTag, int row)
    {
        if (_originalNames == null)
        {
            _originalNames = new Dictionary<long, string>();
            var references = new HashSet<int>();
            for (var reference = 1; reference <= _rows[TypeRefTable]; reference++)
            {
                if (String(Value(TypeRefTable, reference, 1)) != OriginalNameAttribute)
                {
                    continue;
                }

                for (var member = 1; member <= _rows[MemberRefTable]; member++)
                {
                    if (Value(MemberRefTable, member, 0) == (reference << 3 | 1))
                    {
                        references.Add(member << 3 | 3);
                    }
                }
            }

            for (var attribute = 1; references.Count > 0 && attribute <= _rows[CustomAttributeTable]; attribute++)
            {
                if (!references.Contains(Value(CustomAttributeTable, attribute, 1)))
                {
                    continue;
                }

                var parent = Value(CustomAttributeTable, attribute, 0);
                var text = AttributeString(Value(CustomAttributeTable, attribute, 2));
                if (!string.IsNullOrEmpty(text))
                {
                    _originalNames[Key(parent & 31, parent >> 5)] = text;
                }
            }
        }

        return _originalNames.TryGetValue(Key(parentTag, row), out var name) ? name : null;
    }

    private string AttributeString(int blobIndex)
    {
        try
        {
            var at = _blob + blobIndex;
            var length = ReadCompressed(ref at);
            if (length < 3 || _data[at] != 1 || _data[at + 1] != 0 || _data[at + 2] == 0xFF)
            {
                return null;
            }

            at += 2;
            var textLength = ReadCompressed(ref at);
            return at + textLength <= _data.Length ? Encoding.UTF8.GetString(_data, at, textLength) : null;
        }
        catch (IndexOutOfRangeException)
        {
            return null;
        }
    }

    private int ReadCompressed(ref int at)
    {
        var first = _data[at++];
        if ((first & 0x80) == 0)
        {
            return first;
        }

        if ((first & 0xC0) == 0x80)
        {
            return ((first & 0x3F) << 8) | _data[at++];
        }

        var value = ((first & 0x1F) << 24) | (_data[at] << 16) | (_data[at + 1] << 8) | _data[at + 2];
        at += 3;
        return value;
    }

    private static long Key(int tag, int row) => ((long)tag << 32) | (uint)row;

    private string Parameters(int signature, int type, int method)
    {
        var reader = new SignatureReader(this, signature, type, method);
        try
        {
            var convention = reader.Byte();
            if ((convention & 0x10) != 0)
            {
                reader.Compressed();
            }

            var count = reader.Compressed();
            reader.Type();
            var parts = new List<string>();
            for (var index = 0; index < count && index < 64; index++)
            {
                parts.Add(reader.Type());
            }

            return string.Join(", ", parts);
        }
        catch (IndexOutOfRangeException)
        {
            return "?";
        }
        catch (ArgumentException)
        {
            return "?";
        }
    }

    private static string OriginalNamespace(string space)
    {
        foreach (var prefix in new[] { "Il2CppSystem", "Il2CppMono", "Il2CppMicrosoft" })
        {
            if (space == prefix || space.StartsWith(prefix + ".", StringComparison.Ordinal))
            {
                return space.Substring("Il2Cpp".Length);
            }
        }

        return space;
    }

    private static string WithoutArity(string name)
    {
        var tick = name.IndexOf('`');
        return tick < 0 ? name : name.Substring(0, tick);
    }

    private static string GenericDisplayName(string name, List<string> parameters)
    {
        var plain = WithoutArity(name);
        return parameters.Count == 0 || plain == name ? plain : plain + "<" + string.Join(", ", parameters) + ">";
    }

    private string ShortTypeName(int coded)
    {
        var tag = coded & 3;
        var row = coded >> 2;
        string space;
        string name;
        if (tag == 0 && row >= 1 && row <= _rows[TypeDefTable])
        {
            space = Enclosing().ContainsKey(row) ? string.Empty : String(Value(TypeDefTable, row, 2));
            name = String(Value(TypeDefTable, row, 1));
        }
        else if (tag == 1 && row >= 1 && row <= _rows[TypeRefTable])
        {
            var scope = Value(TypeRefTable, row, 0);
            space = (scope & 3) == 3 ? string.Empty : String(Value(TypeRefTable, row, 2));
            name = String(Value(TypeRefTable, row, 1));
        }
        else
        {
            return "?";
        }

        var full = OriginalNamespace(space) + "." + name;
        return Aliases.TryGetValue(full, out var alias) ? alias : name;
    }

    private sealed class SignatureReader
    {
        private readonly ManagedMetadata _owner;
        private readonly int _type;
        private readonly int _method;
        private readonly int _end;
        private int _at;
        private int _depth;

        public SignatureReader(ManagedMetadata owner, int blobIndex, int type, int method)
        {
            _owner = owner;
            _type = type;
            _method = method;
            _at = owner._blob + blobIndex;
            var length = Compressed();
            _end = Math.Min(_at + length, owner._blob + owner._blobSize);
        }

        public byte Byte()
        {
            if (_at >= _end)
            {
                throw new IndexOutOfRangeException();
            }

            return _owner._data[_at++];
        }

        public int Compressed() => _owner.ReadCompressed(ref _at);

        public string Type()
        {
            if (++_depth > 64)
            {
                throw new ArgumentException("Signature too deep");
            }

            try
            {
                var element = Byte();
                if (element >= 0x01 && element <= 0x0E)
                {
                    return Primitives[element];
                }

                switch (element)
                {
                    case 0x0F:
                        return Type() + "*";
                    case 0x10:
                        return "ref " + Type();
                    case 0x11:
                    case 0x12:
                        return Named(Compressed(), null);
                    case 0x13:
                        return Generic(0, _type, Compressed());
                    case 0x14:
                        return ArrayType();
                    case 0x15:
                        Byte();
                        var definition = Compressed();
                        var count = Compressed();
                        var arguments = new List<string>();
                        for (var index = 0; index < count && index < 32; index++)
                        {
                            arguments.Add(Type());
                        }

                        return Named(definition, arguments);
                    case 0x16:
                        return "TypedReference";
                    case 0x18:
                        return "IntPtr";
                    case 0x19:
                        return "UIntPtr";
                    case 0x1B:
                        SkipMethodSignature();
                        return "fnptr";
                    case 0x1C:
                        return "object";
                    case 0x1D:
                        return Type() + "[]";
                    case 0x1E:
                        return Generic(1, _method, Compressed());
                    case 0x1F:
                    case 0x20:
                        Compressed();
                        return Type();
                    case 0x41:
                    case 0x45:
                        return Type();
                    default:
                        throw new ArgumentException("Unknown element type");
                }
            }
            finally
            {
                _depth--;
            }
        }

        private string Generic(int tag, int owner, int number)
        {
            var names = _owner.GenericNames(tag, owner);
            return number < names.Count ? names[number] : (tag == 0 ? "!" : "!!") + number;
        }

        private string ArrayType()
        {
            var element = Type();
            var rank = Compressed();
            var sizes = Compressed();
            for (var index = 0; index < sizes; index++)
            {
                Compressed();
            }

            var bounds = Compressed();
            for (var index = 0; index < bounds; index++)
            {
                Compressed();
            }

            return element + "[" + new string(',', Math.Max(0, rank - 1)) + "]";
        }

        private void SkipMethodSignature()
        {
            var convention = Byte();
            if ((convention & 0x10) != 0)
            {
                Compressed();
            }

            var count = Compressed();
            Type();
            for (var index = 0; index < count; index++)
            {
                Type();
            }
        }

        private string Named(int coded, List<string> arguments)
        {
            if ((coded & 3) == 2)
            {
                return "?";
            }

            var name = _owner.ShortTypeName(coded);
            var plain = WithoutArity(name);
            if (arguments == null || arguments.Count == 0)
            {
                return name == "Il2CppStringArray" ? "string[]" : plain;
            }

            switch (plain)
            {
                case "Il2CppStructArray":
                case "Il2CppReferenceArray":
                case "Il2CppArrayBase":
                    return arguments[0] + "[]";
                case "Nullable":
                    return arguments[0] + "?";
            }

            return plain + "<" + string.Join(", ", arguments) + ">";
        }
    }
}
