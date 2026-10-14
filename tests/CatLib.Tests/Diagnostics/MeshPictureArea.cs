using System;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.Rendering;

namespace CatLib.Tests.Diagnostics;

internal static class MeshPictureArea
{
    public static bool TryRead(Mesh mesh, out Vector2 min, out Vector2 max, out string problem)
    {
        min = default;
        max = default;
        problem = null;
        try
        {
            if (mesh == null || mesh.vertexCount == 0 || !mesh.HasVertexAttribute(VertexAttribute.TexCoord0))
            {
                problem = "the mesh has no picture coordinates";
                return false;
            }

            if (mesh.isReadable)
            {
                var uvs = mesh.uv;
                if (uvs == null || uvs.Length == 0)
                {
                    problem = "the mesh gave no picture coordinates";
                    return false;
                }

                min = uvs[0];
                max = uvs[0];
                foreach (var uv in uvs)
                {
                    min = Vector2.Min(min, uv);
                    max = Vector2.Max(max, uv);
                }

                return true;
            }

            var format = mesh.GetVertexAttributeFormat(VertexAttribute.TexCoord0);
            var size = SizeOf(format);
            if (size == 0 || mesh.GetVertexAttributeDimension(VertexAttribute.TexCoord0) < 2)
            {
                problem = $"picture coordinates are stored as {format}";
                return false;
            }

            var count = mesh.vertexCount;
            var stream = mesh.GetVertexAttributeStream(VertexAttribute.TexCoord0);
            var stride = mesh.GetVertexBufferStride(stream);
            var offset = mesh.GetVertexAttributeOffset(VertexAttribute.TexCoord0);
            var data = ReadBuffer(mesh.GetVertexBufferImpl(stream), stride * count);
            min = new Vector2(float.MaxValue, float.MaxValue);
            max = new Vector2(float.MinValue, float.MinValue);
            for (var index = 0; index < count; index++)
            {
                var at = index * stride + offset;
                var uv = new Vector2(Component(data, at, format), Component(data, at + size, format));
                min = Vector2.Min(min, uv);
                max = Vector2.Max(max, uv);
            }

            return true;
        }
        catch (Exception exception)
        {
            problem = exception.Message;
            return false;
        }
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
}
