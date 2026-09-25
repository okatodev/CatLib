namespace CatLib.UI;

internal static class ToastText
{
    public const int MaxLineLength = 34;
    public const string Ellipsis = "\u2026";

    public static string Format(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var split = text.IndexOf(": ", System.StringComparison.Ordinal);
        if (split < 0)
        {
            return Clip(text);
        }

        return Clip(text.Substring(0, split + 1)) + "\n" + Clip(text.Substring(split + 2));
    }

    public static string Clip(string line) =>
        line.Length <= MaxLineLength ? line : line.Substring(0, MaxLineLength - 1).TrimEnd() + Ellipsis;
}
