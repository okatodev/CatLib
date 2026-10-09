using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CatLib.CrashWatcher.Symbols;

internal sealed class PeImage : IDisposable
{
    public const ushort Amd64 = 0x8664;
    public const int ExportDirectory = 0;
    public const int ExceptionDirectory = 3;
    public const int DebugDirectory = 6;
    public const int ClrDirectory = 14;
    public const uint CodeViewDebugType = 2;
    public const uint CodeViewSignature = 0x53445352;
    public const int MaxChain = 32;
    public const byte ChainInfoFlag = 0x04;
    public const uint ExecutableSection = 0x20000000;

    private readonly FileStream _file;
    private readonly List<PeSection> _sections = new List<PeSection>();
    private readonly uint[] _directoryRva = new uint[16];
    private readonly uint[] _directorySize = new uint[16];
    private uint[] _begins;
    private uint[] _ends;
    private uint[] _unwinds;
    private uint[] _exportRvas;
    private string[] _exportNames;

    private PeImage(string path, FileStream file)
    {
        Path = path;
        _file = file;
    }

    public string Path { get; }

    public ushort Machine { get; private set; }

    public uint SizeOfImage { get; private set; }

    public uint TimeDateStamp { get; private set; }

    public ulong ImageBase { get; private set; }

    public IReadOnlyList<PeSection> Sections => _sections;

    public bool IsAmd64 => Machine == Amd64;

    public static PeImage Open(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return null;
        }

