using CatLib.Config;
using CatLib.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CatLib.UI;

internal sealed class DropdownRow : SettingRow
{
    private readonly TMP_Dropdown _dropdown;

    public DropdownRow(ISetting setting, SettingPresentation presentation, GameObject root, CatLogger log)
        : base(setting, presentation, root, log)
    {
        _dropdown = root.GetComponentInChildren<TMP_Dropdown>(true);
        _dropdown.ClearOptions();
        var options = new Il2CppSystem.Collections.Generic.List<string>();
        foreach (var label in presentation.ChoiceLabels)
        {
            options.Add(label);
        }

        _dropdown.AddOptions(options);
        Pull();
        UiEvents.Listen<int>(_dropdown.onValueChanged, OnValueChanged);
    }

    public TMP_Dropdown Dropdown => _dropdown;

    public override Selectable Control => _dropdown;

    public override void Pull()
    {
        if (_dropdown.IsExpanded)
        {
            return;
        }

        var index = Presentation.IndexOf(CurrentValue);
        if (index >= 0 && _dropdown.value != index)
        {
            _dropdown.SetValueWithoutNotify(index);
        }
    }

    private void OnValueChanged(int index)
    {
        if (index >= 0 && index < Presentation.Choices.Count)
        {
            Write(Presentation.Choices[index]);
        }
    }
}
