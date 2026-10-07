using System.Collections.Generic;
using CatLib.Game;
using CatLib.Tests.Framework;
using CatLib.UI;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.UI;

namespace CatLib.Tests.Suites.Ui;

public sealed class MenuVersionBadgeTest : TestCase
{
    public override string Suite => "Ui";

    public override int Order => 100;

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        if (SettingsMenuFixture.Context != MenuContext.MainMenu || !Singleton<MainMenuInterfacesManager>.HasInstance())
        {
            context.Note("The version badge can only be checked in the main menu");
            yield break;
        }

        var manager = Singleton<MainMenuInterfacesManager>.Instance;
        var menu = manager.MainMenuInterface;
        if (!menu.IsShown)
        {
            manager.ShowMainMenu();
        }

        yield return Wait.Until(() => menu.IsShown && MenuVersionBadge.Root != null, 5, "the main menu with the version badge");
        yield return Wait.Seconds(1);

        var check = GameCompatibility.Current;
        Assert.NotNull(check, "The game build is checked in the main menu");
        context.Note($"Game: {check.Running.Describe()}");
        context.Note($"Made for: {check.Target.Describe()}");
        context.Note($"Status: {check.Status}");
        Assert.NotEqual(GameBuildStatus.Unknown, check.Status, "The game build is recognised");

        var root = MenuVersionBadge.Root;
        var rect = root.transform.TryCast<RectTransform>();
        var canvas = menu.GetComponentInParent<Canvas>().rootCanvas;
        Assert.True(root.transform.parent.Pointer == canvas.transform.Pointer, "The badge sits on the main menu canvas");
        Assert.True(root.GetComponent<CanvasGroup>().alpha > 0.99f, "The badge is visible while the main menu is shown");
        Assert.True(MenuVersionBadge.Label.text.Contains(VersionBadgeText.Title), "The badge shows the CatLib version");
        Assert.True(MenuVersionBadge.IsPlateShown, "The badge has a plate to stay readable over any background");
        Assert.Equal(check.IsWarning, MenuVersionBadge.IsPlateWarning, "The plate is amber only with a warning");

        var canvasRect = canvas.transform.TryCast<RectTransform>();
        var area = canvasRect.rect;
        var mine = CanvasRect(rect, canvasRect);
        context.Note($"Badge at {mine.xMin:0},{mine.yMin:0} size {mine.width:0}x{mine.height:0} on a {area.width:0}x{area.height:0} canvas, lifted by {MenuVersionBadge.Lift:0}");
        Assert.True(mine.xMax <= area.xMax + 0.5f && mine.yMin >= area.yMin - 0.5f && mine.xMin >= area.center.x && mine.yMax <= area.center.y,
            "The badge is inside the bottom right quarter of the screen");
        foreach (var selectable in menu.GetComponentsInChildren<Selectable>(false))
        {
            var other = CanvasRect(selectable.transform.TryCast<RectTransform>(), canvasRect);
            if (other.width < area.width * MenuVersionBadge.MaxObstacleShare && other.height < area.height * MenuVersionBadge.MaxObstacleShare)
            {
                Assert.False(other.Overlaps(mine), $"The badge does not cover the {selectable.name} button");
            }
        }

        var calmText = MenuVersionBadge.Label.text;
        var calmSize = rect.sizeDelta;
        var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        var center = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
        try
        {
            MenuVersionBadge.PointerOverride = () => new Vector3(center.x, center.y, 0f);
            yield return Wait.Frames(3);
            Assert.True(MenuVersionBadge.IsHovered, "Pointing at the badge is noticed");
            Assert.True(MenuVersionBadge.Label.text.Split('\n').Length > calmText.Split('\n').Length, "Pointing at the badge shows the detail line");
            context.Note("Hover: " + MenuVersionBadge.Label.text.Replace("\n", " | "));
            var mods = MenuVersionBadge.ModsLabel;
            Assert.True(mods != null && mods.gameObject.activeSelf && mods.text.Length > 0, "Pointing at the badge lists the installed mods");
            context.Note("Mods: " + mods.text);
            context.Note(MenuVersionBadge.UsesGamePaper ? "The badge is on the game's paper" : "The game's paper was not found, the badge is on a plain plate");

            MenuVersionBadge.PointerOverride = () => new Vector3(-10000f, -10000f, 0f);
            yield return Wait.Frames(3);
            Assert.Equal(calmText, MenuVersionBadge.Label.text, "Moving the pointer away hides the detail line");
            Assert.False(MenuVersionBadge.ModsLabel.gameObject.activeSelf, "Moving the pointer away hides the mods");

            foreach (var preview in new[] { "CMC 1.02.00.1800.9800.100", "CMC 1.01.00.1700.9722.30000" })
            {
                MenuVersionBadge.PreviewCheck = GameCompatibility.Compare(GameBuild.Parse(preview), GameCompatibility.Supported);
                yield return Wait.Frames(3);
                var warning = VersionBadgeText.Warning(MenuVersionBadge.PreviewCheck, UiText.LanguageCode);
                Assert.True(MenuVersionBadge.Label.text.Contains(warning), $"The {MenuVersionBadge.PreviewCheck.Status} warning is shown");
                Assert.True(MenuVersionBadge.IsPlateWarning, "A warning puts the stamp on the badge");
                Assert.True(rect.sizeDelta.y > calmSize.y || check.IsWarning, "A warning makes the badge taller");
                var bounds = CanvasRect(rect, canvasRect);
                Assert.True(bounds.xMin >= area.xMin && bounds.xMax <= area.xMax + 0.5f, "The warning fits on the screen");
                context.Note($"{MenuVersionBadge.PreviewCheck.Status}: {MenuVersionBadge.Label.text.Replace("\n", " | ")}, {bounds.width:0}x{bounds.height:0}");
                yield return Wait.Seconds(1.5);
            }
        }
        finally
        {
            MenuVersionBadge.PointerOverride = null;
            MenuVersionBadge.PreviewCheck = null;
        }

        yield return Wait.Frames(3);
        Assert.Equal(calmText, MenuVersionBadge.Label.text, "The badge returns to the real check");

        manager.ShowSettingsMenu();
        yield return Wait.Seconds(1);
        Assert.True(root.GetComponent<CanvasGroup>().alpha < 0.01f, "The badge hides with the main menu");
        manager.GoBackToLastInterface();
        yield return Wait.Seconds(1);
        Assert.True(root.GetComponent<CanvasGroup>().alpha > 0.99f, "The badge comes back with the main menu");
    }

    private static Rect CanvasRect(RectTransform rect, RectTransform canvasRect)
    {
        var corners = new Il2CppStructArray<Vector3>(4);
        rect.GetWorldCorners(corners);
        var min = canvasRect.InverseTransformPoint(corners[0]);
        var max = canvasRect.InverseTransformPoint(corners[2]);
        return Rect.MinMaxRect(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y), Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
    }
}
