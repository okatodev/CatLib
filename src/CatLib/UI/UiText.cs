using System;
using System.Collections.Generic;
using System.Globalization;
using I2.Loc;

namespace CatLib.UI;

internal static class UiText
{
    public const string ModsTab = "ModsTab";
    public const string ModsList = "ModsList";
    public const string SelectMod = "SelectMod";
    public const string RestartSuffix = "RestartSuffix";
    public const string NoDescription = "NoDescription";
    public const string DefaultValue = "DefaultValue";
    public const string Range = "Range";
    public const string Options = "Options";
    public const string AppliesAfterRestart = "AppliesAfterRestart";
    public const string AfterRestart = "AfterRestart";
    public const string HoverHint = "HoverHint";
    public const string On = "On";
    public const string Off = "Off";
    public const string Version = "Version";
    public const string MessageRestart = "MessageRestart";
    public const string MessageRejected = "MessageRejected";
    public const string MessageAdjusted = "MessageAdjusted";
    public const string MessageReset = "MessageReset";
    public const string SettingsCount = "SettingsCount";
    public const string PendingRestart = "PendingRestart";

    private static readonly Dictionary<string, (string English, string Russian)> Strings = new()
    {
        [ModsTab] = ("Mods", "Моды"),
        [ModsList] = ("Mods", "Моды"),
        [SelectMod] = ("Select a mod", "Выберите мод"),
        [RestartSuffix] = ("(restart)", "(перезапуск)"),
        [NoDescription] = ("No description", "Нет описания"),
        [DefaultValue] = ("Default: {0}", "По умолчанию: {0}"),
        [Range] = ("Range: {0} to {1}", "Диапазон: от {0} до {1}"),
        [Options] = ("Options: {0}", "Варианты: {0}"),
        [AppliesAfterRestart] = ("Applies after a restart", "Применяется после перезапуска"),
        [AfterRestart] = ("After a restart: {0}", "После перезапуска: {0}"),
        [HoverHint] = ("Point at a setting to see its description", "Наведите на настройку, чтобы увидеть описание"),
        [On] = ("On", "Вкл"),
        [Off] = ("Off", "Выкл"),
        [Version] = ("v{0}", "v{0}"),
        [MessageRestart] = ("{0}: {1} will change after a restart", "{0}: «{1}» изменится после перезапуска"),
        [MessageRejected] = ("{0}: \"{2}\" is not a valid value for {1}", "{0}: «{2}» не подходит для «{1}»"),
        [MessageAdjusted] = ("{0}: {1} was set to {3}, the nearest allowed value to \"{2}\"", "{0}: для «{1}» вместо «{2}» установлено ближайшее допустимое {3}"),
        [MessageReset] = ("{0}: settings were reset to their defaults", "{0}: настройки сброшены по умолчанию")
    };

    private static readonly Dictionary<string, (string EnglishOne, string EnglishOther, string RussianOne, string RussianFew, string RussianMany)> Plurals = new()
    {
        [SettingsCount] = ("{0} setting", "{0} settings", "{0} настройка", "{0} настройки", "{0} настроек"),
        [PendingRestart] = ("{0} waiting for a restart", "{0} waiting for a restart", "{0} ждёт перезапуска", "{0} ждут перезапуска", "{0} ждут перезапуска")
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

    public static string Format(string key, string languageCode, params object[] arguments) =>
        string.Format(CultureInfo.InvariantCulture, Get(key, languageCode), arguments);

    public static string Plural(string key, int count, string languageCode)
    {
        if (!Plurals.TryGetValue(key, out var forms))
        {
            return key;
        }

        string pattern;
        if (IsRussian(languageCode))
        {
            var lastTwo = Math.Abs(count) % 100;
            var last = lastTwo % 10;
            pattern = last == 1 && lastTwo != 11
                ? forms.RussianOne
                : last >= 2 && last <= 4 && (lastTwo < 12 || lastTwo > 14)
                    ? forms.RussianFew
                    : forms.RussianMany;
        }
        else
        {
            pattern = count == 1 ? forms.EnglishOne : forms.EnglishOther;
        }

        return string.Format(CultureInfo.InvariantCulture, pattern, count);
    }
}
