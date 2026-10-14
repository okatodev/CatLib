using System.Collections.Generic;
using System.Linq;
using CatLib.Tests.Framework;
using TooLate.Logic;

namespace CatLib.Tests.Suites.TooLate;

public sealed class HoldLedgerTest : TestCase
{
    private const ulong Host = 76561199689020186UL;
    private const ulong Friend = 76561199676542364UL;
    private const ulong Late = 76561199600000000UL;

    public override string Suite => "TooLate";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var ledger = new HoldLedger();
        ledger.RecordAuthority(Friend, 736);
        ledger.RecordPickUp(Friend, 735);
        ledger.RecordAuthority(Friend, 735);
        ledger.RecordPickUp(Host, 591);
        Assert.Equal(2, ledger.HeldCount, "Each player holds one thing");
        Assert.Equal(2, ledger.AuthorityCount, "and moves what they took");

        var mark = ledger.Mark;
        var sent = ledger.Before(mark, null, null);
        Assert.SequenceEqual(new[] { HoldKind.Authority, HoldKind.Authority, HoldKind.PickUp, HoldKind.PickUp }, sent.Select(message => message.Kind),
            "Who moves an entity comes before who holds it");
        Assert.SequenceEqual(new[] { 735u, 591u }, sent.Where(message => message.Kind == HoldKind.PickUp).Select(message => message.EntityId),
            "Held things keep the order they were taken in");

        Assert.False(ledger.Before(mark, id => id != 591, null).Any(message => message.EntityId == 591), "Entities the late player does not have are left out");
        Assert.False(ledger.Before(mark, null, client => client != Host).Any(message => message.ClientId == Host), "and so are players who are gone");

        Assert.False(ledger.RecordDrop(Host, 600), "Dropping something else changes nothing");
        Assert.True(ledger.RecordDrop(Host, 591), "A dropped parcel is not held any more");
        Assert.Equal(1, ledger.HeldCount, "by anyone");
        Assert.Equal(1, ledger.Before(mark, null, null).Count(message => message.Kind == HoldKind.PickUp), "What was dropped after the snapshot is not sent, the late player got the drop");
        ledger.RecordPickUp(Host, 591);
        Assert.Equal(1, ledger.Before(mark, null, null).Count(message => message.Kind == HoldKind.PickUp), "and what was taken after it reached them like for everyone");
        ledger.RecordDrop(Host, 591);

        ledger.RecordPickUp(Host, 735);
        Assert.Equal(1, ledger.HeldCount, "A parcel taken from another player's hands is only in the new hands");
        ledger.RecordPickUp(Late, 700);
        Assert.Equal(3, ledger.ForgetClient(Late) + ledger.ForgetClient(Friend), "A player who leaves holds and moves nothing");
        Assert.Equal(1, ledger.Forget(735), "A disposed parcel is forgotten");
        Assert.Equal(0, ledger.HeldCount, "out of every hand");
        Assert.False(ledger.RecordPickUp(Host, 0), "Nothing cannot be held");

        ledger.RecordAuthority(Host, 10);
        ledger.Clear();
        Assert.Equal(0, ledger.AuthorityCount + ledger.HeldCount, "A new level starts empty");
        yield break;
    }
}
