using System;
using System.Collections.Generic;
using System.Linq;
using CatLib.Net;

namespace CatLib.Voice;

public static class VoiceRoutes
{
    public const int PacketsPerSecondPerPeer = 150;

    public static IReadOnlyList<ulong> FromLocal(SessionRole role, ulong hostId, bool hostShares, IEnumerable<ulong> sharing) => role switch
    {
        SessionRole.Host => (sharing ?? Array.Empty<ulong>()).Where(peer => peer != 0).Distinct().ToList(),
        SessionRole.Client when hostShares && hostId != 0 => new[] { hostId },
        _ => Array.Empty<ulong>()
    };

    public static bool Accept(SessionRole role, ulong sender, ulong hostId, ulong localId, ICollection<ulong> sharing, VoicePacket packet, out ulong speaker)
    {
        speaker = 0;
        switch (role)
        {
            case SessionRole.Host:
                if (sender == 0 || sender == localId || sharing == null || !sharing.Contains(sender))
                {
                    return false;
                }

                speaker = sender;
                return true;
            case SessionRole.Client:
                if (sender == 0 || sender != hostId || packet.Speaker == 0 || packet.Speaker == localId)
                {
                    return false;
                }

                speaker = packet.Speaker;
                return true;
            default:
                return false;
        }
    }

    public static IReadOnlyList<ulong> Forward(SessionRole role, ulong speaker, IEnumerable<ulong> sharing) =>
        role == SessionRole.Host && sharing != null
            ? sharing.Where(peer => peer != 0 && peer != speaker).Distinct().ToList()
            : Array.Empty<ulong>();
}

public sealed class VoiceRate
{
    private readonly Dictionary<ulong, (double Start, int Count)> _windows = new();
    private readonly int _limit;

    public VoiceRate(int limit = VoiceRoutes.PacketsPerSecondPerPeer)
    {
        _limit = limit;
    }

    public int Dropped { get; private set; }

    public bool Allow(ulong peer, double now)
    {
        if (!_windows.TryGetValue(peer, out var window) || now - window.Start >= 1)
        {
            window = (now, 0);
        }

        if (window.Count >= _limit)
        {
            _windows[peer] = window;
            Dropped++;
            return false;
        }

        _windows[peer] = (window.Start, window.Count + 1);
        return true;
    }

    public void Forget(ulong peer) => _windows.Remove(peer);

    public void Clear() => _windows.Clear();
}
