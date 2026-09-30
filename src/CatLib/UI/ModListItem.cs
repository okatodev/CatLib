using System;
using CatLib.Config;
using TMPro;
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
        AddIcon(root, ModIcons.For(settings));
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

    public const float IconLeft = 52f;
    public const float IconSize = 38f;
    public const float LabelLeft = 100f;
    public const float BadgeHeight = 20f;
    public const float BadgeSize = 16f;
    public const float PlaceholderSize = 34f;
    public static readonly Color PausedColor = new(0.68f, 0.43f, 0.1f, 1f);

    public CatSettings Settings { get; }

    public Image Icon { get; private set; }

    public TMP_Text Placeholder { get; private set; }

    public TMP_Text Badge { get; private set; }

    public ModBadgeKind BadgeKind { get; private set; } = ModBadgeKind.None;

    public void UpdateBadge(string language)
    {
        var kind = ModBadge.For(Settings);
        if (kind == BadgeKind || Badge == null)
        {
            return;
        }

        BadgeKind = kind;
        var label = Root.transform.Find("Item Label")?.TryCast<RectTransform>();
        Badge.text = ModBadge.Text(kind, language);
        Badge.color = kind is ModBadgeKind.Paused or ModBadgeKind.Restart ? PausedColor : new Color(_baseColor.r, _baseColor.g, _baseColor.b, _baseColor.a * 0.6f);
        Badge.gameObject.SetActive(kind != ModBadgeKind.None);
        if (label != null)
        {
            label.offsetMin = new Vector2(label.offsetMin.x, kind == ModBadgeKind.None ? _labelBottom : _labelBottom + BadgeHeight - 4f);
        }
    }

    private float _labelBottom;
    private Color _baseColor;

    public GameObject Root { get; }

    public Toggle Toggle { get; }

    public bool IsSelected { get; private set; }

    private void AddIcon(GameObject root, Sprite sprite)
    {
        var label = root.transform.Find("Item Label")?.TryCast<RectTransform>();
        var middle = 0f;
        if (label != null)
        {
            middle = LabelMiddle(label);
            label.offsetMin = new Vector2(LabelLeft, label.offsetMin.y);
            _labelBottom = label.offsetMin.y;
            Badge = CloneText(label.gameObject, root.transform, "Item Badge");
            var badgeRect = Badge.rectTransform;
            badgeRect.anchorMin = new Vector2(0f, 0f);
            badgeRect.anchorMax = new Vector2(1f, 0f);
            badgeRect.pivot = new Vector2(0.5f, 0f);
            badgeRect.offsetMin = new Vector2(LabelLeft, 6f);
            badgeRect.offsetMax = new Vector2(label.offsetMax.x, 6f + BadgeHeight);
            Badge.fontSize = BadgeSize;
            Badge.alignment = TextAlignmentOptions.MidlineLeft;
            _baseColor = Badge.color;
            Badge.gameObject.SetActive(false);
        }

        var iconObject = new GameObject("Item Icon");
        var rect = iconObject.AddComponent<RectTransform>();
        rect.SetParent(root.transform, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(IconLeft, middle);
        rect.sizeDelta = new Vector2(IconSize, IconSize);
        Icon = iconObject.AddComponent<Image>();
        Icon.sprite = sprite;
        Icon.preserveAspect = true;
        Icon.raycastTarget = false;
        Icon.enabled = sprite != null;
        if (sprite == null && label != null)
        {
            Placeholder = CloneText(label.gameObject, rect, "Item Icon Placeholder");
            var placeholderRect = Placeholder.rectTransform;
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.offsetMin = Vector2.zero;
            placeholderRect.offsetMax = Vector2.zero;
            Placeholder.text = "?";
            Placeholder.fontSize = PlaceholderSize;
            Placeholder.alignment = TextAlignmentOptions.Midline;
            Placeholder.margin = Vector4.zero;
            var color = Placeholder.color;
            Placeholder.color = new Color(color.r, color.g, color.b, color.a * 0.45f);
        }
    }

    internal static float LabelMiddle(RectTransform label) =>
        Math.Abs(label.anchorMin.y) < 0.001f && Math.Abs(label.anchorMax.y - 1f) < 0.001f
            ? (label.offsetMin.y + label.offsetMax.y) * 0.5f
            : 0f;

    internal static TMP_Text CloneText(GameObject template, Transform parent, string name)
    {
        var instance = UnityEngine.Object.Instantiate(template, parent, false);
        instance.name = name;
        UiClone.DestroyChildren(instance.transform);
        var text = instance.GetComponent<TMP_Text>();
        text.enableAutoSizing = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        return text;
    }

    public void SetSelected(bool selected)
    {
        IsSelected = selected;
        if (Toggle.isOn != selected)
        {
            Toggle.SetIsOnWithoutNotify(selected);
        }
    }
}
