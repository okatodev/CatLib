using System;
using System.Text;

namespace CatLib.Tests.Suites.Crash;

internal static class SyntheticImage
{
    public static byte[] Build(string pdbName = null, Guid guid = default, int age = 1)
    {
        var file = new byte[0x800];
        file[0] = (byte)'M';
        file[1] = (byte)'Z';
        Put32(file, 0x3C, 0x40);
        file[0x40] = (byte)'P';
        file[0x41] = (byte)'E';
        Put16(file, 0x44, 0x8664);
        Put16(file, 0x46, 2);
        Put16(file, 0x54, 240);
        Put16(file, 0x58, 0x20B);
        Put32(file, 0x58 + 56, 0x3000);
        Put32(file, 0x58 + 60, 0x400);
        Put32(file, 0x58 + 108, 16);
        Put32(file, 0x58 + 112 + 3 * 8, 0x2000);
        Put32(file, 0x58 + 112 + 3 * 8 + 4, 4 * 12);
        Section(file, 0x148, ".text", 0x1000, 0x400, 0x60000020);
        Section(file, 0x170, ".rdata", 0x2000, 0x600, 0x40000040);

        Code(file, 0x1000, 0x53, 0x48, 0x83, 0xEC, 0x20);
        Code(file, 0x100B, 0xE8, 0, 0, 0, 0);
        Code(file, 0x104B, 0xE8, 0, 0, 0, 0);
        Code(file, 0x1030, 0x48, 0x83, 0xC4, 0x20, 0x5B, 0xC3);

        Function(file, 0, 0x1000, 0x1040, 0x2040);
        Function(file, 1, 0x1040, 0x1080, 0x2048);
        Function(file, 2, 0x1080, 0x10A0, 0x2058);
        Function(file, 3, 0x10A0, 0x10C0, 0x2060);
        Data(file, 0x2040, 0x01, 5, 2, 0x00, 0x05, 0x32, 0x01, 0x30);
        Data(file, 0x2048, 0x01, 15, 5, 0x25, 0x0F, 0x64, 0x06, 0x00, 0x0A, 0x03, 0x05, 0x72, 0x01, 0x50);
        Data(file, 0x2058, 0x01, 4, 1, 0x00, 0x04, 0x12);
        Data(file, 0x2060, 0x21, 0, 0, 0x00);
        Put32(file, 0x600 + 0x64, 0x1080);
        Put32(file, 0x600 + 0x68, 0x10A0);
        Put32(file, 0x600 + 0x6C, 0x2058);
        if (pdbName != null)
        {
            Put32(file, 0x58 + 112 + 6 * 8, 0x2100);
            Put32(file, 0x58 + 112 + 6 * 8 + 4, 28);
            var record = new byte[24 + pdbName.Length + 1];
            Encoding.ASCII.GetBytes("RSDS").CopyTo(record, 0);
            guid.ToByteArray().CopyTo(record, 4);
            BitConverter.GetBytes(age).CopyTo(record, 20);
            Encoding.ASCII.GetBytes(pdbName).CopyTo(record, 24);
            Put32(file, 0x700 + 12, 2);
            Put32(file, 0x700 + 16, record.Length);
            Put32(file, 0x700 + 20, 0x2120);
            Put32(file, 0x700 + 24, 0x720);
            record.CopyTo(file, 0x720);
        }

        return file;
    }

    private static void Section(byte[] file, int at, string name, int rva, int raw, uint characteristics)
    {
        Encoding.ASCII.GetBytes(name).CopyTo(file, at);
        Put32(file, at + 8, 0x200);
        Put32(file, at + 12, rva);
        Put32(file, at + 16, 0x200);
        Put32(file, at + 20, raw);
        Put32(file, at + 36, unchecked((int)characteristics));
    }

    private static void Function(byte[] file, int index, int begin, int end, int unwind)
    {
        var at = 0x600 + index * 12;
        Put32(file, at, begin);
        Put32(file, at + 4, end);
        Put32(file, at + 8, unwind);
    }

    private static void Code(byte[] file, int rva, params byte[] bytes) => bytes.CopyTo(file, 0x400 + rva - 0x1000);

    private static void Data(byte[] file, int rva, params byte[] bytes) => bytes.CopyTo(file, 0x600 + rva - 0x2000);

    private static void Put16(byte[] file, int at, int value) => BitConverter.GetBytes((ushort)value).CopyTo(file, at);

    private static void Put32(byte[] file, int at, int value) => BitConverter.GetBytes(value).CopyTo(file, at);
}
