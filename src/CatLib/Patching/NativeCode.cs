using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Il2CppInterop.Common;

namespace CatLib.Patching;

internal static class NativeCode
{
    private const uint ExecuteReadWrite = 0x40;

    public static bool TryGetMethodPointer(MethodBase method, out IntPtr pointer, out string problem)
    {
        pointer = IntPtr.Zero;
        problem = null;
        if (method == null)
        {
            problem = "no method";
            return false;
        }

        try
        {
            var field = Il2CppInteropUtils.GetIl2CppMethodInfoPointerFieldForGeneratedMethod(method);
            if (field == null)
            {
                problem = $"{method.DeclaringType?.Name}.{method.Name} is not a game method";
                return false;
            }

            var methodInfo = (IntPtr)field.GetValue(null);
            if (methodInfo == IntPtr.Zero)
            {
                problem = $"{method.DeclaringType?.Name}.{method.Name} has no method info";
                return false;
            }

            pointer = Marshal.ReadIntPtr(methodInfo);
            if (pointer == IntPtr.Zero)
            {
                problem = $"{method.DeclaringType?.Name}.{method.Name} has no code";
                return false;
            }

            return true;
        }
        catch (Exception exception)
        {
            problem = $"{method.DeclaringType?.Name}.{method.Name}: {exception.GetType().Name}: {exception.Message}";
            return false;
        }
    }

    public static byte[] Read(IntPtr address, int length)
    {
        var bytes = new byte[length];
        Marshal.Copy(address, bytes, 0, length);
        return bytes;
    }

    public static void Write(IntPtr address, byte[] bytes)
    {
        var size = (UIntPtr)(uint)bytes.Length;
        if (!VirtualProtect(address, size, ExecuteReadWrite, out var previous))
        {
            throw new InvalidOperationException($"The game code at 0x{address.ToInt64():X} cannot be made writable (error {Marshal.GetLastWin32Error()})");
        }

        try
        {
            Marshal.Copy(bytes, 0, address, bytes.Length);
        }
        finally
        {
            VirtualProtect(address, size, previous, out _);
            FlushInstructionCache(GetCurrentProcess(), address, size);
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool VirtualProtect(IntPtr address, UIntPtr size, uint protection, out uint previous);

    [DllImport("kernel32.dll")]
    private static extern bool FlushInstructionCache(IntPtr process, IntPtr address, UIntPtr size);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();
}
