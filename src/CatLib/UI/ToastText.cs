using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CatLib.UI;

internal static class ToastText
{
    public const int MaxLineLength = 30;
    public const int MaxLines = 2;
    public const string Ellipsis = "…";

    private const string ClosingMarks = "、。，．・：；！？）」』】〉》〕｝ー～…％";
    private const string OpeningMarks = "（「『【〈《〔｛";

    public static string Format(string text) => Format(text, DisplayWidth, MaxLineLength);

    public static float DisplayWidth(string line)
    {
        var width = 0f;
        foreach (var character in line)
        {
            width += IsWide(character) ? 2f : 1f;
        }

        return width;
    }

    public static bool IsWide(char character) =>
        character >= 'ᄀ' && character <= 'ᅟ'
        || character >= '⺀' && character <= '꓏'
        || character >= '가' && character <= '힣'
        || character >= '豈' && character <= '﫿'
        || character >= '︰' && character <= '﹏'
        || character >= '＀' && character <= '｠'
        || character >= '￠' && character <= '￦';

    public static bool BreaksAnywhere(char character) =>
        IsWide(character)
        && !(character >= '\u1100' && character <= '\u11FF')
        && !(character >= '\u3130' && character <= '\u318F')
        && !(character >= '\uAC00' && character <= '\uD7A3');

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
        var (head, tail) = SplitAtColon(text);
        if (head != null && width(head) <= limit)
        {
            lines.Add(head);
            lines.AddRange(Wrap(tail, width, limit));
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

    public static string Clip(string line) => Clip(line, DisplayWidth, MaxLineLength);

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

    private static (string Head, string Tail) SplitAtColon(string text)
    {
        var ascii = text.IndexOf(": ", StringComparison.Ordinal);
        var wide = text.IndexOf('：');
        if (ascii > 0 && (wide < 0 || ascii < wide))
        {
            return (text.Substring(0, ascii + 1), text.Substring(ascii + 2));
        }

        if (wide > 0 && wide < text.Length - 1)
        {
            return (text.Substring(0, wide + 1), text.Substring(wide + 1).TrimStart());
        }

        return (null, null);
    }

    private static List<(string Text, string Joiner)> Tokens(string text)
    {
        var tokens = new List<(string Text, string Joiner)>();
        var current = new StringBuilder();
        var joiner = string.Empty;
        var separator = string.Empty;

        void Emit()
        {
            if (current.Length > 0)
            {
                tokens.Add((current.ToString(), joiner));
                current.Clear();
            }
        }

        void Start()
        {
            if (current.Length == 0)
            {
                joiner = separator;
                separator = string.Empty;
            }
        }

        bool OnlyOpening() => current.Length > 0 && current.ToString().All(mark => OpeningMarks.IndexOf(mark) >= 0);

        foreach (var character in text)
        {
            if (character == ' ')
            {
                Emit();
                separator = " ";
                continue;
            }

            if (ClosingMarks.IndexOf(character) >= 0)
            {
                if (current.Length == 0 && tokens.Count > 0 && separator.Length == 0)
                {
                    var last = tokens[tokens.Count - 1];
                    tokens[tokens.Count - 1] = (last.Text + character, last.Joiner);
                }
                else
                {
                    Start();
                    current.Append(character);
                }

                continue;
            }

            if (OpeningMarks.IndexOf(character) >= 0 || BreaksAnywhere(character))
            {
                if (current.Length > 0 && !OnlyOpening())
                {
                    Emit();
                }

                Start();
                current.Append(character);
                if (OpeningMarks.IndexOf(character) < 0)
                {
                    Emit();
                }

                continue;
            }

            Start();
            current.Append(character);
        }

        Emit();
        return tokens;
    }

    private static string Join(List<(string Text, string Joiner)> tokens, int start, int end)
    {
        var builder = new StringBuilder();
        for (var index = start; index < end; index++)
        {
            if (index > start)
            {
                builder.Append(tokens[index].Joiner);
            }

            builder.Append(tokens[index].Text);
        }

        return builder.ToString();
    }

    private static List<string> Wrap(string text, Func<string, float> width, float limit)
    {
        var lines = new List<string>();
        var tokens = Tokens(text);
        var start = 0;
        for (var index = 1; index <= tokens.Count; index++)
        {
            if (index == tokens.Count)
            {
                lines.Add(Join(tokens, start, index));
                break;
            }

            if (width(Join(tokens, start, index + 1)) > limit)
            {
                lines.Add(Join(tokens, start, index));
                start = index;
            }
        }

        return lines;
    }

    private static List<string> Balance(string text, Func<string, float> width, float limit)
    {
        var tokens = Tokens(text);
        var best = new List<string> { text };
        var bestScore = (Fits: false, Plain: false, Widest: float.MaxValue);
        for (var index = 1; index < tokens.Count; index++)
        {
            var first = Join(tokens, 0, index);
            var second = Join(tokens, index, tokens.Count);
            var widest = Math.Max(width(first), width(second));
            var score = (Fits: widest <= limit, Plain: !InsideQuotes(first), Widest: widest);
            if (Better(score, bestScore))
            {
                bestScore = score;
                best = new List<string> { first, second };
            }
        }

        return best;
    }

    private static bool Better((bool Fits, bool Plain, float Widest) candidate, (bool Fits, bool Plain, float Widest) best)
    {
        if (candidate.Fits != best.Fits)
        {
            return candidate.Fits;
        }

        if (candidate.Plain != best.Plain)
        {
            return candidate.Plain;
        }

        return candidate.Widest < best.Widest;
    }

    private static bool InsideQuotes(string before)
    {
        int Open(char open, char close) => before.Count(character => character == open) - before.Count(character => character == close);
        var straight = before.Count(character => character == '"');
        var single = 0;
        for (var index = 0; index < before.Length; index++)
        {
            if (before[index] != '\'')
            {
                continue;
            }

            var next = index + 1 < before.Length ? before[index + 1] : ' ';
            if (index == 0 || before[index - 1] == ' ')
            {
                single++;
            }
            else if (single > 0 && (!char.IsLetter(next) || next >= '가' && next <= '힣'))
            {
                single--;
            }
        }

        return Open('«', '»') > 0 || Open('「', '」') > 0 || Open('“', '”') > 0 || Open('„', '“') > 0 && Open('“', '”') <= 0 || straight % 2 == 1 || single > 0;
    }
}
