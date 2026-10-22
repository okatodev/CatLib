using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using CatLib.Diagnostics;
using CatLib.Logging;
using UnityEngine.Diagnostics;

namespace CatLib.Tests.Diagnostics;

public sealed class CrashTrigger
{
    public const double ConfirmSeconds = 3;
    public const string ConfirmHint = "press again within 3 s to crash the game";
    public static readonly TimeSpan HangOnQuit = TimeSpan.FromSeconds(90);
    public const string TrailPrefix = "CatLib.Tests: ";
    public const string DotnetMarker = "CatLib.Tests crash in .NET code";
    public static readonly IntPtr BadAddress = new(0x7FF000000120);

    private readonly CatLogger _log;
    private string _armed;
    private DateTime _armedAt;
    private bool _hangOnQuit;

    public CrashTrigger(CatLogger log)
    {
        _log = log;
    }

    public string NativeOnGameThread() => Confirm("native-main", () =>
    {
        _log.Warning("Crashing the game on purpose: native access violation on the game thread");
        CrashTrail.During(TrailPrefix + "native crash on the game thread");
        Utils.ForceCrash(ForcedCrashCategory.AccessViolation);
    });

    public string NativeOnWorkerThread() => Confirm("native-worker", () =>
    {
        _log.Warning("Crashing the game on purpose: native access violation on a new native thread");
        CrashTrail.During(TrailPrefix + "native crash on a worker thread");
        var thread = CreateThread(IntPtr.Zero, UIntPtr.Zero, new IntPtr(0x10), IntPtr.Zero, 0, out _);
        if (thread == IntPtr.Zero)
        {
            _log.Error($"Could not start the crashing thread, error {Marshal.GetLastWin32Error()}");
        }
    });

    public string Managed() => Confirm("managed", () =>
    {
        _log.Warning("Crashing the game on purpose: unhandled .NET exception on a new thread");
        CrashTrail.During(TrailPrefix + "unhandled .NET exception");
        new Thread(() => throw new InvalidOperationException("CatLib test crash from the developer menu")) { IsBackground = true }.Start();
    });

    public string InDotnetCode() => Confirm("dotnet", () =>
    {
        var marker = DotnetMarker + " at " + DateTime.Now.ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
        _log.Warning($"Crashing the game on purpose: access violation inside .NET code, the report should show \"{marker}\" from the stack");
        CrashTrail.During(TrailPrefix + "crash in .NET code");
        WriteThroughBadPointer(marker, BadAddress);
    });

    public string SetSteamShutdown(bool on)
    {
        CatLib.Net.SessionNetwork.LetGameShutSteamDown = on;
        _log.Info(on
            ? "The game shuts Steam down itself when it quits, like without CatLib 0.7.3: to check whether Steam still crashes then"
            : "CatLib skips the game's shutdown of Steam when it quits again");
        return on ? "the game shuts Steam down on quit" : "CatLib skips it";
    }

    public static bool SteamShutdownByGame => CatLib.Net.SessionNetwork.LetGameShutSteamDown;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void WriteThroughBadPointer(string marker, IntPtr address)
    {
        var length = marker.Length;
        Marshal.WriteInt32(address, length);
        GC.KeepAlive(marker);
    }

    public string ToggleHangOnQuit()
    {
        if (!_hangOnQuit)
        {
            CatLib.Core.CatLibRuntime.ShuttingDown += HangNow;
        }
        else
        {
            CatLib.Core.CatLibRuntime.ShuttingDown -= HangNow;
        }

        _hangOnQuit = !_hangOnQuit;
        _log.Info(_hangOnQuit
            ? $"The game will hang for {HangOnQuit.TotalSeconds} s when it quits, to check the hang report of the crash watcher"
            : "The game will quit normally");
        return _hangOnQuit ? $"armed: the next quit hangs for {HangOnQuit.TotalSeconds} s" : "off";
    }

    private void HangNow()
    {
        _log.Warning($"Hanging the game thread on purpose for {HangOnQuit.TotalSeconds} s while Unity shuts down");
        using (CrashTrail.During(TrailPrefix + "hang on quit"))
        {
            Thread.Sleep(HangOnQuit);
        }
    }

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
