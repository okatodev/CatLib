using System;
using BepInEx.Configuration;
using CatLib.Config;
using CatLib.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CatLib.UI;

internal sealed class TextRow : SettingRow
{
    private readonly TMP_InputField _input;

    public TextRow(ISetting setting, SettingPresentation presentation, GameObject root, CatLogger log)
        : base(setting, presentation, root, log)
    {
        _input = root.GetComponentInChildren<TMP_InputField>(true);
        var placeholder = _input.placeholder == null ? null : _input.placeholder.TryCast<TMP_Text>();
        if (placeholder != null)
        {
            placeholder.text = string.Empty;
        }

        _input.contentType = TMP_InputField.ContentType.Standard;
        Pull();
        UiEvents.Listen<string>(_input.onEndEdit, OnEndEdit);
    }

    public TMP_InputField Input => _input;

    public override Selectable Control => _input;

    public string Format(object value)
    {
        if (Setting.ValueType == typeof(string))
        {
            return value as string ?? string.Empty;
        }

        return value == null ? string.Empty : TomlTypeConverter.ConvertToString(value, Setting.ValueType);
    }

    public override void Pull()
    {
        if (_input.isFocused)
        {
            return;
        }

        var text = Format(CurrentValue);
        if (_input.text != text)
        {
            _input.SetTextWithoutNotify(text);
        }
    }

    private void OnEndEdit(string text)
    {
        if (TryParse(text, out var value))
        {
            Write(value);
        }
        else
        {
            Log.Warning($"\"{text}\" is not a valid value for {Setting.Id}, keeping {Format(CurrentValue)}");
        }

        _input.SetTextWithoutNotify(Format(CurrentValue));
    }

    private bool TryParse(string text, out object value)
    {
        if (Setting.ValueType == typeof(string))
        {
            value = text ?? string.Empty;
            return true;
        }

        try
        {
            value = TomlTypeConverter.ConvertToValue((text ?? string.Empty).Trim(), Setting.ValueType);
            return true;
        }
        catch (Exception)
        {
            value = null;
            return false;
        }
    }
}
