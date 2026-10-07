using System.Collections.Generic;
using System.Linq;
using CatLib.Tests.Framework;
using TooLate.Logic;

namespace CatLib.Tests.Suites.TooLate;

public sealed class LastingMessagesTest : TestCase
{
    public override string Suite => "TooLate";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        Assert.True(LastingMessages<string>.IsLasting(ProtocolCodes.SetParcelDamaged), "A damaged parcel stays damaged");
        Assert.True(LastingMessages<string>.IsLasting(ProtocolCodes.SetInteractionsLocked), "A locked storage stays locked");
        Assert.True(LastingMessages<string>.IsLasting(ProtocolCodes.BoatDestinations), "The boat keeps its destinations");
        Assert.False(LastingMessages<string>.IsLasting(ProtocolCodes.EntityDisposed), "Removed entities are handled apart");
        Assert.False(LastingMessages<string>.IsLasting(ProtocolCodes.GameTimeSynchronization), "The game time is sent all the time anyway");

        var lasting = new LastingMessages<string>();
        Assert.True(lasting.Record(ProtocolCodes.SetParcelDamaged, 573, "573 false"), "The damage of a parcel is recorded");
        lasting.Record(ProtocolCodes.SetParcelDamaged, 575, "575 true");
        lasting.Record(ProtocolCodes.BoatDestinations, 9, "first destinations");
        lasting.Record(ProtocolCodes.SetParcelDamaged, 573, "573 true");
        lasting.Record(ProtocolCodes.BoatDestinations, 12, "second destinations");
        Assert.False(lasting.Record(ProtocolCodes.SetParcelDamaged, 0, "no parcel"), "A damage message needs a parcel");
        Assert.False(lasting.Record(ProtocolCodes.GenericMessage, 5, "text"), "Entity messages go to the state ledger");
        Assert.Equal(3, lasting.Count, "One message per parcel and one list of destinations");

        var all = lasting.For(null);
        Assert.Equal("575 true", all[0].Payload, "Messages come in the order they last changed");
        Assert.Equal("573 true", all[1].Payload, "A parcel keeps its latest damage");
        Assert.Equal("second destinations", all[2].Payload, "The boat keeps its latest destinations");
        Assert.Equal(0u, all[2].EntityId, "Destinations belong to no entity");

        var known = lasting.For(id => id == 573);
        Assert.Equal(2, known.Count, "Unknown parcels are left out, the destinations always come");
        Assert.Equal(1, lasting.Forget(575), "A removed parcel's damage is forgotten");
        Assert.Equal(0, lasting.Forget(0), "Forgetting no entity keeps the destinations");
        lasting.Clear();
        Assert.Equal(0, lasting.Count, "A new level starts empty");
        yield break;
    }
}
