using System;
using CatLib.Config;
using CatLib.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CatLib.UI;

internal abstract class SettingRow
{
    protected SettingRow(ISetting setting, SettingPresentation presentation, GameObject root, CatLogger log)
    {
        Setting = setting;
        Presentation = presentation;
        Root = root;
        Log = log;
        Label = root.transform.Find("panel_Label")?.GetComponentInChildren<TMP_Text>(true);
    }

    public ISetting Setting { get; }

    public SettingPresentation Presentation { get; }

    public GameObject Root { get; }

    public TMP_Text Label { get; }

    public ControlKind Kind => Presentation.Kind;

    public abstract Selectable Control { get; }

    protected CatLogger Log { get; }

    protected object CurrentValue => Setting.IsOverridden ? Setting.BoxedValue : Setting.EntryBase.BoxedValue;

    public bool ShowsOverride { get; private set; }

    public const float OverriddenAlpha = 0.5f;

    private CanvasGroup _valueGroup;

    public CanvasGroup ValueGroup
    {
        get
        {
            if (_valueGroup == null)
            {
                var value = Root.transform.Find("panel_Value")?.gameObject;
                if (value != null)
                {
                    _valueGroup = value.GetComponent<CanvasGroup>() ?? value.AddComponent<CanvasGroup>();
                }
            }

            return _valueGroup;
        }
    }

    public void SyncOverride(string languageCode)
    {
        var overridden = Setting.IsOverridden;
        if (overridden == ShowsOverride)
        {
            return;
        }

        ShowsOverride = overridden;
        var group = ValueGroup;
        if (group != null)
        {
            group.alpha = overridden ? OverriddenAlpha : 1f;
            group.blocksRaycasts = !overridden;
        }

        SetLabel(ModsPanel.RowLabel(Setting, languageCode));
        Pull();
    }

    public void SetLabel(string text)
    {
        if (Label != null)
        {
            Label.text = text;
        }
    }

    public virtual void Update() => Pull();

    public abstract void Pull();

    public virtual void Commit()
    {
    }

    public virtual void Discard()
    {
    }

    protected bool Write(object value)
    {
        if (Setting.IsOverridden)
        {
            Pull();
            return false;
        }

        try
        {
            Setting.EntryBase.BoxedValue = value;
            return true;
        }
        catch (Exception exception)
        {
            Log.Warning($"Could not set {Setting.Id} from the menu: {exception.Message}");
            return false;
        }
    }
}
