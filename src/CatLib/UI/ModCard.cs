using CatLib.Config;
using TMPro;
using UnityEngine;

namespace CatLib.UI;

internal sealed class ModCard
{
    public const float Height = 96f;
    public const float TitleSize = 34f;
    public const float DetailSize = 22f;

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
    }

    public GameObject Root { get; }

    public TMP_Text Title { get; }

    public TMP_Text Version { get; }

    public TMP_Text Status { get; }

    public void Show(CatSettings settings, string languageCode)
    {
        if (settings == null)
        {
            Set(Title, UiText.Get(UiText.SelectMod, languageCode));
            Set(Version, string.Empty);
            Set(Status, string.Empty);
            return;
        }

        Set(Title, settings.DisplayName);
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
