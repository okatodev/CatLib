using System;
using System.Diagnostics;
using System.Text;
using CatLib.Game.Events;
using CatLib.Logging;

namespace CatLib.Net;

internal sealed class SteamSelfCheck
{
    public const string StepEventName = "Net.SelfCheck";
    public const string Payload = "catlib-selfcheck";
    public const double WaitSeconds = 3;

    private readonly CatLogger _log;
    private SteamMessagesTransport _transport;
    private Stopwatch _clock;
    private bool _received;

    public SteamSelfCheck(CatLogger log)
    {
        _log = log;
    }

    public bool IsRunning => _clock != null;

    public string LastResult { get; private set; }

    public void Start(ulong selfId, SteamApiBackend backend)
    {
        if (IsRunning)
        {
            _log.Warning("A Steam self check is already running");
            return;
        }

        _received = false;
        Step("create", backend.ToString());
        var api = SteamMessagesTransport.CreateApi(backend, _log);
        _transport = new SteamMessagesTransport(selfId, api, _log);

        Step("accept", api.Name);
        var accepted = _transport.Accept(selfId);
        _log.Info($"Self check: accept returned {accepted}");

        Step("send", api.Name);
        _transport.Send(selfId, Encoding.ASCII.GetBytes(Payload));
        _log.Info($"Self check: send finished, sent {_transport.Sent}, failed {_transport.Failed}");

        Step("receive", api.Name);
        _clock = Stopwatch.StartNew();
    }

    public void Update()
    {
        if (!IsRunning)
        {
            return;
        }

        _transport.Poll((sender, bytes) =>
        {
            if (sender == _transport.LocalId && Encoding.ASCII.GetString(bytes) == Payload)
            {
                _received = true;
            }
        });

        if (!_received && _clock.Elapsed.TotalSeconds < WaitSeconds)
        {
            return;
        }

        LastResult = $"api={_transport.ApiName} sent={_transport.Sent} failed={_transport.Failed} loopback={(_received ? "received" : "not received")}";
        _log.Message("Steam self check finished: " + LastResult);
        GameEventStream.Publish(StepEventName, "result " + LastResult);
        _clock = null;
        _transport = null;
    }

    private static void Step(string step, string detail) => GameEventStream.Publish(StepEventName, $"step={step} {detail}");
}
