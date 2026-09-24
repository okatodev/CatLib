using UnityEngine;
using UnityEngine.UI;

namespace CatLib.UI;

internal sealed class TabBarFitter
{
    public const float ScreenMargin = 20f;

    private readonly RectTransform _bar;
    private readonly RectTransform _canvas;
    private readonly Vector2 _originalPosition;
    private int _screenWidth;
    private int _screenHeight;
    private bool _fitted;

    public TabBarFitter(RectTransform bar, RectTransform canvas)
    {
        _bar = bar;
        _canvas = canvas;
        _originalPosition = bar.anchoredPosition;
    }

    public float Scale { get; private set; } = 1f;

    public bool IsFitted => _fitted;

    public void Invalidate() => _fitted = false;

    public bool Update()
    {
        if (!_bar.gameObject.activeInHierarchy)
        {
            return false;
        }

        if (_fitted && Screen.width == _screenWidth && Screen.height == _screenHeight)
        {
            return false;
        }

        _screenWidth = Screen.width;
        _screenHeight = Screen.height;
        _bar.localScale = Vector3.one;
        _bar.anchoredPosition = _originalPosition;
        LayoutRebuilder.ForceRebuildLayoutImmediate(_bar);

        if (!Measure(out var left, out var right))
        {
            return false;
        }

        var canvasRect = _canvas.rect;
        var limit = canvasRect.xMax - ScreenMargin;
        var scale = 1f;
        if (right > limit && right > left)
        {
            scale = Mathf.Max(0.5f, (limit - left) / (right - left));
            _bar.localScale = new Vector3(scale, scale, 1f);
            if (Measure(out var scaledLeft, out _))
            {
                _bar.anchoredPosition = _originalPosition + new Vector2(left - scaledLeft, 0f);
            }
        }

        Scale = scale;
        _fitted = true;
        return true;
    }

    public bool Measure(out float left, out float right)
    {
        left = float.MaxValue;
        right = float.MinValue;
        var found = false;

        for (var index = 0; index < _bar.childCount; index++)
        {
            var child = _bar.GetChild(index).TryCast<RectTransform>();
            if (child == null || !child.gameObject.activeSelf)
            {
                continue;
            }

            var rect = child.rect;
            var childLeft = _canvas.InverseTransformPoint(child.TransformPoint(new Vector3(rect.xMin, rect.center.y, 0f))).x;
            var childRight = _canvas.InverseTransformPoint(child.TransformPoint(new Vector3(rect.xMax, rect.center.y, 0f))).x;
            left = Mathf.Min(left, childLeft);
            right = Mathf.Max(right, childRight);
            found = true;
        }

        return found;
    }
}
