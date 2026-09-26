using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShelfLabels;

public static class StandCutter
{
    private static readonly Dictionary<IntPtr, Mesh> Cache = new();

    public const float BoundsTolerance = 0.01f;

    public static Mesh Cut(Mesh source, Transform transform, Vector3 holderPosition, Vector3 up, float threshold, out string result)
    {
        if (Cache.TryGetValue(source.Pointer, out var cached) && cached != null && !cached.WasCollected)
        {
            result = "cached";
            return cached;
        }

        float Height(Vector3 vertex) => Vector3.Dot(transform.TransformPoint(vertex) - holderPosition, up);
        Vector3 Lift(Vector3 vertex, float height) => vertex + transform.InverseTransformVector(up * (threshold - height));

        var mesh = source.isReadable ? Clip(source, Height, threshold, out result) : FlattenFromGpu(source, Height, Lift, threshold, out result);
        if (mesh != null)
        {
            Cache[source.Pointer] = mesh;
        }

        return mesh;
    }

    private static Mesh Clip(Mesh source, Func<Vector3, float> height, float threshold, out string result)
    {
        var sourceVertices = source.vertices;
        var count = sourceVertices.Length;
        var vertices = new Vector3[count];
        var heights = new float[count];
        for (var index = 0; index < count; index++)
        {
            vertices[index] = sourceVertices[index];
            heights[index] = height(vertices[index]);
        }

        var submeshes = new List<int[]>();
        for (var submesh = 0; submesh < source.subMeshCount; submesh++)
        {
            var triangles = source.GetTriangles(submesh);
            var copy = new int[triangles.Length];
            for (var index = 0; index < copy.Length; index++)
            {
                copy[index] = triangles[index];
            }

            submeshes.Add(copy);
        }

        var clip = MeshClip.KeepAbove(heights, submeshes, threshold);
        if (!clip.Changed)
        {
            result = "nothing below the frame";
            return null;
        }

        var total = count + clip.Added.Count;
        var mesh = new Mesh { name = source.name + " (ShelfLabels, no stand)" };
        if (total > 65000)
        {
            mesh.indexFormat = IndexFormat.UInt32;
        }

        mesh.vertices = Extend(vertices, clip, (a, b, t) => Vector3.Lerp(a, b, t));
        var normals = Read(source.normals);
        if (normals.Length == count)
        {
            mesh.normals = Extend(normals, clip, (a, b, t) => Vector3.Lerp(a, b, t).normalized);
        }

        var tangents = Read(source.tangents);
        if (tangents.Length == count)
        {
            mesh.tangents = Extend(tangents, clip, (a, b, t) =>
            {
                var direction = Vector3.Lerp(new Vector3(a.x, a.y, a.z), new Vector3(b.x, b.y, b.z), t).normalized;
                return new Vector4(direction.x, direction.y, direction.z, a.w);
            });
        }

        var uv = Read(source.uv);
        if (uv.Length == count)
        {
            mesh.uv = Extend(uv, clip, (a, b, t) => Vector2.Lerp(a, b, t));
        }

        var uv2 = Read(source.uv2);
        if (uv2.Length == count)
        {
            mesh.uv2 = Extend(uv2, clip, (a, b, t) => Vector2.Lerp(a, b, t));
        }

        var colors = Read(source.colors);
        if (colors.Length == count)
        {
            mesh.colors = Extend(colors, clip, (a, b, t) => Color.Lerp(a, b, t));
        }

        mesh.subMeshCount = clip.Triangles.Length;
        for (var submesh = 0; submesh < clip.Triangles.Length; submesh++)
        {
            mesh.SetTriangles(clip.Triangles[submesh], submesh);
        }

        mesh.RecalculateBounds();
        result = $"removed {clip.Removed} and split {clip.Split} triangle(s)";
        return mesh;
    }

