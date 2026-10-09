using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CatLib.CrashWatcher.Symbols;

internal sealed class GameMethodMap
{
    public const string FileName = "MethodAddressToToken.db";
    public const uint Magic = 0x4D544D55;
    public const int SupportedVersion = 1;

    private readonly string _directory;
    private readonly Dictionary<int, ManagedMetadata> _metadata = new Dictionary<int, ManagedMetadata>();
    private long[] _rvas;
    private int[] _tokens;
    private int[] _assemblyIndexes;
    private string[] _assemblies;

    private GameMethodMap(string directory)
    {
        _directory = directory;
    }

    public int Count => _rvas.Length;

    public static GameMethodMap Open(string interopDirectory, out string problem)
    {
        problem = null;
        var path = Path.Combine(interopDirectory ?? string.Empty, FileName);
        if (!File.Exists(path))
        {
            problem = $"{path} is missing";
            return null;
        }

        try
        {
            var map = new GameMethodMap(interopDirectory);
            problem = map.Load(File.ReadAllBytes(path));
            return problem == null ? map : null;
        }
        catch (Exception exception)
        {
            problem = $"{path} could not be read: {exception.Message}";
            return null;
        }
    }

    public bool TryFloor(uint rva, out uint start)
    {
        start = 0;
        var index = Floor(rva);
        if (index < 0)
        {
            return false;
        }

        start = (uint)_rvas[index];
        return true;
    }

    public bool Contains(uint rva)
    {
        var index = Floor(rva);
        return index >= 0 && _rvas[index] == rva;
    }

    public uint Sample(int index) => (uint)_rvas[index];

    public string NameAt(uint rva, out int sharedWith)
    {
        sharedWith = 0;
        var last = Floor(rva);
        if (last < 0 || _rvas[last] != rva)
        {
            return null;
        }

        var first = last;
        while (first > 0 && _rvas[first - 1] == rva)
        {
            first--;
        }

        for (var index = first; index <= last; index++)
        {
            var name = Resolve(_tokens[index], _assemblyIndexes[index]);
            if (!string.IsNullOrEmpty(name))
            {
                sharedWith = last - first;
                return name;
            }
        }

        return null;
    }

    private int Floor(uint rva)
    {
        var index = Array.BinarySearch(_rvas, (long)rva);
        if (index >= 0)
        {
            while (index + 1 < _rvas.Length && _rvas[index + 1] == rva)
            {
                index++;
            }

            return index;
        }

        return ~index - 1;
    }

    private string Load(byte[] bytes)
    {
        using (var reader = new BinaryReader(new MemoryStream(bytes), Encoding.UTF8))
        {
            if (reader.ReadUInt32() != Magic)
            {
                return "the method map has an unknown format";
            }

            var version = reader.ReadInt32();
            if (version != SupportedVersion)
            {
                return $"the method map has version {version}, this crash watcher reads version {SupportedVersion}";
            }

            var assemblyCount = reader.ReadInt32();
            var methodCount = reader.ReadInt32();
            var dataOffset = reader.ReadInt32();
            if (assemblyCount < 0 || assemblyCount > bytes.Length || methodCount < 0 || dataOffset < 0 || (long)dataOffset + methodCount * 16L > bytes.Length)
            {
                return "the method map is damaged";
            }

            _assemblies = new string[assemblyCount];
            for (var index = 0; index < assemblyCount; index++)
            {
                _assemblies[index] = reader.ReadString();
            }

            reader.BaseStream.Position = dataOffset;
            _rvas = new long[methodCount];
            for (var index = 0; index < methodCount; index++)
            {
                _rvas[index] = reader.ReadInt64();
            }

            _tokens = new int[methodCount];
            _assemblyIndexes = new int[methodCount];
            for (var index = 0; index < methodCount; index++)
            {
                _tokens[index] = reader.ReadInt32();
                _assemblyIndexes[index] = reader.ReadInt32();
            }
        }

        for (var index = 1; index < _rvas.Length; index++)
        {
            if (_rvas[index] < _rvas[index - 1])
            {
                var order = new int[_rvas.Length];
                for (var position = 0; position < order.Length; position++)
                {
                    order[position] = position;
                }

                Array.Sort((long[])_rvas.Clone(), order);
                var rvas = new long[order.Length];
                var tokens = new int[order.Length];
                var assemblies = new int[order.Length];
                for (var position = 0; position < order.Length; position++)
                {
                    rvas[position] = _rvas[order[position]];
                    tokens[position] = _tokens[order[position]];
                    assemblies[position] = _assemblyIndexes[order[position]];
                }

                _rvas = rvas;
                _tokens = tokens;
                _assemblyIndexes = assemblies;
                break;
            }
        }

        return null;
    }

    private string Resolve(int token, int assembly)
    {
        if (assembly < 0 || assembly >= _assemblies.Length)
        {
            return null;
        }

        if (!_metadata.TryGetValue(assembly, out var metadata))
        {
            metadata = LoadAssembly(_assemblies[assembly]);
            _metadata[assembly] = metadata;
        }

        return metadata?.MethodName(token);
    }

    private ManagedMetadata LoadAssembly(string fullName)
    {
        try
        {
            return ReadAssembly(fullName);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private ManagedMetadata ReadAssembly(string fullName)
    {
        var simple = fullName.Split(',')[0].Trim();
        var path = Path.Combine(_directory, simple + ".dll");
        if (!File.Exists(path))
        {
            path = null;
            foreach (var candidate in Directory.GetFiles(_directory, "*.dll"))
            {
                if (Path.GetFileNameWithoutExtension(candidate).EndsWith(simple, StringComparison.OrdinalIgnoreCase))
                {
                    path = candidate;
                    break;
                }
            }
        }

        using (var image = PeImage.Open(path))
        {
            return image == null ? null : ManagedMetadata.Read(image.ReadClrMetadata());
        }
    }
}
