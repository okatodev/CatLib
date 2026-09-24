using System.Collections.Generic;
using CatLib.Game.Events;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.Bridge;

public sealed class MainMenuObservedTest : TestCase
{
    public override string Suite => "Bridge";

    public override int Order => 2;

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        foreach (var counter in GameEventStream.SnapshotCounters())
        {
            context.Note($"{counter.Key}: {counter.Value}");
        }

        Assert.AtLeast(1, GameEventStream.CountOf(BootstrapEvents.MainMenuLoadedName), "Observed MainMenuLoaded events");
        yield break;
    }
}
