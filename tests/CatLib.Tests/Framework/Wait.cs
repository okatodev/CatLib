using System;

namespace CatLib.Tests.Framework;

public static class Wait
{
    public static TestStep NextFrame() => new WaitFramesStep(1);

    public static TestStep Frames(int frames) => new WaitFramesStep(frames);

    public static TestStep Seconds(double seconds) => new WaitSecondsStep(seconds);

    public static TestStep Until(Func<bool> condition, double timeoutSeconds, string description) =>
        new WaitUntilStep(condition, timeoutSeconds, description);
}
