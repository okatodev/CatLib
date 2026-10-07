using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using TooLate.Logic;
using UnityEngine;
using Object = Il2CppSystem.Object;

namespace TooLate.Scene;

public static class GameProtocol
{
    [ThreadStatic]
    private static bool _sending;

    public static bool IsSending => _sending;

    public static NetworkManager Network => Singleton<NetworkManager>.HasInstance() ? Singleton<NetworkManager>.Instance : null;

    public static Server Server
    {
        get
        {
            var network = Network;
            return network == null || !network.IsServer ? null : network._server;
        }
    }

    public static bool IsHosting => Server != null;

    public static ulong LocalClientId
    {
        get
        {
            var network = Network;
            return network == null ? 0 : network.ClientId;
        }
    }

    public static Il2CppReferenceArray<Object> Empty() => new(0);

    public static bool Send(int code, ulong target, Il2CppReferenceArray<Object> payload, bool reliable = true)
    {
        var server = Server;
        if (server == null)
        {
            return false;
        }

        var previous = _sending;
        _sending = true;
        try
        {
            server.SendMessage((ProtocolCode)code, reliable, target, payload ?? Empty());
            return true;
        }
        finally
        {
            _sending = previous;
        }
    }

    public static void SendSave(ulong target, string saveName, byte[] data)
    {
        var network = Network ?? throw new InvalidOperationException("The game has no network manager");
        var previous = _sending;
        _sending = true;
        try
        {
            network.SendSaveSynchronization(target, false, false, false, saveName, data);
        }
        finally
        {
            _sending = previous;
        }
    }

    public static void Disconnect(ulong target)
    {
        var server = Server;
        if (server == null)
        {
            return;
        }

        server.DisconnectClient(target);
    }

    public static Il2CppReferenceArray<Object> Copy(Il2CppReferenceArray<Object> payload)
    {
        if (payload == null)
        {
            return Empty();
        }

        var copy = new Il2CppReferenceArray<Object>(payload.Length);
        for (var index = 0; index < payload.Length; index++)
        {
            copy[index] = payload[index];
        }

        return copy;
    }

    public static Il2CppReferenceArray<Object> Identifiers(IReadOnlyList<EntityPlace> places, uint nextIdentifier)
    {
        var payload = new Il2CppReferenceArray<Object>(places.Count * 2 + 2);
        var index = 0;
        foreach (var place in places)
        {
            payload[index++] = Box(place.Id);
            payload[index++] = Box(new Vector3(place.X, place.Y, place.Z));
        }

        payload[index++] = Box(EntityPlaces.EndMark);
        payload[index] = Box(nextIdentifier);
        return payload;
    }

    public static Il2CppReferenceArray<Object> Generic(uint entityId, string text)
    {
        var payload = new Il2CppReferenceArray<Object>(2);
        payload[0] = Box(entityId);
        payload[1] = new Object(IL2CPP.ManagedStringToIl2Cpp(text ?? string.Empty));
        return payload;
    }

    public static bool TryReadGeneric(Il2CppReferenceArray<Object> payload, out uint entityId, out string text)
    {
        text = null;
        if (!TryReadId(payload, out entityId) || payload.Length < 2 || payload[1] == null || payload[1].GetIl2CppType()?.Name != "String")
        {
            return false;
        }

        text = IL2CPP.Il2CppStringToManaged(payload[1].Pointer);
        return text != null;
    }

    public static bool TryReadId(Il2CppReferenceArray<Object> payload, out uint entityId)
    {
        entityId = 0;
        if (payload == null || payload.Length < 1 || payload[0] == null || payload[0].GetIl2CppType()?.Name != "UInt32")
        {
            return false;
        }

        entityId = payload[0].Unbox<uint>();
        return true;
    }

    public static Il2CppReferenceArray<Object> TimePeriod(int period)
    {
        var payload = new Il2CppReferenceArray<Object>(1);
        payload[0] = Box((byte)period, ClassOf<byte, Il2CppSystem.Byte>());
        return payload;
    }

    public static Object Box(uint value) => Box(value, ClassOf<uint, Il2CppSystem.UInt32>());

    public static Object Box(Vector3 value) => Box(value, ClassOf<Vector3, Vector3>());

    private static unsafe Object Box<T>(T value, IntPtr type) where T : unmanaged
    {
        if (type == IntPtr.Zero)
        {
            throw new InvalidOperationException($"The game has no class for {typeof(T).Name}");
        }

        return new Object(IL2CPP.il2cpp_value_box(type, (IntPtr)(&value)));
    }

    private static IntPtr ClassOf<TManaged, TGame>()
    {
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(TGame).TypeHandle);
        var type = Il2CppClassPointerStore<TManaged>.NativeClassPtr;
        return type != IntPtr.Zero ? type : Il2CppClassPointerStore<TGame>.NativeClassPtr;
    }

    public static string Describe(Il2CppReferenceArray<Object> payload, int most = 6)
    {
        if (payload == null || payload.Length == 0)
        {
            return "no values";
        }

        var parts = new List<string>();
        for (var index = 0; index < payload.Length && index < most; index++)
        {
            parts.Add(DescribeValue(payload[index]));
        }

        if (payload.Length > most)
        {
            parts.Add($"... {payload.Length - most} more");
        }

        return $"{payload.Length} value(s): {string.Join(", ", parts)}";
    }

    private static string DescribeValue(Object value)
    {
        if (value == null)
        {
            return "null";
        }

        try
        {
            var type = value.GetIl2CppType();
            var name = type == null ? "?" : type.Name;
            switch (name)
            {
                case "String":
                    var text = IL2CPP.Il2CppStringToManaged(value.Pointer) ?? string.Empty;
                    return text.Length > 40 ? $"\"{text.Substring(0, 40)}...\"" : $"\"{text}\"";
                case "Boolean":
                    return value.Unbox<bool>() ? "true" : "false";
                case "Int32":
                    return value.Unbox<int>().ToString();
                case "UInt32":
                    return value.Unbox<uint>().ToString();
                case "UInt64":
                    return value.Unbox<ulong>().ToString();
                case "Single":
                    return value.Unbox<float>().ToString("0.###");
                case "Vector3":
                    var vector = value.Unbox<Vector3>();
                    return $"({vector.x:0.##}, {vector.y:0.##}, {vector.z:0.##})";
                case "Byte[]":
                    return $"{new Il2CppStructArray<byte>(value.Pointer).Length} bytes";
                default:
                    return name;
            }
        }
        catch (Exception exception)
        {
            return "? " + exception.GetType().Name;
        }
    }
}
