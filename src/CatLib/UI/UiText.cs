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
    public const string HostSuffix = "HostSuffix";
    public const string HostValue = "HostValue";
    public const string NetPlayerIncompatible = "NetPlayerIncompatible";
    public const string NetPlayerDisconnected = "NetPlayerDisconnected";
    public const string NetPlayerDisconnectedBrief = "NetPlayerDisconnectedBrief";
    public const string NetPlayerIncompatibleBrief = "NetPlayerIncompatibleBrief";
    public const string NetYouWillBeDisconnected = "NetYouWillBeDisconnected";
    public const string NetCardOtherMods = "NetCardOtherMods";
    public const string NetHostIncompatible = "NetHostIncompatible";
    public const string NetHostWithoutCatLib = "NetHostWithoutCatLib";
    public const string ProblemProtocol = "ProblemProtocol";
    public const string ProblemGame = "ProblemGame";
    public const string ProblemMissingOnClient = "ProblemMissingOnClient";
    public const string ProblemMissingOnHost = "ProblemMissingOnHost";
    public const string ProblemVersion = "ProblemVersion";
    public const string SettingsCount = "SettingsCount";
    public const string PendingRestart = "PendingRestart";
    public const string LobbyModsTitle = "LobbyModsTitle";
    public const string LobbyAllMatch = "LobbyAllMatch";
    public const string LobbyPaused = "LobbyPaused";
    public const string LobbyHost = "LobbyHost";
    public const string LobbyYou = "LobbyYou";
    public const string LobbyPlayers = "LobbyPlayers";
    public const string LobbyMods = "LobbyMods";
    public const string RosterChecking = "RosterChecking";
    public const string RosterCompatible = "RosterCompatible";
    public const string RosterLimited = "RosterLimited";
    public const string RosterWithoutCatLib = "RosterWithoutCatLib";
    public const string RosterLeaving = "RosterLeaving";
    public const string MarkWorks = "MarkWorks";
    public const string MarkPaused = "MarkPaused";
    public const string MarkMissing = "MarkMissing";
    public const string MarkOtherVersion = "MarkOtherVersion";
    public const string MarkNotOnHost = "MarkNotOnHost";
    public const string MarkLocal = "MarkLocal";
    public const string RowGameVersion = "RowGameVersion";
    public const string RowHostHas = "RowHostHas";
    public const string RowCatLib = "RowCatLib";
    public const string RowCatLibOther = "RowCatLibOther";
    public const string RowNoMods = "RowNoMods";
    public const string PolicyTitle = "PolicyTitle";
    public const string PolicyWarn = "PolicyWarn";
    public const string PolicyDisconnect = "PolicyDisconnect";
    public const string BadgePaused = "BadgePaused";
    public const string BadgeRestart = "BadgeRestart";
    public const string BadgeHost = "BadgeHost";
    public const string CardAuthor = "CardAuthor";

    private static readonly Dictionary<string, (string English, string Russian)> Strings = new()
    {
        [ModsTab] = ("Mods", "Моды"),
        [ModsList] = ("Mods", "Моды"),
        [SelectMod] = ("Select a mod", "Выберите мод"),
        [RestartSuffix] = ("(restart)", "(перезапуск)"),
        [HostSuffix] = ("(host)", "(хост)"),
        [HostValue] = ("Set by the host, yours: {0}", "Задано хостом, ваше: {0}"),
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
        [MessageRestart] = ("{0}: {1} changes after a restart", "{0}: «{1}» — после перезапуска"),
        [MessageRejected] = ("{0}: \"{2}\" is not a valid value for {1}", "{0}: «{2}» не подходит для «{1}»"),
        [MessageAdjusted] = ("{0}: {1} set to {3} instead of \"{2}\"", "{0}: «{1}» = {3} вместо «{2}»"),
        [MessageReset] = ("{0}: settings reset to defaults", "{0}: настройки сброшены по умолчанию"),
        [NetPlayerIncompatible] = ("{0}: incompatible mods, {1}", "{0}: несовместимые моды, {1}"),
        [NetPlayerDisconnected] = ("{0} was disconnected: {1}", "{0} отключён: {1}"),
        [NetPlayerDisconnectedBrief] = ("{0} was disconnected: {1}", "{0} отключён: {1}"),
        [NetPlayerIncompatibleBrief] = ("{0} has other mods: {1}", "У {0} другие моды: {1}"),
        [NetYouWillBeDisconnected] = ("The host will disconnect you: {0}", "Хост отключит вас: {0}"),
        [NetCardOtherMods] = ("other mods", "другие моды"),
        [NetHostIncompatible] = ("Mods do not match the host: {0}", "Моды не совпадают с хостом: {0}"),
        [NetHostWithoutCatLib] = ("The host has no CatLib, these may not work: {0}", "У хоста нет CatLib, могут не работать: {0}"),
        [ProblemProtocol] = ("CatLib version", "версия CatLib"),
        [ProblemGame] = ("game version", "версия игры"),
        [ProblemMissingOnClient] = ("{0} missing on the client", "{0} нет у клиента"),
        [ProblemMissingOnHost] = ("{0} missing on the host", "{0} нет у хоста"),
        [ProblemVersion] = ("{0} {1} vs {2}", "{0} {1} против {2}"),
        [LobbyModsTitle] = ("Players' mods", "Моды игроков"),
        [LobbyAllMatch] = ("{0} · all match", "{0} · всё совпадает"),
        [LobbyPaused] = ("{0} · paused: {1}", "{0} · на паузе: {1}"),
        [LobbyHost] = ("host", "хост"),
        [LobbyYou] = ("you", "вы"),
        [RosterChecking] = ("checking…", "проверка…"),
        [RosterCompatible] = ("everything matches", "всё совпадает"),
        [RosterLimited] = ("other mods", "другие моды"),
        [RosterWithoutCatLib] = ("no CatLib", "нет CatLib"),
        [RosterLeaving] = ("will be disconnected", "будет отключён"),
        [MarkWorks] = ("works", "работает"),
        [MarkPaused] = ("paused", "на паузе"),
        [MarkMissing] = ("missing · host has {0}", "нет · у хоста {0}"),
        [MarkOtherVersion] = ("host has {0}", "у хоста {0}"),
        [MarkNotOnHost] = ("not on the host · paused", "нет у хоста · на паузе"),
        [MarkLocal] = ("only here", "только у себя"),
        [RowGameVersion] = ("Game version", "Версия игры"),
        [RowHostHas] = ("host has {0}", "у хоста {0}"),
        [RowCatLib] = ("CatLib", "CatLib"),
        [RowCatLibOther] = ("other version, update CatLib", "другая версия, обновите CatLib"),
        [RowNoMods] = ("no mods", "нет модов"),
        [PolicyTitle] = ("Players with other mods", "Игроки с другими модами"),
        [PolicyWarn] = ("let in, pause the mods", "пускать, моды на паузу"),
        [PolicyDisconnect] = ("disconnect", "отключать"),
        [BadgePaused] = ("paused in this game", "на паузе в этой игре"),
        [BadgeRestart] = ("needs a restart", "ждёт перезапуска"),
        [BadgeHost] = ("set by the host", "задано хостом"),
        [CardAuthor] = ("by {0}", "автор: {0}")
    };

    private static readonly Dictionary<string, (string EnglishOne, string EnglishOther, string RussianOne, string RussianFew, string RussianMany)> Plurals = new()
    {
        [SettingsCount] = ("{0} setting", "{0} settings", "{0} настройка", "{0} настройки", "{0} настроек"),
        [PendingRestart] = ("{0} waiting for a restart", "{0} waiting for a restart", "{0} ждёт перезапуска", "{0} ждут перезапуска", "{0} ждут перезапуска"),
        [LobbyPlayers] = ("{0} player", "{0} players", "{0} игрок", "{0} игрока", "{0} игроков"),
        [LobbyMods] = ("{0} mod", "{0} mods", "{0} мод", "{0} мода", "{0} модов")
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
