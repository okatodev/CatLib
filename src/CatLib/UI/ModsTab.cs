using System;
using CatLib.Config;
using CatLib.Il2Cpp;
using CatLib.Logging;
using UnityEngine;
using UnityEngine.UI;

namespace CatLib.UI;

internal sealed class ModsTab
{
    private readonly Il2CppEventBindings _bindings;
    private readonly CatLogger _log;
    private string _languageCode;
    private int _visibilityCheckCountdown;

    public ModsTab(
        MenuContext context,
        OptionsInterface options,
        TabInterface tab,
        GameObject panel,
        ScrollRect listScroll,
        ScrollRect contentScroll,
        GameObject listHeader,
        GameObject contentHeader,
        GameObject templates,
        TabBarFitter fitter,
        ModsPanel controller,
        Il2CppEventBindings bindings,
        CatLogger log)
    {
        Context = context;
        Options = options;
        OptionsPointer = options.Pointer;
        Tab = tab;
        Panel = panel;
        ListScroll = listScroll;
        ContentScroll = contentScroll;
        ListHeader = listHeader;
        ContentHeader = contentHeader;
        Templates = templates;
        Fitter = fitter;
        Controller = controller;
        _bindings = bindings;
        _log = log;
        _languageCode = UiText.LanguageCode;
    }

    public MenuContext Context { get; }

    public OptionsInterface Options { get; }

    public IntPtr OptionsPointer { get; }

    public TabInterface Tab { get; }

    public GameObject Panel { get; }

    public ScrollRect ListScroll { get; }

    public ScrollRect ContentScroll { get; }

    public GameObject ListHeader { get; }

    public GameObject ContentHeader { get; }

    public GameObject Templates { get; }

    public TabBarFitter Fitter { get; }

    public ModsPanel Controller { get; }

    public bool IsVisible { get; private set; } = true;

    public bool IsAlive => UiClone.IsAlive(Options) && UiClone.IsAlive(Tab) && UiClone.IsAlive(Panel);

    public void Update()
    {
        if (--_visibilityCheckCountdown <= 0)
        {
            _visibilityCheckCountdown = 30;
            SetVisible(HasSettings());
        }

        var languageCode = UiText.LanguageCode;
        if (languageCode != _languageCode)
        {
            _languageCode = languageCode;
            ApplyTexts();
            Controller.RebuildRows();
            Fitter.Invalidate();
        }

        Fitter.Update();
        Controller.Update();
    }

    public void ApplyTexts()
    {
        UiClone.SetText(Tab.gameObject, UiText.Get(UiText.ModsTab, _languageCode));
        Controller.SetHeaderText(ListHeader, UiText.Get(UiText.ModsList, _languageCode), Controller.ListWidth);
    }

    public void Detach()
    {
        _bindings.Clear(_log);
    }

    private void SetVisible(bool visible)
    {
        if (visible == IsVisible)
        {
            return;
        }

        IsVisible = visible;
        var tabs = Options._tabs;

        if (!visible)
        {
            if (Options._selectedTab != null && Options._selectedTab.Pointer == Tab.Pointer && tabs.Count > 1)
            {
                tabs[0].SetTabActive(true, true);
            }

            tabs.Remove(Tab);
            Tab.gameObject.SetActive(false);
        }
        else
        {
            Tab.gameObject.SetActive(true);
            if (!tabs.Contains(Tab))
            {
                tabs.Add(Tab);
            }
        }

        Fitter.Invalidate();
        _log.Info($"Mods tab in the {Context} settings menu is now {(visible ? "visible" : "hidden")}");
    }

    private static bool HasSettings()
    {
        foreach (var settings in CatConfig.All)
        {
            if (settings.Settings.Count > 0)
            {
                return true;
            }
        }

        return false;
    }
}
