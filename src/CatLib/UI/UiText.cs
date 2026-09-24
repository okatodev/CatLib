using System;
using System.Collections.Generic;
using I2.Loc;

namespace CatLib.UI;

internal static class UiText
{
    public const string ModsTab = "ModsTab";
    public const string ModsList = "ModsList";
    public const string SelectMod = "SelectMod";

    private static readonly Dictionary<string, (string English, string Russian)> Strings = new()
    {
        [ModsTab] = ("Mods", "Моды"),
        [ModsList] = ("Mods", "Моды"),
        [SelectMod] = ("Select a mod", "Выберите мод")
    };

    public static string LanguageCode
    {
        get
        {
            try
            {
                return LocalizationManager.CurrentLanguageCode ?? string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }
    }

    public static bool IsRussian(string languageCode) => languageCode.StartsWith("ru", StringComparison.OrdinalIgnoreCase);

    public static string Get(string key) => Get(key, LanguageCode);

    public static string Get(string key, string languageCode)
    {
        if (!Strings.TryGetValue(key, out var value))
        {
            return key;
        }

        return IsRussian(languageCode) ? value.Russian : value.English;
    }
}
