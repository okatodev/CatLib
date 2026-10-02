using System.Collections.Generic;
using System.Linq;
using System;
using BoatTweaks.Logic;
using CatLib.Il2Cpp;
using CatLib.Logging;
using UnityEngine;

namespace BoatTweaks.Scene;

public enum DeckBuildResult
{
    NotReady,
    Done,
    Skipped
}

public sealed class DeckDecorTemplates
{
    private readonly Dictionary<(int Short, int Long), List<GameObject>> _byFootprint = new();

    public List<string> Unused { get; } = new();

    public List<string> Used { get; } = new();

    public bool IsEmpty => _byFootprint.Count == 0;

    public void Add(GameObject prop, (int Short, int Long) footprint, string label)
    {
        if (!_byFootprint.TryGetValue(footprint, out var list))
        {
            list = new List<GameObject>();
            _byFootprint[footprint] = list;
        }

        list.Add(prop);
        Used.Add(label);
    }

    public IReadOnlyList<GameObject> For((int Short, int Long) footprint) =>
        _byFootprint.TryGetValue(footprint, out var list) ? list : Array.Empty<GameObject>();

    public string Describe() =>
        $"used: {(Used.Count == 0 ? "none" : string.Join(", ", Used))}; not used: {(Unused.Count == 0 ? "none" : string.Join(", ", Unused))}";
}

public sealed class DeckBuilder
{
    public const string ClonePrefix = "BoatTweaks ";
    public const float BlockerFill = 0.9f;
    public const float DefaultCellSize = 0.25f;

    private readonly CatLogger _log;
    private bool _describedTemplate;

    public DeckBuilder(CatLogger log)
    {
        _log = log;
    }

    public DeckBuildResult TryBuild(EntityInteractableStore store, string key, DeckPlan plan, DeckDecorTemplates decor, GameObject fallbackBlocker, int blockerLayer,
        out List<(int Row, int Column)> takenCells)
    {
        takenCells = null;
        if (!Il2CppArrays.TryRead2D<Vector3>(store._grid, out var grid))
        {
            return DeckBuildResult.NotReady;
        }

        var rows = grid.GetLength(0);
        var columns = grid.GetLength(1);
        var layout = store._PrefilledStorageTextAsset_k__BackingField;
        var reserved = PrefillFootprint.Parse(layout == null ? null : layout.text);
        var pattern = plan.Kind == DeckPlanKind.Pattern
            ? plan.Pattern
            : PatternGenerator.Generate(rows, columns, plan.Density, plan.EdgesOnly, reserved, plan.Seed);

        if (pattern.Rows != rows || pattern.Columns != columns)
        {
            _log.Warning($"{plan.Describe()} is {pattern.Rows}x{pattern.Columns}, but the deck of {key} is {rows}x{columns}; the deck stays empty");
            return DeckBuildResult.Skipped;
        }

        var template = FindBlocker(store, blockerLayer) ?? fallbackBlocker;
        if (template == null)
        {
            _log.Warning($"No blocker to copy was found for {key}; the deck stays empty");
            return DeckBuildResult.Skipped;
        }

        var cell = store._Padding_k__BackingField * 2f;
        if (cell <= 0f)
        {
            cell = DefaultCellSize;
        }

        var templateTransform = template.transform;
        var blockerParent = templateTransform.IsChildOf(store.transform) ? templateTransform.parent : Child(store.transform, "Colliders");
        var propParent = Child(store.transform, DeckDecor.VisualsName);
        var templateBox = template.GetComponent<BoxCollider>();
        if (!_describedTemplate)
        {
            _describedTemplate = true;
            _log.Info($"Blocker template {template.name} has {DescribeComponents(template)}; the mod places plain box colliders on its layer instead of copies");
        }

        var height = templateBox == null ? cell * 2f : templateBox.size.y * templateTransform.localScale.y;
        var centerY = templateBox == null ? cell : templateBox.center.y * templateTransform.localScale.y;
        var baseY = templateTransform.localPosition.y;
        var random = new System.Random(plan.Seed ^ (plan.Name ?? string.Empty).Length);

        var allPieces = pattern.Pieces();
        var pieces = DeckPieces.Avoiding(allPieces, reserved);
        var droppedPieces = allPieces.Count - pieces.Count;
        var placed = new List<(int Row, int Column)>();
        foreach (var (row, column) in pieces.SelectMany(piece => piece.Cells()))
        {
            var position = grid[row, column];
            var blocker = new GameObject($"{ClonePrefix}Blocker {row},{column}");
            blocker.layer = template.layer;
            var blockerTransform = blocker.transform;
            blockerTransform.SetParent(blockerParent, false);
            blockerTransform.localPosition = new Vector3(position.x, baseY, position.z);
            blockerTransform.localRotation = Quaternion.identity;
            blockerTransform.localScale = Vector3.one;
            var box = blocker.AddComponent<BoxCollider>();
            box.isTrigger = templateBox != null && templateBox.isTrigger;
            box.size = new Vector3(cell * BlockerFill, height, cell * BlockerFill);
            box.center = new Vector3(0f, centerY, 0f);
            placed.Add((row, column));
        }

        var decorated = 0;
        foreach (var piece in pieces)
        {
            decorated += Decorate(piece, grid, decor, propParent, random);
        }

        Physics.SyncTransforms();
        var skipped = pattern.BlockedCount - placed.Count;
        _log.Info($"Deck built for {key}: {plan.Describe()}, {placed.Count} cell(s) taken, {skipped} cell(s) of {droppedPieces} piece(s) left free for arriving parcels, " +
                  $"pieces {string.Join(" ", pieces.Select(piece => $"{piece.Rows}x{piece.Columns}@{piece.Row},{piece.Column}"))} with {decorated} decoration object(s)");
        takenCells = placed;
        return DeckBuildResult.Done;
    }

