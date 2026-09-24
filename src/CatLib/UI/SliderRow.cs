using System.Diagnostics;
using CatLib.Config;
using CatLib.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CatLib.UI;

internal sealed class SliderRow : SettingRow
{
    public const int CommitDelayMilliseconds = 250;

    private readonly Slider _slider;
    private readonly TMP_Text _display;
    private object _pending;
    private bool _hasPending;
    private long _pendingSince;

    public SliderRow(ISetting setting, SettingPresentation presentation, GameObject root, CatLogger log)
        : base(setting, presentation, root, log)
    {
        _slider = root.GetComponentInChildren<Slider>(true);
        _display = root.transform.Find("panel_Value/text_DisplayValue")?.GetComponent<TMP_Text>();
        _slider.wholeNumbers = presentation.WholeNumbers;
        _slider.minValue = presentation.Min;
        _slider.maxValue = presentation.Max;
        Pull();
        UiEvents.Listen<float>(_slider.onValueChanged, OnValueChanged);
    }

    public Slider Slider => _slider;

    public string DisplayedText => _display == null ? null : _display.text;

    public bool HasPendingValue => _hasPending;

    public override Selectable Control => _slider;

    public override void Update()
    {
        if (_hasPending)
        {
            var elapsed = (Stopwatch.GetTimestamp() - _pendingSince) * 1000L / Stopwatch.Frequency;
            if (elapsed >= CommitDelayMilliseconds)
            {
                Commit();
            }

            return;
        }

        Pull();
    }

    public override void Pull()
    {
        var value = Presentation.ToSlider(CurrentValue);
        if (!Mathf.Approximately(_slider.value, value))
        {
            _slider.SetValueWithoutNotify(value);
        }

        SetDisplay(value);
    }

    public override void Commit()
    {
        if (!_hasPending)
        {
            return;
        }

        _hasPending = false;
        Write(_pending);
        Pull();
    }

    public override void Discard()
    {
        _hasPending = false;
        _pending = null;
    }

    private void OnValueChanged(float value)
    {
        if (Setting.IsOverridden)
        {
            Discard();
            Pull();
            return;
        }

        _pending = Presentation.FromSlider(value);
        _hasPending = true;
        _pendingSince = Stopwatch.GetTimestamp();
        SetDisplay(value);
    }

    private void SetDisplay(float value)
    {
        if (_display == null)
        {
            return;
        }

        var text = Presentation.FormatSlider(value);
        if (_display.text != text)
        {
            _display.text = text;
        }
    }
}
