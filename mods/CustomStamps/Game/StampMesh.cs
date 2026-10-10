using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.Rendering;

namespace CustomStamps.Game;

internal static class StampMesh
{
    public const float FaceOffsetShare = 0.002f;

    private static readonly Dictionary<IntPtr, Mesh> Quads = new();

    public static int Fit(StampHelper helper, List<string> report)
    {
        var fitted = 0;
        foreach (var filter in helper.GetComponentsInChildren<MeshFilter>(true))
        {
            var mesh = filter.sharedMesh;
            if (mesh == null)
            {
                continue;
            }

            if (!Quads.TryGetValue(mesh.Pointer, out var quad) || quad == null || quad.WasCollected)
            {
                quad = Build(mesh, out var how);
                Quads[mesh.Pointer] = quad;
                report.Add($"mesh {mesh.name} of {mesh.vertexCount} vertices, bounds {mesh.bounds.size}, replaced by {how}");
            }

            filter.sharedMesh = quad;
            var renderer = filter.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                var materials = renderer.sharedMaterials;
                if (materials != null && materials.Length > 1)
                {
                    renderer.sharedMaterials = new[] { materials[0] };
                }
            }

            fitted++;
        }

        return fitted;
    }

    private static Mesh Build(Mesh original, out string how)
    {
        string gpuProblem;
        try
        {
            var copy = CopyFromGpu(original, out how, out gpuProblem);
            if (copy != null)
            {
                return copy;
            }
        }
        catch (Exception exception)
        {
            gpuProblem = exception.Message;
        }

        var square = Square(original, out how);
        how += " on both sides, its own data could not be read: " + gpuProblem;
        return square;
    }

    private static Mesh CopyFromGpu(Mesh source, out string how, out string problem)
    {
        how = null;
        problem = null;
        var count = source.vertexCount;
        if (count < 3 || !source.HasVertexAttribute(VertexAttribute.TexCoord0))
        {
            problem = "it has no picture coordinates";
            return null;
        }

        if (source.GetVertexAttributeFormat(VertexAttribute.Position) != VertexAttributeFormat.Float32 || source.GetVertexAttributeDimension(VertexAttribute.Position) < 3)
        {
            problem = $"positions are stored as {source.GetVertexAttributeFormat(VertexAttribute.Position)}";
            return null;
        }

        var uvFormat = source.GetVertexAttributeFormat(VertexAttribute.TexCoord0);
        if (source.GetVertexAttributeDimension(VertexAttribute.TexCoord0) < 2 || SizeOf(uvFormat) == 0)
        {
            problem = $"picture coordinates are stored as {uvFormat}";
            return null;
        }

        for (var submesh = 0; submesh < source.subMeshCount; submesh++)
        {
            if (source.GetTopology(submesh) != MeshTopology.Triangles)
            {
                problem = $"part {submesh} is made of {source.GetTopology(submesh)}";
                return null;
            }
        }

        var streams = new List<byte[]>();
        for (var stream = 0; stream < source.vertexBufferCount; stream++)
        {
            streams.Add(ReadBuffer(source.GetVertexBufferImpl(stream), source.GetVertexBufferStride(stream) * count));
        }

        var indexBuffer = source.GetIndexBufferImpl();
        var indexSize = source.indexFormat == IndexFormat.UInt16 ? 2 : 4;
        var indexCount = indexBuffer.count * indexBuffer.stride / indexSize;
        var indexBytes = ReadBuffer(indexBuffer, indexCount * indexSize);

        var positions = Attribute(source, streams, VertexAttribute.Position, out var positionStride, out var positionOffset);
        var coordinates = Attribute(source, streams, VertexAttribute.TexCoord0, out var uvStride, out var uvOffset);
        var vertices = new Vector3[count];
        var uvs = new Vector2[count];
        var min = new Vector2(float.MaxValue, float.MaxValue);
        var max = new Vector2(float.MinValue, float.MinValue);
        for (var index = 0; index < count; index++)
        {
            var at = index * positionStride + positionOffset;
            vertices[index] = new Vector3(BitConverter.ToSingle(positions, at), BitConverter.ToSingle(positions, at + 4), BitConverter.ToSingle(positions, at + 8));
            var uvAt = index * uvStride + uvOffset;
            var uv = new Vector2(Component(coordinates, uvAt, uvFormat), Component(coordinates, uvAt + SizeOf(uvFormat), uvFormat));
            uvs[index] = uv;
            min = Vector2.Min(min, uv);
            max = Vector2.Max(max, uv);
        }

        var expected = source.bounds;
        var found = new Bounds(vertices[0], Vector3.zero);
        foreach (var vertex in vertices)
        {
            found.Encapsulate(vertex);
        }

        if (Vector3.Distance(found.min, expected.min) > 0.001f || Vector3.Distance(found.max, expected.max) > 0.001f)
        {
            problem = $"the data from the video card does not match the mesh ({found.min}..{found.max}, expected {expected.min}..{expected.max})";
            return null;
        }

        var span = max - min;
        if (span.x <= 1e-6f || span.y <= 1e-6f)
        {
            problem = "its picture coordinates cover no area";
            return null;
        }

        for (var index = 0; index < count; index++)
        {
            uvs[index] = new Vector2((uvs[index].x - min.x) / span.x, (uvs[index].y - min.y) / span.y);
        }

        var triangles = new List<int>();
        for (var submesh = 0; submesh < source.subMeshCount; submesh++)
        {
            var descriptor = source.GetSubMesh(submesh);
            for (var index = descriptor.indexStart; index < descriptor.indexStart + descriptor.indexCount && index < indexCount; index++)
            {
                var value = indexSize == 2 ? BitConverter.ToUInt16(indexBytes, index * 2) : BitConverter.ToInt32(indexBytes, index * 4);
                triangles.Add(value + descriptor.baseVertex);
            }
        }

        var mesh = new Mesh { name = "CatLib custom stamp " + source.name };
        mesh.hideFlags = HideFlags.DontUnloadUnusedAsset;
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        how = $"copied from the video card with its picture area {min.x:0.###}..{max.x:0.###} x {min.y:0.###}..{max.y:0.###} stretched to the whole picture, {triangles.Count / 3} triangle(s)";
        return mesh;
    }

    private static byte[] Attribute(Mesh source, List<byte[]> streams, VertexAttribute attribute, out int stride, out int offset)
    {
        var stream = source.GetVertexAttributeStream(attribute);
        stride = source.GetVertexBufferStride(stream);
        offset = source.GetVertexAttributeOffset(attribute);
        return streams[stream];
    }

    private static int SizeOf(VertexAttributeFormat format) => format switch
    {
        VertexAttributeFormat.Float32 => 4,
        VertexAttributeFormat.Float16 => 2,
        VertexAttributeFormat.UNorm16 => 2,
        VertexAttributeFormat.UNorm8 => 1,
        _ => 0
    };

    private static float Component(byte[] data, int at, VertexAttributeFormat format) => format switch
    {
        VertexAttributeFormat.Float32 => BitConverter.ToSingle(data, at),
        VertexAttributeFormat.Float16 => (float)BitConverter.Int16BitsToHalf(BitConverter.ToInt16(data, at)),
        VertexAttributeFormat.UNorm16 => BitConverter.ToUInt16(data, at) / 65535f,
        _ => data[at] / 255f
    };

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

    private static Mesh Square(Mesh original, out string how)
    {
        var bounds = original.bounds;
        Vector3 across;
        Vector3 up;
        if (!TryAxes(original, out across, out up, out how))
        {
            AxesFromBounds(bounds.extents, out across, out up);
            how = "a square laid along its bounds";
        }

        var normal = Vector3.Cross(across, up).normalized;
        var extents = bounds.extents;
        var half = Math.Max(Projected(extents, across), Projected(extents, up));
        if (half <= 0f)
        {
            half = Math.Max(extents.x, Math.Max(extents.y, extents.z));
        }

        var offset = half * 2f * FaceOffsetShare;
        var vertices = new Vector3[8];
        var normals = new Vector3[8];
        var uvs = new Vector2[8];
        var triangles = new int[12];
        Face(bounds.center, normal, up, half, offset, 0, vertices, normals, uvs, triangles);
        Face(bounds.center, -normal, up, half, offset, 4, vertices, normals, uvs, triangles);

        var quad = new Mesh { name = "CatLib custom stamp " + original.name };
        quad.hideFlags = HideFlags.DontUnloadUnusedAsset;
        quad.vertices = vertices;
        quad.normals = normals;
        quad.uv = uvs;
        quad.triangles = triangles;
        quad.RecalculateTangents();
        quad.RecalculateBounds();
        return quad;
    }

    private static void Face(Vector3 center, Vector3 normal, Vector3 up, float half, float offset, int first, Vector3[] vertices, Vector3[] normals, Vector2[] uvs, int[] triangles)
    {
        var right = Vector3.Cross(up, -normal).normalized;
        var middle = center + normal * offset;
        vertices[first] = middle - right * half - up * half;
        vertices[first + 1] = middle - right * half + up * half;
        vertices[first + 2] = middle + right * half + up * half;
        vertices[first + 3] = middle + right * half - up * half;
        uvs[first] = new Vector2(0f, 0f);
        uvs[first + 1] = new Vector2(0f, 1f);
        uvs[first + 2] = new Vector2(1f, 1f);
        uvs[first + 3] = new Vector2(1f, 0f);
        for (var index = 0; index < 4; index++)
        {
            normals[first + index] = normal;
        }

        var triangle = first / 4 * 6;
        triangles[triangle] = first;
        triangles[triangle + 1] = first + 1;
        triangles[triangle + 2] = first + 2;
        triangles[triangle + 3] = first;
        triangles[triangle + 4] = first + 2;
        triangles[triangle + 5] = first + 3;
    }

    private static bool TryAxes(Mesh mesh, out Vector3 across, out Vector3 up, out string how)
    {
        across = Vector3.zero;
        up = Vector3.zero;
        how = null;
        if (!mesh.isReadable)
        {
            return false;
        }

        try
        {
            var vertices = mesh.vertices;
            var uvs = mesh.uv;
            var triangles = mesh.triangles;
            if (vertices == null || uvs == null || triangles == null || uvs.Length != vertices.Length)
            {
                return false;
            }

            for (var index = 0; index + 2 < triangles.Length; index += 3)
            {
                var p0 = vertices[triangles[index]];
                var e1 = vertices[triangles[index + 1]] - p0;
                var e2 = vertices[triangles[index + 2]] - p0;
                var t0 = uvs[triangles[index]];
                var d1 = uvs[triangles[index + 1]] - t0;
                var d2 = uvs[triangles[index + 2]] - t0;
                var determinant = d1.x * d2.y - d2.x * d1.y;
                if (Math.Abs(determinant) < 1e-9f)
                {
                    continue;
                }

                var tangent = (e1 * d2.y - e2 * d1.y) / determinant;
                var bitangent = (e2 * d1.x - e1 * d2.x) / determinant;
                if (tangent.sqrMagnitude < 1e-12f || bitangent.sqrMagnitude < 1e-12f)
                {
                    continue;
                }

                across = tangent.normalized;
                up = (bitangent - across * Vector3.Dot(across, bitangent)).normalized;
                how = "a square turned like its picture";
                return up.sqrMagnitude > 0.5f;
            }
        }
        catch (Exception)
        {
        }

        return false;
    }

    private static void AxesFromBounds(Vector3 extents, out Vector3 across, out Vector3 up)
    {
        var axes = new[] { Vector3.right, Vector3.up, Vector3.forward };
        var sizes = new[] { extents.x, extents.y, extents.z };
        var thin = 0;
        for (var index = 1; index < 3; index++)
        {
            if (sizes[index] < sizes[thin])
            {
                thin = index;
            }
        }

        var others = new List<Vector3>();
        for (var index = 0; index < 3; index++)
        {
            if (index != thin)
            {
                others.Add(axes[index]);
            }
        }

        across = others[0];
        up = others[1];
    }

    private static float Projected(Vector3 extents, Vector3 axis) =>
        Math.Abs(extents.x * axis.x) + Math.Abs(extents.y * axis.y) + Math.Abs(extents.z * axis.z);
}
