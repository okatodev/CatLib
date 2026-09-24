using UnityEngine;

namespace CatLib.Tests.Suites.Ui;

internal readonly struct RectExtent
{
    private RectExtent(float minX, float maxX, float minY, float maxY)
    {
        MinX = minX;
        MaxX = maxX;
        MinY = minY;
        MaxY = maxY;
    }

    public float MinX { get; }

    public float MaxX { get; }

    public float MinY { get; }

    public float MaxY { get; }

    public float Width => MaxX - MinX;

    public float Height => MaxY - MinY;

    public static RectExtent Of(RectTransform target, RectTransform space)
    {
        var rect = target.rect;
        var minX = float.MaxValue;
        var maxX = float.MinValue;
        var minY = float.MaxValue;
        var maxY = float.MinValue;

        foreach (var corner in new[]
                 {
                     new Vector3(rect.xMin, rect.yMin, 0f),
                     new Vector3(rect.xMin, rect.yMax, 0f),
                     new Vector3(rect.xMax, rect.yMin, 0f),
                     new Vector3(rect.xMax, rect.yMax, 0f)
                 })
        {
            var local = space.InverseTransformPoint(target.TransformPoint(corner));
            minX = Mathf.Min(minX, local.x);
            maxX = Mathf.Max(maxX, local.x);
            minY = Mathf.Min(minY, local.y);
            maxY = Mathf.Max(maxY, local.y);
        }

        return new RectExtent(minX, maxX, minY, maxY);
    }

    public override string ToString() => $"x {MinX:0}..{MaxX:0}, y {MinY:0}..{MaxY:0}";
}
