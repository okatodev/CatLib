using System;
using System.Collections.Generic;
using I2.Loc;

namespace CatLib.Localization;

public static class CatLanguage
{
    public const string Fallback = "en";

    public static string Current
    {
        get
        {
            string code;
            try
            {
                code = LocalizationManager.CurrentLanguageCode;
            }
            catch (Exception)
            {
                code = null;
            }

            return Normalize(code);
        }
    }

    public static string Normalize(string code) =>
        string.IsNullOrWhiteSpace(code) ? Fallback : code.Trim().Replace('_', '-').ToLowerInvariant();

    public static IReadOnlyList<string> Chain(string code)
    {
        var normalized = Normalize(code);
        var chain = new List<string> { normalized };
        var dash = normalized.IndexOf('-');
        if (dash > 0)
        {
            chain.Add(normalized.Substring(0, dash));
        }

        if (!chain.Contains(Fallback))
        {
            chain.Add(Fallback);
        }

        return chain;
    }
}
