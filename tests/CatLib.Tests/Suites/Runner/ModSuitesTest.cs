using System.Collections.Generic;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.Runner;

public sealed class ModSuitesTest : TestCase
{
    public override string Suite => "Runner";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        Assert.True(TestCase.IsModInstalled("CatLib"), "CatLib is found");
        Assert.False(TestCase.IsModInstalled("CatLib.NoSuchMod"), "A mod that is not installed is not found");
        Assert.Equal(null, RequiredMod, "Tests of CatLib itself need no mod");
        foreach (var suite in TestCase.ModSuites)
        {
            context.Note($"{suite}: {(TestCase.IsModInstalled(suite) ? "installed" : "not installed, its tests are skipped")}");
        }

        yield break;
    }
}
