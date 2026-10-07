using System.Collections.Generic;
using System.Linq;
using CatLib.Tests.Framework;
using TooLate.Logic;

namespace CatLib.Tests.Suites.TooLate;

public sealed class StateLedgerTest : TestCase
{
    public override string Suite => "TooLate";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        Assert.Equal("Unlock", StateLedger.KindOf("Unlock"), "A message without values is its own kind");
        Assert.Equal("Pile", StateLedger.KindOf("Pile:4"), "The kind is the part before the first value");
        Assert.Equal("SwitchLens", StateLedger.KindOf("SwitchLens;1;"), "with either separator");
        Assert.Equal("CustomerCounter;OpenState", StateLedger.KindOf("CustomerCounter;OpenState;True"), "Counter messages keep their action in the kind");
        Assert.True(StateLedger.IsLasting("Unlock"), "Unlocks stay");
        Assert.True(StateLedger.IsLasting("CustomerCounter;OpenState"), "An open counter stays open");
        Assert.False(StateLedger.IsLasting("CustomerCounter;Wheel"), "The number wheel turns by steps, a replay would turn it again");
        Assert.False(StateLedger.IsLasting("Captain"), "The captain's lines are of the moment");
        Assert.False(StateLedger.IsLasting("Customer"), "Customers are not in the snapshot");

        var ledger = new StateLedger();
        Assert.True(ledger.Record(346, "Pile:4"), "A pile size is recorded");
        ledger.Record(209, "Unlock");
        ledger.Record(134, "CustomerCounter;OpenState;True");
        Assert.False(ledger.Record(196, "Captain;1;BoatDialogues/Arrival_8;;;;"), "A captain line is not recorded");
        Assert.False(ledger.Record(0, "Unlock"), "Messages without an entity are not recorded");
        ledger.Record(346, "Pile:3");
        ledger.Record(134, "CustomerCounter;OpenState;False");
        Assert.Equal(3, ledger.Count, "Each entity keeps one message per kind");

        var all = ledger.For(null);
        Assert.Equal("Unlock", all[0].Text, "The oldest change comes first");
        Assert.Equal("Pile:3", all[1].Text, "A pile keeps its latest size");
        Assert.Equal("CustomerCounter;OpenState;False", all[2].Text, "A counter keeps its latest state, sent last as it changed last");

        var known = ledger.For(id => id != 209);
        Assert.False(known.Any(message => message.EntityId == 209), "Entities the joining player does not have are left out");

        Assert.Equal(1, ledger.Forget(346), "A disposed entity's messages are forgotten");
        Assert.Equal(2, ledger.Count, "and only those");
        ledger.Clear();
        Assert.Equal(0, ledger.Count, "A new level starts empty");
        yield break;
    }
}
