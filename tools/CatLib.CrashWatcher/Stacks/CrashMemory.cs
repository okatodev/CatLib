using System;
using System.Collections.Generic;
using System.Text;
using CatLib.Diagnostics;

namespace CatLib.CrashWatcher.Stacks;

internal static class CrashMemory
{
    public const int StackBytes = 128 * 1024;
    public const int PageSize = 4096;
    public const int MaxStrings = 8;
    public const int MaxStringLength = 300;
    public const int MinStringLength = 3;
    public const int MaxDeclaredLength = 1 << 20;
    public const int DotnetLengthOffset = 8;
    public const int DotnetCharsOffset = 12;
    public const int Il2CppLengthOffset = 16;
    public const int Il2CppCharsOffset = 20;
    public const string DotnetMark = ".NET";
    public const string Il2CppMark = "IL2CPP";

    public static List<string> LogLines(IProcessMemory memory, ulong address, int size)
    {
        var lines = new List<string>();
        if (memory == null || address == 0 || size <= 0 || size > CrashLogRing.MaxCapacity)
        {
            return lines;
        }

        var header = new byte[CrashLogRing.HeaderSize];
        if (!memory.TryRead(address, header, header.Length) || !CrashLogRing.TryReadHeader(header, out var written, out var capacity) || capacity != size)
        {
            return lines;
        }

        var data = new byte[capacity];
        if (!memory.TryRead(address + CrashLogRing.HeaderSize, data, capacity))
        {
            return lines;
        }

        return CrashLogRing.Lines(data, written);
    }

    public static List<string> StackStrings(IProcessMemory memory, IList<ulong> registers, ulong stackPointer, ulong dotnetType, ulong il2cppType)
    {
        var found = new List<string>();
        if (memory == null || (dotnetType == 0 && il2cppType == 0))
        {
            return found;
        }

        var seen = new HashSet<ulong>();
        var texts = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in registers ?? new ulong[0])
        {
            Consider(memory, value, dotnetType, il2cppType, seen, texts, found);
        }

        var page = new byte[PageSize];
        var address = stackPointer & ~7UL;
        var end = address + StackBytes;
        while (address < end && found.Count < MaxStrings)
        {
            var length = (int)Math.Min((ulong)PageSize - (address & (PageSize - 1)), end - address);
            if (!memory.TryRead(address, page, length))
            {
                break;
            }

            for (var position = 0; position + 8 <= length && found.Count < MaxStrings; position += 8)
            {
                Consider(memory, BitConverter.ToUInt64(page, position), dotnetType, il2cppType, seen, texts, found);
            }

            address += (ulong)length;
        }

        return found;
    }

    public static bool IsReadable(string text)
    {
        if (string.IsNullOrEmpty(text) || text.Length < MinStringLength)
        {
            return false;
        }

        var letters = 0;
        var bad = 0;
        foreach (var character in text)
        {
            if (char.IsLetterOrDigit(character))
            {
                letters++;
            }
            else if (char.IsControl(character) && character != '\n' && character != '\r' && character != '\t')
            {
                bad++;
            }
            else if (char.IsSurrogate(character) || character == '\uFFFD')
            {
                bad++;
            }
        }

        return bad == 0 && letters * 2 >= text.Length;
    }

    public static string Quote(string mark, string text)
    {
        var flat = (text ?? string.Empty).Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", " ");
        return mark + ": \"" + flat + "\"";
    }

    private static void Consider(IProcessMemory memory, ulong value, ulong dotnetType, ulong il2cppType, HashSet<ulong> seen, HashSet<string> texts, List<string> found)
    {
        if (found.Count >= MaxStrings || value < 0x10000 || (value & 1) != 0 || value > 0x7FFFFFFFFFFFUL)
        {
            return;
        }

        if (dotnetType != 0)
        {
            Try(memory, value, dotnetType, DotnetLengthOffset, DotnetCharsOffset, DotnetMark, seen, texts, found);
            if (value > DotnetCharsOffset)
            {
                Try(memory, value - DotnetCharsOffset, dotnetType, DotnetLengthOffset, DotnetCharsOffset, DotnetMark, seen, texts, found);
            }
        }

        if (il2cppType != 0)
        {
            Try(memory, value, il2cppType, Il2CppLengthOffset, Il2CppCharsOffset, Il2CppMark, seen, texts, found);
            if (value > Il2CppCharsOffset)
            {
                Try(memory, value - Il2CppCharsOffset, il2cppType, Il2CppLengthOffset, Il2CppCharsOffset, Il2CppMark, seen, texts, found);
            }
        }
    }

    private static void Try(IProcessMemory memory, ulong objectAddress, ulong type, int lengthOffset, int charsOffset, string mark,
        HashSet<ulong> seen, HashSet<string> texts, List<string> found)
    {
        if (found.Count >= MaxStrings || (objectAddress & 7) != 0 || !seen.Add(objectAddress ^ type))
        {
            return;
        }

        var header = new byte[charsOffset];
        if (!memory.TryRead(objectAddress, header, header.Length) || BitConverter.ToUInt64(header, 0) != type)
        {
            return;
        }

        var length = BitConverter.ToInt32(header, lengthOffset);
        if (length < MinStringLength || length > MaxDeclaredLength)
        {
            return;
        }

        var shown = Math.Min(length, MaxStringLength);
        var chars = new byte[shown * 2];
        if (!memory.TryRead(objectAddress + (ulong)charsOffset, chars, chars.Length))
        {
            return;
        }

        var text = Encoding.Unicode.GetString(chars);
        if (!IsReadable(text) || !texts.Add(text))
        {
            return;
        }

        found.Add(Quote(mark, length > shown ? text + "..." : text));
    }
}
