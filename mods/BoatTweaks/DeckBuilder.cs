using System;
using System.Collections.Generic;
using System.Linq;
using CatLib.Il2Cpp;
using CatLib.Logging;
using UnityEngine;

namespace BoatTweaks;

public enum DeckBuildResult
{
    NotReady,
    Done,
    Skipped
}

public sealed class DeckDecorTemplates
{
    public List<GameObject> LargeCrates { get; } = new();

    public List<GameObject> SmallCrates { get; } = new();

    public List<GameObject> Singles { get; } = new();

    public bool IsEmpty => LargeCrates.Count == 0 && SmallCrates.Count == 0 && Singles.Count == 0;
}

public sealed class DeckBuilder
{
    public const string ClonePrefix = "BoatTweaks ";
    public const float BlockerFill = 0.9f;
    public const float DefaultCellSize = 0.25f;

    private readonly CatLogger _log;

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
        var height = templateBox == null ? cell * 2f : templateBox.size.y * templateTransform.localScale.y;
        var centerY = templateBox == null ? cell : templateBox.center.y * templateTransform.localScale.y;
        var baseY = templateTransform.localPosition.y;
        var random = new System.Random(plan.Seed ^ (plan.Name ?? string.Empty).Length);

        var placed = new List<(int Row, int Column)>();
        foreach (var (row, column) in pattern.BlockedCells())
        {
            if (reserved.Contains((row, column)))
            {
                continue;
            }

            var position = grid[row, column];
            var blocker = UnityEngine.Object.Instantiate(template, blockerParent);
            blocker.name = $"{ClonePrefix}Blocker {row},{column}";
            blocker.SetActive(true);
            var blockerTransform = blocker.transform;
            blockerTransform.localPosition = new Vector3(position.x, baseY, position.z);
            blockerTransform.localRotation = Quaternion.identity;
            blockerTransform.localScale = Vector3.one;
            var box = blocker.GetComponent<BoxCollider>();
            if (box != null)
            {
                box.size = new Vector3(cell * BlockerFill, height, cell * BlockerFill);
                box.center = new Vector3(0f, centerY, 0f);
            }

            var renderer = blocker.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.enabled = false;
            }

            placed.Add((row, column));
        }

        var pieces = DeckPieces.Split(rows, columns, new HashSet<(int Row, int Column)>(placed));
        var decorated = 0;
        foreach (var piece in pieces)
        {
            decorated += Decorate(piece, grid, decor, propParent, random);
        }

        Physics.SyncTransforms();
        var skipped = pattern.BlockedCount - placed.Count;
        _log.Info($"Deck built for {key}: {plan.Describe()}, {placed.Count} cell(s) taken, {skipped} left free for arriving parcels, " +
                  $"pieces {string.Join(" ", pieces.Select(piece => $"{piece.Rows}x{piece.Columns}@{piece.Row},{piece.Column}"))} with {decorated} decoration object(s)");
        takenCells = placed;
        return DeckBuildResult.Done;
    }

    private static int Decorate(DeckPiece piece, Vector3[,] grid, DeckDecorTemplates decor, Transform parent, System.Random random)
    {
        var templates = piece.CellCount == 4 ? decor.LargeCrates : piece.CellCount == 2 ? decor.SmallCrates : decor.Singles;
        if (templates.Count == 0)
        {
            if (piece.CellCount == 1 || decor.Singles.Count == 0)
            {
                return 0;
            }

            return piece.Cells().Sum(cell => Decorate(new DeckPiece(cell.Row, cell.Column, 1, 1), grid, decor, parent, random));
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
        if (piece.CellCount == 2)
        {
            var extent = LocalExtent(prop, parent);
            var longAlongRows = extent.x >= extent.z;
            var pieceAlongRows = piece.Rows == 2;
            yaw = (longAlongRows == pieceAlongRows ? 0f : 90f) + 180f * random.Next(2);
        }
        else
        {
            yaw = 90f * random.Next(4);
        }

        prop.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        return 1;
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
