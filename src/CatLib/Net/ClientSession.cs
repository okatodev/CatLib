using System;
using System.Collections.Generic;
using CatLib.Events;
using CatLib.Logging;

namespace CatLib.Net;

public sealed class ClientSession
{
    private readonly ISessionTransport _transport;
    private readonly LocalIdentity _identity;
    private readonly ISessionSettingsSink _sink;
    private readonly Func<double> _clock;
    private readonly double _verdictTimeout;
    private readonly CatLogger _log;
    private double _startedAt;

    public ClientSession(ISessionTransport transport, LocalIdentity identity, ulong hostId, ISessionSettingsSink sink, Func<double> clock, double verdictTimeoutSeconds, CatLogger log)
    {
        _transport = transport;
        _identity = identity;
        HostId = hostId;
        _sink = sink;
        _clock = clock;
        _verdictTimeout = verdictTimeoutSeconds;
        _log = log;
    }

    public event Action<PeerReport> Completed;

    public event Action<SessionApplyResult> SettingsApplied;

    public ulong HostId { get; }

    public SessionStatus Status { get; private set; } = SessionStatus.Waiting;

    public PeerReport Report { get; private set; }

    public SessionApplyResult LastApply { get; private set; } = SessionApplyResult.Empty;

    public void Start()
    {
        _startedAt = _clock();
        Status = SessionStatus.Waiting;
        Send(MessageCodec.Encode(new HelloMessage(_identity)));
    }

    public void OnReceived(ulong from, byte[] data)
    {
        if (from != HostId || Status == SessionStatus.Stopped)
        {
            return;
        }

        DecodedMessage message;
        try
        {
            message = MessageCodec.Decode(data);
        }
        catch (WireFormatException exception)
        {
            _log?.Warning($"Ignored a malformed message from the host: {exception.Message}");
            return;
        }

        if (message.IsProtocolMismatch)
        {
            if (Status == SessionStatus.Waiting)
            {
                Complete(SessionStatus.Rejected, new[] { new CompatibilityProblem(ProblemKind.ProtocolMismatch, "catlib", message.Protocol.ToString(), MessageCodec.ProtocolVersion.ToString()) }, null, null);
            }

            return;
        }

        switch (message.Payload)
        {
            case VerdictMessage verdict when Status == SessionStatus.Waiting:
                if (verdict.Accepted)
                {
                    ApplySettings(verdict.Settings);
                }

                Complete(verdict.Accepted ? SessionStatus.Accepted : SessionStatus.Rejected, verdict.Problems, null, null);
                break;
            case SettingsUpdateMessage update when Status == SessionStatus.Accepted:
                ApplySettings(update.Settings);
                break;
            default:
                _log?.Warning($"Ignored an unexpected {message.Type} message from the host in state {Status}");
                break;
        }
    }

    public void Update()
    {
        if (Status == SessionStatus.Waiting && _clock() - _startedAt >= _verdictTimeout)
        {
            Complete(SessionStatus.PeerWithoutCatLib, Compatibility.CompareWithoutHost(_identity), null, null);
        }
    }

    public void Stop()
    {
        if (Status == SessionStatus.Stopped)
        {
            return;
        }

        Status = SessionStatus.Stopped;
        _sink.Clear();
    }

    private void ApplySettings(IReadOnlyList<SessionSettingValue> settings)
    {
        LastApply = _sink.Apply(settings);
        SafeInvoker.Invoke(SettingsApplied, LastApply, "ClientSession.SettingsApplied", _log);
    }

    private void Complete(SessionStatus status, IReadOnlyList<CompatibilityProblem> problems, string catLibVersion, string gameVersion)
    {
        Status = status;
        Report = new PeerReport(HostId, status, problems, catLibVersion, gameVersion);
        SafeInvoker.Invoke(Completed, Report, "ClientSession.Completed", _log);
    }

    private void Send(byte[] payload)
    {
        try
        {
            _transport.Send(HostId, payload);
        }
        catch (Exception exception)
        {
            _log?.Warning($"Sending to the host failed: {exception.Message}");
        }
    }
}
