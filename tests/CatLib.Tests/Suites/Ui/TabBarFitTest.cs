using System.Collections.Generic;
using CatLib.Tests.Framework;
using CatLib.UI;
using UnityEngine;

namespace CatLib.Tests.Suites.Ui;

public sealed class TabBarFitTest : TestCase
{
    public override string Suite => "Ui";

    public override int Order => 4;

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        foreach (var step in SettingsMenuFixture.EnsureModsTab(context))
        {
            yield return step;
        }

        var modsTab = SettingsMenuFixture.Tab;
        yield return Wait.Until(() => modsTab.Fitter.IsFitted, 5, "the tab bar to be fitted");

        var canvas = modsTab.Options.transform.TryCast<RectTransform>().rect;
        var measured = modsTab.Fitter.Measure(out var left, out var right);

        context.Note($"Screen {Screen.width}x{Screen.height}, canvas {canvas.width:0}x{canvas.height:0}");
        context.Note($"Tab bar spans {left:0}..{right:0} of {canvas.xMin:0}..{canvas.xMax:0}, scale {modsTab.Fitter.Scale:0.###}");

        Assert.True(measured, "The tab bar must have measurable children");
        Assert.True(left >= canvas.xMin, "The left edge of the tab bar must stay on screen");
        Assert.True(right <= canvas.xMax - TabBarFitter.ScreenMargin + 1f, "The right edge of the tab bar, including the page hint, must stay on screen");
    }
}
