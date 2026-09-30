using System;
using System.Collections.Generic;

namespace CatLib.Localization;

public static class PluralRules
{
    public const string Zero = "zero";
    public const string One = "one";
    public const string Two = "two";
    public const string Few = "few";
    public const string Many = "many";
    public const string Other = "other";

    public static readonly IReadOnlyList<string> AllCategories = new[] { Zero, One, Two, Few, Many, Other };

    private static readonly string[] OnlyOther = { Other };
    private static readonly string[] OneOther = { One, Other };
    private static readonly string[] OneFewMany = { One, Few, Many };
    private static readonly string[] OneFewOther = { One, Few, Other };
        private static readonly string[] Arabic = { Zero, One, Two, Few, Many, Other };

    private static readonly HashSet<string> NoPlural = new(StringComparer.Ordinal)
    {
        "ja", "zh", "ko", "th", "vi", "id", "ms", "lo", "my", "km"
    };

    private static readonly HashSet<string> EastSlavic = new(StringComparer.Ordinal) { "ru", "uk", "be" };

    private static readonly HashSet<string> WestSlavic = new(StringComparer.Ordinal) { "cs", "sk" };

    private static readonly HashSet<string> OneForZeroAndOne = new(StringComparer.Ordinal) { "fr", "hy", "kab" };

    private static readonly HashSet<string> RomanceMany = new(StringComparer.Ordinal) { "es", "it", "ca" };

    public static string Category(string language, long count)
    {
        var n = Math.Abs(count);
        var code = Base(language);
        if (NoPlural.Contains(code))
        {
            return Other;
        }

        if (EastSlavic.Contains(code))
        {
            var last = n % 10;
            var lastTwo = n % 100;
            if (last == 1 && lastTwo != 11)
            {
                return One;
            }

            return last >= 2 && last <= 4 && (lastTwo < 12 || lastTwo > 14) ? Few : Many;
        }

        if (code == "pl")
        {
            var last = n % 10;
            var lastTwo = n % 100;
            if (n == 1)
            {
                return One;
            }

            return last >= 2 && last <= 4 && (lastTwo < 12 || lastTwo > 14) ? Few : Many;
        }

        if (WestSlavic.Contains(code))
        {
            return n == 1 ? One : n >= 2 && n <= 4 ? Few : Other;
        }

        if (code == "pt")
        {
            var portugal = Normalized(language) == "pt-pt";
            return (portugal ? n == 1 : n <= 1) ? One : MillionMany(n);
        }

        if (OneForZeroAndOne.Contains(code))
        {
            return n <= 1 ? One : code == "fr" ? MillionMany(n) : Other;
        }

        if (RomanceMany.Contains(code))
        {
            return n == 1 ? One : MillionMany(n);
        }

        if (code == "ar")
        {
            var lastTwo = n % 100;
            return n == 0 ? Zero : n == 1 ? One : n == 2 ? Two : lastTwo >= 3 && lastTwo <= 10 ? Few : lastTwo >= 11 ? Many : Other;
        }

        if (code == "tr" || code == "az")
        {
            return n == 1 ? One : Other;
        }

        return n == 1 ? One : Other;
    }

    public static IReadOnlyList<string> Categories(string language)
    {
        var code = Base(language);
        if (NoPlural.Contains(code))
        {
            return OnlyOther;
        }

        if (EastSlavic.Contains(code) || code == "pl")
        {
            return OneFewMany;
        }

        if (WestSlavic.Contains(code))
        {
            return OneFewOther;
        }

        if (code == "ar")
        {
            return Arabic;
        }

        return OneOther;
    }

    public static bool IsCategory(string name) => Array.IndexOf(Arabic, name) >= 0;

    private static string MillionMany(long n) => n != 0 && n % 1000000 == 0 ? Many : Other;

    private static string Normalized(string language) => CatLanguage.Normalize(language);

    private static string Base(string language)
    {
        var normalized = Normalized(language);
        var dash = normalized.IndexOf('-');
        return dash > 0 ? normalized.Substring(0, dash) : normalized;
    }
}
