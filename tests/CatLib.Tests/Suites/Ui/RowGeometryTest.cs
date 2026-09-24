using System.Collections.Generic;
using CatLib.Tests.Framework;
using CatLib.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CatLib.Tests.Suites.Ui;

public sealed class RowGeometryTest : TestCase
{
    public const float Tolerance = 12f;
    public const float MaxTrackThickness = 60f;

    public override string Suite => "Ui";

    public override int Order => 10;

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        using var sandbox = new UiRowsSandbox("UiGeometry");
        foreach (var step in SettingsMenuFixture.SelectMod(context, sandbox.Settings))
        {
            yield return step;
        }

        var controller = SettingsMenuFixture.Tab.Controller;

        foreach (var row in controller.Rows)
        {
            var labelPanel = row.Root.transform.Find("panel_Label")?.TryCast<RectTransform>();
            var dash = row.Root.transform.Find("panel_Label/img_DashLine")?.TryCast<RectTransform>();
            if (labelPanel == null || dash == null)
            {
                continue;
            }

            var dashExtent = RectExtent.Of(dash, labelPanel);
            var panelRect = labelPanel.rect;
            Assert.True(dashExtent.MinX >= panelRect.xMin - 1f && dashExtent.MaxX <= panelRect.xMax + 1f,
                $"Dash line of {row.Setting.Key} ({dashExtent}) must stay inside its label panel (x {panelRect.xMin:0}..{panelRect.xMax:0})");
        }

        foreach (var key in new[] { "Count", "Speed" })
        {
            var slider = SettingsMenuFixture.Row<SliderRow>(key).Slider;
            var sliderRect = slider.transform.TryCast<RectTransform>();
            var background = slider.transform.Find("img_Bkg").TryCast<RectTransform>();
            var track = RectExtent.Of(background, sliderRect);
            context.Note($"{key}: slider width {sliderRect.rect.width:0}, track {track}");

            Assert.True(Mathf.Abs(track.Width - sliderRect.rect.width) <= Tolerance,
                $"Track of {key} must span the slider: track {track.Width:0}, slider {sliderRect.rect.width:0}");
            Assert.True(track.Height > 5f && track.Height <= MaxTrackThickness,
                $"Track of {key} must have a visible thickness, got {track.Height:0}");
        }

        var headers = new List<GameObject>(controller.SectionHeaders) { SettingsMenuFixture.Tab.ListHeader };
        foreach (var header in headers)
        {
            var label = header.GetComponentInChildren<TMP_Text>(true);
            var tape = label.transform.parent.TryCast<RectTransform>();
            var preferred = label.GetPreferredValues(label.text).x;
            Assert.True(preferred <= tape.rect.width,
                $"Tape \"{label.text}\" must fit its text: text {preferred:0}, tape {tape.rect.width:0}");
        }
    }
}
