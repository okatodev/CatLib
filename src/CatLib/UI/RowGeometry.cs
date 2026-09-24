using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CatLib.UI;

internal static class RowGeometry
{
    public const float DashHeight = 30f;
    public const float DashOffset = -15f;
    public const float TapePadding = 40f;

    public static void FitDash(GameObject row)
    {
        var dash = row.transform.Find("panel_Label/img_DashLine")?.TryCast<RectTransform>();
        if (dash == null)
        {
            return;
        }

        dash.anchorMin = Vector2.zero;
        dash.anchorMax = Vector2.one;
        dash.pivot = new Vector2(0.5f, 0.5f);
        dash.sizeDelta = new Vector2(0f, DashHeight);
        dash.anchoredPosition = new Vector2(0f, DashOffset);
    }

    public static bool FitSliderBackground(GameObject row, float templateRowWidth, float rowWidth)
    {
        if (templateRowWidth <= 0f)
        {
            return false;
        }

        var slider = row.GetComponentInChildren<Slider>(true);
        var background = slider == null ? null : slider.transform.Find("img_Bkg")?.TryCast<RectTransform>();
        var value = row.transform.Find("panel_Value")?.TryCast<RectTransform>();
        if (background == null || value == null)
        {
            return false;
        }

        var span = value.anchorMax.x - value.anchorMin.x;
        var shrink = (templateRowWidth - rowWidth) * span;
        var size = background.sizeDelta;
        background.sizeDelta = new Vector2(size.x + shrink, size.y - shrink);
        return true;
    }

    public static void SetTapeText(GameObject header, string text, float minWidth, float maxWidth)
    {
        var label = header.GetComponentInChildren<TMP_Text>(true);
        if (label == null)
        {
            return;
        }

        label.text = text;
        var tape = label.transform.parent?.TryCast<RectTransform>();
        var labelRect = label.transform.TryCast<RectTransform>();
        if (tape == null || labelRect == null || tape.Pointer == header.transform.Pointer)
        {
            return;
        }

        var preferred = label.GetPreferredValues(text).x;
        var width = Mathf.Clamp(preferred - labelRect.sizeDelta.x + TapePadding, minWidth, Mathf.Max(minWidth, maxWidth));
        tape.sizeDelta = new Vector2(width, tape.sizeDelta.y);
    }

    public static float TapeWidth(GameObject header)
    {
        var label = header.GetComponentInChildren<TMP_Text>(true);
        var tape = label == null ? null : label.transform.parent?.TryCast<RectTransform>();
        return tape == null ? 0f : tape.sizeDelta.x;
    }
}
