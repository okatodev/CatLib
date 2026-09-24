using System.Collections.Generic;
using CatLib.Tests.Framework;
using CatLib.UI;
using TMPro;
using UnityEngine;

namespace CatLib.Tests.Suites.Ui;

public sealed class ModsTabLayoutTest : TestCase
{
    public override string Suite => "Ui";

    public override int Order => 2;

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        foreach (var step in SettingsMenuFixture.EnsureModsTab(context))
        {
            yield return step;
        }

        var modsTab = SettingsMenuFixture.Tab;
        var list = modsTab.ListScroll.transform.TryCast<RectTransform>();
        var content = modsTab.ContentScroll.transform.TryCast<RectTransform>();

        var listRight = list.anchoredPosition.x + list.sizeDelta.x / 2f;
        var contentLeft = content.anchoredPosition.x - content.sizeDelta.x / 2f;
        context.Note($"List pane width {list.sizeDelta.x}, content pane width {content.sizeDelta.x}, gap {contentLeft - listRight}");

        var language = UiText.LanguageCode;
        var tabText = modsTab.Tab.GetComponentInChildren<TMP_Text>(true).text;
        var listHeader = modsTab.ListHeader.GetComponentInChildren<TMP_Text>(true).text;
        var contentHeader = modsTab.ContentHeader.GetComponentInChildren<TMP_Text>(true).text;
        context.Note($"Language: {language}, tab \"{tabText}\", headers \"{listHeader}\" and \"{contentHeader}\"");

        Assert.True(list.parent.Pointer == modsTab.Panel.transform.Pointer, "The list pane must be a direct child of the Mods panel");
        Assert.True(content.parent.Pointer == modsTab.Panel.transform.Pointer, "The content pane must be a direct child of the Mods panel");
        Assert.True(contentLeft - listRight >= ModsTabBuilder.PaneGap - 1f, "The panes must not overlap");
        Assert.True(content.sizeDelta.x > list.sizeDelta.x, "The content pane must be wider than the list pane");
        Assert.Equal(UiText.Get(UiText.ModsTab, language), tabText, "Tab title");
        Assert.Equal(UiText.Get(UiText.ModsList, language), listHeader, "List header");
        var selected = modsTab.Controller.Selected;
        var expectedHeader = selected == null
            ? UiText.Get(UiText.SelectMod, language)
            : string.IsNullOrEmpty(selected.Version) ? selected.DisplayName : selected.DisplayName + " " + selected.Version;
        Assert.Equal(expectedHeader, contentHeader, "Content header");
        Assert.Equal(1 + modsTab.Controller.Items.Count, modsTab.ListScroll.content.childCount, "Children of the list pane content");
        Assert.True(modsTab.Controller.Items.Count >= 1, "The Mods list must contain at least the CatLib.Tests demo mod");
        yield break;
    }
}
