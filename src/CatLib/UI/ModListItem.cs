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
        var sprite = ModIcons.Trimmed(settings, out var scale);
        AddIcon(root, sprite, scale);
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

    public const float IconLeft = 50f;
    public const float IconSize = 50f;
    public const float LabelLeft = 108f;
    public const float NameBottom = 33f;
    public const float NameTop = 6f;
    public const float MetaBottom = 7f;
    public const float MetaHeight = 32f;
    public const float MetaSize = 17f;
    public const float MetaAlpha = 0.6f;
    public const float PlaceholderSize = 40f;

    public CatSettings Settings { get; }

    public Image Icon { get; private set; }

    public TMP_Text Placeholder { get; private set; }

    public TMP_Text Meta { get; private set; }

    public float IconScale { get; private set; } = 1f;

    public ModBadgeKind BadgeKind { get; private set; } = ModBadgeKind.None;

    public void UpdateBadge(string language)
    {
        BadgeKind = ModBadge.For(Settings);
        if (Meta == null)
        {
            return;
        }

        var text = ModListMeta.Text(Settings.Version, ModsPanel.VisibleSettings(Settings).Count, BadgeKind, language);
        if (Meta.text != text)
        {
            Meta.text = text;
        }
    }

    public GameObject Root { get; }

    public Toggle Toggle { get; }

    public bool IsSelected { get; private set; }

    private void AddIcon(GameObject root, Sprite sprite, float scale)
    {
        var label = root.transform.Find("Item Label")?.TryCast<RectTransform>();
        var middle = 0f;
        if (label != null)
        {
            middle = LabelMiddle(label);
            label.offsetMin = new Vector2(LabelLeft, NameBottom);
            label.offsetMax = new Vector2(label.offsetMax.x, -NameTop);
            var name = label.GetComponent<TMP_Text>();
            if (name != null)
            {
                name.textWrappingMode = TextWrappingModes.NoWrap;
                name.overflowMode = TextOverflowModes.Ellipsis;
            }

            Meta = CloneText(label.gameObject, root.transform, "Item Meta");
            var metaRect = Meta.rectTransform;
            metaRect.anchorMin = new Vector2(0f, 0f);
            metaRect.anchorMax = new Vector2(1f, 0f);
            metaRect.pivot = new Vector2(0.5f, 0f);
            metaRect.offsetMin = new Vector2(LabelLeft, MetaBottom);
            metaRect.offsetMax = new Vector2(label.offsetMax.x, MetaBottom + MetaHeight);
            Meta.fontSize = MetaSize;
            Meta.fontStyle = FontStyles.Normal;
            Meta.richText = true;
            Meta.alignment = TextAlignmentOptions.MidlineLeft;
            var color = Meta.color;
            Meta.color = new Color(color.r, color.g, color.b, color.a * MetaAlpha);
            Meta.text = string.Empty;
        }

        IconScale = sprite == null ? 1f : scale;
        var side = IconSize * IconScale;
        var iconObject = new GameObject("Item Icon");
        var rect = iconObject.AddComponent<RectTransform>();
        rect.SetParent(root.transform, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(IconLeft + IconSize / 2f, middle);
        rect.sizeDelta = new Vector2(side, side);
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