    private static Mesh FlattenFromGpu(Mesh source, Func<Vector3, float> height, Func<Vector3, float, Vector3> lift, float threshold, out string result)
    {
        var count = source.vertexCount;
        if (source.GetVertexAttributeFormat(VertexAttribute.Position) != VertexAttributeFormat.Float32 || source.GetVertexAttributeDimension(VertexAttribute.Position) < 3)
        {
            result = $"positions are stored as {source.GetVertexAttributeFormat(VertexAttribute.Position)} x{source.GetVertexAttributeDimension(VertexAttribute.Position)}";
            return null;
        }

        var positionStream = source.GetVertexAttributeStream(VertexAttribute.Position);
        var positionOffset = source.GetVertexAttributeOffset(VertexAttribute.Position);
        var streams = new List<byte[]>();
        for (var stream = 0; stream < source.vertexBufferCount; stream++)
        {
            var stride = source.GetVertexBufferStride(stream);
            streams.Add(ReadBuffer(source.GetVertexBufferImpl(stream), stride * count));
        }

        var indexBuffer = source.GetIndexBufferImpl();
        var indexSize = source.indexFormat == IndexFormat.UInt16 ? 2 : 4;
        var indexCount = indexBuffer.count * indexBuffer.stride / indexSize;
        var indices = ReadBuffer(indexBuffer, indexCount * indexSize);

        var positions = streams[positionStream];
        var positionStride = source.GetVertexBufferStride(positionStream);
        var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        var lifted = 0;
        for (var index = 0; index < count; index++)
        {
            var at = index * positionStride + positionOffset;
            var vertex = new Vector3(BitConverter.ToSingle(positions, at), BitConverter.ToSingle(positions, at + 4), BitConverter.ToSingle(positions, at + 8));
            min = Vector3.Min(min, vertex);
            max = Vector3.Max(max, vertex);
            var vertexHeight = height(vertex);
            if (vertexHeight >= threshold)
            {
                continue;
            }

            var moved = lift(vertex, vertexHeight);
            BitConverter.GetBytes(moved.x).CopyTo(positions, at);
            BitConverter.GetBytes(moved.y).CopyTo(positions, at + 4);
            BitConverter.GetBytes(moved.z).CopyTo(positions, at + 8);
            lifted++;
        }

        var expected = source.bounds;
        if (Vector3.Distance(min, expected.min) > BoundsTolerance || Vector3.Distance(max, expected.max) > BoundsTolerance)
        {
            result = $"the data read from the video card does not match the mesh (bounds {min}..{max}, expected {expected.min}..{expected.max})";
            return null;
        }

        if (lifted == 0)
        {
            result = "nothing below the frame";
            return null;
        }

        var mesh = new Mesh { name = source.name + " (ShelfLabels, no stand)" };
        mesh.SetVertexBufferParams(count, source.GetVertexAttributes());
        for (var stream = 0; stream < streams.Count; stream++)
        {
            Upload(streams[stream], pointer => mesh.InternalSetVertexBufferData(stream, pointer, 0, 0, streams[stream].Length, 1, MeshUpdateFlags.DontValidateIndices));
        }

        mesh.SetIndexBufferParams(indexCount, source.indexFormat);
        Upload(indices, pointer => mesh.InternalSetIndexBufferData(pointer, 0, 0, indexCount, indexSize, MeshUpdateFlags.DontValidateIndices));
        mesh.subMeshCount = source.subMeshCount;
        for (var submesh = 0; submesh < source.subMeshCount; submesh++)
        {
            mesh.SetSubMesh(submesh, source.GetSubMesh(submesh), MeshUpdateFlags.Default);
        }

        mesh.RecalculateBounds();
        result = $"lifted {lifted} of {count} vertices to the cut, read from the video card";
        return mesh;
    }

    private static byte[] ReadBuffer(GraphicsBuffer buffer, int bytes)
    {
        try
        {
            var data = new Il2CppStructArray<byte>(bytes);
            buffer.InternalGetData(new Il2CppSystem.Array(data.Pointer), 0, 0, bytes, 1);
            var result = new byte[bytes];
            for (var index = 0; index < bytes; index++)
            {
                result[index] = data[index];
            }

            return result;
        }
        finally
        {
            buffer.Release();
        }
    }

    private static void Upload(byte[] data, Action<IntPtr> upload)
    {
        var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
        try
        {
            upload(handle.AddrOfPinnedObject());
        }
        finally
        {
            handle.Free();
        }
    }

    private static T[] Read<T>(Il2CppStructArray<T> source) where T : unmanaged
    {
        if (source == null)
        {
            return Array.Empty<T>();
        }

        var result = new T[source.Length];
        for (var index = 0; index < result.Length; index++)
        {
            result[index] = source[index];
        }

        return result;
    }

    private static T[] Extend<T>(T[] source, MeshClip clip, Func<T, T, float, T> lerp)
    {
        var result = new T[source.Length + clip.Added.Count];
        Array.Copy(source, result, source.Length);
        for (var index = 0; index < clip.Added.Count; index++)
        {
            var (a, b, t) = clip.Added[index];
            result[source.Length + index] = lerp(source[a], source[b], t);
        }

        return result;
    }
}
