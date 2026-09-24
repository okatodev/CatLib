using System;
using System.Diagnostics;
using CatLib.Core;

namespace CatLib.Tests.Framework;

public abstract class TestStep
{
    internal abstract void Begin();

    internal abstract bool IsDone();
}

internal sealed class WaitFramesStep : TestStep
{
    private readonly int _frames;
    private long _targetFrame;

    public WaitFramesStep(int frames)
    {
        _frames = Math.Max(0, frames);
    }

    internal override void Begin() => _targetFrame = FrameLoop.FrameCount + _frames;

    internal override bool IsDone() => FrameLoop.FrameCount >= _targetFrame;
}

internal sealed class WaitSecondsStep : TestStep
{
    private readonly TimeSpan _duration;
    private readonly Stopwatch _stopwatch = new();

    public WaitSecondsStep(double seconds)
    {
        _duration = TimeSpan.FromSeconds(Math.Max(0, seconds));
    }

    internal override void Begin() => _stopwatch.Restart();

    internal override bool IsDone() => _stopwatch.Elapsed >= _duration;
}

internal sealed class WaitUntilStep : TestStep
{
    private readonly Func<bool> _condition;
    private readonly TimeSpan _timeout;
    private readonly string _description;
    private readonly Stopwatch _stopwatch = new();

    public WaitUntilStep(Func<bool> condition, double timeoutSeconds, string description)
    {
        _condition = condition ?? throw new ArgumentNullException(nameof(condition));
        _timeout = TimeSpan.FromSeconds(Math.Max(0, timeoutSeconds));
        _description = description;
    }

    internal override void Begin() => _stopwatch.Restart();

    internal override bool IsDone()
    {
        if (_condition())
        {
            return true;
        }

        if (_stopwatch.Elapsed >= _timeout)
        {
            throw new AssertionException($"Timed out after {_timeout.TotalSeconds:0.##} s waiting for {_description}");
        }

        return false;
    }
}
