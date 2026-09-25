using System;
using System.Collections.Generic;
using CatLib.Game.Events;
using CatLib.Logging;

namespace CatLib.Net;

internal sealed class SteamMessagesTransport : ISessionTransport
{
    public const int Channel = 0x4341;
    public const int SendFlags = 8 | 32;
    public const int ResultOk = 1;
    public const string NativeCallEventName = "Net.NativeCall";

    private readonly ISteamChannelApi _api;
    private readonly CatLogger _log;
    private readonly HashSet<string> _announcedCalls = new();

    public SteamMessagesTransport(ulong localId, ISteamChannelApi api, CatLogger log)
    {
        LocalId = localId;
        _api = api;
        _log = log;
    }

    public ulong LocalId { get; }

    public string ApiName => _api.Name;

    public int Sent { get; private set; }

    public int Received { get; private set; }

    public int Failed { get; private set; }

    public static ISteamChannelApi CreateApi(SteamApiBackend backend, CatLogger log)
    {
        if (backend == SteamApiBackend.Flat)
        {
            var flat = FlatSteamChannelApi.TryCreate(out var error);
            if (flat != null)
            {
                log.Info($"Using the flat Steam API through {flat.Accessor}");
                return flat;
            }

            log.Warning($"The flat Steam API is unavailable ({error}), falling back to the interop API");
        }

        return new InteropSteamChannelApi();
    }

    public void Send(ulong peer, byte[] payload)
    {
        Breadcrumb("send", peer);
        var result = _api.Send(peer, payload, SendFlags, Channel);
        if (result == ResultOk)
        {
            Sent++;
            _log.Debug($"Sent {payload.Length} bytes to {peer} on the CatLib channel");
            return;
        }

        Failed++;
        _log.Warning($"Sending {payload.Length} bytes to {peer} on the CatLib channel failed with Steam result {result}");
    }

    public bool Accept(ulong peer)
    {
        Breadcrumb("accept", peer);
        return _api.Accept(peer);
    }

    public int Poll(Action<ulong, byte[]> received)
    {
        Breadcrumb("receive", 0);
        return _api.Receive(Channel, (sender, bytes) =>
        {
            if (bytes == null)
            {
                _log.Warning($"Dropped a message with an invalid size from {sender} on the CatLib channel");
                return;
            }

            Received++;
            _log.Info($"Received {bytes.Length} bytes from {sender} on the CatLib channel");
            try
            {
                received(sender, bytes);
            }
            catch (Exception exception)
            {
                _log.Error("Handling a message from the CatLib channel failed", exception);
            }
        });
    }

    private void Breadcrumb(string operation, ulong peer)
    {
        if (!_announcedCalls.Add(operation))
        {
            return;
        }

        _log.Info($"First {operation} call through the {_api.Name} Steam API");
        GameEventStream.Publish(NativeCallEventName, $"op={operation} api={_api.Name} peer={peer}");
    }
}
