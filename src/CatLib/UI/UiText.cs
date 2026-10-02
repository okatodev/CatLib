using System;
using CatLib.Localization;
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
    public const string BuildNewer = "BuildNewer";
    public const string BuildOlder = "BuildOlder";
    public const string BuildMatches = "BuildMatches";
    public const string BuildCompare = "BuildCompare";
    public const string BuildNumber = "BuildNumber";
    public const string BuildUnknown = "BuildUnknown";

    public const string KeyPrefix = "ui.";

    private static readonly object Sync = new();
    private static TextCatalog _catalog;

    internal static TextCatalog Catalog
    {
        get
        {
            lock (Sync)
            {
                if (_catalog == null)
                {
                    var catalog = CatLocalization.For(PluginMeta.Guid);
                    if (catalog.FindExact(KeyPrefix + ModsTab, CatLanguage.Fallback) == null)
                    {
                        catalog.LoadEmbedded(typeof(UiText).Assembly, typeof(UiText).Assembly.GetName().Name + "." + CatLocalization.EmbeddedFolder + ".");
                    }

                    _catalog = catalog;
                }

                return _catalog;
            }
        }
    }

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

    public static string Get(string key) => Get(key, LanguageCode);

    public static string Get(string key, string languageCode) => Catalog.Find(KeyPrefix + key, languageCode) ?? key;

    public static string Format(string key, string languageCode, params object[] arguments) =>
        Catalog.FormatFor(languageCode, KeyPrefix + key, arguments);

    public static string Plural(string key, int count, string languageCode) =>
        Catalog.FindPlural(KeyPrefix + key, count, languageCode) == null ? key : Catalog.PluralFor(languageCode, KeyPrefix + key, count);
}
