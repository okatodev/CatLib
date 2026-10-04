using System.Collections.Generic;
using CatLib.Game;
using System.Linq;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.StackIt;

public sealed class AnchorIndexTest : TestCase
{
    public override string Suite => "StackIt";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        Assert.SequenceEqual(new[] { 0, 1, 2, 2, 2 }, new[] { 1, 2, 3, 4, 5 }.Select(StoreGrid.AnchorIndex),
            "The anchor cell of a parcel side is its half rounded to even, like the game: 1 to 0, 2 to 1, 3 to 2, 4 to 2, 5 to 2");
        yield break;
    }
}
