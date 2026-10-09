using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CatLib.CrashWatcher.Symbols;

internal sealed class Il2CppCodeNames
{
    public const string AnchorModule = "mscorlib.dll";
    public const int CodeRegistrationSize = 136;
    public const int MetadataRegistrationTypeSizesOffset = 96;
    public const int MaxBacktrack = 400;
    public const ulong CountLimit = 0xC0000;
    public const int MaxDepth = 12;
    public const string FullySharedPrefix = "__Il2CppFullySharedGeneric";

    private const int Void = 0x01;
    private const int Pointer = 0x0F;
    private const int ValueType = 0x11;
    private const int Class = 0x12;
    private const int Var = 0x13;
    private const int Array = 0x14;
    private const int GenericInstance = 0x15;
    private const int SingleArray = 0x1D;
    private const int MethodVar = 0x1E;

    private static readonly Dictionary<int, string> Primitives = new Dictionary<int, string>
    {
        [0x01] = "void", [0x02] = "bool", [0x03] = "char", [0x04] = "sbyte", [0x05] = "byte", [0x06] = "short", [0x07] = "ushort",
        [0x08] = "int", [0x09] = "uint", [0x0A] = "long", [0x0B] = "ulong", [0x0C] = "float", [0x0D] = "double", [0x0E] = "string",
        [0x16] = "TypedReference", [0x18] = "IntPtr", [0x19] = "UIntPtr", [0x1B] = "fnptr", [0x1C] = "object"
    };

    private readonly PeImage _image;
    private readonly Il2CppMetadataFile _metadata;
    private readonly List<KeyValuePair<uint, byte[]>> _data = new List<KeyValuePair<uint, byte[]>>();
    private readonly Dictionary<uint, int> _genericByRva = new Dictionary<uint, int>();
    private readonly Dictionary<uint, int> _genericShared = new Dictionary<uint, int>();
    private readonly Dictionary<uint, int> _methodByRva = new Dictionary<uint, int>();
    private uint[] _genericStarts = new uint[0];
    private uint[] _methodStarts = new uint[0];
    private ulong[] _types;
    private ulong[] _genericInsts;
    private byte[] _methodSpecs;
    private readonly Dictionary<int, string> _typeNames = new Dictionary<int, string>();
    private readonly Dictionary<int, bool> _fullyShared = new Dictionary<int, bool>();

    private Il2CppCodeNames(PeImage image, Il2CppMetadataFile metadata)
    {
        _image = image;
        _metadata = metadata;
    }

    public ulong CodeRegistration { get; private set; }

    public ulong MetadataRegistration { get; private set; }

    public int Version => _metadata.Version;

    public int GenericMethodCount => _genericByRva.Count;

    public int MethodCount => _methodByRva.Count;

