using System;
using UnityEngine;
using UnityEngine.UI;

namespace CatLib.UI;

internal sealed class RowTemplates
{
    public const float RowHeight = 50f;
    public const float ListItemHeight = 60f;

    private RowTemplates(GameObject header, GameObject toggle, GameObject slider, GameObject dropdown, GameObject text, GameObject listItem, float templateRowWidth)
    {
        TemplateRowWidth = templateRowWidth;
        TapeWidth = RowGeometry.TapeWidth(header);
        Header = header;
        Toggle = toggle;
        Slider = slider;
        Dropdown = dropdown;
        Text = text;
        ListItem = listItem;
    }

    public float TemplateRowWidth { get; }

    public float TapeWidth { get; }

    public GameObject Header { get; }

    public GameObject Toggle { get; }

    public GameObject Slider { get; }

    public GameObject Dropdown { get; }

    public GameObject Text { get; }

    public GameObject ListItem { get; }

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
        return new RowTemplates(header, toggle, slider, dropdown, text, listItem, templateRowWidth);
    }

    private static float ContainerWidth(Transform control)
    {
        var container = control?.parent?.parent?.parent?.TryCast<RectTransform>();
        return container == null ? 0f : container.rect.width;
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
