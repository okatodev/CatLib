using System.Collections.Generic;
using System.IO;
using CatLib.Core;
using CatLib.Tests.Diagnostics.Inspection;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.Inspection;

public sealed class EntityDumpTest : TestCase
{
    public override string Suite => "Inspection";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var directory = Path.Combine(Path.GetTempPath(), "CatLibInspection");
        var inspector = new EntityInspector(directory, () => EntityInspector.DefaultFocusTypes, CatLibRuntime.Log.Scope("InspectTest"));
        var path = inspector.Dump();
        try
        {
            Assert.True(File.Exists(path), "The dump file must be written");
            var text = File.ReadAllText(path);
            context.Note($"Dump of {text.Length} characters");
            foreach (var section in new[] { "== TARGET", "== ENTITIES IN THE SCENE", "== STORAGES THAT ARE NOT PARCELS", "== EntityInteractableStore", "== EntityRepairWorkstation", "== EntityStorageLabel", "== ParcelDeliverer", "== ParcelDeliveryManager" })
            {
                Assert.True(text.Contains(section), "The dump must contain " + section);
            }

            Assert.False(text.Contains("Unknown type"), "Every default focus type must exist in the game");
            foreach (var line in text.Split('\n'))
            {
                if (line.StartsWith("Camera:") || line.StartsWith("All cameras:"))
                {
                    context.Note(line.TrimEnd());
                }
            }

            Assert.True(text.Contains("All cameras: "), "The dump must list the cameras");
        }
        finally
        {
            File.Delete(path);
        }

        yield break;
    }
}
