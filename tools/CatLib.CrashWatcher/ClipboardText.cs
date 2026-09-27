using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace CatLib.CrashWatcher;

internal static class ClipboardText
{
    public const int Attempts = 10;

    public static bool Set(IntPtr owner, string text)
    {
        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            if (NativeMethods.OpenClipboard(owner))
            {
                try
                {
                    return Put(text ?? string.Empty);
                }
                finally
                {
                    NativeMethods.CloseClipboard();
                }
            }

            Thread.Sleep(50);
        }

        return false;
    }

    private static bool Put(string text)
    {
        NativeMethods.EmptyClipboard();
        var chars = text.ToCharArray();
        var memory = NativeMethods.GlobalAlloc(NativeMethods.MovableMemory, new UIntPtr((uint)((chars.Length + 1) * 2)));
        if (memory == IntPtr.Zero)
        {
            return false;
        }

        var pointer = NativeMethods.GlobalLock(memory);
        if (pointer == IntPtr.Zero)
        {
            NativeMethods.GlobalFree(memory);
            return false;
        }

        Marshal.Copy(chars, 0, pointer, chars.Length);
        Marshal.WriteInt16(pointer, chars.Length * 2, 0);
        NativeMethods.GlobalUnlock(memory);
        if (NativeMethods.SetClipboardData(NativeMethods.UnicodeText, memory) == IntPtr.Zero)
        {
            NativeMethods.GlobalFree(memory);
            return false;
        }

        return true;
    }
}
