using System.Collections.Generic;
using CatLib.Game.Bridge;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.Bridge;

public sealed class BridgeErrorFreeTest : TestCase
{
    public override string Suite => "Bridge";

    public override int Order => 1;

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var failures = new List<string>();
        foreach (var name in ManagerRegistry.Names)
        {
            var error = ManagerRegistry.LastError(name);
            if (error != null)
            {
                failures.Add($"{name}: {error}");
            }
        }

        foreach (var failure in failures)
        {
            context.Note(failure);
        }

        Assert.Equal(0, failures.Count, "Managers with binding errors");
        yield break;
    }
}
