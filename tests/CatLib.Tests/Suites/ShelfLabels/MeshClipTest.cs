using System.Collections.Generic;
using System.Linq;
using CatLib.Tests.Framework;
using ShelfLabels;

namespace CatLib.Tests.Suites.ShelfLabels;

public sealed class MeshClipTest : TestCase
{
    public override string Suite => "ShelfLabels";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var heights = new[] { 1f, 1f, 1f, -1f, -1f, -1f, 1f, 1f, -1f, 0f };
        var frame = new[] { 0, 1, 2 };
        var stand = new[] { 3, 4, 5 };
        var oneInside = new[] { 6, 3, 4 };
        var twoInside = new[] { 6, 7, 8 };

        var keep = MeshClip.KeepAbove(heights, new[] { frame }, 0f);
        Assert.False(keep.Changed, "A frame above the cut is untouched");
        Assert.SequenceEqual(frame, keep.Triangles[0], "Same triangle");

        var drop = MeshClip.KeepAbove(heights, new[] { frame, stand }, 0f);
        Assert.Equal(1, drop.Removed, "A stand entirely below the cut is removed");
        Assert.Equal(0, drop.Triangles[1].Length, "Its submesh becomes empty");
        Assert.Equal(3, drop.Triangles[0].Length, "The frame stays");

        var one = MeshClip.KeepAbove(heights, new[] { oneInside }, 0f);
        Assert.Equal(1, one.Split, "A leg crossing the cut is split");
        Assert.Equal(3, one.Triangles[0].Length, "One vertex above gives one smaller triangle");
        Assert.Equal(6, one.Triangles[0][0], "It keeps the vertex above and the winding");
        Assert.Equal(2, one.Added.Count, "Two new vertices on the cut");
        Assert.True(one.Added.All(added => System.Math.Abs(added.T - 0.5f) < 1e-5f || System.Math.Abs(added.T - 0.5f) < 1e-5f), "They sit halfway on edges from 1 to -1");

        var two = MeshClip.KeepAbove(heights, new[] { twoInside }, 0f);
        Assert.Equal(6, two.Triangles[0].Length, "Two vertices above give two triangles");
        Assert.SequenceEqual(new[] { 6, 7 }, two.Triangles[0].Take(2), "Winding of the kept edge is preserved");

        var shared = MeshClip.KeepAbove(heights, new[] { new[] { 6, 3, 4, 6, 4, 3 } }, 0f);
        Assert.Equal(2, shared.Added.Count, "Triangles sharing a crossing edge share the new vertex");

        var onCut = MeshClip.KeepAbove(heights, new[] { new[] { 9, 0, 1 } }, 0f);
        Assert.False(onCut.Changed, "A vertex exactly on the cut counts as kept");
        yield break;
    }
}
