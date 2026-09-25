using System;
using System.Collections.Generic;
using CatLib.Events;
using CatLib.Logging;

namespace CatLib.Net;

public sealed class ClientSession
{
    public const double MinAnnounceResponseSeconds = 1;

    private readonly ISessionTransport _transport;
    private readonly LocalIdentity _identity;
    private readonly ISessionSettingsSink _sink;
    private readonly Func<double> _clock;
    private readonly double _verdictTimeout;
    private readonly CatLogger _log;
    private readonly Func<ulong, bool> _isHostCandidate;
    private double _startedAt;
    private double _lastHelloAt = double.NegativeInfinity;

    public ClientSession(ISessionTransport transport, LocalIdentity identity, ISessionSettingsSink sink, Func<double> clock, double verdictTimeoutSeconds, CatLogger log, Func<ulong, bool> isHostCandidate = null)
    {
        _transport = transport;
        _identity = identity;
        _sink = sink;
        _clock = clock;
        _verdictTimeout = verdictTimeoutSeconds;
        _log = log;
        _isHostCandidate = isHostCandidate ?? (_ => true);
    }

    public event Action<PeerReport> Completed;

    public event Action<SessionApplyResult> SettingsApplied;

    public ulong HostId { get; private set; }

    public int HellosSent { get; private set; }

    public SessionStatus Status { get; private set; } = SessionStatus.Waiting;

    public PeerReport Report { get; private set; }

    public SessionApplyResult LastApply { get; private set; } = SessionApplyResult.Empty;

    public void Start()
    {
        _startedAt = _clock();
        Status = SessionStatus.Waiting;
    }

    public void ResendHello()
    {
        if (HostId != 0 && Status == SessionStatus.Waiting)
        {
            SendHello();
        }
    }

    public void OnReceived(ulong from, byte[] data)
    {
        if (Status == SessionStatus.Stopped)
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
            _log?.Warning($"Ignored a malformed message from {from}: {exception.Message}");
            return;
        }

        if (HostId == 0)
        {
            if (Status != SessionStatus.Waiting || !_isHostCandidate(from))
            {
                _log?.Warning($"Ignored a {message.Type} message from {from}, it is not a known host candidate");
                return;
            }

            if (message.IsProtocolMismatch)
            {
                HostId = from;
                Complete(SessionStatus.Rejected, new[] { new CompatibilityProblem(ProblemKind.ProtocolMismatch, "catlib", message.Protocol.ToString(), MessageCodec.ProtocolVersion.ToString()) });
                return;
            }

            if (message.Payload is AnnounceMessage)
            {
                HostId = from;
                SendHello();
            }

            return;
        }

        if (from != HostId)
        {
            return;
        }

        if (message.IsProtocolMismatch)
        {
            if (Status == SessionStatus.Waiting)
            {
                Complete(SessionStatus.Rejected, new[] { new CompatibilityProblem(ProblemKind.ProtocolMismatch, "catlib", message.Protocol.ToString(), MessageCodec.ProtocolVersion.ToString()) });
            }

            return;
        }

        switch (message.Payload)
        {
            case AnnounceMessage when Status == SessionStatus.Waiting:
                if (_clock() - _lastHelloAt >= MinAnnounceResponseSeconds)
                {
                    SendHello();
                }

                break;
            case AnnounceMessage:
                break;
            case VerdictMessage when Status != SessionStatus.Waiting:
                _log?.Debug($"Ignored a repeated verdict from the host in state {Status}");
                break;
            case VerdictMessage verdict when Status == SessionStatus.Waiting:
                if (verdict.Accepted)
                {
                    ApplySettings(verdict.Settings);
                }

                Complete(verdict.Accepted ? SessionStatus.Accepted : SessionStatus.Rejected, verdict.Problems, verdict.Disconnecting);
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
            Complete(SessionStatus.PeerWithoutCatLib, Compatibility.CompareWithoutHost(_identity));
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

    private void SendHello()
    {
        HellosSent++;
        _lastHelloAt = _clock();
        try
        {
            _transport.Send(HostId, MessageCodec.Encode(new HelloMessage(_identity)));
        }
        catch (Exception exception)
        {
            _log?.Warning($"Sending to the host failed: {exception.Message}");
        }
    }

    private void ApplySettings(IReadOnlyList<SessionSettingValue> settings)
    {
        LastApply = _sink.Apply(settings);
        SafeInvoker.Invoke(SettingsApplied, LastApply, "ClientSession.SettingsApplied", _log);
    }

    private void Complete(SessionStatus status, IReadOnlyList<CompatibilityProblem> problems, bool disconnecting = false)
    {
        Status = status;
        Report = new PeerReport(HostId, status, problems, null, null, disconnecting);
        SafeInvoker.Invoke(Completed, Report, "ClientSession.Completed", _log);
    }
}
