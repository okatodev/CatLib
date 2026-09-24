using UnityEngine;
using UnityEngine.UI;

namespace CatLib.UI;

internal static class RowSizer
{
    public static void Fit(GameObject row, float width, float height)
    {
        var rect = row.transform.TryCast<RectTransform>();
        if (rect != null)
        {
            rect.sizeDelta = new Vector2(width, height);
        }

        var layout = row.GetComponent<LayoutElement>() ?? row.AddComponent<LayoutElement>();
        layout.minHeight = height;
        layout.preferredHeight = height;
        layout.preferredWidth = width;
    }
}
