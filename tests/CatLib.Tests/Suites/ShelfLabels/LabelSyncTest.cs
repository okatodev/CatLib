using System.Collections.Generic;
using System.Linq;
using CatLib.Net;
using CatLib.Tests.Framework;
using CatLib.Tests.Suites.Network;
using ShelfLabels;
using static CatLib.Tests.Suites.Network.NetworkFixture;

namespace CatLib.Tests.Suites.ShelfLabels;

public sealed class LabelSyncTest : TestCase
{
    public override string Suite => "ShelfLabels";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var mod = Mod("catlib.shelflabels", "0.1.0", SessionPolicy.RequiredOnAll);
        var fixture = new ModMessagingFixture(Identity(mod), Identity(mod));
        var hostBoard = new LabelBoard();
        var clientBoard = new LabelBoard();
        var hostPlacements = new PlacementBoard();
        var clientPlacements = new PlacementBoard();
        var hostStands = new StandBoard();
        var clientStands = new StandBoard();
        var hideStands = false;
        var slots = 2;
        var defaultPlacement = Placement.Auto;
        var hostSync = new LabelSync(fixture.HostMessenger.Channel("catlib.shelflabels"), hostBoard, hostPlacements, hostStands, id => id == 390 || id == 12 ? 10 : 0, () => slots, () => defaultPlacement, () => hideStands, null);
        var clientSync = new LabelSync(fixture.ClientMessenger.Channel("catlib.shelflabels"), clientBoard, clientPlacements, clientStands, id => 10, () => slots, () => Placement.Right, () => true, null);

        hostBoard.Set(new LabelSlot(12, 1), 5);
        hostPlacements.Set(12, Placement.Below);
        clientBoard.Set(new LabelSlot(777, 1), 3);
        clientPlacements.Set(777, Placement.Left);
        hostStands.Set(12, true);
        clientStands.Set(777, true);
        fixture.Connect();
        fixture.Pump();
        Assert.Equal(1, clientBoard.Count, "Joining replaces whatever the player had with the host's pictures");
        Assert.Equal(5, clientBoard.Get(new LabelSlot(12, 1)), "The host's picture arrived");
        Assert.Equal(1, clientPlacements.Count, "Joining replaces the player's placements too");
        Assert.Equal(Placement.Below, clientPlacements.Get(12, Placement.Auto), "The host's placement arrived");
        Assert.Equal(1, clientStands.Count, "Joining replaces the player's stand choices too");
        Assert.True(clientStands.IsHidden(12, false), "The host's stand choice arrived");

        Assert.True(clientSync.ToggleStand(390), "A player asks to hide a stand");
        fixture.Pump();
        Assert.True(hostStands.IsHidden(390, false) && clientStands.IsHidden(390, false), "The stand is hidden for both, starting from the host's default");
        hideStands = true;
        hostSync.ToggleStand(12);
        fixture.Pump();
        Assert.False(clientStands.IsHidden(12, true), "The host shows the stand again for everyone");

        Assert.True(clientSync.CyclePlacement(390), "A player asks to change a placement");
        fixture.Pump();
        Assert.Equal(Placement.Right, hostPlacements.Get(390, Placement.Auto), "The host moves from its default to the next placement");
        Assert.Equal(Placement.Right, clientPlacements.Get(390, Placement.Auto), "The player follows");
        defaultPlacement = Placement.Below;
        clientSync.CyclePlacement(12);
        hostSync.CyclePlacement(12);
        fixture.Pump();
        Assert.Equal(Placement.Auto, hostPlacements.Get(12, Placement.Auto), "Below, then no copies, then back to the freer side");
        Assert.Equal(Placement.Auto, clientPlacements.Get(12, Placement.Auto), "Both players end with the same placement");

        Assert.True(clientSync.Click(new LabelSlot(390, 2), 1), "A player's click goes to the host");
        Assert.Equal(0, clientBoard.Get(new LabelSlot(390, 2)), "The player does not change anything by itself");
        fixture.Pump();
        Assert.Equal(1, hostBoard.Get(new LabelSlot(390, 2)), "The host applied the click");
        Assert.Equal(1, clientBoard.Get(new LabelSlot(390, 2)), "The player got the result");

        clientSync.Click(new LabelSlot(390, 2), -1);
        clientSync.Click(new LabelSlot(390, 2), -1);
        fixture.Pump();
        Assert.Equal(9, hostBoard.Get(new LabelSlot(390, 2)), "Two quick clicks back both count and wrap");
        Assert.Equal(9, clientBoard.Get(new LabelSlot(390, 2)), "The player follows");

        hostSync.Click(new LabelSlot(12, 1), 1);
        fixture.Pump();
        Assert.Equal(6, hostBoard.Get(new LabelSlot(12, 1)), "The host's own click");
        Assert.Equal(6, clientBoard.Get(new LabelSlot(12, 1)), "reaches the player");

        clientSync.Click(new LabelSlot(390, 3), 1);
        clientSync.Click(new LabelSlot(55, 1), 1);
        fixture.ClientMessenger.Channel("catlib.shelflabels").SendToHost(LabelSync.StepMessage, "390/1/+5");
        fixture.ClientMessenger.Channel("catlib.shelflabels").SendToHost(LabelSync.SetMessage, "390/1=4");
        fixture.ClientMessenger.Channel("catlib.shelflabels").SendToHost(LabelSync.PlaceMessage, "55/+1");
        fixture.ClientMessenger.Channel("catlib.shelflabels").SendToHost(LabelSync.PlaceMessage, "390/-1");
        fixture.ClientMessenger.Channel("catlib.shelflabels").SendToHost(LabelSync.LayoutMessage, "390=3");
        fixture.ClientMessenger.Channel("catlib.shelflabels").SendToHost(LabelSync.StandMessage, "55/toggle");
        fixture.ClientMessenger.Channel("catlib.shelflabels").SendToHost(LabelSync.StandMessage, "390/on");
        fixture.ClientMessenger.Channel("catlib.shelflabels").SendToHost(LabelSync.StandsMessage, "390=0");
        fixture.Pump();
        Assert.Equal(10, hostSync.Rejected, "Hidden slots, unknown labels, bad steps, forged states and bad placement or stand requests are refused");
        Assert.True(hostStands.IsHidden(390, false), "Refused stand requests change nothing");
        Assert.Equal(Placement.Right, hostPlacements.Get(390, Placement.Auto), "Refused placement requests change nothing");
        Assert.False(hostBoard.Has(new LabelSlot(390, 3)) || hostBoard.Has(new LabelSlot(55, 1)) || hostBoard.Has(new LabelSlot(390, 1)), "Refused requests change nothing");

        slots = 1;
        clientSync.Click(new LabelSlot(390, 2), 1);
        fixture.Pump();
        Assert.Equal(9, hostBoard.Get(new LabelSlot(390, 2)), "A slot hidden by the host cannot be changed");
        Assert.True(hostBoard.Has(new LabelSlot(390, 2)), "but its picture is kept");

        Assert.True(LabelSync.TryParseStep("390/2/-1", out var parsed, out var step) && parsed == new LabelSlot(390, 2) && step == -1, "Step parsing");
        Assert.False(LabelSync.TryParseStep("390/2/0", out _, out _), "Zero step");
        Assert.False(LabelSync.TryParseStep("390/2", out _, out _), "Missing step");
        yield break;
    }
}
