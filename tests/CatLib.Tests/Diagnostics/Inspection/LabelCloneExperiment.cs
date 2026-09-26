using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CatLib.Il2Cpp;
using CatLib.Logging;
using HarmonyLib;
using UnityEngine;

namespace CatLib.Tests.Diagnostics.Inspection;

public sealed class LabelCloneExperiment
{
    public const string CloneSuffix = " (CatLib.Tests clone)";
    public const float Spacing = 0.05f;
    public const int MaxTreeLines = 60;

    private static CatLogger _traceLog;
    private static Harmony _harmony;

    private readonly CatLogger _log;
    private readonly List<Pair> _pairs = new();

    public LabelCloneExperiment(CatLogger log)
    {
        _log = log;
    }

    public void Toggle()
    {
        EnsureTrace();
        if (_pairs.Count > 0)
        {
            RemoveAll();
            return;
        }

        var camera = EntityInspector.FindCamera();
        var target = camera == null ? null : EntityInspector.FindTarget(camera, out _);
        var label = target == null ? null : target.GetComponentInParent<EntityStorageLabel>(true) ?? target.GetComponentInChildren<EntityStorageLabel>(true);
        if (label == null)
        {
            _log.Warning("Label clone experiment: look at a shelf label first");
            return;
        }

        var tag = label.transform.parent;
        if (tag == null || tag.parent == null)
        {
            _log.Warning("Label clone experiment: the label has no holder to copy");
            return;
        }

        _log.Info("Label clone experiment: holder tree of " + EntityInspector.PathOf(tag) + Environment.NewLine + Tree(tag));

        var holder = new GameObject("CatLib.Tests clone holder");
        holder.SetActive(false);
        holder.transform.SetParent(tag.parent, false);
        GameObject clone = null;
        try
        {
            clone = UnityEngine.Object.Instantiate(tag.gameObject, holder.transform);
            clone.name = tag.name + CloneSuffix;
            var removed = Strip(clone);
            var disabled = 0;
            foreach (var cloneLabelComponent in clone.GetComponentsInChildren<EntityStorageLabel>(true))
            {
                cloneLabelComponent.enabled = false;
                disabled++;
            }
            clone.transform.SetParent(tag.parent, false);
            clone.transform.localPosition = tag.localPosition;
            clone.transform.localRotation = tag.localRotation;
            clone.transform.localScale = tag.localScale;
            clone.transform.position += tag.right * (Width(tag) + Spacing);

            var cloneLabel = clone.GetComponentInChildren<EntityStorageLabel>(true);
            var pair = new Pair(label, cloneLabel, clone, label._currentSpriteIndex, cloneLabel == null ? -1 : cloneLabel._currentSpriteIndex);
            _pairs.Add(pair);
            var bound = Bind(pair);
            _log.Info($"Label clone experiment: cloned {tag.name}, removed {removed}, disabled {disabled} label script(s), active {clone.activeInHierarchy}, " +
                      $"clone label {(cloneLabel == null ? "missing" : "index " + cloneLabel._currentSpriteIndex)}, original index {label._currentSpriteIndex}, {bound}");
        }
        catch (Exception exception)
        {
            _log.Error("Label clone experiment failed", exception);
            if (clone != null)
            {
                UnityEngine.Object.DestroyImmediate(clone);
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(holder);
        }
    }

    public void Update()
    {
        foreach (var pair in _pairs)
        {
            Watch(pair.Original, ref pair.OriginalIndex, "original");
            Watch(pair.Clone, ref pair.CloneIndex, "clone");
        }
    }

    private void Watch(EntityStorageLabel label, ref int last, string role)
    {
        if (label == null || label.WasCollected)
        {
            return;
        }

        var index = label._currentSpriteIndex;
        if (index == last)
        {
            return;
        }

        _log.Info($"Label clone experiment: {role} label changed from {last} to {index}, sprite {SpriteName(label)}");
        last = index;
    }

    private string Bind(Pair pair)
    {
        var action = pair.Clone == null ? null : pair.Clone.GetComponent<EntityInteractableAction>();
        if (action == null)
        {
            return "no interaction action on the clone";
        }

        pair.Bindings.Add<EntityInteractableAction.InteractedHandler>("PrimaryInteracted",
            new Action<PlayerEntityBase>(_ => OnCloneClick(pair, 1, "PrimaryInteracted")),
            handler => action.add_PrimaryInteracted(handler), handler => action.remove_PrimaryInteracted(handler));
        pair.Bindings.Add<EntityInteractableAction.SecondaryInteractedHandler>("SecondaryInteracted",
            new Action(() => OnCloneClick(pair, -1, "SecondaryInteracted")),
            handler => action.add_SecondaryInteracted(handler), handler => action.remove_SecondaryInteracted(handler));
        pair.Bindings.Add<EntityInteractableAction.ClientLocallyInteractedHandler>("LocallyPrimaryInteracted",
            new Action(() => _log.Info("Label clone experiment: clone event LocallyPrimaryInteracted")),
            handler => action.add_LocallyPrimaryInteracted(handler), handler => action.remove_LocallyPrimaryInteracted(handler));
        return $"clone events bound {pair.Bindings.Count}, failed {pair.Bindings.Failures.Count}{(pair.Bindings.Failures.Count > 0 ? ": " + string.Join("; ", pair.Bindings.Failures) : string.Empty)}";
    }

    private void OnCloneClick(Pair pair, int step, string eventName)
    {
        var sprites = pair.Original == null || pair.Original.WasCollected ? null : pair.Original.PossibleSprites;
        if (sprites == null || sprites.Length == 0 || pair.Clone == null || pair.Clone.WasCollected)
        {
            _log.Info($"Label clone experiment: clone event {eventName}, nothing to show");
            return;
        }

        pair.ShownIndex = ((pair.ShownIndex + step) % sprites.Length + sprites.Length) % sprites.Length;
        var renderer = pair.Clone.IllustrationSpriteRenderer;
        if (renderer != null)
        {
            renderer.sprite = sprites[pair.ShownIndex];
        }

        var sprite = sprites[pair.ShownIndex];
        _log.Info($"Label clone experiment: clone event {eventName}, the mod shows {pair.ShownIndex} ({(sprite == null ? "empty" : sprite.name)})");
    }

    private void EnsureTrace()
    {
        if (_harmony != null)
        {
            return;
        }

        _traceLog = _log;
        try
        {
            _harmony = new Harmony("catlib.tests.labelclone");
            _harmony.Patch(AccessTools.Method(typeof(EntityInteractableAction), nameof(EntityInteractableAction.Interact)),
                prefix: new HarmonyMethod(typeof(LabelCloneExperiment), nameof(InteractPrefix)));
            _harmony.Patch(AccessTools.Method(typeof(EntityInteractableAction), nameof(EntityInteractableAction.InteractSecondary)),
                prefix: new HarmonyMethod(typeof(LabelCloneExperiment), nameof(InteractSecondaryPrefix)));
            _log.Info("Label clone experiment: tracing Interact and InteractSecondary");
        }
        catch (Exception exception)
        {
            _log.Error("Label clone experiment: patching Interact failed", exception);
        }
    }

    private static void InteractPrefix(EntityInteractableAction __instance, bool is_from_network) => Trace("Interact", __instance, is_from_network);

    private static void InteractSecondaryPrefix(EntityInteractableAction __instance, bool is_from_network) => Trace("InteractSecondary", __instance, is_from_network);

    private static void Trace(string method, EntityInteractableAction action, bool fromNetwork)
    {
        try
        {
            var name = action == null ? "null" : action.transform.parent == null ? action.name : action.transform.parent.name;
            if (name.IndexOf("StorageTag", StringComparison.Ordinal) >= 0)
            {
                _traceLog?.Info($"Label clone experiment: {method} on {name}, from network {fromNetwork}");
            }
        }
        catch (Exception exception)
        {
            _traceLog?.Warning($"Label clone experiment: tracing {method} failed: {exception.Message}");
        }
    }

    private void RemoveAll()
    {
        var removed = 0;
        foreach (var pair in _pairs)
        {
            pair.Bindings.Clear(_log);
            if (pair.Root != null && !pair.Root.WasCollected)
            {
                UnityEngine.Object.DestroyImmediate(pair.Root);
                removed++;
            }
        }

        _pairs.Clear();
        _log.Info($"Label clone experiment: removed {removed} clone(s)");
    }

    private static string Strip(GameObject clone)
    {
        var removed = new List<string>();
        var components = new List<Component>();
        foreach (var identifier in clone.GetComponentsInChildren<EntityPermanentIdentifier>(true))
        {
            components.Add(identifier);
        }

        foreach (var network in clone.GetComponentsInChildren<EntityNetwork>(true))
        {
            components.Add(network);
        }

        foreach (var component in components)
        {
            removed.Add(component.GetIl2CppType().Name + " on " + component.name);
            UnityEngine.Object.DestroyImmediate(component);
        }

        return removed.Count == 0 ? "nothing" : string.Join(", ", removed);
    }

    private static float Width(Transform tag)
    {
        var renderers = tag.GetComponentsInChildren<Renderer>(false);
        if (renderers.Length == 0)
        {
            return 0.3f;
        }

        var bounds = renderers[0].bounds;
        for (var index = 1; index < renderers.Length; index++)
        {
            bounds.Encapsulate(renderers[index].bounds);
        }

        var local = tag.InverseTransformVector(bounds.size);
        return Mathf.Abs(local.x) * tag.lossyScale.x;
    }

    private static string SpriteName(EntityStorageLabel label)
    {
        var renderer = label.IllustrationSpriteRenderer;
        var sprite = renderer == null ? null : renderer.sprite;
        return sprite == null ? "none" : sprite.name;
    }

    private static string Tree(Transform root)
    {
        var builder = new StringBuilder();
        var lines = 0;
        Append(builder, root, 0, ref lines);
        return builder.ToString();
    }

    private static void Append(StringBuilder builder, Transform transform, int depth, ref int lines)
    {
        if (lines++ >= MaxTreeLines)
        {
            return;
        }

        var components = transform.GetComponents<Component>()
            .Where(component => component != null)
            .Select(component => component.GetIl2CppType().Name)
            .Where(name => name != "Transform");
        builder.Append(new string(' ', depth * 2 + 2)).Append(transform.name)
            .Append(transform.gameObject.activeSelf ? string.Empty : " (inactive)")
            .Append("  [").Append(LayerMask.LayerToName(transform.gameObject.layer)).Append("]  ")
            .AppendLine(string.Join(", ", components));
        for (var index = 0; index < transform.childCount; index++)
        {
            Append(builder, transform.GetChild(index), depth + 1, ref lines);
        }
    }

    private sealed class Pair
    {
        public Pair(EntityStorageLabel original, EntityStorageLabel clone, GameObject root, int originalIndex, int cloneIndex)
        {
            Original = original;
            Clone = clone;
            Root = root;
            OriginalIndex = originalIndex;
            CloneIndex = cloneIndex;
            ShownIndex = originalIndex;
        }

        public Il2CppEventBindings Bindings { get; } = new();

        public int ShownIndex;

        public EntityStorageLabel Original { get; }

        public EntityStorageLabel Clone { get; }

        public GameObject Root { get; }

        public int OriginalIndex;

        public int CloneIndex;
    }
}
