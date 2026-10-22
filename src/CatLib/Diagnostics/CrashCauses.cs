using System;
using System.Collections.Generic;

namespace CatLib.Diagnostics;

internal static class CrashCauses
{
    public const string KeyPrefix = "cause.";
    public const string SteamShutdown = "steamShutdown";
    public const string SteamBreakpoint = "steamBreakpoint";
    public const string SteamStructOverflow = "steamStructOverflow";
    public const string DeveloperMenu = "developerMenu";
    public const string DeveloperMenuPrefix = "CatLib.Tests:";
    public const string SteamModule = "steamclient64.dll";
    public const string SteamShutdownFrame = "SteamAPI_Shutdown";
    public const string SessionInfoFrame = "GetSessionConnectionInfo";
    public const uint BreakpointCode = 0x80000003;
    public const uint FailFastCode = 0xC0000409;

    public static readonly IReadOnlyDictionary<string, string> Report = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [SteamShutdown] = "Steam crashed in one of its own threads while the game shut Steam down (SteamAPI_Shutdown on the game thread). " +
                          "CatLib 0.7.3 and later skip that shutdown when the game quits, unless the developer menu lets the game do it.",
        [SteamBreakpoint] = "Steam stopped at a breakpoint of its own. Such stops happen only while a debugger follows the game; " +
                            "crash watcher 1.1.0 and later let them go on.",
        [SteamStructOverflow] = "A Steam call through interop wrote more than interop gave it room for (GetSessionConnectionInfo) and overwrote the stack. " +
                                "CatLib 0.7.1 and later call Steam directly for this.",
        [DeveloperMenu] = "The developer menu of CatLib.Tests did this on purpose."
    };

    public static string Find(CrashSession session, uint exitCode, CrashEventInfo info)
    {
        if (session == null || info == null)
        {
            return string.Empty;
        }

        foreach (var operation in session.Unfinished)
        {
            if (operation.Text.StartsWith(DeveloperMenuPrefix, StringComparison.Ordinal))
            {
                return DeveloperMenu;
            }
        }

        var crashed = info.HasFault ? CrashText.FindThread(info, info.ThreadId) : null;
        var game = CrashText.FindThread(info, session.MainThreadId);
        var inSteam = string.Equals(info.Module, SteamModule, StringComparison.OrdinalIgnoreCase);
        if (inSteam && (exitCode == BreakpointCode || info.ExceptionCode == "0x80000003"))
        {
            return SteamBreakpoint;
        }

        if (inSteam && game != null && game != crashed && HasFrame(game, SteamShutdownFrame))
        {
            return SteamShutdown;
        }

        if ((exitCode == FailFastCode || info.ExceptionCode == "0xc0000409") && crashed != null && HasFrame(crashed, SessionInfoFrame))
        {
            return SteamStructOverflow;
        }

        return string.Empty;
    }

    public static string ReportText(string cause) => cause != null && Report.TryGetValue(cause, out var text) ? text : string.Empty;

    private static bool HasFrame(CrashThreadStack stack, string name)
    {
        foreach (var frame in stack.Frames)
        {
            if (frame.IndexOf(name, StringComparison.Ordinal) >= 0)
            {
                return true;
            }
        }

        return false;
    }
}
