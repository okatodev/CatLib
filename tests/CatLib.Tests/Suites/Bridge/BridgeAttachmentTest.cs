using System.Collections.Generic;
using CatLib.Game.Bridge;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.Bridge;

public sealed class BridgeAttachmentTest : TestCase
{
    public override string Suite => "Bridge";

    public override int Order => 0;

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var mismatches = new List<string>();
        foreach (var name in ManagerRegistry.Names)
        {
            if (!ManagerRegistry.IsAttached(name))
            {
                context.Note($"{name}: not attached");
                continue;
            }

            var bound = ManagerRegistry.BoundEventCount(name);
            var expected = ManagerRegistry.ExpectedEventCount(name);
            context.Note($"{name}: attached, {bound}/{expected} events bound");
            if (bound != expected)
            {
                mismatches.Add($"{name} {bound}/{expected}");
            }
        }

        Assert.True(ManagerRegistry.IsAttached(ManagerRegistry.BootstrapManagerName), "Bridge must be attached to BootstrapManager");
        Assert.True(mismatches.Count == 0, "Attached trackers with missing bindings: " + string.Join(", ", mismatches));
        yield break;
    }
}
