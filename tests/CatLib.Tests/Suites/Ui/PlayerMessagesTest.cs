using System.Collections.Generic;
using CatLib.Tests.Framework;
using CatLib.UI;

namespace CatLib.Tests.Suites.Ui;

public sealed class PlayerMessagesTest : TestCase
{
    public override string Suite => "Ui";

    public override int Order => 14;

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        using var sandbox = new UiRowsSandbox("UiMessages");
        foreach (var step in SettingsMenuFixture.SelectMod(context, sandbox.Settings))
        {
            yield return step;
        }

        var controller = SettingsMenuFixture.Tab.Controller;
        var language = UiText.LanguageCode;

        sandbox.Config.WriteValue("General", "Count", "50");
        yield return Wait.Until(() => sandbox.Config.Adjusted.Count >= 1, 5, "the out of range value to be adjusted");
        yield return Wait.Frames(2);

        var adjusted = UiText.Format(UiText.MessageAdjusted, language, sandbox.Settings.DisplayName, "Count", "50", "10");
        Assert.Equal(adjusted, PlayerMessages.Last, "Message for an adjusted value");
        Assert.Equal(adjusted, controller.StatusLabel.text, "The status line must show the latest message");

        sandbox.Config.WriteValue("General", "Count", "many");
        yield return Wait.Until(() => sandbox.Config.Rejected.Count >= 1, 5, "the invalid value to be rejected");

        Assert.Equal(UiText.Format(UiText.MessageRejected, language, sandbox.Settings.DisplayName, "Count", "many", "10"), PlayerMessages.Last, "Message for a rejected value");

        sandbox.FastMode.Entry.Value = true;
        yield return Wait.Frames(2);

        Assert.Equal(UiText.Format(UiText.MessageRestart, language, sandbox.Settings.DisplayName, "Fast Mode"), PlayerMessages.Last, "Message for a restart-only change");
        context.Note("Warning log lines about \"50\" and \"many\" are expected");
    }
}
