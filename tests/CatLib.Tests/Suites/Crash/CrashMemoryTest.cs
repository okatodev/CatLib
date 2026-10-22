using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CatLib.CrashWatcher.Stacks;
using CatLib.Diagnostics;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.Crash;

public sealed class CrashMemoryTest : TestCase
{
    private const ulong DotnetType = 0x7FFA00001000;
    private const ulong Il2CppType = 0x1D600002000;
    private const ulong StackPointer = 0x9000000;

    public override string Suite => "Crash";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var memory = new Memory();
        var ring = Ring("[Info   :CatLib] first\n[Info:A] second\n[Warning:CatLib] third, not in the file yet\n");
        memory.Put(0x5000000, ring);
        var lines = CrashMemory.LogLines(memory, 0x5000000, 64);
        Assert.SequenceEqual(new[] { "[Info:A] second", "[Warning:CatLib] third, not in the file yet" }, lines,
            "The log in memory is read from its oldest whole line to the newest; a line cut by the ring is left out");
        Assert.Equal(0, CrashMemory.LogLines(memory, 0x5000000, 128).Count, "A size that does not match the header reads nothing");
        Assert.Equal(0, CrashMemory.LogLines(memory, 0x6000000, 64).Count, "Memory that is gone reads nothing");

        var text = "Before Steam shuts down: session closed";
        memory.Put(0x7000000, DotnetString(text));
        memory.Put(0x7100000, Il2CppString("p_EntityCustomer"));
        memory.Put(0x7200000, DotnetString("\u0001\u0002\u0003binary"));
        memory.Put(0x7300000, DotnetString("ab"));
        var stack = new byte[CrashMemory.PageSize];
        BitConverter.GetBytes(0x7000000UL).CopyTo(stack, 8);
        BitConverter.GetBytes(0x7000000UL + CrashMemory.DotnetCharsOffset).CopyTo(stack, 16);
        BitConverter.GetBytes(0x7200000UL).CopyTo(stack, 24);
        BitConverter.GetBytes(0x7300000UL).CopyTo(stack, 32);
        BitConverter.GetBytes(0x12345UL).CopyTo(stack, 40);
        memory.Put(StackPointer, stack);
        var found = CrashMemory.StackStrings(memory, new ulong[] { 0, 0x7100000 }, StackPointer, DotnetType, Il2CppType);
        Assert.SequenceEqual(new[] { "IL2CPP: \"p_EntityCustomer\"", ".NET: \"" + text + "\"" }, found,
            "Strings in the registers come first, then the stack from the top; a pointer into the text counts once, binary and very short ones are left out");
        Assert.Equal(0, CrashMemory.StackStrings(memory, null, StackPointer, 0, 0).Count, "Without the string types nothing is searched");

        Assert.True(CrashMemory.IsReadable("Steam shuts down"), "Words are text");
        Assert.False(CrashMemory.IsReadable("\u0000\u0000\u0000"), "Zeros are not");
        Assert.False(CrashMemory.IsReadable("@@@@@@!!"), "Nor punctuation alone");
        Assert.Equal(".NET: \"a\\nb\"", CrashMemory.Quote(CrashMemory.DotnetMark, "a\nb"), "Line breaks stay on one line");
        yield break;
    }

    private static byte[] Ring(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        long written = bytes.Length;
        var data = new byte[64];
        for (var index = 0; index < bytes.Length; index++)
        {
            data[index % 64] = bytes[index];
        }

        var ring = new byte[CrashLogRing.HeaderSize + 64];
        BitConverter.GetBytes(CrashLogRing.Magic).CopyTo(ring, CrashLogRing.MagicOffset);
        BitConverter.GetBytes(written).CopyTo(ring, CrashLogRing.WrittenOffset);
        BitConverter.GetBytes(64).CopyTo(ring, CrashLogRing.CapacityOffset);
        data.CopyTo(ring, CrashLogRing.HeaderSize);
        return ring;
    }

    private static byte[] DotnetString(string text)
    {
        var bytes = new byte[CrashMemory.DotnetCharsOffset + text.Length * 2];
        BitConverter.GetBytes(DotnetType).CopyTo(bytes, 0);
        BitConverter.GetBytes(text.Length).CopyTo(bytes, CrashMemory.DotnetLengthOffset);
        Encoding.Unicode.GetBytes(text).CopyTo(bytes, CrashMemory.DotnetCharsOffset);
        return bytes;
    }

    private static byte[] Il2CppString(string text)
    {
        var bytes = new byte[CrashMemory.Il2CppCharsOffset + text.Length * 2];
        BitConverter.GetBytes(Il2CppType).CopyTo(bytes, 0);
        BitConverter.GetBytes(text.Length).CopyTo(bytes, CrashMemory.Il2CppLengthOffset);
        Encoding.Unicode.GetBytes(text).CopyTo(bytes, CrashMemory.Il2CppCharsOffset);
        return bytes;
    }

    private sealed class Memory : IProcessMemory
    {
        private readonly List<KeyValuePair<ulong, byte[]>> _blocks = new();

        public void Put(ulong address, byte[] bytes) => _blocks.Add(new KeyValuePair<ulong, byte[]>(address, bytes));

        public bool TryRead(ulong address, byte[] buffer, int count)
        {
            foreach (var block in _blocks)
            {
                if (address >= block.Key && address + (ulong)count <= block.Key + (ulong)block.Value.Length)
                {
                    Array.Copy(block.Value, (int)(address - block.Key), buffer, 0, count);
                    return true;
                }
            }

            return false;
        }

        public bool IsExecutable(ulong address) => false;
    }
}
