using System;
using System.Collections.Generic;
using CatLib.Il2Cpp;
using CatLib.Tests.Framework;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace CatLib.Tests.Suites.Interop;

public sealed class Il2CppArraysTest : TestCase
{
    public override string Suite => "Interop";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var grid = NewArray2D(IL2CPP.GetIl2CppClass("mscorlib.dll", "System", "Boolean"), 3, 4);
        Assert.True(Il2CppArrays.TryGetRank(grid, out var rank, out var elementSize), "A native two-dimensional array is recognized");
        context.Note($"bool[3,4]: rank {rank}, element size {elementSize}");
        Assert.Equal(2, rank, "Rank");
        Assert.Equal(1, elementSize, "A bool element is one byte");
        Assert.True(Il2CppArrays.TryGetBounds(grid, out var rows, out var columns), "Bounds are read");
        Assert.Equal((3, 4), (rows, columns), "Rows and columns");

        Assert.True(Il2CppArrays.TrySet(grid, 2, 3, true), "The last cell is written");
        Assert.True(Il2CppArrays.TrySet(grid, 0, 1, true), "A cell in the first row is written");
        Assert.True(Il2CppArrays.TryRead2D<bool>(grid, out var values), "The array is read back");
        Assert.True(values[2, 3] && values[0, 1] && !values[1, 1], "Written cells hold their values, others stay false");

        Assert.False(Il2CppArrays.TrySet(grid, 3, 0, true), "A row past the end is refused");
        Assert.False(Il2CppArrays.TrySet(grid, 0, -1, true), "A negative column is refused");
        Assert.False(Il2CppArrays.TryRead2D<Vector3>(grid, out _), "Reading with a wrong element type is refused");
        Assert.False(Il2CppArrays.TrySet(grid, 0, 0, 7), "Writing an int into a bool array is refused");

        var flat = new Il2CppStructArray<bool>(4);
        Assert.False(Il2CppArrays.TryGetBounds(flat, out _, out _), "A one-dimensional array has no two-dimensional bounds");
        Assert.False(Il2CppArrays.TryRead2D<bool>(flat, out _), "A one-dimensional array is not read as a grid");
        Assert.False(Il2CppArrays.TryGetRank(new Il2CppSystem.Object(), out _, out _), "A plain object is not an array");
        Assert.False(Il2CppArrays.TryGetRank(null, out _, out _), "Null is not an array");

        var vectors = NewArray2D(IL2CPP.GetIl2CppClass("UnityEngine.CoreModule.dll", "UnityEngine", "Vector3"), 2, 2);
        Assert.True(Il2CppArrays.TryGetRank(vectors, out _, out var vectorSize), "A Vector3 grid is recognized");
        context.Note($"Vector3[2,2]: element size {vectorSize}");
        Assert.Equal(12, vectorSize, "A Vector3 element is twelve bytes");
        Assert.True(Il2CppArrays.TrySet(vectors, 1, 0, new Vector3(1f, 2f, 3f)), "A Vector3 grid is written");
        Assert.True(Il2CppArrays.TryRead2D<Vector3>(vectors, out var read) && read[1, 0] == new Vector3(1f, 2f, 3f), "A Vector3 grid is read back");
        Assert.False(Il2CppArrays.TryRead2D<bool>(vectors, out _), "A Vector3 grid is not read as bool");
        yield break;
    }

    private static Il2CppSystem.Object NewArray2D(IntPtr elementClass, int rows, int columns)
    {
        if (elementClass == IntPtr.Zero)
        {
            throw new InvalidOperationException("Element class was not found");
        }

        var arrayClass = IL2CPP.il2cpp_array_class_get(elementClass, 2);
        var lengths = new[] { (ulong)rows, (ulong)columns };
        var lowerBounds = new ulong[2];
        var pointer = IL2CPP.il2cpp_array_new_full(arrayClass, ref lengths[0], ref lowerBounds[0]);
        return new Il2CppSystem.Object(pointer);
    }
}
