using System.Collections.Generic;
using CatLib.Tests.Framework;
using CatLib.UI;

namespace CatLib.Tests.Suites.Ui;

public sealed class MenuNoticesTest : TestCase
{
    public const string Notice = "CatLib notice test";

    public override string Suite => "Ui";

    public override int Order => 19;

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        if (SettingsMenuFixture.Context != MenuContext.MainMenu)
        {
            context.Note("Main menu notices can only be checked in the main menu");
            yield break;
        }

        var lobby = MenuNotices.Lobby();
        Assert.NotNull(lobby, "The multiplayer lobby interface must be found");
        Assert.NotNull(lobby.BackButton, "The lobby must have its back button");
        Assert.False(lobby.IsShown, "The lobby is not shown in the main menu, so leaving must do nothing");
        Assert.False(MenuNotices.LeaveLobby(), "Leaving is refused while the lobby is hidden");

        var label = MenuNotices.LobbyLabel();
        Assert.NotNull(label, "The lobby waiting label must be found");
        var original = label.text;
        MenuNotices.Apply(label, Notice);
        var withNotice = label.text;
        MenuNotices.Apply(label, Notice);
        var repeated = label.text;
        MenuNotices.Apply(label, null);
        context.Note($"Lobby label: \"{original}\"");

        Assert.True(withNotice.StartsWith(original) && withNotice.Contains(Notice), "The notice is added under the lobby label");
        Assert.Equal(withNotice, repeated, "Applying the same notice twice changes nothing");
        Assert.Equal(original, label.text, "Clearing restores the lobby label");

        var slots = lobby._playerSlots;
        context.Note($"Lobby player cards: {(slots == null ? 0 : slots.Count)}");
        if (slots != null && slots.Count > 0)
        {
            var card = slots[0].PlayerNameText;
            Assert.NotNull(card, "A player card must have a name label");
            var name = card.text;
            MenuNotices.Apply(card, Notice);
            var marked = card.text;
            MenuNotices.Apply(card, null);
            Assert.True(marked.StartsWith(name) && marked.Contains(Notice), "The notice is added under the player name");
            Assert.Equal(name, card.text, "Clearing restores the player name");
        }

        MenuNotices.SetPlayerNotice(76561190000000002UL, Notice);
        MenuNotices.ApplyToPlayerCards(lobby);
        var leaked = 0;
        for (var index = 0; slots != null && index < slots.Count; index++)
        {
            leaked += slots[index].PlayerNameText != null && slots[index].PlayerNameText.text.Contains(Notice) ? 1 : 0;
        }

        MenuNotices.ClearPlayerNotice(76561190000000002UL);
        Assert.Equal(0, leaked, "A notice for a player who is not in the lobby must not appear on any card");
        yield break;
    }
}
