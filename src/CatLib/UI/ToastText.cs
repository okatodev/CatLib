using System;
using System.Collections.Generic;
using System.Linq;

namespace CatLib.UI;

internal static class ToastText
{
    public const int MaxLineLength = 30;
    public const int MaxLines = 2;
    public const string Ellipsis = "…";

    public static string Format(string text) => Format(text, line => line.Length, MaxLineLength);

    public static string Format(string text, Func<string, float> width, float limit)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        text = string.Join(" ", text.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries));
        if (width(text) <= limit)
        {
            return text;
        }

        var lines = new List<string>();
        var split = text.IndexOf(": ", StringComparison.Ordinal);
        if (split > 0 && width(text.Substring(0, split + 1)) <= limit)
        {
            lines.Add(text.Substring(0, split + 1));
            lines.AddRange(Wrap(text.Substring(split + 2), width, limit));
        }
        else
        {
            lines.AddRange(Wrap(text, width, limit));
        }

        if (lines.Count > MaxLines)
        {
            lines = Balance(text, width, limit);
        }

        return string.Join("\n", lines.Select(line => Clip(line, width, limit)));
    }

    public static string Clip(string line) => Clip(line, value => value.Length, MaxLineLength);

    public static string Clip(string line, Func<string, float> width, float limit)
    {
        if (width(line) <= limit)
        {
            return line;
        }

        var length = line.Length;
        while (length > 1 && width(line.Substring(0, length - 1).TrimEnd() + Ellipsis) > limit)
        {
            length--;
        }

        return line.Substring(0, Math.Max(1, length - 1)).TrimEnd() + Ellipsis;
    }

    private static List<string> Wrap(string text, Func<string, float> width, float limit)
    {
        var lines = new List<string>();
        var current = string.Empty;
        foreach (var word in text.Split(' '))
        {
            var candidate = current.Length == 0 ? word : current + " " + word;
            if (current.Length == 0 || width(candidate) <= limit)
            {
                current = candidate;
                continue;
            }

            lines.Add(current);
            current = word;
        }

        if (current.Length > 0)
        {
            lines.Add(current);
        }

        return lines;
    }

    private static List<string> Balance(string text, Func<string, float> width, float limit)
    {
        var best = new List<string> { text };
        var bestWidest = float.MaxValue;
        var bestQuoted = true;
        for (var index = text.IndexOf(' '); index > 0; index = text.IndexOf(' ', index + 1))
        {
            var first = text.Substring(0, index);
            var second = text.Substring(index + 1);
            var widest = Math.Max(width(first), width(second));
            var quoted = InsideQuotes(first);
            if ((bestQuoted && !quoted) || (quoted == bestQuoted && widest < bestWidest))
            {
                bestWidest = widest;
                bestQuoted = quoted;
                best = new List<string> { first, second };
            }
        }

        return best;
    }

    private static bool InsideQuotes(string before)
    {
        var guillemets = before.Count(character => character == '\u00AB') - before.Count(character => character == '\u00BB');
        var straight = before.Count(character => character == '"');
        return guillemets > 0 || straight % 2 == 1;
    }
}
