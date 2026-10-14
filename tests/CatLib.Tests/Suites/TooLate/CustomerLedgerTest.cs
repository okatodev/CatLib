using System.Collections.Generic;
using System.Linq;
using CatLib.Tests.Framework;
using TooLate.Logic;

namespace CatLib.Tests.Suites.TooLate;

public sealed class CustomerLedgerTest : TestCase
{
    public override string Suite => "TooLate";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        Assert.Equal(CustomerLedger<string>.Slot.Appearance, CustomerLedger<string>.SlotOf("CustomerAppearance;5;Child;7", out _), "The looks of a customer are kept");
        Assert.Equal(CustomerLedger<string>.Slot.Request, CustomerLedger<string>.SlotOf("Customer;1;60;Ч;Names/60;Surnames/5;FoggyMountains", out _), "A request is kept");
        Assert.Equal(CustomerLedger<string>.Slot.Request, CustomerLedger<string>.SlotOf("Customer;2;54;З;ClientDialogues2/HelloDeposit_8", out _), "and a parcel to hand over");
        Assert.Equal(CustomerLedger<string>.Slot.Farewell, CustomerLedger<string>.SlotOf("Customer;6;ClientDialogues2/ByeDeposit_1", out _), "A goodbye is kept apart");
        Assert.Equal(CustomerLedger<string>.Slot.None, CustomerLedger<string>.SlotOf("Customer;4;RequestWrong", out _), "A wrong parcel is of the moment");
        Assert.Equal(CustomerLedger<string>.Slot.None, CustomerLedger<string>.SlotOf("CustomerAudio;5;1;1", out _), "and so are the voices");
        Assert.Equal(CustomerLedger<string>.Slot.Carry, CustomerLedger<string>.SlotOf("CarryParcel;735;False;True", out var carried), "A carried parcel is kept");
        Assert.Equal(735u, carried, "with its id");

        var ledger = new CustomerLedger<string>();
        ledger.RecordSpawn(700, "p_Entity_Carryable_Parcel_Small", "boat parcel");
        ledger.RecordLine(732, "CustomerAppearance;0;Elderly;4");
        ledger.RecordSpawn(732, "p_EntityCustomer", "spawn 732");
        Assert.True(ledger.RecordLine(732, "CustomerAppearance;0;Elderly;4"), "A customer's looks are recorded after it spawned");
        ledger.RecordLine(732, "CustomerAudio;0;1;0.85");
        ledger.RecordLine(732, "Customer;1;60;Ч;Names/60;Surnames/5;FoggyMountains");
        ledger.RecordSpawn(733, "p_Entity_Storage_CustomerParcels_2Standard", "spawn 733");
        ledger.RecordSpawn(734, "p_EntityCustomer", "spawn 734");
        ledger.RecordLine(734, "CustomerAppearance;5;Child;7");
        ledger.RecordStored(733, 735, "735 in 733");
        ledger.RecordSpawn(735, "p_Entity_Carryable_Parcel_GiantPlus", "spawn 735");
        ledger.RecordStored(735, 736, "736 on 735");
        ledger.RecordSpawn(736, "p_Entity_Carryable_Parcel_FlatLetter", "spawn 736");
        ledger.RecordProperties(735, "properties 735");
        ledger.RecordProperties(736, "properties 736");
        ledger.RecordLine(734, "CarryParcel;735;False;True");
        ledger.RecordLine(734, "Customer;2;54;З;ClientDialogues2/HelloDeposit_8");
        Assert.False(ledger.RecordLine(700, "CarryParcel;1;False;True"), "Lines of entities that are not customers are not recorded");
        Assert.Equal(2, ledger.CustomerCount, "Two customers wait");
        Assert.Equal(5, ledger.Count, "with their storage and the parcels in it, the boat's parcel is not theirs");

