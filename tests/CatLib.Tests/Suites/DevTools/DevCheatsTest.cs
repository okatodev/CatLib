using System;
using System.Collections.Generic;
using System.Linq;
using CatLib.DevTools;
using CatLib.Tests.Diagnostics;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.DevTools;

public sealed class DevCheatsTest : TestCase
{
    public override string Suite => "DevTools";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var sizes = ParcelSpawner.Sizes.Select(item => item.Size).ToList();
        var gameSizes = Enum.GetValues(typeof(PackageSize)).Cast<PackageSize>().Where(size => size != PackageSize.None).ToList();
        Assert.Equal(gameSizes.Count, sizes.Count, "Every parcel size of the game can be spawned");
        Assert.Equal(sizes.Count, sizes.Distinct().Count(), "and each size once");
        Assert.Equal("Flat letter", ParcelSpawner.LabelOf(PackageSize.FlatLetter), "A size has a readable name");

        Assert.Equal(ParcelSpawner.Distance, ParcelSpawner.SpawnDistance(null), "Nothing in front: the parcel appears at the usual distance");
        Assert.Equal(ParcelSpawner.Distance, ParcelSpawner.SpawnDistance(5f), "A far wall changes nothing");
        Assert.True(Math.Abs(ParcelSpawner.SpawnDistance(1.2f) - (1.2f - ParcelSpawner.WallGap)) < 0.0001f, "A near wall: the parcel appears before it");
        Assert.Equal(ParcelSpawner.NearestDistance, ParcelSpawner.SpawnDistance(0.1f), "but never inside the player");

        BehaviorConstraint? behavior = null;
        var choice = DevItem.Choice("Spawn", "Behavior condition", ParcelSpawner.BehaviorChoices, () => behavior, value => behavior = value);
        Assert.Equal("random", choice.State, "A choice shows its value");
        var choices = new DevMenuModel();
        choices.Add(choice);
        choices.Press(DevKey.Enter);
        Assert.Equal(BehaviorConstraint.None, behavior, "Enter moves to the next value");
        for (var step = 0; step < 5; step++)
        {
            choices.Press(DevKey.Enter);
        }

        Assert.Equal(BehaviorConstraint.Corrupted, behavior, "the curse is the last one");
        Assert.Equal("Behavior condition: cursed", choices.Status, "and the status names it");
        choices.Press(DevKey.Enter);
        Assert.Null(behavior, "After the last value it starts again");
        behavior = (BehaviorConstraint)3;
        Assert.Equal("?", choice.State, "A value outside the list shows as unknown");

        Assert.True(ParcelSpawner.IsInLevel(true, true, false, false), "A player spawns parcels once they stand in the level, the game starts its day only on the host");
        Assert.False(ParcelSpawner.IsInLevel(true, true, true, false), "The host waits until its game has started the day");
        Assert.False(ParcelSpawner.IsInLevel(true, false, false, false), "Nobody spawns before standing in the level");
        Assert.False(new SpawnConditions(null, null, null).IsSet, "All random: the game chooses");
        Assert.Equal("cursed, to Hazelton", new SpawnConditions(null, BehaviorConstraint.Corrupted, ParcelRegion.Hazelton).ToString(), "Conditions read as text");
        Assert.Equal("no storage condition, fragile", new SpawnConditions(StorageConstraint.None, BehaviorConstraint.Fragile, null).ToString(), "None is named");
        Assert.Equal(Enum.GetValues(typeof(ParcelRegion)).Length, ParcelSpawner.RegionChoices.Count, "Every destination can be chosen, None stands for random");

        Assert.Equal(11u, PlayerStubs.NetworkIdentifierOf(1), "Player 1 is the game's network identifier 11");
        Assert.Equal(5, PlayerStubs.PlayerOf(15), "and identifier 15 is player 5");
        Assert.True(PlayerStubs.IsStubClient(PlayerStubs.StubClientOf(8)), "A stub has a client id of its own");
        Assert.False(PlayerStubs.IsStubClient(76561199689020186UL), "a Steam id is never a stub");
        Assert.False(PlayerStubs.IsStubClient(1), "nor a client of a game without the Steam network");
        Assert.Equal(7, new PlayerStubs(context.Log).Items().Count(), "Players 2 to 8 can be stubs, player 1 is the host");

        Assert.Equal("step 1 of 4", PileClearer.StepText(0, 4), "Pile steps count from one");
        Assert.Equal("cleared", PileClearer.StepText(-1, 4), "A pile at -1 is cleared");
        Assert.Equal("cleared", PileClearer.StepText(3, 3), "A pile past its last step is cleared too, the game shows it so");
        Assert.False(PileClearer.IsClearedStep(2, 3), "The last step is not cleared yet");
        Assert.Equal("cold room pile", PileClearer.NameOf(PileType.ColdRoom), "Piles have readable names");

        var model = new DevMenuModel();
        var hider = new UiHider(context.Log, model);
        model.Add(DevItem.Command(UiHider.Group, "Preview", () => "shown"));
        model.Add(hider.Item);
        Assert.Equal(2, model.Current.Count, "The notifications toggle waits until the UI is hidden");

        model.Press(DevKey.Digit2);
        Assert.True(hider.Hidden, "The toggle hides the UI");
        Assert.Equal(3, model.Current.Count, "and shows the notifications toggle");
        Assert.Equal(UiHider.NotificationsLabel, model.Current[2].Label, "right after it");
        Assert.Equal("on", model.Current[2].State, "Notifications are hidden too by default");

        model.Press(DevKey.Digit3);
        Assert.False(hider.HideNotifications, "Notifications can stay");
        model.Press(DevKey.Digit2);
        Assert.False(hider.Hidden, "The toggle shows the UI again");
        Assert.Equal(2, model.Current.Count, "and takes the notifications toggle away");
        Assert.False(hider.HideNotifications, "which keeps its choice");

        model.Press(DevKey.Digit2);
        Assert.Equal(3, model.Current.Count, "Hiding again brings it back once");
        hider.Hidden = true;
        Assert.Equal(3, model.Current.Count, "Setting the same value adds nothing");
        hider.Hidden = false;
        yield break;
    }
}
