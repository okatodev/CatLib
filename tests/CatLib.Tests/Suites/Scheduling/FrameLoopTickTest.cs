using System.Collections.Generic;
using CatLib.Core;
using CatLib.Tests.Framework;
using UnityEngine;

namespace CatLib.Tests.Suites.Scheduling;

public sealed class FrameLoopTickTest : TestCase
{
    private const int FramesToObserve = 30;

    public override string Suite => "Scheduling";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var loopStart = FrameLoop.FrameCount;
        var unityStart = Time.frameCount;

        yield return Wait.Frames(FramesToObserve);

        var loopDelta = FrameLoop.FrameCount - loopStart;
        var unityDelta = (long)(Time.frameCount - unityStart);
        context.Note($"FrameLoop advanced {loopDelta} frames, Unity advanced {unityDelta} frames");

        Assert.Equal((long)FramesToObserve, loopDelta, "FrameLoop frame delta");
        Assert.Equal(unityDelta, loopDelta, "FrameLoop must tick exactly once per Unity frame");
    }
}
