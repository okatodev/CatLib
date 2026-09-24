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

    protected object CurrentValue => Setting.EntryBase.BoxedValue;

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

    protected bool Write(object value)
    {
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
