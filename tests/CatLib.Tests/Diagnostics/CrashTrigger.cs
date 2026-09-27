using System;
using System.Runtime.InteropServices;
using System.Threading;
using CatLib.Logging;
using UnityEngine.Diagnostics;

namespace CatLib.Tests.Diagnostics;

public sealed class CrashTrigger
{
    public const double ConfirmSeconds = 3;
    public const string ConfirmHint = "press again within 3 s to crash the game";

    private readonly CatLogger _log;
    private string _armed;
    private DateTime _armedAt;

    public CrashTrigger(CatLogger log)
    {
        _log = log;
    }

    public string NativeOnGameThread() => Confirm("native-main", () =>
    {
        _log.Warning("Crashing the game on purpose: native access violation on the game thread");
        Utils.ForceCrash(ForcedCrashCategory.AccessViolation);
    });

    public string NativeOnWorkerThread() => Confirm("native-worker", () =>
    {
        _log.Warning("Crashing the game on purpose: native access violation on a new native thread");
        var thread = CreateThread(IntPtr.Zero, UIntPtr.Zero, new IntPtr(0x10), IntPtr.Zero, 0, out _);
        if (thread == IntPtr.Zero)
        {
            _log.Error($"Could not start the crashing thread, error {Marshal.GetLastWin32Error()}");
        }
    });

    public string Managed() => Confirm("managed", () =>
    {
        _log.Warning("Crashing the game on purpose: unhandled .NET exception on a new thread");
        new Thread(() => throw new InvalidOperationException("CatLib test crash from the developer menu")) { IsBackground = true }.Start();
    });

    private string Confirm(string kind, Action crash)
    {
        var now = DateTime.UtcNow;
        if (_armed != kind || (now - _armedAt).TotalSeconds > ConfirmSeconds)
        {
            _armed = kind;
            _armedAt = now;
            return ConfirmHint;
        }

        _armed = null;
        crash();
        return "crashing";
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateThread(IntPtr attributes, UIntPtr stackSize, IntPtr start, IntPtr parameter, uint flags, out uint threadId);
}
