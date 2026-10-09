using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CatLib.CrashWatcher.Symbols;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.Crash;

public sealed class PdbPublicsTest : TestCase
{
    public const int BlockSize = 512;
    public const string PdbName = "Synthetic_player.pdb";

    public override string Suite => "Crash";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var root = Path.Combine(Path.GetTempPath(), "CatLibPdb_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            var guid = new Guid("11223344-5566-7788-99aa-bbccddeeff00");
            var dll = Path.Combine(root, "Synthetic.dll");
            File.WriteAllBytes(dll, SyntheticImage.Build(PdbName, guid, 3));
            var pdb = Path.Combine(root, PdbName);
            File.WriteAllBytes(pdb, Msf(guid));

            using (var image = PeImage.Open(dll))
            {
                Assert.True(image.TryGetCodeView(out var pdbName, out var key), "The image names its PDB");
                Assert.Equal(PdbName, pdbName, "PDB file name from the CodeView record");
                Assert.Equal("11223344556677889900AABBCCDDEEFF".Length + 1, key.Length, "Symbol server key is the GUID and the age");
                Assert.Equal(guid.ToString("N").ToUpperInvariant() + "3", key, "Symbol server key in the layout of symbol servers");

                var publics = PdbPublics.Read(pdb, image, key, out var problem);
                Assert.Null(problem, "The synthetic PDB reads without problems");
                Assert.Equal(3, publics.Count, "Three public symbols");
                Assert.Equal("?Update@Player@@QEAAXXZ", publics.NameAt(0x1000), "A function symbol at the start of the first section");
                Assert.Equal("PlainFunction", publics.NameAt(0x1040), "A plain C name");
                Assert.True(publics.TryFloor(0x2018, out var start, out var name) && start == 0x2010 && name == "SomeData", "Symbols of the second section use its address");

                var other = PdbPublics.Read(pdb, image, Guid.NewGuid().ToString("N").ToUpperInvariant() + "1", out problem);
                Assert.Null(other, "A PDB of another build is refused");
                Assert.True(problem.Contains("another build"), "And the reason says so");
            }

            using (var names = new CodeNames(Path.Combine(root, "no-interop"), null))
            {
                Assert.Equal("Player::Update + 0x10", names.Name(dll, 0x1010), "A frame inside a function gets its PDB name, made readable");
                Assert.Equal("PlainFunction + 0x0", names.Name(dll, 0x1040), "A frame at the start of a function");
                Assert.Equal("Player::Update + 0x40", names.Name(dll, 0x1040, true), "A return address after the last call belongs to the calling function");
            }

            File.WriteAllBytes(pdb, Encoding.ASCII.GetBytes("not a pdb"));
            using (var image = PeImage.Open(dll))
            {
                Assert.Null(PdbPublics.Read(pdb, image, null, out var problem), "A broken PDB is refused");
                Assert.True(problem.Contains("MSF"), "Because it is no MSF file");
            }
        }
        finally
        {
            try
            {
                Directory.Delete(root, true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        yield break;
    }

    private static byte[] Msf(Guid guid)
    {
        var info = new byte[28];
        BitConverter.GetBytes(20000404).CopyTo(info, 0);
        BitConverter.GetBytes(3).CopyTo(info, 8);
        guid.ToByteArray().CopyTo(info, 12);

        var dbi = new byte[64 + 22];
        BitConverter.GetBytes(-1).CopyTo(dbi, 0);
        BitConverter.GetBytes((ushort)5).CopyTo(dbi, 20);
        BitConverter.GetBytes(22).CopyTo(dbi, 48);
        for (var index = 0; index < 11; index++)
        {
            BitConverter.GetBytes((ushort)(index == PdbPublics.SectionHeaderIndex ? 4 : 0xFFFF)).CopyTo(dbi, 64 + index * 2);
        }

        var sections = new byte[80];
        BitConverter.GetBytes(0x1000).CopyTo(sections, 12);
        BitConverter.GetBytes(0x2000).CopyTo(sections, 52);

        var records = new MemoryStream();
        Public(records, 1, 0x0, "?Update@Player@@QEAAXXZ");
        Public(records, 1, 0x40, "PlainFunction");
        Public(records, 2, 0x10, "SomeData");

        var streams = new List<byte[]> { new byte[0], info, new byte[0], dbi, sections, records.ToArray() };
        var blocks = new List<byte[]> { new byte[BlockSize], new byte[BlockSize], new byte[BlockSize] };
        var streamBlocks = new List<List<int>>();
        foreach (var stream in streams)
        {
            var list = new List<int>();
            for (var at = 0; at < stream.Length; at += BlockSize)
            {
                var block = new byte[BlockSize];
                Array.Copy(stream, at, block, 0, Math.Min(BlockSize, stream.Length - at));
                list.Add(blocks.Count);
                blocks.Add(block);
            }

            streamBlocks.Add(list);
        }

        var directory = new MemoryStream();
        var writer = new BinaryWriter(directory);
        writer.Write(streams.Count);
        foreach (var stream in streams)
        {
            writer.Write(stream.Length);
        }

        foreach (var list in streamBlocks)
        {
            foreach (var block in list)
            {
                writer.Write(block);
            }
        }

        var directoryBytes = directory.ToArray();
        var directoryBlock = blocks.Count;
        var directoryData = new byte[BlockSize];
        Array.Copy(directoryBytes, directoryData, directoryBytes.Length);
        blocks.Add(directoryData);
        var mapBlock = blocks.Count;
        var map = new byte[BlockSize];
        BitConverter.GetBytes(directoryBlock).CopyTo(map, 0);
        blocks.Add(map);

        var super = blocks[0];
        Encoding.ASCII.GetBytes(PdbPublics.MsfMagic).CopyTo(super, 0);
        BitConverter.GetBytes(BlockSize).CopyTo(super, 32);
        BitConverter.GetBytes(1).CopyTo(super, 36);
        BitConverter.GetBytes(blocks.Count).CopyTo(super, 40);
        BitConverter.GetBytes(directoryBytes.Length).CopyTo(super, 44);
        BitConverter.GetBytes(mapBlock).CopyTo(super, 52);

        var file = new byte[blocks.Count * BlockSize];
        for (var index = 0; index < blocks.Count; index++)
        {
            blocks[index].CopyTo(file, index * BlockSize);
        }

        return file;
    }

    private static void Public(MemoryStream records, int segment, int offset, string name)
    {
        var nameBytes = Encoding.ASCII.GetBytes(name);
        var length = 2 + 4 + 4 + 2 + nameBytes.Length + 1;
        var padded = (length + 2 + 3) / 4 * 4 - 2;
        var record = new byte[2 + padded];
        BitConverter.GetBytes((ushort)padded).CopyTo(record, 0);
        BitConverter.GetBytes(PdbPublics.PublicSymbol).CopyTo(record, 2);
        BitConverter.GetBytes(2).CopyTo(record, 4);
        BitConverter.GetBytes(offset).CopyTo(record, 8);
        BitConverter.GetBytes((ushort)segment).CopyTo(record, 12);
        nameBytes.CopyTo(record, 14);
        records.Write(record, 0, record.Length);
    }
}
