using System;
using CatLib.Config;
using UnityEngine;
using UnityEngine.UI;

namespace CatLib.UI;

internal sealed class ModListItem
{
    public ModListItem(CatSettings settings, GameObject root, Action<ModListItem> selected)
    {
        Settings = settings;
        Root = root;
        Toggle = root.GetComponent<Toggle>();
        Toggle.SetIsOnWithoutNotify(false);
        UiClone.SetText(root, CatLib.Localization.SettingTexts.ModName(settings, UiText.LanguageCode));
        UiEvents.Listen<bool>(Toggle.onValueChanged, isOn =>
        {
            if (isOn)
            {
                selected(this);
            }
            else if (IsSelected)
            {
                Toggle.SetIsOnWithoutNotify(true);
            }
        });
    }

    public CatSettings Settings { get; }

    public GameObject Root { get; }

    public Toggle Toggle { get; }

    public bool IsSelected { get; private set; }

    public void SetSelected(bool selected)
    {
        IsSelected = selected;
        if (Toggle.isOn != selected)
        {
            Toggle.SetIsOnWithoutNotify(selected);
        }
    }
}
