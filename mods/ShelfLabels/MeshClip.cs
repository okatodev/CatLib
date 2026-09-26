using System;
using System.Collections.Generic;

namespace ShelfLabels;

public sealed class MeshClip
{
    private MeshClip(int[][] triangles, List<(int A, int B, float T)> added, int removed, int split)
    {
        Triangles = triangles;
        Added = added;
        Removed = removed;
        Split = split;
    }

    public int[][] Triangles { get; }

    public IReadOnlyList<(int A, int B, float T)> Added { get; }

    public int Removed { get; }

    public int Split { get; }

    public bool Changed => Removed > 0 || Split > 0;

    public static MeshClip KeepAbove(IReadOnlyList<float> heights, IReadOnlyList<int[]> submeshes, float threshold)
    {
        if (heights == null || submeshes == null)
        {
            throw new ArgumentNullException(heights == null ? nameof(heights) : nameof(submeshes));
        }

        var added = new List<(int A, int B, float T)>();
        var edges = new Dictionary<(int, int), int>();
        var result = new int[submeshes.Count][];
        var removed = 0;
        var split = 0;

        int Cross(int a, int b)
        {
            var key = a < b ? (a, b) : (b, a);
            if (edges.TryGetValue(key, out var existing))
            {
                return existing;
            }

            var from = key.Item1;
            var to = key.Item2;
            var span = heights[to] - heights[from];
            var t = Math.Abs(span) < 1e-9f ? 0.5f : (threshold - heights[from]) / span;
            t = Math.Clamp(t, 0f, 1f);
            var index = heights.Count + added.Count;
            added.Add((from, to, t));
            edges[key] = index;
            return index;
        }

        for (var submesh = 0; submesh < submeshes.Count; submesh++)
        {
            var source = submeshes[submesh] ?? Array.Empty<int>();
            var output = new List<int>(source.Length);
            for (var index = 0; index + 2 < source.Length; index += 3)
            {
                var corners = new[] { source[index], source[index + 1], source[index + 2] };
                var inside = new bool[3];
                var count = 0;
                for (var corner = 0; corner < 3; corner++)
                {
                    inside[corner] = heights[corners[corner]] >= threshold;
                    count += inside[corner] ? 1 : 0;
                }

                if (count == 3)
                {
                    output.Add(corners[0]);
                    output.Add(corners[1]);
                    output.Add(corners[2]);
                    continue;
                }

                if (count == 0)
                {
                    removed++;
                    continue;
                }

                split++;
                if (count == 1)
                {
                    var first = Array.IndexOf(inside, true);
                    var i = corners[first];
                    var j = corners[(first + 1) % 3];
                    var k = corners[(first + 2) % 3];
                    output.Add(i);
                    output.Add(Cross(i, j));
                    output.Add(Cross(i, k));
                }
                else
                {
                    var outside = Array.IndexOf(inside, false);
                    var k = corners[outside];
                    var i = corners[(outside + 1) % 3];
                    var j = corners[(outside + 2) % 3];
                    var p = Cross(j, k);
                    var q = Cross(k, i);
                    output.Add(i);
                    output.Add(j);
                    output.Add(p);
                    output.Add(i);
                    output.Add(p);
                    output.Add(q);
                }
            }

            result[submesh] = output.ToArray();
        }

        return new MeshClip(result, added, removed, split);
    }
}
