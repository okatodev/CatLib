using System.Collections.Generic;
using CatLib.Tests.Framework;
using CatLib.UI;

namespace CatLib.Tests.Suites.Ui;

public sealed class ModCardTest : TestCase
{
    public override string Suite => "Ui";

    public override int Order => 11;

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        using var sandbox = new UiRowsSandbox("UiCard");
        foreach (var step in SettingsMenuFixture.SelectMod(context, sandbox.Settings))
        {
            yield return step;
        }

        var card = SettingsMenuFixture.Tab.Controller.Card;
        var language = UiText.LanguageCode;

        Assert.Equal(sandbox.Settings.DisplayName, card.Title.text, "Card title");
        Assert.Equal(string.Empty, card.Version.text, "A mod without a version shows no version");
        Assert.Equal(UiText.Plural(UiText.SettingsCount, UiRowsSandbox.VisibleCount, language), card.Status.text, "Card status");
        Assert.Equal(0, card.Root.transform.GetSiblingIndex(), "The card must be the first element of the settings pane");
        Assert.False(card.Description.gameObject.activeSelf, "A mod without a description shows none");
        Assert.Equal(ModCard.Height, card.CurrentHeight, "Without a description the card keeps its height");

        sandbox.Settings.Description = "A test mod with a description long enough to wrap onto a second line of the card, so the card grows with it.";
        SettingsMenuFixture.Tab.Controller.Select(sandbox.Settings);
        yield return Wait.NextFrame();
        context.Note($"Card with a description: {card.CurrentHeight:0} high, description {card.Description.rectTransform.rect.height:0}");
        Assert.True(card.Description.gameObject.activeSelf, "The description shows under the status and the author");
        Assert.True(card.CurrentHeight > ModCard.Height, "The card grows with its description");
        Assert.True(card.Description.rectTransform.rect.width > card.Title.rectTransform.rect.width, "The description takes the whole width, also under the icon");

        sandbox.FastMode.Entry.Value = true;
        yield return Wait.Frames(ModsPanel.CardRefreshIntervalFrames + 2);

        var expected = UiText.Plural(UiText.SettingsCount, UiRowsSandbox.VisibleCount, language) + " · " + UiText.Plural(UiText.PendingRestart, 1, language);
        context.Note("Status with a pending restart: " + card.Status.text);
        Assert.Equal(expected, card.Status.text, "Card status with a pending restart");
    }
}
