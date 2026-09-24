using System.Text;

namespace CatLib.UI;

internal static class LabelFormatter
{
    public static string Prettify(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(key.Length + 8);
        for (var index = 0; index < key.Length; index++)
        {
            var current = key[index];
            if (current == '_' || current == '-' || char.IsWhiteSpace(current))
            {
                AppendSpace(builder);
                continue;
            }

            if (index > 0 && NeedsSpace(key, index))
            {
                AppendSpace(builder);
            }

            builder.Append(current);
        }

        var text = builder.ToString().Trim();
        return text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);
    }

    private static bool NeedsSpace(string key, int index)
    {
        var previous = key[index - 1];
        var current = key[index];

        if (char.IsUpper(current) && char.IsLower(previous))
        {
            return true;
        }

        if (char.IsUpper(current) && char.IsUpper(previous) && index + 1 < key.Length && char.IsLower(key[index + 1]))
        {
            return true;
        }

        if (char.IsDigit(current) && char.IsLetter(previous))
        {
            return true;
        }

        return char.IsLetter(current) && char.IsDigit(previous);
    }

    private static void AppendSpace(StringBuilder builder)
    {
        if (builder.Length > 0 && builder[builder.Length - 1] != ' ')
        {
            builder.Append(' ');
        }
    }
}