    private static int Decorate(DeckPiece piece, Vector3[,] grid, DeckDecorTemplates decor, Transform parent, System.Random random)
    {
        var templates = decor.For(piece.Footprint);
        if (templates.Count == 0)
        {
            return piece.CellCount == 1 ? 0 : DeckPieces.SplitPiece(piece).Sum(part => Decorate(part, grid, decor, parent, random));
        }

        var center = Vector3.zero;
        foreach (var (row, column) in piece.Cells())
        {
            center += grid[row, column];
        }

        center /= piece.CellCount;
        var template = templates[random.Next(templates.Count)];
        var prop = UnityEngine.Object.Instantiate(template, parent);
        prop.name = $"{ClonePrefix}Prop {piece.Row},{piece.Column} {piece.Rows}x{piece.Columns}";
        prop.SetActive(true);
        prop.transform.localPosition = new Vector3(center.x, template.transform.localPosition.y, center.z);
        prop.transform.localRotation = Quaternion.identity;

        float yaw;
        if (piece.Rows != piece.Columns)
        {
            var extent = LocalExtent(prop, parent);
            var longAlongRows = extent.x >= extent.z;
            var pieceAlongRows = piece.Rows > piece.Columns;
            yaw = (longAlongRows == pieceAlongRows ? 0f : 90f) + 180f * random.Next(2);
        }
        else
        {
            yaw = 90f * random.Next(4);
        }

        prop.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        return 1;
    }

    public static bool TryMeasure(GameObject prop, out Vector3 size)
    {
        size = Vector3.zero;
        var space = prop.transform.parent;
        if (space == null)
        {
            return false;
        }

        var toSpace = space.worldToLocalMatrix;
        var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        var found = false;
        foreach (var filter in prop.GetComponentsInChildren<MeshFilter>(true))
        {
            var mesh = filter == null ? null : filter.sharedMesh;
            if (mesh == null)
            {
                continue;
            }

            var matrix = toSpace * filter.transform.localToWorldMatrix;
            var bounds = mesh.bounds;
            for (var corner = 0; corner < 8; corner++)
            {
                var local = new Vector3(
                    (corner & 1) == 0 ? bounds.min.x : bounds.max.x,
                    (corner & 2) == 0 ? bounds.min.y : bounds.max.y,
                    (corner & 4) == 0 ? bounds.min.z : bounds.max.z);
                var point = matrix.MultiplyPoint3x4(local);
                min = Vector3.Min(min, point);
                max = Vector3.Max(max, point);
                found = true;
            }
        }

        if (!found)
        {
            return false;
        }

        size = max - min;
        return true;
    }

    public static Vector3 LocalExtent(GameObject prop, Transform parent)
    {
        var renderers = prop.GetComponentsInChildren<Renderer>(false);
        if (renderers.Length == 0)
        {
            return Vector3.one;
        }

        var bounds = renderers[0].bounds;
        for (var index = 1; index < renderers.Length; index++)
        {
            bounds.Encapsulate(renderers[index].bounds);
        }

        var local = parent.InverseTransformVector(bounds.size);
        return new Vector3(Mathf.Abs(local.x), Mathf.Abs(local.y), Mathf.Abs(local.z));
    }

    private static string DescribeComponents(GameObject gameObject)
    {
        var names = new List<string>();
        foreach (var component in gameObject.GetComponents<Component>())
        {
            names.Add(component == null ? "missing" : component.GetIl2CppType().Name);
        }

        return string.Join(", ", names);
    }

    public static GameObject FindBlocker(EntityInteractableStore store, int blockerLayer)
    {
        if (blockerLayer < 0)
        {
            return null;
        }

        foreach (var transform in store.GetComponentsInChildren<Transform>(true))
        {
            if (transform.gameObject.layer == blockerLayer && !transform.name.StartsWith(ClonePrefix, StringComparison.Ordinal))
            {
                return transform.gameObject;
            }
        }

        return null;
    }

    private static Transform Child(Transform parent, string name)
    {
        for (var index = 0; index < parent.childCount; index++)
        {
            var child = parent.GetChild(index);
            if (child.name == name)
            {
                return child;
            }
        }

        return parent;
    }
}
