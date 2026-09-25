using System;
using System.Collections.Generic;
using System.Text;
using CatLib.Game.Events;
using CatLib.Logging;
using CatLib.Net;

namespace CatLib.Tests.Diagnostics;

public sealed class NetworkProbes
{
    public const string ProbeText = "catlib-probe";
    public const string SentEventName = "Probe.Sent";
    public const int UnknownProtocolCode = 200;
    public const uint UnusedNetworkIdentifier = 0xFFFFFFF0;

    private static readonly string[] Steps = { "UnknownCode", "ManagerMessage", "GenericMessage", "SteamChannel" };

    private readonly CatLogger _log;
    private readonly HashSet<ulong> _peers = new();
    private int _next;

    public NetworkProbes(CatLogger log)
    {
        _log = log;
        NetworkEvents.ClientConnected += peer => _peers.Add(peer);
        NetworkEvents.ClientDisconnected += peer => _peers.Remove(peer);
        BootstrapEvents.GameRestartStarted += () =>
        {
            _peers.Clear();
            _next = 0;
        };
    }

    public void SendNext()
    {
        var targets = Targets(out var isHost);
        var step = isHost ? Steps[_next % Steps.Length] : "SteamChannel";
        if (isHost)
        {
            _next++;
        }

        if (targets.Count == 0)
        {
            _log.Warning($"Probe {step}: nobody to send to. Host a lobby and let the other player join first");
            return;
        }

        foreach (var target in targets)
        {
            try
            {
                Send(step, target);
                _log.Message(isHost ? $"Probe {_next}/{Steps.Length} {step} sent to {target}" : $"Probe {step} sent to {target}");
                GameEventStream.Publish(SentEventName, $"kind={step} target={target}");
            }
            catch (Exception exception)
            {
                _log.Error($"Probe {step} to {target} failed", exception);
            }
        }
    }

    private static unsafe Il2CppSystem.Object BoxUInt32(uint value)
    {
        var boxed = default(Il2CppSystem.UInt32);
        *(uint*)&boxed = value;
        return boxed.BoxIl2CppObject();
    }

    private List<ulong> Targets(out bool isHost)
    {
        isHost = Singleton<NetworkManager>.HasInstance() && Singleton<NetworkManager>.Instance.IsServer;
        var targets = new List<ulong>();
        if (isHost)
        {
            var self = Singleton<NetworkManager>.Instance.ClientId;
            foreach (var peer in _peers)
            {
                if (peer != self)
                {
                    targets.Add(peer);
                }
            }
        }
        else if (SessionNetwork.Client != null && SessionNetwork.Client.HostId != 0)
        {
            targets.Add(SessionNetwork.Client.HostId);
        }

        return targets;
    }

    private void Send(string step, ulong target)
    {
        var server = Singleton<NetworkManager>.HasInstance() ? Singleton<NetworkManager>.Instance._server : null;
        switch (step)
        {
            case "UnknownCode":
                server.SendMessage((ProtocolCode)UnknownProtocolCode, true, target, new Il2CppSystem.Object[] { (Il2CppSystem.String)ProbeText });
                break;
            case "ManagerMessage":
                server.SendMessage(ProtocolCode.ManagerMessage, true, target, new Il2CppSystem.Object[] { (Il2CppSystem.String)ProbeText });
                break;
            case "GenericMessage":
                server.SendMessage(ProtocolCode.GenericMessage, true, target, new[] { BoxUInt32(UnusedNetworkIdentifier), (Il2CppSystem.String)ProbeText });
                break;
            default:
                var transport = SessionNetwork.Transport ?? throw new InvalidOperationException("No CatLib network session is active on this side");
                transport.Send(target, Encoding.ASCII.GetBytes(ProbeText));
                break;
        }
    }
}
