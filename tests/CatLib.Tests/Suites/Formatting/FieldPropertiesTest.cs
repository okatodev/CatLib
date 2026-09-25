using System.Collections.Generic;
using System.Linq;
using CatLib.Tests.Diagnostics.Inspection;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.Formatting;

public sealed class FieldPropertiesTest : TestCase
{
    public override string Suite => "Formatting";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var store = Names(typeof(EntityInteractableStore));
        context.Note("EntityInteractableStore fields: " + string.Join(", ", store));

        foreach (var expected in new[] { "StorageMaximumHeight", "StorageMaximumApprovedHeight", "MaximumParcelAmount", "StorageType", "PrefilledStorageTextAsset", "_availableCells", "_gridColumnSize", "_gridRowSize" })
        {
            Assert.True(store.Contains(expected), "EntityInteractableStore must expose " + expected);
        }

        Assert.False(store.Contains("CurrentHeight"), "Properties computed by game code must not be read");
        Assert.False(store.Contains("IsHeightApproved"), "Only the backing field of IsHeightApproved may be read, never its getter");

        var label = Names(typeof(EntityStorageLabel));
        Assert.True(label.Contains("PossibleSprites") && label.Contains("_currentSpriteIndex"), "EntityStorageLabel fields");

        var workstation = Names(typeof(EntityRepairWorkstation));
        Assert.True(workstation.Contains("LinkedStore") && workstation.Contains("_currentRepairIndex"), "EntityRepairWorkstation fields");

        var parcel = Names(typeof(EntityParcel));
        context.Note("EntityParcel fields: " + string.Join(", ", parcel));
        Assert.True(parcel.Count > 0, "EntityParcel exposes its own fields and those of Entity");
        yield break;
    }

    private static List<string> Names(System.Type type) =>
        InteropInspector.FieldProperties(type).Select(property => InspectionText.MemberName(property.Name)).ToList();
}
