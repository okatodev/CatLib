using UnityEngine;
using UnityEngine.UI;

namespace CatLib.UI;

internal static class ScrollReveal
{
    public const float Margin = 12f;

    public static RectTransform ChildOf(Transform content, Transform selected)
    {
        for (var current = selected; current != null; current = current.parent)
        {
            var parent = current.parent;
            if (parent != null && parent.Pointer == content.Pointer)
            {
                return current.TryCast<RectTransform>();
            }
        }

        return null;
    }

    public static float Offset(float viewMin, float viewMax, float targetMin, float targetMax, float margin)
    {
        var view = viewMax - viewMin;
        var target = targetMax - targetMin + margin * 2f;
        if (target >= view || targetMax + margin > viewMax)
        {
            return viewMax - (targetMax + margin);
        }

        if (targetMin - margin < viewMin)
        {
            return viewMin - (targetMin - margin);
        }

        return 0f;
    }

    public static bool Reveal(ScrollRect scroll, RectTransform target, bool toTop)
    {
        var content = scroll == null ? null : scroll.content;
        var viewport = scroll == null ? null : ModsTabBuilder.ViewportOf(scroll);
        if (content == null || viewport == null || target == null)
        {
            return false;
        }

        if (toTop)
        {
            if (scroll.verticalNormalizedPosition < 0.999f)
            {
                scroll.StopMovement();
                scroll.verticalNormalizedPosition = 1f;
                return true;
            }

            return false;
        }

        var viewRect = viewport.rect;
        var viewMin = content.InverseTransformPoint(viewport.TransformPoint(new Vector3(0f, viewRect.yMin, 0f))).y;
        var viewMax = content.InverseTransformPoint(viewport.TransformPoint(new Vector3(0f, viewRect.yMax, 0f))).y;
        var targetRect = target.rect;
        var targetMin = content.InverseTransformPoint(target.TransformPoint(new Vector3(0f, targetRect.yMin, 0f))).y;
        var targetMax = content.InverseTransformPoint(target.TransformPoint(new Vector3(0f, targetRect.yMax, 0f))).y;
        var offset = Offset(viewMin, viewMax, targetMin, targetMax, Margin);
        if (System.Math.Abs(offset) < 0.5f)
        {
            return false;
        }

        scroll.StopMovement();
        var position = content.anchoredPosition;
        var scale = content.localScale.y;
        var contentHeight = content.rect.height * scale;
        var viewHeight = (viewMax - viewMin) * scale;
        var maximum = System.Math.Max(0f, contentHeight - viewHeight);
        var shifted = position.y + offset * scale;
        var anchoredTop = content.anchorMin.y > 0.5f && content.pivot.y > 0.5f;
        content.anchoredPosition = new Vector2(position.x, anchoredTop ? System.Math.Clamp(shifted, 0f, maximum) : shifted);
        return true;
    }
}
