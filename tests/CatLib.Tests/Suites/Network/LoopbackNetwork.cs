using System;
using System.Collections.Generic;
using CatLib.Net;

namespace CatLib.Tests.Suites.Network;

internal sealed class LoopbackNetwork
{
    private readonly Dictionary<ulong, Action<ulong, byte[]>> _receivers = new();
    private readonly Queue<(ulong From, ulong To, byte[] Payload)> _queue = new();

    public double Now { get; set; }

    public int Delivered { get; private set; }

    public LoopbackTransport Join(ulong id, Action<ulong, byte[]> receiver)
    {
        _receivers[id] = receiver;
        return new LoopbackTransport(this, id);
    }

    public void Leave(ulong id) => _receivers.Remove(id);

    public void Enqueue(ulong from, ulong to, byte[] payload) => _queue.Enqueue((from, to, payload));

    public int Pump()
    {
        var delivered = 0;
        while (_queue.Count > 0)
        {
            var (from, to, payload) = _queue.Dequeue();
            if (_receivers.TryGetValue(to, out var receiver))
            {
                receiver(from, payload);
                delivered++;
            }
        }

        Delivered += delivered;
        return delivered;
    }
}

internal sealed class LoopbackTransport : ISessionTransport
{
    private readonly LoopbackNetwork _network;

    public LoopbackTransport(LoopbackNetwork network, ulong localId)
    {
        _network = network;
        LocalId = localId;
    }

    public ulong LocalId { get; }

    public int Sent { get; private set; }

    public void Send(ulong peer, byte[] payload)
    {
        Sent++;
        _network.Enqueue(LocalId, peer, payload);
    }
}