    public static string FindMetadata(string gameAssemblyPath)
    {
        var folder = Path.GetDirectoryName(gameAssemblyPath ?? string.Empty);
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
        {
            return null;
        }

        foreach (var data in Directory.GetDirectories(folder, "*_Data"))
        {
            var path = Path.Combine(data, "il2cpp_data", "Metadata", Il2CppMetadataFile.FileName);
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    public static Il2CppCodeNames Open(PeImage gameAssembly, string metadataPath, out string problem)
    {
        var metadata = Il2CppMetadataFile.Open(metadataPath, out problem);
        if (metadata == null)
        {
            return null;
        }

        if (!gameAssembly.IsAmd64)
        {
            problem = "GameAssembly.dll is not a 64-bit image";
            return null;
        }

        var names = new Il2CppCodeNames(gameAssembly, metadata);
        problem = names.Load();
        return problem == null ? names : null;
    }

    public string GenericNameAt(uint rva, out int sharedWith)
    {
        sharedWith = 0;
        if (!_genericByRva.TryGetValue(rva, out var spec))
        {
            return null;
        }

        _genericShared.TryGetValue(rva, out sharedWith);
        return MethodSpecName(spec);
    }

    public string MethodNameAt(uint rva)
    {
        return _methodByRva.TryGetValue(rva, out var method) ? MethodName(method, null, null) : null;
    }

    public bool TryFloorGeneric(uint rva, out uint start) => Floor(_genericStarts, rva, out start);

    public bool TryFloorMethod(uint rva, out uint start) => Floor(_methodStarts, rva, out start);

    private static bool Floor(uint[] starts, uint rva, out uint start)
    {
        start = 0;
        var index = System.Array.BinarySearch(starts, rva);
        if (index < 0)
        {
            index = ~index - 1;
        }

        if (index < 0)
        {
            return false;
        }

        start = starts[index];
        return true;
    }

    private string Load()
    {
        foreach (var section in _image.Sections)
        {
            if (section.IsExecutable || section.RawSize == 0)
            {
                continue;
            }

            var bytes = _image.Read(section.VirtualAddress, (int)Math.Min(section.RawSize, section.VirtualSize == 0 ? section.RawSize : section.VirtualSize));
            if (bytes != null)
            {
                _data.Add(new KeyValuePair<uint, byte[]>(section.VirtualAddress, bytes));
            }
        }

        CodeRegistration = FindCodeRegistration();
        if (CodeRegistration == 0)
        {
            return "the code registration of IL2CPP was not found in GameAssembly.dll";
        }

        MetadataRegistration = FindMetadataRegistration();
        if (MetadataRegistration == 0)
        {
            return "the metadata registration of IL2CPP was not found in GameAssembly.dll";
        }

        var types = ReadWords(MetadataRegistration + 48, 2);
        var insts = ReadWords(MetadataRegistration + 16, 2);
        var specs = ReadWords(MetadataRegistration + 64, 2);
        var table = ReadWords(MetadataRegistration + 32, 2);
        var generic = ReadWords(CodeRegistration + 16, 2);
        if (types == null || insts == null || specs == null || table == null || generic == null)
        {
            return "the IL2CPP registrations could not be read";
        }

        _types = ReadWords(types[1], (int)Math.Min(types[0], CountLimit * 4));
        _genericInsts = ReadWords(insts[1], (int)Math.Min(insts[0], CountLimit * 4));
        _methodSpecs = _image.ReadVirtual(specs[1], (int)Math.Min(specs[0], CountLimit * 4) * 12);
        var tableBytes = _image.ReadVirtual(table[1], (int)Math.Min(table[0], CountLimit * 4) * 16);
        var pointers = ReadWords(generic[1], (int)Math.Min(generic[0], CountLimit * 4));
        if (_types == null || _genericInsts == null || _methodSpecs == null || tableBytes == null || pointers == null)
        {
            return "the IL2CPP generic method tables could not be read";
        }

        for (var at = 0; at + 16 <= tableBytes.Length; at += 16)
        {
            var spec = BitConverter.ToInt32(tableBytes, at);
            var pointerIndex = BitConverter.ToInt32(tableBytes, at + 4);
            if (pointerIndex < 0 || pointerIndex >= pointers.Length || pointers[pointerIndex] < _image.ImageBase)
            {
                continue;
            }

            var rva = (uint)(pointers[pointerIndex] - _image.ImageBase);
            if (_genericByRva.ContainsKey(rva))
            {
                _genericShared.TryGetValue(rva, out var shared);
                _genericShared[rva] = shared + 1;
            }
            else
            {
                _genericByRva[rva] = spec;
            }
        }

        LoadModules();
        _genericStarts = new List<uint>(_genericByRva.Keys).ToArray();
        System.Array.Sort(_genericStarts);
        _methodStarts = new List<uint>(_methodByRva.Keys).ToArray();
        System.Array.Sort(_methodStarts);
        return null;
    }

    private void LoadModules()
    {
        var header = ReadWords(CodeRegistration + 120, 2);
        if (header == null || header[0] > MaxBacktrack)
        {
            return;
        }

        var modules = ReadWords(header[1], (int)header[0]);
        if (modules == null)
        {
            return;
        }

        var images = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < _metadata.ImageCount; index++)
        {
            var image = _metadata.Image(index);
            images[_metadata.String(image.NameIndex)] = index;
        }

        foreach (var module in modules)
        {
            var fields = ReadWords(module, 3);
            if (fields == null || fields[1] > CountLimit * 4)
            {
                continue;
            }

            var name = ReadString(fields[0]);
            if (name == null || !images.TryGetValue(name, out var imageIndex))
            {
                continue;
            }

            var pointers = ReadWords(fields[2], (int)fields[1]);
            if (pointers == null)
            {
                continue;
            }

            var image = _metadata.Image(imageIndex);
            for (var type = image.FirstType; type < image.FirstType + image.TypeCount; type++)
            {
                var definition = _metadata.TypeDefinition(type);
                if (definition == null || definition.FirstMethodIndex < 0)
                {
                    continue;
                }

                for (var method = definition.FirstMethodIndex; method < definition.FirstMethodIndex + definition.MethodCount; method++)
                {
                    var record = _metadata.Method(method);
                    var row = record == null ? -1 : (int)(record.Token & 0xFFFFFF) - 1;
                    if (row < 0 || row >= pointers.Length || pointers[row] < _image.ImageBase)
                    {
                        continue;
                    }

                    var rva = (uint)(pointers[row] - _image.ImageBase);
                    if (!_methodByRva.ContainsKey(rva))
                    {
                        _methodByRva[rva] = method;
                    }
                }
            }
        }
    }

    private ulong FindCodeRegistration()
    {
        var imageCount = _metadata.ImageCount;
        var modules = FindWords(new HashSet<ulong>(FindStrings(AnchorModule)));
        var moduleAddresses = new HashSet<ulong>();
        foreach (var list in modules.Values)
        {
            moduleAddresses.UnionWith(list);
        }

        var slots = new HashSet<ulong>();
        foreach (var list in FindWords(moduleAddresses).Values)
        {
            slots.UnionWith(list);
        }

        var starts = new HashSet<ulong>();
        var limit = Math.Min(MaxBacktrack, imageCount);
        foreach (var slot in slots)
        {
            for (var back = 0; back <= limit; back++)
            {
                starts.Add(slot - (ulong)back * 8);
            }
        }

        var owners = FindWords(starts);
        var found = new List<ulong>();
        foreach (var slot in slots)
        {
            for (var back = 0; back <= limit; back++)
            {
                if (!owners.TryGetValue(slot - (ulong)back * 8, out var places) || places.Count != 1)
                {
                    continue;
                }

                var count = ReadWords(places[0] - 8, 1);
                if (count == null || count[0] == 0 || count[0] > (ulong)imageCount || (ulong)back >= count[0])
                {
                    continue;
                }

                var candidate = places[0] - (CodeRegistrationSize - 8);
                if (!found.Contains(candidate))
                {
                    found.Add(candidate);
                }

                break;
            }
        }

        return found.Count == 1 ? found[0] : 0;
    }

    private ulong FindMetadataRegistration()
    {
        var counts = FindWords(new HashSet<ulong> { (ulong)_metadata.TypeDefinitionCount });
        foreach (var at in counts.TryGetValue((ulong)_metadata.TypeDefinitionCount, out var places) ? places : new List<ulong>())
        {
            var start = at - MetadataRegistrationTypeSizesOffset;
            var fields = ReadWords(start, 16);
            if (fields == null)
            {
                continue;
            }

            var valid = true;
            for (var index = 0; index < 16 && valid; index++)
            {
                if (index % 2 == 0)
                {
                    valid = fields[index] < CountLimit;
                }
                else if (fields[index] == 0)
                {
                    valid = index >= 14 || index == 1;
                }
                else
                {
                    valid = _image.ReadVirtual(fields[index], 1) != null;
                }
            }

            if (valid)
            {
                return start;
            }
        }

        return 0;
    }

    private List<ulong> FindStrings(string text)
    {
        var found = new List<ulong>();
        var pattern = Encoding.ASCII.GetBytes(text + "\0");
        foreach (var section in _data)
        {
            var bytes = section.Value;
            for (var at = 0; at + pattern.Length <= bytes.Length; at++)
            {
                if (bytes[at] != pattern[0])
                {
                    continue;
                }

                var match = true;
                for (var index = 1; index < pattern.Length && match; index++)
                {
                    match = bytes[at + index] == pattern[index];
                }

                if (match && (at == 0 || bytes[at - 1] == 0))
                {
                    found.Add(_image.ImageBase + section.Key + (uint)at);
                }
            }
        }

        return found;
    }

    private Dictionary<ulong, List<ulong>> FindWords(HashSet<ulong> values)
    {
        var found = new Dictionary<ulong, List<ulong>>();
        if (values.Count == 0)
        {
            return found;
        }

        foreach (var section in _data)
        {
            var bytes = section.Value;
            for (var at = 0; at + 8 <= bytes.Length; at += 8)
            {
                var value = BitConverter.ToUInt64(bytes, at);
                if (!values.Contains(value))
                {
                    continue;
                }

                if (!found.TryGetValue(value, out var list))
                {
                    list = new List<ulong>();
                    found[value] = list;
                }

                list.Add(_image.ImageBase + section.Key + (uint)at);
            }
        }

        return found;
    }

    private ulong[] ReadWords(ulong address, int count)
    {
        if (count < 0)
        {
            return null;
        }

        var bytes = _image.ReadVirtual(address, count * 8);
        if (bytes == null)
        {
            return null;
        }

        var words = new ulong[count];
        for (var index = 0; index < count; index++)
        {
            words[index] = BitConverter.ToUInt64(bytes, index * 8);
        }

        return words;
    }

    private string ReadString(ulong address)
    {
        var bytes = _image.ReadVirtual(address, 256);
        if (bytes == null)
        {
            return null;
        }

        var end = System.Array.IndexOf(bytes, (byte)0);
        return end <= 0 ? null : Encoding.UTF8.GetString(bytes, 0, end);
    }

    private string MethodSpecName(int spec)
    {
        if (spec < 0 || (spec + 1) * 12 > _methodSpecs.Length)
        {
            return null;
        }

        var method = BitConverter.ToInt32(_methodSpecs, spec * 12);
        var classInst = BitConverter.ToInt32(_methodSpecs, spec * 12 + 4);
        var methodInst = BitConverter.ToInt32(_methodSpecs, spec * 12 + 8);
        return MethodName(method, InstArguments(classInst), InstArguments(methodInst));
    }

    private List<ulong> InstArguments(int index)
    {
        if (index < 0 || index >= _genericInsts.Length)
        {
            return null;
        }

        return InstArgumentsAt(_genericInsts[index]);
    }

    private List<ulong> InstArgumentsAt(ulong address)
    {
        var inst = ReadWords(address, 2);
        if (inst == null || inst[0] > 64)
        {
            return null;
        }

        var arguments = ReadWords(inst[1], (int)inst[0]);
        return arguments == null ? null : new List<ulong>(arguments);
    }

    private string MethodName(int methodIndex, List<ulong> classArguments, List<ulong> methodArguments)
    {
        var method = _metadata.Method(methodIndex);
        if (method == null)
        {
            return null;
        }

        var context = new GenericContext(classArguments, methodArguments);
        var builder = new StringBuilder();
        builder.Append(TypeDefinitionFullName(method.DeclaringType, classArguments, 0));
        builder.Append('.').Append(_metadata.String(method.NameIndex));
        var parameterNames = method.GenericContainerIndex >= 0 ? _metadata.GenericParameterNames(method.GenericContainerIndex) : null;
        if (methodArguments != null && methodArguments.Count > 0)
        {
            builder.Append('<').Append(string.Join(", ", ArgumentNames(methodArguments, parameterNames, null, 0))).Append('>');
        }
        else if (parameterNames != null && parameterNames.Count > 0)
        {
            builder.Append('<').Append(string.Join(", ", parameterNames)).Append('>');
        }

        var parameters = new List<string>();
        for (var index = 0; index < method.ParameterCount && index < 64; index++)
        {
            var typeIndex = _metadata.ParameterType(method.ParameterStart + index);
            parameters.Add(typeIndex < 0 || typeIndex >= _types.Length ? "?" : TypeName(_types[typeIndex], context, 0));
        }

        builder.Append('(').Append(string.Join(", ", parameters)).Append(')');
        return builder.ToString();
    }

    private List<string> ArgumentNames(List<ulong> arguments, List<string> parameterNames, GenericContext context, int depth)
    {
        var names = new List<string>();
        for (var index = 0; index < arguments.Count; index++)
        {
            names.Add(IsFullyShared(arguments[index]) && parameterNames != null && index < parameterNames.Count
                ? parameterNames[index]
                : TypeName(arguments[index], context, depth));
        }

        return names;
    }

    private List<string> ParameterNamesOf(int definition)
    {
        var record = _metadata.TypeDefinition(definition);
        return record == null || record.GenericContainerIndex < 0 ? null : _metadata.GenericParameterNames(record.GenericContainerIndex);
    }

    private bool IsFullyShared(ulong typeAddress)
    {
        var definition = DefinitionOf(typeAddress);
        if (definition < 0)
        {
            return false;
        }

        if (!_fullyShared.TryGetValue(definition, out var shared))
        {
            var record = _metadata.TypeDefinition(definition);
            shared = record != null && _metadata.String(record.NameIndex).StartsWith(FullySharedPrefix, StringComparison.Ordinal);
            _fullyShared[definition] = shared;
        }

        return shared;
    }

    private string TypeDefinitionFullName(int definition, List<ulong> arguments, int depth) => GenericTypeName(definition, arguments, null, depth, true);

    private string GenericTypeName(int definition, List<ulong> arguments, GenericContext context, int depth, bool full)
    {
        var record = _metadata.TypeDefinition(definition);
        if (record == null)
        {
            return "?";
        }

        arguments = arguments ?? new List<ulong>();
        var parameters = ParameterNamesOf(definition) ?? new List<string>();
        var outer = depth < MaxDepth && record.DeclaringTypeIndex >= 0 && record.DeclaringTypeIndex < _types.Length ? DefinitionOf(_types[record.DeclaringTypeIndex]) : -1;
        var outerParameters = outer >= 0 ? ParameterNamesOf(outer)?.Count ?? 0 : 0;
        var outerCount = arguments.Count > 0 ? Math.Min(arguments.Count, outerParameters) : 0;
        var rawName = _metadata.String(record.NameIndex);
        var name = full ? WithoutArity(rawName) : ShortDefinitionName(definition);
        if (arguments.Count > outerCount)
        {
            var own = arguments.GetRange(outerCount, arguments.Count - outerCount);
            var ownNames = parameters.Count > outerCount ? parameters.GetRange(outerCount, parameters.Count - outerCount) : null;
            name += "<" + string.Join(", ", ArgumentNames(own, ownNames, context, depth + 1)) + ">";
        }
        else if (arguments.Count == 0 && full && rawName.IndexOf('`') >= 0 && parameters.Count > outerParameters)
        {
            name += "<" + string.Join(", ", parameters.GetRange(outerParameters, parameters.Count - outerParameters)) + ">";
        }

        if (outer >= 0 && (full || outerCount > 0))
        {
            return GenericTypeName(outer, outerCount > 0 ? arguments.GetRange(0, outerCount) : null, context, depth + 1, full) + "." + name;
        }

        if (!full)
        {
            return name;
        }

        var space = _metadata.String(record.NamespaceIndex);
        return space.Length == 0 ? name : space + "." + name;
    }

    private string ShortDefinitionName(int definition)
    {
        if (_typeNames.TryGetValue(definition, out var cached))
        {
            return cached;
        }

        var record = _metadata.TypeDefinition(definition);
        var name = record == null ? "?" : WithoutArity(_metadata.String(record.NameIndex));
        if (record != null && _metadata.String(record.NamespaceIndex) == "System")
        {
            switch (name)
            {
                case "Object":
                    name = "object";
                    break;
                case "String":
                    name = "string";
                    break;
            }
        }

        _typeNames[definition] = name;
        return name;
    }

    private int DefinitionOf(ulong typeAddress)
    {
        var type = _image.ReadVirtual(typeAddress, 12);
        if (type == null)
        {
            return -1;
        }

        var data = BitConverter.ToUInt64(type, 0);
        var kind = (int)(BitConverter.ToUInt32(type, 8) >> 16 & 0xFF);
        if (kind == Class || kind == ValueType)
        {
            return (int)data;
        }

        if (kind == GenericInstance)
        {
            var generic = ReadWords(data, 1);
            return generic == null ? -1 : DefinitionOf(generic[0]);
        }

        return -1;
    }

    private string TypeName(ulong typeAddress, GenericContext context, int depth)
    {
        if (depth > MaxDepth)
        {
            return "?";
        }

        var type = _image.ReadVirtual(typeAddress, 12);
        if (type == null)
        {
            return "?";
        }

        var data = BitConverter.ToUInt64(type, 0);
        var bits = BitConverter.ToUInt32(type, 8);
        var kind = (int)(bits >> 16 & 0xFF);
        var byReference = (bits >> 29 & 1) != 0 && kind != Void;
        var name = KindName(kind, data, context, depth);
        return byReference ? "ref " + name : name;
    }

    private string KindName(int kind, ulong data, GenericContext context, int depth)
    {
        if (Primitives.TryGetValue(kind, out var primitive))
        {
            return primitive;
        }

        switch (kind)
        {
            case Class:
            case ValueType:
                return ShortDefinitionName((int)data);
            case GenericInstance:
                var generic = ReadWords(data, 2);
                if (generic == null)
                {
                    return "?";
                }

                var definition = DefinitionOf(generic[0]);
                var arguments = InstArgumentsAt(generic[1]) ?? new List<ulong>();
                if (definition < 0)
                {
                    return "?";
                }

                if (ShortDefinitionName(definition) == "Nullable" && arguments.Count == 1)
                {
                    return TypeName(arguments[0], context, depth + 1) + "?";
                }

                return GenericTypeName(definition, arguments, context, depth + 1, false);
            case Var:
            case MethodVar:
                var parameter = _metadata.GenericParameter((int)data);
                var substitutes = kind == Var ? context?.ClassArguments : context?.MethodArguments;
                if (parameter != null && substitutes != null && parameter.Number < substitutes.Count && !IsFullyShared(substitutes[parameter.Number]))
                {
                    return TypeName(substitutes[parameter.Number], null, depth + 1);
                }

                return parameter == null ? "?" : _metadata.String(parameter.NameIndex);
            case SingleArray:
                return TypeName(data, context, depth + 1) + "[]";
            case Pointer:
                return TypeName(data, context, depth + 1) + "*";
            case Array:
                var array = _image.ReadVirtual(data, 9);
                if (array == null)
                {
                    return "?[]";
                }

                return TypeName(BitConverter.ToUInt64(array, 0), context, depth + 1) + "[" + new string(',', Math.Max(0, array[8] - 1)) + "]";
            default:
                return "?";
        }
    }

    private static string WithoutArity(string name)
    {
        var tick = name.IndexOf('`');
        return tick < 0 ? name : name.Substring(0, tick);
    }

    private sealed class GenericContext
    {
        public GenericContext(List<ulong> classArguments, List<ulong> methodArguments)
        {
            ClassArguments = classArguments;
            MethodArguments = methodArguments;
        }

        public List<ulong> ClassArguments { get; }

        public List<ulong> MethodArguments { get; }
    }
}
