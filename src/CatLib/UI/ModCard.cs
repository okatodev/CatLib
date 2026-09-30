using CatLib.Config;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CatLib.UI;

internal sealed class ModCard
{
    public const float Height = 96f;
    public const float TitleSize = 34f;
    public const float DetailSize = 22f;
    public const float IconSize = 84f;
    public const float IconGap = 16f;
    public const float PlaceholderSize = 64f;

    public ModCard(RowTemplates templates, Transform parent, float width)
    {
        Root = new GameObject("group_CatLibModCard");
        Root.AddComponent<RectTransform>();
        Root.transform.SetParent(parent, false);
        Root.transform.SetSiblingIndex(0);
        RowSizer.Fit(Root, width, Height);

        Title = templates.CreateText(Root.transform, "text_CatLibModTitle");
        Place(Title, 0f, 48f, TitleSize, TextAlignmentOptions.Left);
        Version = templates.CreateText(Root.transform, "text_CatLibModVersion");
        Place(Version, 0f, 48f, DetailSize, TextAlignmentOptions.Right);
        Status = templates.CreateText(Root.transform, "text_CatLibModStatus");
        Place(Status, 52f, 36f, DetailSize, TextAlignmentOptions.Left);
        Author = templates.CreateText(Root.transform, "text_CatLibModAuthor");
        Place(Author, 52f, 36f, DetailSize, TextAlignmentOptions.Right);
        var authorColor = Author.color;
        Author.color = new Color(authorColor.r, authorColor.g, authorColor.b, authorColor.a * 0.7f);

        var iconObject = new GameObject("image_CatLibModIcon");
        var rect = iconObject.AddComponent<RectTransform>();
        rect.SetParent(Root.transform, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(0f, -(Height - IconSize) / 2f);
        rect.sizeDelta = new Vector2(IconSize, IconSize);
        Icon = iconObject.AddComponent<Image>();
        Icon.preserveAspect = true;
        Icon.raycastTarget = false;
        Placeholder = templates.CreateText(iconObject.transform, "text_CatLibModIconPlaceholder");
        var placeholderRect = Placeholder.rectTransform;
        placeholderRect.anchorMin = Vector2.zero;
        placeholderRect.anchorMax = Vector2.one;
        placeholderRect.pivot = new Vector2(0.5f, 0.5f);
        placeholderRect.offsetMin = Vector2.zero;
        placeholderRect.offsetMax = Vector2.zero;
        Placeholder.text = "?";
        Placeholder.fontSize = PlaceholderSize;
        Placeholder.enableAutoSizing = false;
        Placeholder.alignment = TextAlignmentOptions.Center;
        Placeholder.raycastTarget = false;
        var placeholderColor = Placeholder.color;
        Placeholder.color = new Color(placeholderColor.r, placeholderColor.g, placeholderColor.b, placeholderColor.a * 0.45f);
        iconObject.SetActive(false);
    }

    public Image Icon { get; }

    public TMP_Text Placeholder { get; }

    public TMP_Text Author { get; }

    public GameObject Root { get; }

    public TMP_Text Title { get; }

    public TMP_Text Version { get; }

    public TMP_Text Status { get; }

    public void Show(CatSettings settings, string languageCode)
    {
        if (settings == null)
        {
            Icon.gameObject.SetActive(false);
            Indent(Title, 0f);
            Indent(Status, 0f);
            Set(Author, string.Empty);
            Set(Title, UiText.Get(UiText.SelectMod, languageCode));
            Set(Version, string.Empty);
            Set(Status, string.Empty);
            return;
        }

        var sprite = ModIcons.For(settings);
        Icon.sprite = sprite;
        Icon.enabled = sprite != null;
        Placeholder.gameObject.SetActive(sprite == null);
        Icon.gameObject.SetActive(true);
        var inset = IconSize + IconGap;
        Set(Author, string.IsNullOrWhiteSpace(settings.Author) ? string.Empty : UiText.Format(UiText.CardAuthor, languageCode, settings.Author));
        Indent(Title, inset);
        Indent(Status, inset);
        Set(Title, CatLib.Localization.SettingTexts.ModName(settings, languageCode));
        Set(Version, string.IsNullOrEmpty(settings.Version) ? string.Empty : UiText.Format(UiText.Version, languageCode, settings.Version));
        Set(Status, ContextText.CardStatus(settings, languageCode));
    }

    private static void Set(TMP_Text text, string value)
    {
        if (text.text != value)
        {
            text.text = value;
        }
    }

    private static void Indent(TMP_Text text, float left)
    {
        var rect = text.rectTransform;
        if (rect.offsetMin.x != left)
        {
            rect.offsetMin = new Vector2(left, rect.offsetMin.y);
        }
    }

    private static void Place(TMP_Text text, float top, float height, float fontSize, TextAlignmentOptions alignment)
    {
        var rect = text.transform.TryCast<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(0f, height);
        rect.anchoredPosition = new Vector2(0f, -top);
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
    }
}
