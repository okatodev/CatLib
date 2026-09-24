using System;
using CatLib.Il2Cpp;
using CatLib.Logging;
using UnityEngine;
using UnityEngine.UI;

namespace CatLib.UI;

internal static class ModsTabBuilder
{
    public const string TabName = "btn_TopTab CatLibMods";
    public const string PanelName = "panel_CatLibMods";
    public const string ListScrollName = "scroll_CatLibModList";
    public const string ContentScrollName = "scroll_CatLibModSettings";
    public const string TemplatesName = "templates_CatLib";
    public const float ListWidthRatio = 0.28f;
    public const float PaneGap = 30f;
    public const float ScrollbarAllowance = 40f;

    public static bool IsReady(OptionsInterface options)
    {
        var tabs = options._tabs;
        return tabs != null && tabs.Count > 0 && tabs[0].Selected != null && options.TabsParent != null;
    }

    public static ModsTab Build(MenuContext context, OptionsInterface options, CatLogger log)
    {
        var tabs = options._tabs;
        var templateTab = tabs[tabs.Count - 1];
        var templatePanel = FindPanelTemplate(options);
        if (templatePanel == null)
        {
            throw new InvalidOperationException("No settings panel with a scroll view was found to use as a template");
        }

        var panel = UiClone.CloneInactive(templatePanel, PanelName);
        panel.SetActive(false);
        DestroyGameInterfaces(panel);

        var contentScroll = FindDirectScroll(panel.transform);
        if (contentScroll == null)
        {
            throw new InvalidOperationException($"{templatePanel.name} has no scroll view");
        }

        var templates = new GameObject(TemplatesName);
        templates.SetActive(false);
        templates.transform.SetParent(panel.transform, false);

        var headerTemplate = CaptureHeaderTemplate(contentScroll, templates.transform);
        if (headerTemplate == null)
        {
            throw new InvalidOperationException($"{templatePanel.name} has no settings header to use as a template");
        }

        UiClone.DestroyChildren(contentScroll.content);

        var listScroll = UnityEngine.Object.Instantiate(contentScroll.gameObject, panel.transform, false).GetComponent<ScrollRect>();
        listScroll.gameObject.name = ListScrollName;
        contentScroll.gameObject.name = ContentScrollName;
        Split(listScroll, contentScroll);

        var listHeader = UnityEngine.Object.Instantiate(headerTemplate, listScroll.content, false);
        listHeader.name = "group_SettingsHeader CatLibModList";
        listHeader.SetActive(true);
        var contentHeader = UnityEngine.Object.Instantiate(headerTemplate, contentScroll.content, false);
        contentHeader.name = "group_SettingsHeader CatLibModSettings";
        contentHeader.SetActive(true);

        Selectable firstSelected = panel.GetComponentInChildren<SelectableButton>(true);
        if (firstSelected == null)
        {
            firstSelected = panel.GetComponentInChildren<Selectable>(true);
        }

        var tabObject = UiClone.CloneInactive(templateTab.gameObject, TabName);
        UiClone.StripLocalization(tabObject);
        var tab = tabObject.GetComponent<TabInterface>();
        var button = tabObject.GetComponent<Button>();
        UiClone.DisablePersistentListeners(button.onClick);
        tab.AssociatedButton = button;
        tab.AssociatedPanel = panel;
        tab.ContentScrollRect = contentScroll;
        tab.FirstSelected = firstSelected;

        UiClone.Place(panel, templatePanel.transform.parent, -1);
        UiClone.Place(tabObject, templateTab.transform.parent, templateTab.transform.GetSiblingIndex() + 1);
        tab.SetTabActive(false, false);

        var bindings = new Il2CppEventBindings();
        bindings.Add<TabInterface.SelectedHandler>("TabInterface.Selected",
            new Action<TabInterface>(selected => options.TabInterface_OnSelected(selected)),
            tab.add_Selected, tab.remove_Selected);
        if (bindings.Failures.Count > 0)
        {
            throw new InvalidOperationException("Could not subscribe to the tab selection: " + bindings.Failures[0]);
        }

        tabs.Add(tab);

        var fitter = new TabBarFitter(options.TabsParent.TryCast<RectTransform>(), options.transform.TryCast<RectTransform>());
        var modsTab = new ModsTab(context, options, tab, panel, listScroll, contentScroll, listHeader, contentHeader, templates, fitter, bindings, log);
        modsTab.ApplyTexts();
        return modsTab;
    }

    private static GameObject FindPanelTemplate(OptionsInterface options)
    {
        var tabs = options._tabs;
        GameObject fallback = null;
        for (var index = 0; index < tabs.Count; index++)
        {
            var panel = tabs[index].AssociatedPanel;
            if (panel == null || FindDirectScroll(panel.transform) == null)
            {
                continue;
            }

            if (panel.GetComponent<AudioSettingsInterface>() != null)
            {
                return panel;
            }

            fallback ??= panel;
        }

        return fallback;
    }

    private static ScrollRect FindDirectScroll(Transform panel)
    {
        for (var index = 0; index < panel.childCount; index++)
        {
            var scroll = panel.GetChild(index).GetComponent<ScrollRect>();
            if (scroll != null)
            {
                return scroll;
            }
        }

        return null;
    }

    private static void DestroyGameInterfaces(GameObject panel)
    {
        UiClone.DestroyComponents<AudioSettingsInterface>(panel);
        UiClone.DestroyComponents<GameplaySettingsInterface>(panel);
        UiClone.DestroyComponents<ControlsSettingsInterface>(panel);
        UiClone.DestroyComponents<InputsSettingsInterface>(panel);
    }

    private static GameObject CaptureHeaderTemplate(ScrollRect scroll, Transform templates)
    {
        var content = scroll.content;
        for (var index = 0; index < content.childCount; index++)
        {
            var child = content.GetChild(index).gameObject;
            if (!child.name.StartsWith("group_SettingsHeader", StringComparison.Ordinal))
            {
                continue;
            }

            var template = UnityEngine.Object.Instantiate(child, templates, false);
            template.name = "template_Header";
            UiClone.StripLocalization(template);
            return template;
        }

        return null;
    }

    private static void Split(ScrollRect listScroll, ScrollRect contentScroll)
    {
        var source = contentScroll.transform.TryCast<RectTransform>();
        var size = source.sizeDelta;
        var position = source.anchoredPosition;
        var listWidth = Mathf.Round(size.x * ListWidthRatio);
        var contentWidth = size.x - listWidth - PaneGap;

        var list = listScroll.transform.TryCast<RectTransform>();
        list.sizeDelta = new Vector2(listWidth, size.y);
        list.anchoredPosition = new Vector2(position.x - size.x / 2f + listWidth / 2f, position.y);

        source.sizeDelta = new Vector2(contentWidth, size.y);
        source.anchoredPosition = new Vector2(position.x + size.x / 2f - contentWidth / 2f, position.y);

        FitContentWidth(listScroll, listWidth);
        FitContentWidth(contentScroll, contentWidth);
    }

    private static void FitContentWidth(ScrollRect scroll, float width)
    {
        var content = scroll.content;
        content.sizeDelta = new Vector2(width - ScrollbarAllowance, content.sizeDelta.y);
        content.anchoredPosition = new Vector2(0f, content.anchoredPosition.y);
    }
}
