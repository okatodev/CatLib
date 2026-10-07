using System.Collections.Generic;
using System.Linq;
using CatLib.Tests.Framework;
using TooLate.Logic;

namespace CatLib.Tests.Suites.TooLate;

public sealed class JoinFlowTest : TestCase
{
    public override string Suite => "TooLate";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var playing = new HostState(true, true, false, 0);
        Assert.True(JoinGate.CanAccept(playing) && JoinGate.CanStart(playing), "During the day a player is let in and starts loading");
        var recap = new HostState(true, true, false, JoinGate.EveningRecap);
        Assert.True(JoinGate.CanAccept(recap), "During the results a player can connect");
        Assert.False(JoinGate.CanStart(recap), "but waits in the lobby");
        Assert.False(JoinGate.CanStart(new HostState(true, true, false, JoinGate.DawnRecap)), "The morning results wait too");
        Assert.True(JoinGate.CanStart(new HostState(true, true, false, 3)), "The night is gameplay");
        Assert.False(JoinGate.ReadyToStart(playing, 1), "A player who just connected first settles in the lobby");
        Assert.True(JoinGate.ReadyToStart(playing, JoinGate.SettleSeconds), "and starts after a few seconds");
        Assert.False(JoinGate.CanAccept(new HostState(true, false, false, 0)), "Not while the host is still loading");
        Assert.False(JoinGate.CanAccept(new HostState(true, true, true, 0)), "Not while the game restarts");
        Assert.False(JoinGate.CanAccept(new HostState(false, true, false, 0)), "Only the host lets players in");

        Assert.Equal(Route.Pass, MessageRoute.For(JoinStage.Waiting, ProtocolCodes.ConnectAck), "The connection answer reaches a waiting player");
        Assert.Equal(Route.Pass, MessageRoute.For(JoinStage.Loading, ProtocolCodes.GrantPlayerSpawn), "So does the permission to appear");
        Assert.Equal(Route.Drop, MessageRoute.For(JoinStage.Loading, ProtocolCodes.PlayerSpawnedBroadcast), "Other players are sent again when the joining player appears");
        Assert.Equal(Route.Drop, MessageRoute.For(JoinStage.Waiting, 40), "A waiting player in the lobby gets no warehouse changes");
        Assert.Equal(Route.Hold, MessageRoute.For(JoinStage.Loading, 40), "A loading player gets them later, in order");
        Assert.Equal(Route.Drop, MessageRoute.For(JoinStage.Loading, ProtocolCodes.GenericMessage), "Entity messages wait for the saved entities, their latest state is sent after the player appears");
        Assert.Equal(Route.Drop, MessageRoute.For(JoinStage.Loading, ProtocolCodes.MovementAndRotationBroadcast), "Movement is sent again all the time, it is not kept");
        Assert.Equal(Route.HoldLatest, MessageRoute.For(JoinStage.Loading, ProtocolCodes.GameTimeSynchronization), "Only the latest game time is kept");
        Assert.Equal(Route.Drop, MessageRoute.For(JoinStage.Loading, ProtocolCodes.SaveSynchronization), "The game's own save copy is not sent while joining");
        Assert.Equal(Route.Pass, MessageRoute.For(JoinStage.Playing, ProtocolCodes.SaveSynchronization), "Later it is, like for everyone");
        Assert.Equal(Route.Replace, MessageRoute.For(JoinStage.Playing, ProtocolCodes.IdentifiersSynchronization), "The identifiers always come from the snapshot");
        Assert.Equal(Route.Pass, MessageRoute.For(JoinStage.Appearing, ProtocolCodes.PlayerSpawnedBroadcast), "When the player appears, the game sends them the other players");
        Assert.Equal(Route.Hold, MessageRoute.For(JoinStage.Appearing, 40), "Warehouse changes wait until the saved entities have their identifiers");
        Assert.Equal(Route.Drop, MessageRoute.For(JoinStage.Appearing, ProtocolCodes.GenericMessage), "Entity messages are sent from the state after the player appears");
        Assert.Equal(Route.Pass, MessageRoute.For(JoinStage.Synced, 40), "After the identifiers everything goes through");

        var ticket = new JoinTicket<string>(7, "Friend", 10);
        Assert.Equal(JoinStage.Waiting, ticket.Stage, "A ticket starts waiting");
        ticket.Accept(40, true, "dropped");
        Assert.Equal(1, ticket.Dropped, "Dropped messages are counted");
        ticket.MoveTo(JoinStage.Loading, 12);
        ticket.Accept(40, true, "first");
        ticket.Accept(ProtocolCodes.GameTimeSynchronization, false, "time 1");
        ticket.Accept(42, true, "second");
        ticket.Accept(ProtocolCodes.GameTimeSynchronization, false, "time 2");
        ticket.Accept(ProtocolCodes.MovementAndRotationBroadcast, false, "moving");
        var held = ticket.TakeHeld();
        Assert.SequenceEqual(new[] { "first", "second", "time 2" }, held.Select(message => message.Payload), "Held in order, the time only once and the newest");
        Assert.Equal(0, ticket.Held.Count, "Taking empties the list");
        Assert.Equal(2, ticket.Dropped, "Movement was dropped");
        Assert.Equal(12.0, ticket.StageSince, "The stage remembers when it began");
        ticket.Accept(40, true, "a");
        ticket.Accept(41, true, "b");
        ticket.Accept(42, true, "c");
        Assert.SequenceEqual(new[] { "a", "b" }, ticket.TakeHeld(2).Select(message => message.Payload), "Held messages go out in batches, oldest first");
        ticket.Accept(43, true, "d");
        Assert.SequenceEqual(new[] { "c", "d" }, ticket.TakeHeld(5).Select(message => message.Payload), "New messages queue after the rest");
        yield break;
    }
}
