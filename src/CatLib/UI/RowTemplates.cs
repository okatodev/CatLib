using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CatLib.UI;

internal sealed class RowTemplates
{
    public const float RowHeight = 50f;
    public const float ListItemHeight = 78f;

    public const string ButtonCaptionName = "txt_InputKey";
    public const string ButtonIconName = "img_InputKey";

    private RowTemplates(GameObject header, GameObject toggle, GameObject slider, GameObject dropdown, GameObject text, GameObject listItem, GameObject button, float templateRowWidth)
    {
        TemplateRowWidth = templateRowWidth;
        Label = toggle.transform.Find("panel_Label")?.GetComponentInChildren<TMP_Text>(true)?.gameObject
                ?? throw new InvalidOperationException("The row label template was not found");
        TapeWidth = RowGeometry.TapeWidth(header);
        Header = header;
        Toggle = toggle;
        Slider = slider;
        Dropdown = dropdown;
        Text = text;
        ListItem = listItem;
        Button = button;
    }

    public float TemplateRowWidth { get; }

    public float TapeWidth { get; }

    public GameObject Header { get; }

    public GameObject Label { get; }

    public GameObject Toggle { get; }

    public GameObject Slider { get; }

    public GameObject Dropdown { get; }

    public GameObject Text { get; }

    public GameObject ListItem { get; }

    public GameObject Button { get; }

    public Vector2 ButtonSize { get; private set; }

    public static RowTemplates Capture(OptionsInterface options, GameObject header, Transform holder)
    {
        var gameplay = FindGameplay(options) ?? throw new InvalidOperationException("The gameplay settings panel was not found");

        var toggle = CaptureRow(gameplay.VSyncToggle?.transform, holder, "template_Toggle");
        var slider = CaptureRow(gameplay.FieldOfViewSlider?.transform, holder, "template_Slider");
        var dropdown = CaptureRow(gameplay.FullScreenDropdown?.transform, holder, "template_Dropdown");
        var text = CaptureRow(gameplay.PlayerNameField?.transform, holder, "template_Text");

        var itemSource = gameplay.FullScreenDropdown.template?.GetComponentInChildren<Toggle>(true)
                         ?? throw new InvalidOperationException("The dropdown list item template was not found");
        var listItem = UnityEngine.Object.Instantiate(itemSource.gameObject, holder, false);
        listItem.name = "template_ListItem";
        UiClone.StripLocalization(listItem);

        var templateRowWidth = ContainerWidth(gameplay.FieldOfViewSlider.transform);
        var button = CaptureButton(options, holder, out var buttonSize);
        return new RowTemplates(header, toggle, slider, dropdown, text, listItem, button, templateRowWidth) { ButtonSize = buttonSize };
    }

    private static GameObject CaptureButton(OptionsInterface options, Transform holder, out Vector2 size)
    {
        size = Vector2.zero;
        RebindButton source = null;
        var tabs = options._tabs;
        for (var index = 0; index < tabs.Count && source == null; index++)
        {
            var panel = tabs[index].AssociatedPanel;
            if (panel == null || panel.GetComponentInChildren<InputsSettingsInterface>(true) == null)
            {
                continue;
            }

            var buttons = panel.GetComponentsInChildren<RebindButton>(true);
            foreach (var candidate in buttons)
            {
                if (candidate._IsKeyboardMouse_k__BackingField && candidate.transform.Find(ButtonCaptionName) != null)
                {
                    source = candidate;
                    break;
                }

                source ??= candidate;
            }
        }

        if (source == null)
        {
            return null;
        }

        var template = UnityEngine.Object.Instantiate(source.gameObject, holder, false);
        template.name = "template_Button";
        UnityEngine.Object.DestroyImmediate(template.GetComponent<RebindButton>());
        UiClone.StripLocalization(template);
        var icon = template.transform.Find(ButtonIconName);
        if (icon != null)
        {
            icon.gameObject.SetActive(false);
        }

        var caption = template.transform.Find(ButtonCaptionName);
        if (caption != null)
        {
            caption.gameObject.SetActive(true);
            var text = caption.GetComponent<TMP_Text>();
            if (text != null)
            {
                text.enabled = true;
            }
        }

        var rect = source.transform.TryCast<RectTransform>();
        size = rect == null ? Vector2.zero : rect.rect.size;
        return template;
    }

    private static float ContainerWidth(Transform control)
    {
        var container = control?.parent?.parent?.parent?.TryCast<RectTransform>();
        return container == null ? 0f : container.rect.width;
    }

    public TMP_Text CreateText(Transform parent, string name)
    {
        var instance = UnityEngine.Object.Instantiate(Label, parent, false);
        instance.name = name;
        instance.SetActive(true);
        var text = instance.GetComponent<TMP_Text>();
        text.text = string.Empty;
        return text;
    }

    public static GameObject Create(GameObject template, Transform parent, string name)
    {
        var instance = UnityEngine.Object.Instantiate(template, parent, false);
        instance.name = name;
        instance.SetActive(true);
        return instance;
    }

    private static GameplaySettingsInterface FindGameplay(OptionsInterface options)
    {
        var tabs = options._tabs;
        for (var index = 0; index < tabs.Count; index++)
        {
            var panel = tabs[index].AssociatedPanel;
            var gameplay = panel == null ? null : panel.GetComponent<GameplaySettingsInterface>();
            if (gameplay != null)
            {
                return gameplay;
            }
        }

        return null;
    }

    private static GameObject CaptureRow(Transform control, Transform holder, string name)
    {
        var row = control?.parent?.parent;
        if (row == null)
        {
            throw new InvalidOperationException($"The row for {name} was not found");
        }

        var template = UnityEngine.Object.Instantiate(row.gameObject, holder, false);
        template.name = name;
        UiClone.StripLocalization(template);
        return template;
    }
}
