using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CatLib.Core;
using CatLib.Logging;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using TooLate.Logic;
using TooLate.Scene;
using Object = Il2CppSystem.Object;

namespace TooLate.Research;

public sealed class MessageRecorder
{
    public const double SummarySeconds = 5.0;

    private readonly CatLogger _log;
    private readonly Dictionary<string, int> _quiet = new(StringComparer.Ordinal);
    private double _summaryAt;

    public MessageRecorder(CatLogger log) => _log = log;

    public bool IsEnabled { get; private set; }

    public void SetEnabled(bool enabled)
    {
        if (IsEnabled == enabled)
        {
            return;
        }

        if (!enabled)
        {
            Summarize();
        }

        IsEnabled = enabled;
        _summaryAt = FrameLoop.Realtime + SummarySeconds;
        _log.Info(enabled
            ? "[Messages] Recording the game's network messages: every message once, moving and animation messages as counts every 5 seconds"
            : "[Messages] Recording stopped");
    }

    public void HostSent(int code, bool reliable, ulong target, Il2CppReferenceArray<Object> payload) =>
        Record($"host -> {target}", code, reliable, payload);

    public void ClientSent(int code, bool reliable, Il2CppReferenceArray<Object> payload) =>
        Record("client -> host", code, reliable, payload);

    public void Update()
    {
        if (IsEnabled && FrameLoop.Realtime >= _summaryAt)
        {
            Summarize();
            _summaryAt = FrameLoop.Realtime + SummarySeconds;
        }
    }

    private void Record(string direction, int code, bool reliable, Il2CppReferenceArray<Object> payload)
    {
        if (MessageRoute.IsContinuous(code) || code == ProtocolCodes.GameTimeSynchronization)
        {
            var key = $"{direction} {Name(code)}";
            _quiet[key] = _quiet.TryGetValue(key, out var count) ? count + 1 : 1;
            return;
        }

        _log.Info(string.Format(CultureInfo.InvariantCulture, "[Messages] {0:0.000} f{1} {2}: {3} ({4}){5}, {6}",
            FrameLoop.Realtime, FrameLoop.FrameCount, direction, Name(code), code, reliable ? string.Empty : " unreliable", GameProtocol.Describe(payload)));
    }

    private void Summarize()
    {
        if (_quiet.Count == 0)
        {
            return;
        }

        _log.Info("[Messages] Last seconds: " + string.Join(", ", _quiet.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key} x{pair.Value}")));
        _quiet.Clear();
    }

    public static string Name(int code) => Enum.IsDefined(typeof(ProtocolCode), code) ? ((ProtocolCode)code).ToString() : "Code" + code;
}
