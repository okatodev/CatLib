using System;
using System.Collections.Generic;
using System.Text;

namespace CatLib.Diagnostics;

internal static class CrashLogRing
{
    public const ulong Magic = 0x3130474F4C544143UL;
    public const int HeaderSize = 24;
    public const int MagicOffset = 0;
    public const int WrittenOffset = 8;
    public const int CapacityOffset = 16;
    public const int DefaultCapacity = 64 * 1024;
    public const int MaxCapacity = 4 * 1024 * 1024;

    public static bool TryReadHeader(byte[] header, out long written, out int capacity)
    {
        written = 0;
        capacity = 0;
        if (header == null || header.Length < HeaderSize || BitConverter.ToUInt64(header, MagicOffset) != Magic)
        {
            return false;
        }

        written = BitConverter.ToInt64(header, WrittenOffset);
        capacity = BitConverter.ToInt32(header, CapacityOffset);
        return written >= 0 && capacity > 0 && capacity <= MaxCapacity;
    }

    public static byte[] Unroll(byte[] data, long written)
    {
        if (data == null || data.Length == 0 || written <= 0)
        {
            return new byte[0];
        }

        var capacity = data.Length;
        if (written <= capacity)
        {
            var start = new byte[written];
            Array.Copy(data, start, (int)written);
            return start;
        }

        var head = (int)(written % capacity);
        var ordered = new byte[capacity];
        Array.Copy(data, head, ordered, 0, capacity - head);
        Array.Copy(data, 0, ordered, capacity - head, head);
        return ordered;
    }

    public static List<string> Lines(byte[] data, long written)
    {
        var bytes = Unroll(data, written);
        var start = 0;
        if (written > (data?.Length ?? 0))
        {
            var newline = Array.IndexOf(bytes, (byte)'\n');
            start = newline < 0 ? bytes.Length : newline + 1;
        }

        var text = Encoding.UTF8.GetString(bytes, start, bytes.Length - start);
        var lines = new List<string>();
        foreach (var line in text.Replace("\r\n", "\n").Split('\n'))
        {
            if (line.Length > 0)
            {
                lines.Add(line);
            }
        }

        return lines;
    }

    public const int MatchedLines = 3;

    public static List<string> Missing(IList<string> fileLines, IList<string> memoryLines)
    {
        var missing = new List<string>();
        if (memoryLines == null || memoryLines.Count == 0)
        {
            return missing;
        }

        var tail = LastNonEmpty(fileLines, MatchedLines);
        if (tail.Count == 0)
        {
            missing.AddRange(memoryLines);
            return missing;
        }

        for (var used = tail.Count; used >= 1; used--)
        {
            var sequence = tail.GetRange(tail.Count - used, used);
            for (var end = memoryLines.Count - 1; end >= used - 1; end--)
            {
                var partial = false;
                var matches = true;
                for (var offset = 0; offset < used && matches; offset++)
                {
                    var memory = memoryLines[end - used + 1 + offset].TrimEnd();
                    var file = sequence[offset];
                    if (memory == file)
                    {
                        continue;
                    }

                    if (offset == used - 1 && memory.StartsWith(file, StringComparison.Ordinal))
                    {
                        partial = true;
                        continue;
                    }

                    matches = false;
                }

                if (!matches)
                {
                    continue;
                }

                for (var next = partial ? end : end + 1; next < memoryLines.Count; next++)
                {
                    missing.Add(memoryLines[next]);
                }

                return missing;
            }
        }

        var first = memoryLines[0].TrimEnd();
        foreach (var line in fileLines)
        {
            if (line.TrimEnd() == first)
            {
                return missing;
            }
        }

        missing.AddRange(memoryLines);
        return missing;
    }

    private static List<string> LastNonEmpty(IList<string> lines, int count)
    {
        var result = new List<string>();
        if (lines == null)
        {
            return result;
        }

        for (var index = lines.Count - 1; index >= 0 && result.Count < count; index--)
        {
            var line = lines[index].TrimEnd();
            if (line.Length > 0)
            {
                result.Insert(0, line);
            }
        }

        return result;
    }
}
