using System.Collections.Generic;
using System.Runtime.InteropServices;
using BepInEx;
using CatLib.Game;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.Platform;

public sealed class RuntimeInfoTest : TestCase
{
    public override string Suite => "Platform";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        context.Note($"Trigger: {context.Trigger}");
        context.Note($"CatLib: {CatLib.PluginMeta.Version}");
        context.Note($"BepInEx: {Paths.BepInExVersion}");
        context.Note($"Runtime: {RuntimeInformation.FrameworkDescription}");
        context.Note($"OS: {RuntimeInformation.OSDescription}");
        context.Note($"Unity: {GameInfo.UnityVersion}");
        context.Note($"Application version: {GameInfo.ApplicationVersion}");

        Assert.NotEmpty(GameInfo.UnityVersion, "Unity version");
        yield break;
    }
}
