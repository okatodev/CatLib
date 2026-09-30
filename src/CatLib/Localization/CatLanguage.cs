using System;
using System.Collections.Generic;
using CatLib.Logging;
using I2.Loc;

namespace CatLib.Localization;

public static class CatLanguage
{
    public const string Fallback = "en";
    public const int PollIntervalFrames = 10;

    private static string _known;
    private static int _countdown;
    private static IReadOnlyList<string> _gameLanguages;

    public static event Action<string> Changed;

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

    public static IReadOnlyList<string> GameLanguages
    {
        get
        {
            if (_gameLanguages != null && _gameLanguages.Count > 0)
            {
                return _gameLanguages;
            }

            var result = new List<string>();
            try
            {
                var codes = LocalizationManager.GetAllLanguagesCode(true, true);
                for (var index = 0; codes != null && index < codes.Count; index++)
                {
                    var code = codes[index];
                    if (!string.IsNullOrWhiteSpace(code) && !result.Contains(Normalize(code)))
                    {
                        result.Add(Normalize(code));
                    }
                }
            }
            catch (Exception)
            {
            }

            if (result.Count > 0)
            {
                _gameLanguages = result;
            }

            return result;
        }
    }

    public static string Game(string term) => Game(term, null);

    public static string Game(string term, string language)
    {
        if (string.IsNullOrEmpty(term))
        {
            return null;
        }

        try
        {
            var name = string.IsNullOrEmpty(language) ? null : LocalizationManager.GetLanguageFromCode(language, false);
            if (!string.IsNullOrEmpty(language) && string.IsNullOrEmpty(name))
            {
                return null;
            }

            var text = LocalizationManager.GetTranslation(term, true, 0, true, false, null, name, true);
            return string.IsNullOrEmpty(text) ? null : text;
        }
        catch (Exception)
        {
            return null;
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

    internal static void Poll(CatLogger log)
    {
        if (--_countdown > 0)
        {
            return;
        }

        _countdown = PollIntervalFrames;
        var current = Current;
        if (_known == null)
        {
            _known = current;
            return;
        }

        if (current == _known)
        {
            return;
        }

        _known = current;
        log?.Info($"The game language is now {current}");
        var handlers = Changed;
        if (handlers == null)
        {
            return;
        }

        foreach (var handler in handlers.GetInvocationList())
        {
            try
            {
                ((Action<string>)handler)(current);
            }
            catch (Exception exception)
            {
                log?.Error($"A language change handler of {handler.Method.DeclaringType?.FullName} failed", exception);
            }
        }
    }
}
