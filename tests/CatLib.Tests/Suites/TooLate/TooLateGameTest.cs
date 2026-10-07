using System.Collections.Generic;
using CatLib.Tests.Framework;
using TooLate;
using TooLate.Patches;
using TooLate.Scene;

namespace CatLib.Tests.Suites.TooLate;

public sealed class TooLateGameTest : TestCase
{
    public override string Suite => "TooLate";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var joins = TooLatePlugin.Joins;
        if (joins == null)
        {
            context.Note("Too Late is not loaded here, its hooks into the game can only be checked in the game");
            yield break;
        }

        context.Note(joins.Describe());
        Assert.True(TooLatePatches.Group != null && TooLatePatches.Group.IsActive, "The patches of Too Late are installed");
        Assert.True(joins.CanJoinInLevel, "The check that turns players away from a started level is found in this game version");
        Assert.True(joins.CanRaiseLimit, "The player limit is found in this game version");

        var boot = Singleton<BootstrapManager>.HasInstance() ? Singleton<BootstrapManager>.Instance : null;
        if (!GameProtocol.IsHosting || boot == null || !boot.LoadFinalized)
        {
            context.Note("The snapshot for a joining player can only be checked as the host in a level");
            yield break;
        }

        Assert.True(joins.InLevel, "Too Late knows the host is in a level, so players who connect now join the game in progress");

        var snapshot = SaveSnapshot.Write();
        context.Note("Snapshot: " + snapshot);
        Assert.True(snapshot.Data.Length > 0, "The snapshot is a file with data");
        Assert.True(snapshot.Places.Count > 0, "It holds the entities of the warehouse");
        Assert.True(System.Linq.Enumerable.All(snapshot.Places, place => place.Id != 0), "Every entity has a network identifier");
    }
}
