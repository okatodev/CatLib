using System.Collections.Generic;
using System.Linq;
using CatLib.Net;
using CatLib.Tests.Framework;
using CatLib.UI;

namespace CatLib.Tests.Suites.Presentation;

public sealed class LobbyRosterTextTest : TestCase
{
    public const ulong HostId = 76561190000000001UL;
    public const ulong FriendId = 76561190000000002UL;

    public override string Suite => "Presentation";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var host = Identity(Mod("boat", "Boat Tweaks", "0.2.0", SessionPolicy.RequiredOnAll), Mod("labels", "Shelf Labels", "0.1.0", SessionPolicy.RequiredOnAll),
            Mod("look", "Look", "1.0.0", SessionPolicy.ClientOnly));
        var friend = Identity(Mod("boat", "Boat Tweaks", "0.1.0", SessionPolicy.RequiredOnAll));
        var roster = RosterBuilder.Build(host, HostId, "Okato", IncompatiblePlayerAction.Warn, new[]
        {
            new RosterPeer(FriendId, "Okato Nekomato", RosterPeerState.Evaluated, friend, Compatibility.Compare(host, friend), false)
        });

        var toggles = 0;
        var content = LobbyRosterText.Build(roster, FriendId, false, "ru", () => toggles++);
        Assert.Equal("Моды игроков", content.Title, "Russian title");
        Assert.Equal("2 игрока · на паузе: 2", content.Summary, "Two mods are paused for two players");
        Assert.Equal(FoldoutTone.Warning, content.SummaryTone, "A warning when mods are paused");
        Assert.Equal(2, content.Sections.Count, "One section per player");
        Assert.Equal("Okato (хост)", content.Sections[0].Title, "The host is marked");
        Assert.Equal("Okato Nekomato (вы)", content.Sections[1].Title, "The local player is marked");
        Assert.Equal("3 мода", content.Sections[0].Detail, "The host's mod count");
        Assert.Equal(string.Empty, content.Sections[0].Status, "No status next to a host that has CatLib");
        Assert.Equal("другие моды", content.Sections[1].Status, "The friend has other mods");

        var friendRows = content.Sections[1].Rows;
        var boat = friendRows.Single(row => row.Left == "Boat Tweaks");
        Assert.Equal("0.1.0", boat.Middle, "The friend's own version");
        Assert.Equal("у хоста 0.2.0", boat.Right, "Advice with the host's version");
        Assert.Equal(FoldoutTone.Bad, boat.Tone, "Another version is a problem");
        var labels = friendRows.Single(row => row.Left == "Shelf Labels");
        Assert.Equal("нет · у хоста 0.1.0", labels.Right, "A missing mod says which version to install");
        Assert.Equal("-", labels.Middle, "No version for a missing mod");
        var hostBoat = content.Sections[0].Rows.Single(row => row.Left == "Boat Tweaks");
        Assert.Equal("на паузе", hostBoat.Right, "The host's own mod is paused while the friend has another version");
        Assert.Equal("только у себя", content.Sections[0].Rows.Single(row => row.Left == "Look").Right, "Local mods are not checked");

        var policy = content.TopRows.Single();
        Assert.Null(policy.OnClick, "Only the host can change the policy");
        var hostView = LobbyRosterText.Build(roster, HostId, true, "en", () => toggles++);
        Assert.NotNull(hostView.TopRows.Single().OnClick, "The host can change the policy in the lobby");
        hostView.TopRows.Single().OnClick();
        Assert.Equal(1, toggles, "Clicking the policy row toggles it");
        Assert.Equal("let in, pause the mods", hostView.TopRows.Single().Right, "English policy text");

        var same = Identity(Mod("boat", "Boat Tweaks", "0.2.1", SessionPolicy.RequiredOnAll), Mod("labels", "Shelf Labels", "0.1.0", SessionPolicy.RequiredOnAll));
        var good = RosterBuilder.Build(host, HostId, "Okato", IncompatiblePlayerAction.Warn, new[]
        {
            new RosterPeer(FriendId, "Friend", RosterPeerState.Evaluated, same, Compatibility.Compare(host, same), false)
        });
        var goodView = LobbyRosterText.Build(good, HostId, true, "en", null);
        Assert.Equal("2 players · all match", goodView.Summary, "Everything matches");
        Assert.Equal(FoldoutTone.Good, goodView.SummaryTone, "Good tone");
        Assert.Equal("works", goodView.Sections[1].Rows.First().Right, "Shared mods work");
        Assert.False(goodView.Signature() == content.Signature(), "Different content has a different signature");
        Assert.Equal(goodView.Signature(), LobbyRosterText.Build(good, HostId, true, "en", null).Signature(), "The same content has the same signature");
        Assert.Equal(0, LobbyRosterText.Build(null, HostId, true, "en", null).Sections.Count, "No roster, no sections");
        yield break;
    }

    private static ModInfo Mod(string id, string name, string version, SessionPolicy policy) => new(id, name, version, policy, VersionRule.SameMinor);

    private static LocalIdentity Identity(params ModInfo[] mods) => new("0.6.0", "CMC 1.01", mods);
}
