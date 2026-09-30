using System.Collections.Generic;
using CatLib.Tests.Framework;
using CatLib.UI;

namespace CatLib.Tests.Suites.Presentation;

public sealed class ScrollRevealTest : TestCase
{
    public override string Suite => "Presentation";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        Assert.Equal(0f, ScrollReveal.Offset(-400f, 0f, -200f, -150f, 10f), "A row inside the view stays where it is");
        Assert.Equal(60f, ScrollReveal.Offset(-400f, 0f, -450f, -400f, 10f), "A row below the view moves up until it is in view with the margin");
        Assert.Equal(-60f, ScrollReveal.Offset(-400f, 0f, 0f, 50f, 10f), "A row above the view moves down until it is in view with the margin");
        Assert.Equal(-110f, ScrollReveal.Offset(-400f, 0f, -300f, 100f, 10f), "A row taller than the view shows its top");
        Assert.Equal(5f, ScrollReveal.Offset(-400f, 0f, -395f, -300f, 10f), "A row cut by the bottom edge moves up a little");
        yield break;
    }
}
