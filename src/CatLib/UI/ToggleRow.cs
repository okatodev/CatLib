using CatLib.Config;
using CatLib.Logging;
using UnityEngine;
using UnityEngine.UI;

namespace CatLib.UI;

internal sealed class ToggleRow : SettingRow
{
    private readonly Toggle _toggle;

    public ToggleRow(ISetting setting, SettingPresentation presentation, GameObject root, CatLogger log)
        : base(setting, presentation, root, log)
    {
        _toggle = root.GetComponentInChildren<Toggle>(true);
        Pull();
        UiEvents.Listen<bool>(_toggle.onValueChanged, isOn => Write(isOn));
    }

    public Toggle Toggle => _toggle;

    public override Selectable Control => _toggle;

    public override void Pull()
    {
        var value = CurrentValue is bool isOn && isOn;
        if (_toggle.isOn != value)
        {
            _toggle.SetIsOnWithoutNotify(value);
        }
    }
}
