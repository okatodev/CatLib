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

        sandbox.FastMode.Entry.Value = true;
        yield return Wait.Frames(ModsPanel.CardRefreshIntervalFrames + 2);

        var expected = UiText.Plural(UiText.SettingsCount, UiRowsSandbox.VisibleCount, language) + " · " + UiText.Plural(UiText.PendingRestart, 1, language);
        context.Note("Status with a pending restart: " + card.Status.text);
        Assert.Equal(expected, card.Status.text, "Card status with a pending restart");
    }
}