        var mark = ledger.Mark;
        var replay = ledger.Before(mark, id => id == 700, 17, 40, 43);
        Assert.Equal(9, replay.Count, "Five spawns, two parcel descriptions and two placements are sent again");
        Assert.Equal("spawn 732", replay[0].Payload, "in the order the game sent them");
        Assert.Equal("735 in 733", replay.Single(message => message.EntityId == 735 && message.Code == 40).Payload, "A parcel goes back into the customer's storage");
        var codes735 = replay.Where(message => message.EntityId == 735).Select(message => message.Code).ToList();
        Assert.SequenceEqual(new[] { 17, 40, 43 }, codes735, "A parcel spawns before it is placed and described, like other players get it");
        var order = replay.Select(message => (message.EntityId, message.Code)).ToList();
        Assert.True(order.IndexOf((735u, 17)) < order.IndexOf((736u, 40)), "A parcel on top of another waits for the one below");
        Assert.SequenceEqual(new[] { 732u, 734u }, ledger.Customers(), "Customers are listed in the order they came");
        Assert.SequenceEqual(new[] { "CustomerAppearance;0;Elderly;4", "Customer;1;60;Ч;Names/60;Surnames/5;FoggyMountains" }, ledger.LinesOf(732, 0).Select(line => line.Text),
            "A customer gets its looks and then its request, the voice is left out");
        Assert.SequenceEqual(new[] { "CustomerAppearance;5;Child;7", "CarryParcel;735;False;True", "Customer;2;54;З;ClientDialogues2/HelloDeposit_8" },
            ledger.LinesOf(734, 0).Select(line => line.Text), "A customer with a parcel holds it before speaking");

        Assert.Equal(0, ledger.Before(mark, id => id == 735 || id == 736 || id == 700, 17, 40, 43).Count(message => message.EntityId == 735),
            "Entities the joining player has from the snapshot are not spawned again");
        var known733 = ledger.Before(mark, id => id == 733, 17, 40, 43);
        Assert.True(known733.Any(message => message.EntityId == 735 && message.Code == 40), "A parcel goes into a storage the player has");

        ledger.RecordSpawn(740, "p_EntityCustomer", "spawn 740");
        ledger.RecordLine(740, "CustomerAppearance;1;Adult;2");
        Assert.False(ledger.Before(mark, null, 17, 40, 43).Any(message => message.EntityId == 740), "A customer who came while the player loaded is in the held messages");
        Assert.SequenceEqual(new[] { 740u }, ledger.MembersSince(mark), "and gets its lines later");
        ledger.RecordLine(732, "Customer;3;;;FoggyMountains");
        Assert.SequenceEqual(new[] { "Customer;3;;;FoggyMountains" }, ledger.LinesOf(732, mark).Select(line => line.Text), "A new request after the snapshot is sent on its own");

        ledger.RecordStored(733, 999, "999 in 733");
        ledger.RecordProperties(999, "properties 999");
        Assert.False(ledger.Before(ledger.Mark, null, 17, 40, 43).Any(message => message.EntityId == 999), "A parcel without its spawn cannot be sent");

        Assert.True(ledger.Forget(733), "The customer's storage goes when the parcels are taken");
        Assert.False(ledger.Before(ledger.Mark, null, 17, 40, 43).Any(message => message.EntityId is 733 or 735 or 736),
            "and its parcels are the warehouse's from then on");
        ledger.Forget(734);
        ledger.Forget(732);
        ledger.Forget(740);
        Assert.Equal(0, ledger.CustomerCount, "Customers who left are forgotten");

        for (uint id = 1000; id < 1000 + CustomerLedger<string>.MostRecent * 3; id++)
        {
            ledger.RecordSpawn(id, "p_Entity_Carryable_Parcel_Small", "parcel");
        }

        ledger.RecordSpawn(5000, "p_Entity_Storage_CustomerParcels_1Standard", "late storage");
        ledger.RecordStored(5000, 1000 + CustomerLedger<string>.MostRecent * 3 - 1, "newest parcel in it");
        Assert.True(ledger.Before(ledger.Mark, null, 17, 40, 43).Any(message => message.EntityId == 1000 + CustomerLedger<string>.MostRecent * 3 - 1),
            "Recent spawns are kept a while, a parcel can be stored after it spawned");
        Assert.True(ledger.Count <= 3, "Parcels of the warehouse are not kept");

        ledger.Clear();
        Assert.Equal(0L, ledger.Mark, "A new level starts empty");
        yield break;
    }
}
