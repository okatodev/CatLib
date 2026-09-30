using System;
using BetterRepair.Logic;
using CatLib.Logging;
using CatLib.Net;

namespace BetterRepair.Sync;

public sealed class StockSync
{
    public const string StockMessage = "stock";

    private readonly ModChannel _channel;
    private readonly Func<int> _current;
    private readonly CatLogger _log;

    public StockSync(ModChannel channel, Func<int> current, CatLogger log)
    {
        _channel = channel;
        _current = current;
        _log = log;
        _channel.Received += OnReceived;
        _channel.PeerJoined += peer => _channel.SendTo(peer, StockMessage, StockText.Format(_current()));
    }

    public event Action<int> StockReceived;

    public int Rejected { get; private set; }

    public int Broadcast(int current) => _channel.Broadcast(StockMessage, StockText.Format(current));

    private void OnReceived(ModMessage message)
    {
        if (message.Name != StockMessage || !message.FromHost || message.IsLocal)
        {
            return;
        }

        if (!StockText.TryParse(message.Text, out var current))
        {
            Rejected++;
            _log.Warning($"Ignored a cardboard stock message with the value \"{message.Text}\"");
            return;
        }

        StockReceived?.Invoke(current);
    }
}
