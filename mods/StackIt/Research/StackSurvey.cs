using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using CatLib.Logging;
using UnityEngine;

namespace StackIt.Research;

public sealed class StackSurvey
{
    public const int GridProbeLimit = 64;
    public const float TopRounding = 0.01f;

    private readonly CatLogger _log;

    public StackSurvey(CatLogger log)
    {
        _log = log;
    }

    public string ParcelGrids()
    {
        var stores = AllStores();
        if (stores.Count == 0)
        {
            return "no storages here";
        }

        var firstOfSize = new Dictionary<PackageSize, EntityInteractableStore>();
        var mismatches = new Dictionary<PackageSize, int>();
        var counts = new Dictionary<PackageSize, int>();
        foreach (var store in stores)
        {
            var properties = ParcelOf(store);
            if (properties == null)
            {
                continue;
            }

            var size = properties.PackageSize;
            counts[size] = counts.TryGetValue(size, out var count) ? count + 1 : 1;
            if (!firstOfSize.ContainsKey(size))
            {
                firstOfSize[size] = store;
            }

            var footprint = Footprint(store);
            var grid = GridSize(store);
            if (!SameRectangle(footprint, grid))
            {
                mismatches[size] = mismatches.TryGetValue(size, out var wrong) ? wrong + 1 : 1;
            }
        }

        foreach (var pair in firstOfSize.OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal))
        {
            var store = pair.Value;
            var footprint = Footprint(store);
            var grid = GridSize(store);
            var collider = store.StorageCollider;
            var box = collider == null ? Vector3.zero : Vector3.Scale(collider.size, collider.transform.lossyScale);
            _log.Info(Format("Size {0}: footprint {1}x{2}, top grid {3}x{4}{5}, cell {6:0.###}x{7:0.###}, padding {8:0.###}, storage box {9:0.###} x {10:0.###} x {11:0.###} m, body top at {12:0.###} m, storage box top at {15:0.###} m, {13} of {14} parcel(s) with a different grid",
                pair.Key, footprint.x, footprint.y, grid.x, grid.y, SameRectangle(footprint, grid) ? string.Empty : " (DIFFERENT)",
                store._gridColumnSize, store._gridRowSize, store.Padding, box.x, box.y, box.z, Top(store),
                mismatches.TryGetValue(pair.Key, out var wrong) ? wrong : 0, counts[pair.Key], StorageTop(store)));
        }