        var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.RandomAccess);
        try
        {
            var image = new PeImage(path, file);
            if (image.ReadHeaders())
            {
                return image;
            }
        }
        catch (IOException)
        {
        }
        catch (ArgumentException)
        {
        }

        file.Dispose();
        return null;
    }

    public void Dispose() => _file.Dispose();

    public byte[] Read(uint rva, int count)
    {
        if (count <= 0)
        {
            return new byte[0];
        }

        foreach (var section in _sections)
        {
            var size = Math.Max(section.VirtualSize, section.RawSize);
            if (rva < section.VirtualAddress || rva - section.VirtualAddress >= size)
            {
                continue;
            }

            var offset = rva - section.VirtualAddress;
            var buffer = new byte[count];
            var available = section.RawSize > offset ? (int)Math.Min(count, section.RawSize - offset) : 0;
            if (available > 0)
            {
                ReadAt((long)section.RawPointer + offset, buffer, available);
            }

            return buffer;
        }

        return null;
    }

    public string SectionName(uint rva)
    {
        foreach (var section in _sections)
        {
            if (rva >= section.VirtualAddress && rva - section.VirtualAddress < Math.Max(section.VirtualSize, section.RawSize))
            {
                return section.Name;
            }
        }

        return null;
    }

    public bool IsExecutable(uint rva)
    {
        foreach (var section in _sections)
        {
            if (rva >= section.VirtualAddress && rva - section.VirtualAddress < Math.Max(section.VirtualSize, section.RawSize))
            {
                return (section.Characteristics & ExecutableSection) != 0;
            }
        }

        return false;
    }

    public byte[] ReadVirtual(ulong address, int count)
    {
        if (address < ImageBase || address - ImageBase >= SizeOfImage)
        {
            return null;
        }

        return Read((uint)(address - ImageBase), count);
    }

    public bool TryGetCodeView(out string pdbName, out string key)
    {
        pdbName = null;
        key = null;
        if (!TryGetDirectory(DebugDirectory, out var rva, out var size))
        {
            return false;
        }

        var entries = Read(rva, (int)Math.Min(size, 28 * 32));
        if (entries == null)
        {
            return false;
        }

        for (var at = 0; at + 28 <= entries.Length; at += 28)
        {
            if (BitConverter.ToUInt32(entries, at + 12) != CodeViewDebugType)
            {
                continue;
            }

            var length = (int)Math.Min(BitConverter.ToUInt32(entries, at + 16), 1024);
            var record = Read(BitConverter.ToUInt32(entries, at + 20), length);
            if (record == null || length < 25 || BitConverter.ToUInt32(record, 0) != CodeViewSignature)
            {
                continue;
            }

            var guidBytes = new byte[16];
            Array.Copy(record, 4, guidBytes, 0, 16);
            var age = BitConverter.ToUInt32(record, 20);
            var end = Array.IndexOf(record, (byte)0, 24);
            var path = Encoding.UTF8.GetString(record, 24, (end < 0 ? record.Length : end) - 24);
            pdbName = path.Substring(path.LastIndexOfAny(new[] { '\\', '/' }) + 1);
            key = new Guid(guidBytes).ToString("N").ToUpperInvariant() + age.ToString("X", System.Globalization.CultureInfo.InvariantCulture);
            return pdbName.Length > 0;
        }

        return false;
    }

    public bool TryReadUInt32(uint rva, out uint value)
    {
        var bytes = Read(rva, 4);
        value = bytes == null ? 0 : BitConverter.ToUInt32(bytes, 0);
        return bytes != null;
    }

    public bool TryGetDirectory(int index, out uint rva, out uint size)
    {
        rva = _directoryRva[index];
        size = _directorySize[index];
        return rva != 0 && size != 0;
    }

    public bool TryFindFunction(uint rva, out RuntimeFunction function)
    {
        LoadFunctions();
        function = default(RuntimeFunction);
        var index = Array.BinarySearch(_begins, rva);
        if (index < 0)
        {
            index = ~index - 1;
        }

        if (index < 0 || rva >= _ends[index])
        {
            return false;
        }

        function = Resolve(new RuntimeFunction(_begins[index], _ends[index], _unwinds[index]));
        return function.UnwindInfo != 0;
    }

    public uint PrimaryStart(RuntimeFunction function)
    {
        var current = function;
        for (var depth = 0; depth < MaxChain; depth++)
        {
            var header = Read(current.UnwindInfo, 4);
            if (header == null || (header[0] >> 3 & ChainInfoFlag) == 0)
            {
                return current.Begin;
            }

            var chained = ReadChained(current.UnwindInfo, header[2]);
            if (chained.UnwindInfo == 0)
            {
                return current.Begin;
            }

            current = chained;
        }

        return current.Begin;
    }

    public RuntimeFunction ReadChained(uint unwindInfo, int codeCount)
    {
        var at = unwindInfo + 4 + (uint)(((codeCount + 1) & ~1) * 2);
        var bytes = Read(at, 12);
        if (bytes == null)
        {
            return default(RuntimeFunction);
        }

        return Resolve(new RuntimeFunction(BitConverter.ToUInt32(bytes, 0), BitConverter.ToUInt32(bytes, 4), BitConverter.ToUInt32(bytes, 8)));
    }

    public bool HasFunctionStartBetween(uint fromExclusive, uint toInclusive)
    {
        LoadFunctions();
        var index = Array.BinarySearch(_begins, fromExclusive + 1);
        if (index < 0)
        {
            index = ~index;
        }

        return index < _begins.Length && _begins[index] <= toInclusive;
    }

    public bool IsFunctionStart(uint rva)
    {
        LoadFunctions();
        return Array.BinarySearch(_begins, rva) >= 0;
    }

    public bool TryFindExport(uint rva, out string name, out uint start)
    {
        LoadExports();
        name = null;
        start = 0;
        var index = Array.BinarySearch(_exportRvas, rva);
        if (index < 0)
        {
            index = ~index - 1;
        }

        if (index < 0)
        {
            return false;
        }

        name = _exportNames[index];
        start = _exportRvas[index];
        return true;
    }

    public byte[] ReadClrMetadata()
    {
        if (!TryGetDirectory(ClrDirectory, out var clr, out _))
        {
            return null;
        }

        var header = Read(clr, 16);
        if (header == null)
        {
            return null;
        }

        var rva = BitConverter.ToUInt32(header, 8);
        var size = BitConverter.ToUInt32(header, 12);
        return rva == 0 || size == 0 ? null : Read(rva, (int)size);
    }

    private RuntimeFunction Resolve(RuntimeFunction function)
    {
        var current = function;
        for (var depth = 0; depth < MaxChain && (current.UnwindInfo & 1) != 0; depth++)
        {
            var bytes = Read(current.UnwindInfo - 1, 12);
            if (bytes == null)
            {
                return default(RuntimeFunction);
            }

            current = new RuntimeFunction(BitConverter.ToUInt32(bytes, 0), BitConverter.ToUInt32(bytes, 4), BitConverter.ToUInt32(bytes, 8));
        }

        return (current.UnwindInfo & 1) != 0 ? default(RuntimeFunction) : current;
    }

    private bool ReadHeaders()
    {
        var dos = new byte[64];
        if (ReadAt(0, dos, 64) < 64 || dos[0] != 'M' || dos[1] != 'Z')
        {
            return false;
        }

        var peOffset = BitConverter.ToInt32(dos, 0x3C);
        var coff = new byte[24];
        if (peOffset <= 0 || ReadAt(peOffset, coff, 24) < 24 || coff[0] != 'P' || coff[1] != 'E' || coff[2] != 0 || coff[3] != 0)
        {
            return false;
        }

        Machine = BitConverter.ToUInt16(coff, 4);
        var sectionCount = BitConverter.ToUInt16(coff, 6);
        TimeDateStamp = BitConverter.ToUInt32(coff, 8);
        var optionalSize = BitConverter.ToUInt16(coff, 20);
        var optional = new byte[optionalSize];
        if (ReadAt(peOffset + 24, optional, optionalSize) < optionalSize || optionalSize < 2)
        {
            return false;
        }

        var magic = BitConverter.ToUInt16(optional, 0);
        var directoriesAt = magic == 0x20B ? 112 : magic == 0x10B ? 96 : -1;
        if (directoriesAt < 0 || optionalSize < directoriesAt)
        {
            return false;
        }

        SizeOfImage = BitConverter.ToUInt32(optional, 56);
        ImageBase = magic == 0x20B ? BitConverter.ToUInt64(optional, 24) : BitConverter.ToUInt32(optional, 28);
        var directoryCount = Math.Min(16, Math.Min(BitConverter.ToInt32(optional, directoriesAt - 4), (optionalSize - directoriesAt) / 8));
        for (var index = 0; index < directoryCount; index++)
        {
            _directoryRva[index] = BitConverter.ToUInt32(optional, directoriesAt + index * 8);
            _directorySize[index] = BitConverter.ToUInt32(optional, directoriesAt + index * 8 + 4);
        }

        var table = new byte[sectionCount * 40];
        if (ReadAt(peOffset + 24 + optionalSize, table, table.Length) < table.Length)
        {
            return false;
        }

        for (var index = 0; index < sectionCount; index++)
        {
            var at = index * 40;
            var nameLength = Array.IndexOf(table, (byte)0, at, 8) - at;
            var name = Encoding.ASCII.GetString(table, at, nameLength < 0 ? 8 : nameLength);
            _sections.Add(new PeSection(name, BitConverter.ToUInt32(table, at + 12), BitConverter.ToUInt32(table, at + 8),
                BitConverter.ToUInt32(table, at + 20), BitConverter.ToUInt32(table, at + 16), BitConverter.ToUInt32(table, at + 36)));
        }

        return true;
    }

    private void LoadFunctions()
    {
        lock (_sections)
        {
            LoadFunctionsLocked();
        }
    }

    private void LoadFunctionsLocked()
    {
        if (_begins != null)
        {
            return;
        }

        var begins = new List<uint>();
        var ends = new List<uint>();
        var unwinds = new List<uint>();
        if (IsAmd64 && TryGetDirectory(ExceptionDirectory, out var rva, out var size))
        {
            var bytes = Read(rva, (int)size) ?? new byte[0];
            var last = 0u;
            var sorted = true;
            for (var at = 0; at + 12 <= bytes.Length; at += 12)
            {
                var begin = BitConverter.ToUInt32(bytes, at);
                var end = BitConverter.ToUInt32(bytes, at + 4);
                if (begin == 0 && end == 0)
                {
                    continue;
                }

                sorted &= begin >= last;
                last = begin;
                begins.Add(begin);
                ends.Add(end);
                unwinds.Add(BitConverter.ToUInt32(bytes, at + 8));
            }

            if (!sorted)
            {
                var order = new int[begins.Count];
                for (var index = 0; index < order.Length; index++)
                {
                    order[index] = index;
                }

                var keys = begins.ToArray();
                Array.Sort(keys, order);
                begins = new List<uint>(keys);
                var sortedEnds = new List<uint>();
                var sortedUnwinds = new List<uint>();
                foreach (var index in order)
                {
                    sortedEnds.Add(ends[index]);
                    sortedUnwinds.Add(unwinds[index]);
                }

                ends = sortedEnds;
                unwinds = sortedUnwinds;
            }
        }

        _ends = ends.ToArray();
        _unwinds = unwinds.ToArray();
        _begins = begins.ToArray();
    }

    private void LoadExports()
    {
        lock (_sections)
        {
            LoadExportsLocked();
        }
    }

    private void LoadExportsLocked()
    {
        if (_exportRvas != null)
        {
            return;
        }

        var found = new SortedDictionary<uint, string>();
        if (TryGetDirectory(ExportDirectory, out var rva, out var size))
        {
            var directory = Read(rva, 40);
            if (directory != null)
            {
                var functionCount = BitConverter.ToUInt32(directory, 20);
                var nameCount = BitConverter.ToUInt32(directory, 24);
                var functions = Read(BitConverter.ToUInt32(directory, 28), (int)Math.Min(functionCount, 65536) * 4);
                var names = Read(BitConverter.ToUInt32(directory, 32), (int)Math.Min(nameCount, 65536) * 4);
                var ordinals = Read(BitConverter.ToUInt32(directory, 36), (int)Math.Min(nameCount, 65536) * 2);
                if (functions != null && names != null && ordinals != null)
                {
                    for (var index = 0; index < names.Length / 4 && index * 2 + 2 <= ordinals.Length; index++)
                    {
                        var ordinal = BitConverter.ToUInt16(ordinals, index * 2);
                        if (ordinal * 4 + 4 > functions.Length)
                        {
                            continue;
                        }

                        var target = BitConverter.ToUInt32(functions, ordinal * 4);
                        if (target == 0 || (target >= rva && target < rva + size) || found.ContainsKey(target))
                        {
                            continue;
                        }

                        var name = ReadName(BitConverter.ToUInt32(names, index * 4));
                        if (!string.IsNullOrEmpty(name))
                        {
                            found[target] = name;
                        }
                    }
                }
            }
        }

        _exportNames = new string[found.Count];
        var rvas = new uint[found.Count];
        var position = 0;
        foreach (var pair in found)
        {
            rvas[position] = pair.Key;
            _exportNames[position] = pair.Value;
            position++;
        }

        _exportRvas = rvas;
    }

    private string ReadName(uint rva)
    {
        var bytes = Read(rva, 256);
        if (bytes == null)
        {
            return null;
        }

        var length = Array.IndexOf(bytes, (byte)0);
        return Encoding.ASCII.GetString(bytes, 0, length < 0 ? bytes.Length : length);
    }

    private int ReadAt(long offset, byte[] buffer, int count)
    {
        lock (_file)
        {
            _file.Position = offset;
            var total = 0;
            while (total < count)
            {
                var read = _file.Read(buffer, total, count - total);
                if (read <= 0)
                {
                    break;
                }

                total += read;
            }

            return total;
        }
    }
}
