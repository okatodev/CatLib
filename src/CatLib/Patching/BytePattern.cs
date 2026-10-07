using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace CatLib.Patching;

public sealed class BytePattern
{
    public const string Wildcard = "??";

    private readonly byte[] _bytes;
    private readonly bool[] _known;

    private BytePattern(byte[] bytes, bool[] known)
    {
        _bytes = bytes;
        _known = known;
    }

    public int Length => _bytes.Length;

    public static BytePattern Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("A byte pattern needs at least one byte", nameof(text));
        }

        var parts = text.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        var bytes = new byte[parts.Length];
        var known = new bool[parts.Length];
        for (var index = 0; index < parts.Length; index++)
        {
            var part = parts[index];
            if (part == Wildcard || part == "?")
            {
                continue;
            }

            if (part.Length != 2 || !byte.TryParse(part, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out bytes[index]))
            {
                throw new FormatException($"\"{part}\" is not a byte in the pattern \"{text}\"; write bytes as two hex digits or {Wildcard}");
            }

            known[index] = true;
        }

        if (!known.Any(value => value))
        {
            throw new ArgumentException("A byte pattern needs at least one known byte", nameof(text));
        }

        return new BytePattern(bytes, known);
    }

    public bool MatchesAt(byte[] data, int start)
    {
        if (data == null || start < 0 || start + _bytes.Length > data.Length)
        {
            return false;
        }

        for (var index = 0; index < _bytes.Length; index++)
        {
            if (_known[index] && data[start + index] != _bytes[index])
            {
                return false;
            }
        }

        return true;
    }

    public IReadOnlyList<int> FindAll(byte[] data)
    {
        var found = new List<int>();
        if (data == null)
        {
            return found;
        }

        for (var start = 0; start + _bytes.Length <= data.Length; start++)
        {
            if (MatchesAt(data, start))
            {
                found.Add(start);
            }
        }

        return found;
    }

    public override string ToString() =>
        string.Join(" ", _bytes.Select((value, index) => _known[index] ? value.ToString("X2", CultureInfo.InvariantCulture) : Wildcard));
}
