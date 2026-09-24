using System;
using CatLib.Il2Cpp;
using TMPro;
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
    public const float PaneInset = 90f;
    public const float ScrollbarAllowance = 40f;
    public const float ContextHeight = 72f;
    public const float ContextGap = 12f;
    public const float ContextFontSize = 22f;
    public const float StatusWidth = 960f;
    public const float StatusHeight = 70f;
    public const float StatusBottom = 95f;
    public const float StatusFontSize = 24f;

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
        var (listWidth, contentWidth) = Split(listScroll, contentScroll);
        EnableViewportMask(listScroll);
        EnableViewportMask(contentScroll);

        var listHeader = UnityEngine.Object.Instantiate(headerTemplate, listScroll.content, false);
        listHeader.name = "group_SettingsHeader CatLibModList";
        listHeader.SetActive(true);
        var rowTemplates = RowTemplates.Capture(options, headerTemplate, templates.transform);
        var contextText = CreateContextText(rowTemplates, panel.transform, contentScroll, contentWidth);
        var statusText = CreateStatusText(rowTemplates, panel.transform);

        var resetButton = panel.GetComponentInChildren<SelectableButton>(true);
        Selectable firstSelected = resetButton;
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
        var controller = new ModsPanel(options, tab, rowTemplates, listScroll, contentScroll, contextText, statusText, resetButton,
            listWidth - ScrollbarAllowance, contentWidth - ScrollbarAllowance, log);
        var modsTab = new ModsTab(context, options, tab, panel, listScroll, contentScroll, listHeader, templates, fitter, controller, bindings, log);
        modsTab.ApplyTexts();
        controller.RefreshList(true);
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

    private static (float ListWidth, float ContentWidth) Split(ScrollRect listScroll, ScrollRect contentScroll)
    {
        var source = contentScroll.transform.TryCast<RectTransform>();
        var size = source.sizeDelta;
        var position = source.anchoredPosition;
        var usable = size.x - PaneInset * 2f;
        var listWidth = Mathf.Round(usable * ListWidthRatio);
        var contentWidth = usable - listWidth - PaneGap;
        var left = position.x - size.x / 2f + PaneInset;

        var list = listScroll.transform.TryCast<RectTransform>();
        list.sizeDelta = new Vector2(listWidth, size.y);
        list.anchoredPosition = new Vector2(left + listWidth / 2f, position.y);

        source.sizeDelta = new Vector2(contentWidth, size.y);
        source.anchoredPosition = new Vector2(left + listWidth + PaneGap + contentWidth / 2f, position.y);

        FitContentWidth(listScroll, listWidth);
        FitContentWidth(contentScroll, contentWidth);
        return (listWidth, contentWidth);
    }

    private static TMP_Text CreateContextText(RowTemplates templates, Transform panel, ScrollRect contentScroll, float contentWidth)
    {
        var scroll = contentScroll.transform.TryCast<RectTransform>();
        var size = scroll.sizeDelta;
        var position = scroll.anchoredPosition;
        var reserved = ContextHeight + ContextGap;
        scroll.sizeDelta = new Vector2(size.x, size.y - reserved);
        scroll.anchoredPosition = new Vector2(position.x, position.y + reserved / 2f);

        var text = templates.CreateText(panel, "text_CatLibContext");
        var rect = text.transform.TryCast<RectTransform>();
        rect.anchorMin = scroll.anchorMin;
        rect.anchorMax = scroll.anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(contentWidth - ScrollbarAllowance, ContextHeight);
        rect.anchoredPosition = new Vector2(position.x, position.y - size.y / 2f + ContextHeight / 2f);
        text.fontSize = ContextFontSize;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    private static TMP_Text CreateStatusText(RowTemplates templates, Transform panel)
    {
        var text = templates.CreateText(panel, "text_CatLibStatus");
        var rect = text.transform.TryCast<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = new Vector2(StatusWidth, StatusHeight);
        rect.anchoredPosition = new Vector2(0f, StatusBottom);
        text.fontSize = StatusFontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    internal static RectTransform ViewportOf(ScrollRect scroll) =>
        scroll.viewport ?? scroll.content?.parent?.TryCast<RectTransform>();

    private static void EnableViewportMask(ScrollRect scroll)
    {
        var viewport = ViewportOf(scroll);
        if (viewport == null)
        {
            return;
        }

        var mask = viewport.GetComponent<Mask>();
        if (mask != null)
        {
            mask.enabled = true;
        }

        var graphic = viewport.GetComponent<Image>();
        if (graphic != null)
        {
            graphic.enabled = true;
        }
    }

    private static void FitContentWidth(ScrollRect scroll, float width)
    {
        var content = scroll.content;
        content.sizeDelta = new Vector2(width - ScrollbarAllowance, content.sizeDelta.y);
        content.anchoredPosition = new Vector2(0f, content.anchoredPosition.y);
    }
}
