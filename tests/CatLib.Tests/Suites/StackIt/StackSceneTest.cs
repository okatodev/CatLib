using System.Collections.Generic;
using System.Linq;
using CatLib.Game;
using CatLib.Tests.Framework;
using StackIt;
using StackIt.Patches;
using UnityEngine;

namespace CatLib.Tests.Suites.StackIt;

public sealed class StackSceneTest : TestCase
{
    public override string Suite => "StackIt";

    public override int Order => 50;

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var controller = StackItPlugin.Controller;
        if (controller == null || !CatParcels.IsAvailable)
        {
            context.Note("Stacking can only be checked in a level with Stack it! installed");
            yield break;
        }

        Assert.NotNull(StackPatches.Group, "The patches were set up");
        Assert.True(StackPatches.Group.IsActive, "The game is patched: " + string.Join("; ", StackPatches.Group.Problems));

        var cells = new List<Vector3>();
        var stored = 0;
        var sameFootprint = 0;
        var sticking = new List<string>();
        var wrongGrid = new List<string>();
        var different = new List<string>();
        foreach (var store in Object.FindObjectsOfType<EntityInteractableStore>())
        {
            if (!StoreGrid.IsParcel(store))
            {
                continue;
            }

            var entity = store.LinkedEntity;
            var footprint = entity.Interactable.Pickable.Size;
            if (!StoreGrid.TryGetSize(store, out var size) || !((size.x == footprint.x && size.y == footprint.y) || (size.x == footprint.y && size.y == footprint.x)))
            {
                wrongGrid.Add(global::StackIt.StackController.Name(entity) + $" grid {size.x}x{size.y} footprint {footprint.x}x{footprint.y}");
            }

            var parent = store.ParentStore;
            if (!StoreGrid.IsAlive(parent))
            {
                continue;
            }

            var info = parent.GetStoredEntityInfo(entity);
            if (info == null || !StoreGrid.TryGetFootprint(parent, entity, info.AnchorIndices, info.YawSteps, cells))
            {
                continue;
            }

            stored++;
            var game = new HashSet<Vector2Int>();
            for (var index = 0; info.EntityFootprintIndices != null && index < info.EntityFootprintIndices.Count; index++)
            {
                game.Add(info.EntityFootprintIndices[index]);
            }

            var ours = new HashSet<Vector2Int>(cells.Select(point => parent.GetGridPosition(point, true)));
            if (ours.SetEquals(game))
            {
                sameFootprint++;
            }
            else
            {
                different.Add(global::StackIt.StackController.Name(entity));
            }

            if (cells.Any(point => !StoreGrid.Contains(parent, parent.GetGridPosition(point, false))))
            {
                sticking.Add(global::StackIt.StackController.Name(entity));
            }
        }

        context.Note($"Stored parcels: {stored}, same footprint as the game: {sameFootprint}, sticking out of the parcel under them: {sticking.Count}, bridges known: {controller.Registry.Count}");
        Assert.Equal(0, wrongGrid.Count, "The grid on top of every parcel is its footprint: " + string.Join(", ", wrongGrid));
        Assert.Equal(0, different.Count, "The mod finds the same cells as the game for every stored parcel: " + string.Join(", ", different));
        foreach (var bridge in controller.Registry.All)
        {
            Assert.True(sticking.Contains(global::StackIt.StackController.Name(bridge.Entity)), global::StackIt.StackController.Name(bridge.Entity) + " sticks out of the parcel under its centre");
        }

        if (sticking.Count > controller.Registry.Count)
        {
            context.Note($"{sticking.Count - controller.Registry.Count} parcel(s) stick out without a parcel under them: left by Keep balance or loaded that way");
        }

        foreach (var bridge in controller.Registry.All)
        {
            Assert.True(bridge.Main.ContainsEntity(bridge.Entity), global::StackIt.StackController.Name(bridge.Entity) + " is stored on its centre parcel");
            foreach (var held in bridge.Held)
            {
                Assert.True(StoreGrid.IsFree(held.Store, held.Cell), global::StackIt.StackController.Name(bridge.Entity) + " holds a cell the game sees as free");
            }
        }

        context.Note(controller.Describe());
        yield break;
    }
}
