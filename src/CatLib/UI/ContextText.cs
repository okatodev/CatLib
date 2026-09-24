using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using CatLib.Config;

namespace CatLib.UI;

internal static class ContextText
{
    public const int MaxListedOptions = 6;

    public static string For(ISetting setting, string languageCode)
    {
        var entry = setting.EntryBase;
        var description = entry.Description?.Description;
        var presentation = SettingPresentation.For(setting.ValueType, entry.Description?.AcceptableValues);

        var details = new List<string>
        {
            UiText.Format(UiText.DefaultValue, languageCode, FormatValue(setting, presentation, entry.DefaultValue, languageCode))
        };

        if (presentation.Kind == ControlKind.Slider)
        {
            details.Add(UiText.Format(UiText.Range, languageCode, presentation.FormatSlider(presentation.Min), presentation.FormatSlider(presentation.Max)));
        }
        else if (presentation.Kind == ControlKind.Dropdown)
        {
            var labels = presentation.ChoiceLabels.Take(MaxListedOptions).ToList();
            var text = string.Join(", ", labels) + (presentation.ChoiceLabels.Count > MaxListedOptions ? ", ..." : string.Empty);
            details.Add(UiText.Format(UiText.Options, languageCode, text));
        }

        if (setting.IsOverridden)
        {
            details.Add(UiText.Format(UiText.HostValue, languageCode, FormatValue(setting, presentation, setting.BoxedLocalValue, languageCode)));
        }

        if (setting.IsRestartPending)
        {
            details.Add(UiText.Format(UiText.AfterRestart, languageCode, FormatValue(setting, presentation, setting.BoxedLocalValue, languageCode)));
        }
        else if (setting.IsRestartRequired)
        {
            details.Add(UiText.Get(UiText.AppliesAfterRestart, languageCode));
        }

        var first = string.IsNullOrWhiteSpace(description) ? UiText.Get(UiText.NoDescription, languageCode) : description.Trim();
        return first + "\n" + string.Join(" · ", details);
    }

    public static string FormatValue(ISetting setting, SettingPresentation presentation, object value, string languageCode)
    {
        if (value == null)
        {
            return string.Empty;
        }

        switch (presentation.Kind)
        {
            case ControlKind.Toggle:
                return UiText.Get(value is bool isOn && isOn ? UiText.On : UiText.Off, languageCode);
            case ControlKind.Slider:
                return presentation.FormatSlider(presentation.ToSlider(value));
            case ControlKind.Dropdown:
                var index = presentation.IndexOf(value);
                if (index >= 0)
                {
                    return presentation.ChoiceLabels[index];
                }

                break;
        }

        return setting.ValueType == typeof(string) ? (string)value : TomlTypeConverter.ConvertToString(value, setting.ValueType);
    }

    public static string CardStatus(CatSettings settings, string languageCode)
    {
        var visible = ModsPanel.VisibleSettings(settings);
        var pending = visible.Count(setting => setting.IsRestartPending);
        var status = UiText.Plural(UiText.SettingsCount, visible.Count, languageCode);
        return pending > 0 ? status + " · " + UiText.Plural(UiText.PendingRestart, pending, languageCode) : status;
    }
}
