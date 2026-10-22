using System.Collections.Generic;
using System.Linq;

namespace CatLib.Voice;

public sealed class VoiceActivity
{
    public const double DefaultHoldSeconds = 0.35;

    private readonly Dictionary<ulong, double> _lastHeard = new();
    private readonly HashSet<ulong> _speaking = new();
    private readonly double _hold;

    public VoiceActivity(double holdSeconds = DefaultHoldSeconds)
    {
        _hold = holdSeconds;
    }

    public IReadOnlyCollection<ulong> Speaking => _speaking;

    public bool IsSpeaking(ulong speaker) => _speaking.Contains(speaker);

    public bool Heard(ulong speaker, double now)
    {
        _lastHeard[speaker] = now;
        return _speaking.Add(speaker);
    }

    public IReadOnlyList<ulong> Expire(double now)
    {
        var quiet = _speaking.Where(speaker => !_lastHeard.TryGetValue(speaker, out var at) || now - at > _hold).ToList();
        foreach (var speaker in quiet)
        {
            _speaking.Remove(speaker);
        }

        return quiet;
    }

    public bool Forget(ulong speaker)
    {
        _lastHeard.Remove(speaker);
        return _speaking.Remove(speaker);
    }

    public IReadOnlyList<ulong> Clear()
    {
        var all = _speaking.ToList();
        _speaking.Clear();
        _lastHeard.Clear();
        return all;
    }
}
