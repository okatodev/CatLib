using System;
using System.Diagnostics;
using CatLib.Config;
using CatLib.Events;
using CatLib.Logging;

namespace CatLib.UI;

internal static class PlayerMessages
{
    public const double DisplaySeconds = 8;
    public const string ReferenceLine = "Сначала наведитесь на метку по";
    public const float ReferenceShare = 0.9f;

    private static readonly Stopwatch Clock = Stopwatch.StartNew();
    private static CatLogger _log;
    private static bool _subscribed;

    public static event Action<string> Posted;

    public static string Last { get; private set; }

    public static int Count { get; private set; }

    public static double LastPostedAt { get; private set; } = double.MinValue;

    public static bool IsFresh => Last != null && Clock.Elapsed.TotalSeconds - LastPostedAt <= DisplaySeconds;

    internal static void Initialize(CatLogger log)
    {
        _log = log;
        if (_subscribed)
        {
            return;
        }

        _subscribed = true;
        CatConfig.RestartRequired += OnRestartRequired;
        CatConfig.ValueRejected += OnValueRejected;
        CatConfig.ValueAdjusted += OnValueAdjusted;
    }

    public static string LastBrief { get; private set; }

    public static void Post(string text) => Post(text, null);

    public static void Post(string text, string brief)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        Last = text;
        LastBrief = ToastText.Format(brief ?? text);
        LastPostedAt = Clock.Elapsed.TotalSeconds;
        Count++;
        SafeInvoker.Invoke(Posted, text, "PlayerMessages.Posted", _log);
        DeliverInGame(brief ?? text);
    }

    private static void DeliverInGame(string raw)
    {
        try
        {
            if (!Singleton<InterfaceManager>.HasInstance())
            {
                return;
            }

            var manager = Singleton<InterfaceManager>.Instance;
            var settings = manager.SettingsInterface;
            if (settings != null && settings.IsShown)
            {
                return;
            }

            var notifications = manager.NotificationInterface;
            if (notifications == null)
            {
                return;
            }

            notifications.ShowNotification(FitToNotification(notifications, raw));
        }
        catch (Exception exception)
        {
            _log?.Warning($"Could not show an in-game notification: {exception.Message}");
        }
    }

    private static string FitToNotification(NotificationInterface notifications, string raw)
    {
        try
        {
            var prefab = notifications.NotificationItemInterfacePrefab;
            var text = prefab == null ? null : prefab.LinkedText;
            if (text != null)
            {
                var limit = text.GetPreferredValues(ReferenceLine).x * ReferenceShare;
                if (limit > 0)
                {
                    return ToastText.Format(raw, line => text.GetPreferredValues(line).x, limit);
                }
            }
        }
        catch (Exception exception)
        {
            _log?.Debug($"Measuring the notification text failed, using the character limit: {exception.Message}");
        }

        return ToastText.Format(raw);
    }

    private static void OnRestartRequired(ISetting setting)
    {
        var language = UiText.LanguageCode;
        Post(UiText.Format(UiText.MessageRestart, language, setting.Owner.DisplayName, Label(setting)));
    }

    private static void OnValueRejected(SettingValueProblem problem) =>
        Post(UiText.Format(UiText.MessageRejected, UiText.LanguageCode, Owner(problem), Label(problem), problem.RawValue, problem.EffectiveValue));

    private static void OnValueAdjusted(SettingValueProblem problem) =>
        Post(UiText.Format(UiText.MessageAdjusted, UiText.LanguageCode, Owner(problem), Label(problem), problem.RawValue, problem.EffectiveValue));

    private static string Owner(SettingValueProblem problem) => problem.Setting?.Owner.DisplayName ?? problem.OwnerId;

    private static string Label(SettingValueProblem problem) => problem.Setting == null ? LabelFormatter.Prettify(problem.Key) : Label(problem.Setting);

    private static string Label(ISetting setting) => CatLib.Localization.SettingTexts.Label(setting, UiText.LanguageCode);
}