        _log.Info($"Parcel grids: {firstOfSize.Count} size(s) from {counts.Values.Sum()} parcel(s)");
        return "see the log";
    }

    public string Storages()
    {
        var roots = AllStores().Where(store => store.ParentStore == null && ParcelOf(store) == null).ToList();
        if (roots.Count == 0)
        {
            return "no storages here";
        }

        foreach (var root in roots.OrderBy(root => root.StorageType).ThenBy(NameOf, StringComparer.Ordinal))
        {
            var grid = GridSize(root);
            var collider = root.StorageCollider;
            var box = collider == null ? Vector3.zero : Vector3.Scale(collider.size, collider.transform.lossyScale);
            _log.Info(Format("Storage {0} ({1}): grid {2}x{3}, cell {4:0.###}x{5:0.###}, box {6:0.###} x {7:0.###} x {8:0.###} m, floor at {9:0.###} m, max height {10:0.###}, approved height {11:0.###}, max weight {12:0.###}, max parcels {13}, tip over {14:0.#}, height now {15:0.###}, weight now {16:0.###}",
                NameOf(root), root.StorageType, grid.x, grid.y, root._gridColumnSize, root._gridRowSize, box.x, box.y, box.z, Floor(root),
                root.StorageMaximumHeight, root.StorageMaximumApprovedHeight, root.StorageMaximumWeight, root.MaximumParcelAmount, root.TipOverAngle,
                SafeHeight(root), SafeWeight(root)));
            WriteChildren(root, 1);
        }

        _log.Info($"Storages: {roots.Count} root storage(s)");
        return "see the log";
    }

    public string LevelTops()
    {
        var roots = AllStores().Where(store => store.ParentStore == null && ParcelOf(store) == null).ToList();
        var written = 0;
        foreach (var root in roots.OrderBy(NameOf, StringComparer.Ordinal))
        {
            var parcels = new List<EntityInteractableStore>();
            Collect(root, parcels, 0);
            if (parcels.Count < 2)
            {
                continue;
            }

            written++;
            var builder = new StringBuilder();
            builder.Append(Format("Tops on {0} ({1}):", NameOf(root), root.StorageType));
            foreach (var group in parcels.GroupBy(store => Mathf.Round(Top(store) / TopRounding) * TopRounding).OrderBy(group => group.Key))
            {
                builder.Append(Format(" [{0:0.00} m: {1}]", group.Key, string.Join(", ", group.Select(store => NameOf(store) + Format(" {0:0.###}", Top(store))))));
            }

            _log.Info(builder.ToString());
        }

        _log.Info($"Level tops: {written} storage(s) with two parcels or more");
        return "see the log";
    }

    private void WriteChildren(EntityInteractableStore store, int depth)
    {
        if (depth > 16)
        {
            return;
        }

        var children = store.GetChildren();
        for (var index = 0; children != null && index < children.Count; index++)
        {
            var child = children[index];
            if (child == null)
            {
                continue;
            }

            var info = store.GetStoredEntityInfo(child);
            var cells = new List<string>();
            var footprint = info == null ? null : info.EntityFootprintIndices;
            for (var cell = 0; footprint != null && cell < footprint.Count; cell++)
            {
                cells.Add(footprint[cell].x + ":" + footprint[cell].y);
            }

            var childStore = child.Interactable == null ? null : child.Interactable.Store;
            var grid = childStore == null ? Vector2Int.zero : GridSize(childStore);
            _log.Info(Format("{0}{1} at {2}:{3} turned {4}, cells [{5}], top grid {6}x{7}, top at {8:0.###} m",
                new string(' ', depth * 2), NameOf(child), info == null ? -1 : info.AnchorIndices.x, info == null ? -1 : info.AnchorIndices.y,
                info == null ? -1 : info.YawSteps, string.Join(" ", cells), grid.x, grid.y, childStore == null ? 0f : Top(childStore)));
            if (childStore != null)
            {
                WriteChildren(childStore, depth + 1);
            }
        }
    }

    private static void Collect(EntityInteractableStore store, List<EntityInteractableStore> result, int depth)
    {
        var children = depth > 16 ? null : store.GetChildren();
        for (var index = 0; children != null && index < children.Count; index++)
        {
            var childStore = children[index] == null || children[index].Interactable == null ? null : children[index].Interactable.Store;
            if (childStore == null)
            {
                continue;
            }

            result.Add(childStore);
            Collect(childStore, result, depth + 1);
        }
    }

    private static List<EntityInteractableStore> AllStores()
    {
        var result = new List<EntityInteractableStore>();
        foreach (var store in UnityEngine.Object.FindObjectsOfType<EntityInteractableStore>())
        {
            if (store != null && !store.WasCollected)
            {
                result.Add(store);
            }
        }

        return result;
    }

    private static EntityProperties ParcelOf(EntityInteractableStore store)
    {
        var entity = store.LinkedEntity;
        return entity == null || entity.IsDisposed ? null : entity.Properties;
    }

    private static Vector2Int Footprint(EntityInteractableStore store)
    {
        var entity = store.LinkedEntity;
        var pickable = entity == null || entity.Interactable == null ? null : entity.Interactable.Pickable;
        return pickable == null ? Vector2Int.zero : pickable.Size;
    }

    public static Vector2Int GridSize(EntityInteractableStore store)
    {
        var columns = 0;
        while (columns < GridProbeLimit && Valid(store, new Vector2Int(columns, 0)))
        {
            columns++;
        }

        var rows = 0;
        while (rows < GridProbeLimit && Valid(store, new Vector2Int(0, rows)))
        {
            rows++;
        }

        return new Vector2Int(columns, rows);
    }

    private static bool Valid(EntityInteractableStore store, Vector2Int cell)
    {
        try
        {
            return store.IsGridIndexValid(cell);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool SameRectangle(Vector2Int a, Vector2Int b) =>
        (a.x == b.x && a.y == b.y) || (a.x == b.y && a.y == b.x);

    private static float Top(EntityInteractableStore store)
    {
        var entity = store.LinkedEntity;
        var pickable = entity == null || entity.Interactable == null ? null : entity.Interactable.Pickable;
        var body = pickable == null ? null : pickable.MainCollider;
        if (body != null)
        {
            return body.bounds.max.y;
        }

        return StorageTop(store);
    }

    private static float StorageTop(EntityInteractableStore store)
    {
        var collider = store.StorageCollider;
        return collider == null ? store.transform.position.y : collider.bounds.max.y;
    }

    private static float Floor(EntityInteractableStore store)
    {
        var collider = store.StorageCollider;
        return collider == null ? store.transform.position.y : collider.bounds.min.y;
    }

    private static float SafeHeight(EntityInteractableStore store)
    {
        try
        {
            return store.ComputeHeight();
        }
        catch (Exception)
        {
            return -1f;
        }
    }

    private static float SafeWeight(EntityInteractableStore store)
    {
        try
        {
            return store.ComputeWeight();
        }
        catch (Exception)
        {
            return -1f;
        }
    }

    private static string NameOf(EntityInteractableStore store)
    {
        var entity = store.LinkedEntity;
        return entity != null ? NameOf(entity) : store.gameObject.name;
    }

    private static string NameOf(Entity entity)
    {
        var properties = entity.IsDisposed ? null : entity.Properties;
        if (properties == null)
        {
            return entity.name;
        }

        var id = entity.Network == null ? 0u : entity.Network.NetworkIdentifier;
        return properties.PackageSize + "#" + id + (properties.BehaviorConstraint == BehaviorConstraint.None ? string.Empty : " " + properties.BehaviorConstraint);
    }

    private static string Format(string format, params object[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
}
