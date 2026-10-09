using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CatLib.CrashWatcher.Symbols;

internal sealed class PdbPublics
{
    public const string MsfMagic = "Microsoft C/C++ MSF 7.00\r\n\u001aDS\0\0\0";
    public const int DbiStream = 3;
    public const int PdbInfoStream = 1;
    public const ushort PublicSymbol = 0x110E;
    public const int SectionHeaderIndex = 5;
    public const int MaxStreamSize = 512 * 1024 * 1024;

    private uint[] _rvas;
    private string[] _names;

    private PdbPublics()
    {
    }

    public int Count => _rvas.Length;

    public static PdbPublics Read(string path, PeImage image, string expectedKey, out string problem)
    {
        problem = null;
        try
        {
            using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.RandomAccess))
            {
                var msf = new MsfReader(file);
                problem = msf.Open();
                if (problem != null)
                {
                    return null;
                }

                var key = msf.Key();
                if (expectedKey != null && key != null && !key.StartsWith(expectedKey.Substring(0, 32), StringComparison.OrdinalIgnoreCase))
                {
                    problem = $"{Path.GetFileName(path)} is for another build ({key}, expected {expectedKey})";
                    return null;
                }

                var publics = new PdbPublics();
                problem = publics.Load(msf, image);
                return problem == null ? publics : null;
            }
        }
        catch (Exception exception)
        {
            problem = $"{Path.GetFileName(path)} could not be read: {exception.Message}";
            return null;
        }
    }

    public string NameAt(uint rva)
    {
        var index = Array.BinarySearch(_rvas, rva);
        return index >= 0 ? _names[index] : null;
    }

    public bool TryFloor(uint rva, out uint start, out string name)
    {
        start = 0;
        name = null;
        var index = Array.BinarySearch(_rvas, rva);
        if (index < 0)
        {
            index = ~index - 1;
        }

        if (index < 0)
        {
            return false;
        }

        start = _rvas[index];
        name = _names[index];
        return true;
    }

    private string Load(MsfReader msf, PeImage image)
    {
        var dbi = msf.Stream(DbiStream);
        if (dbi == null || dbi.Length < 64)
        {
            return "the PDB has no DBI stream";
        }

        var symbolStream = BitConverter.ToUInt16(dbi, 20);
        var optionalAt = 64L + BitConverter.ToInt32(dbi, 24) + BitConverter.ToInt32(dbi, 28) + BitConverter.ToInt32(dbi, 32) +
                         BitConverter.ToInt32(dbi, 36) + BitConverter.ToInt32(dbi, 40) + BitConverter.ToInt32(dbi, 52);
        var optionalSize = BitConverter.ToInt32(dbi, 48);
        var sections = new List<uint>();
        if (optionalAt + (SectionHeaderIndex + 1) * 2 <= dbi.Length && optionalSize >= (SectionHeaderIndex + 1) * 2)
        {
            var headerStream = BitConverter.ToUInt16(dbi, (int)optionalAt + SectionHeaderIndex * 2);
            var headers = headerStream == 0xFFFF ? null : msf.Stream(headerStream);
            if (headers != null)
            {
                for (var at = 0; at + 40 <= headers.Length; at += 40)
                {
                    sections.Add(BitConverter.ToUInt32(headers, at + 12));
                }
            }
        }

        if (sections.Count == 0 && image != null)
        {
            foreach (var section in image.Sections)
            {
                sections.Add(section.VirtualAddress);
            }
        }

        var records = symbolStream == 0xFFFF ? null : msf.Stream(symbolStream);
        if (records == null)
        {
            return "the PDB has no symbol records";
        }

        var found = new SortedDictionary<uint, string>();
        var position = 0;
        while (position + 4 <= records.Length)
        {
            var length = BitConverter.ToUInt16(records, position);
            var kind = BitConverter.ToUInt16(records, position + 2);
            if (length < 2 || position + 2 + length > records.Length)
            {
                break;
            }

            if (kind == PublicSymbol && length >= 12)
            {
                var offset = BitConverter.ToUInt32(records, position + 8);
                var segment = BitConverter.ToUInt16(records, position + 12);
                if (segment >= 1 && segment <= sections.Count)
                {
                    var nameStart = position + 14;
                    var nameEnd = Array.IndexOf(records, (byte)0, nameStart, position + 2 + length - nameStart);
                    var name = Encoding.UTF8.GetString(records, nameStart, (nameEnd < 0 ? position + 2 + length : nameEnd) - nameStart);
                    var rva = sections[segment - 1] + offset;
                    if (!found.ContainsKey(rva) || IsBetterName(name, found[rva]))
                    {
                        found[rva] = name;
                    }
                }
            }

            position += 2 + length;
        }

        if (found.Count == 0)
        {
            return "the PDB has no public symbols";
        }

        _rvas = new uint[found.Count];
        _names = new string[found.Count];
        var index = 0;
        foreach (var pair in found)
        {
            _rvas[index] = pair.Key;
            _names[index] = pair.Value;
            index++;
        }

        return null;
    }

    private static bool IsBetterName(string candidate, string current) =>
        current.StartsWith("__imp_", StringComparison.Ordinal) && !candidate.StartsWith("__imp_", StringComparison.Ordinal);

    private sealed class MsfReader
    {
        private readonly FileStream _file;
        private int _blockSize;
        private uint[] _streamSizes;
        private uint[][] _streamBlocks;

        public MsfReader(FileStream file)
        {
            _file = file;
        }

        public string Open()
        {
            var super = ReadAt(0, 56);
            if (super == null || Encoding.ASCII.GetString(super, 0, 32) != MsfMagic)
            {
                return "the PDB is not an MSF 7.00 file";
            }

            _blockSize = BitConverter.ToInt32(super, 32);
            var directoryBytes = BitConverter.ToInt32(super, 44);
            var blockMap = BitConverter.ToUInt32(super, 52);
            if (_blockSize < 512 || _blockSize > 65536 || directoryBytes <= 0 || directoryBytes > 64 * 1024 * 1024)
            {
                return "the PDB has an unexpected layout";
            }

            var directoryBlockCount = (directoryBytes + _blockSize - 1) / _blockSize;
            var map = ReadAt((long)blockMap * _blockSize, directoryBlockCount * 4);
            if (map == null)
            {
                return "the PDB directory could not be read";
            }

            var directory = new byte[directoryBytes];
            for (var block = 0; block < directoryBlockCount; block++)
            {
                var length = Math.Min(_blockSize, directoryBytes - block * _blockSize);
                var bytes = ReadAt((long)BitConverter.ToUInt32(map, block * 4) * _blockSize, length);
                if (bytes == null)
                {
                    return "the PDB directory could not be read";
                }

                Array.Copy(bytes, 0, directory, block * _blockSize, length);
            }

            var count = BitConverter.ToInt32(directory, 0);
            if (count <= 0 || count > 1 << 20 || 4 + count * 4 > directory.Length)
            {
                return "the PDB directory is damaged";
            }

            _streamSizes = new uint[count];
            _streamBlocks = new uint[count][];
            var at = 4 + count * 4;
            for (var stream = 0; stream < count; stream++)
            {
                var size = BitConverter.ToUInt32(directory, 4 + stream * 4);
                _streamSizes[stream] = size == uint.MaxValue ? 0 : size;
                var blocks = (int)((_streamSizes[stream] + (uint)_blockSize - 1) / (uint)_blockSize);
                if (at + blocks * 4 > directory.Length)
                {
                    return "the PDB directory is damaged";
                }

                _streamBlocks[stream] = new uint[blocks];
                for (var block = 0; block < blocks; block++)
                {
                    _streamBlocks[stream][block] = BitConverter.ToUInt32(directory, at + block * 4);
                }

                at += blocks * 4;
            }

            return null;
        }

        public string Key()
        {
            var info = Stream(PdbInfoStream);
            if (info == null || info.Length < 28)
            {
                return null;
            }

            var guid = new byte[16];
            Array.Copy(info, 12, guid, 0, 16);
            return new Guid(guid).ToString("N").ToUpperInvariant();
        }

        public byte[] Stream(int index)
        {
            if (index < 0 || index >= _streamSizes.Length || _streamSizes[index] == 0 || _streamSizes[index] > MaxStreamSize)
            {
                return null;
            }

            var size = (int)_streamSizes[index];
            var result = new byte[size];
            var blocks = _streamBlocks[index];
            for (var block = 0; block < blocks.Length; block++)
            {
                var length = Math.Min(_blockSize, size - block * _blockSize);
                var bytes = ReadAt((long)blocks[block] * _blockSize, length);
                if (bytes == null)
                {
                    return null;
                }

                Array.Copy(bytes, 0, result, block * _blockSize, length);
            }

            return result;
        }

        private byte[] ReadAt(long offset, int count)
        {
            if (offset < 0 || offset + count > _file.Length)
            {
                return null;
            }

            var buffer = new byte[count];
            _file.Position = offset;
            var total = 0;
            while (total < count)
            {
                var read = _file.Read(buffer, total, count - total);
                if (read <= 0)
                {
                    return null;
                }

                total += read;
            }

            return buffer;
        }
    }
}
