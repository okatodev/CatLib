using System;
using System.Collections.Generic;
using CatLib.Logging;
using UnityEngine;

namespace ShelfLabels;

public sealed class LabelView
{
    public const string CopySuffix = " [ShelfLabels]";
    public const float ProbeShrink = 0.4f;
    public const float StandMargin = 0.002f;

    private readonly SortedDictionary<int, Copy> _copies = new();
    private Placement _resolved;
    private float _width;
    private float _height;
    private Vector3 _right;
    private Vector3 _up;
    private Vector3 _normal;
    private float _standCut = float.NaN;
    private StandPlan _standPlan;

    public LabelView(EntityStorageLabel original, int labelId)
    {
        Original = original;
        Pointer = original.Pointer;
        Holder = original.transform.parent;
        LabelId = labelId;
    }

    public EntityStorageLabel Original { get; }

    public IntPtr Pointer { get; }

    public Transform Holder { get; }

    public int LabelId { get; }

    public int CopyCount => _copies.Count;

    public Placement Resolved => _resolved;

    public Placement Applied { get; private set; } = (Placement)(-1);

    public bool? AppliedStandHidden { get; private set; }

    public string StandProblem => _standPlan?.Problem;

    public string StandSummary => _standPlan?.Summary;

    public IEnumerable<IntPtr> Roots
    {
        get
        {
            yield return Holder.Pointer;
            foreach (var copy in _copies.Values)
            {
                if (copy.Root != null && !copy.Root.WasCollected)
                {
                    yield return copy.Root.transform.Pointer;
                }
            }
        }
    }

    public bool IsAlive => Original != null && !Original.WasCollected && Holder != null && !Holder.WasCollected;

    public int SpriteCount
    {
        get
        {
            var sprites = IsAlive ? Original.PossibleSprites : null;
            return sprites == null ? 0 : sprites.Length;
        }
    }

    public IEnumerable<IntPtr> Actions
    {
        get
        {
            foreach (var copy in _copies.Values)
            {
                yield return copy.Action;
            }
        }
    }

    public bool TryGetSlot(IntPtr action, out int slot)
    {
        foreach (var pair in _copies)
        {
            if (pair.Value.Action == action)
            {
                slot = pair.Key;
                return true;
            }
        }

        slot = 0;
        return false;
    }

    public void Sync(int count, Placement placement, bool hideStand, float spacing, CatLogger log)
    {
        if (!IsAlive)
        {
            return;
        }

        if (placement != Applied || hideStand != AppliedStandHidden)
        {
            RemoveAll();
            Applied = placement;
            AppliedStandHidden = hideStand;
        }

        if (placement == Placement.None)
        {
            count = 0;
        }

        for (var slot = LabelSlot.MaxSlots; slot > count; slot--)
        {
            Remove(slot);
        }

        if (count == 0)
        {
            return;
        }

        if (_copies.Count == 0)
        {
            Measure();
            _resolved = Resolve(placement, spacing);
        }

        if (hideStand && _standPlan == null)
        {
            _standPlan = PlanStand(log);
        }

        for (var slot = 1; slot <= count; slot++)
        {
            if (!_copies.ContainsKey(slot))
            {
                Create(slot, spacing, hideStand ? _standPlan : null, log);
            }
        }
    }

    public void Show(Func<LabelSlot, int> picture)
    {
        if (!IsAlive)
        {
            return;
        }

        var sprites = Original.PossibleSprites;
        foreach (var pair in _copies)
        {
            var renderer = pair.Value.Renderer;
            if (renderer == null || renderer.WasCollected || sprites == null || sprites.Length == 0)
            {
                continue;
            }

            var index = picture(new LabelSlot(LabelId, pair.Key));
            renderer.sprite = sprites[index >= 0 && index < sprites.Length ? index : 0];
        }
    }

    public void RemoveAll()
    {
        foreach (var slot in new List<int>(_copies.Keys))
        {
            Remove(slot);
        }
    }

    private void Remove(int slot)
    {
        if (!_copies.TryGetValue(slot, out var copy))
        {
            return;
        }

        _copies.Remove(slot);
        if (copy.Root != null && !copy.Root.WasCollected)
        {
            UnityEngine.Object.Destroy(copy.Root);
        }
    }

    private void Create(int slot, float spacing, StandPlan stand, CatLogger log)
    {
        var holder = new GameObject("ShelfLabels holder");
        holder.SetActive(false);
        holder.transform.SetParent(Holder.parent, false);
        GameObject clone = null;
        try
        {
            clone = UnityEngine.Object.Instantiate(Holder.gameObject, holder.transform);
            clone.name = Holder.name + " " + slot + CopySuffix;
            Strip(clone);
            var label = clone.GetComponentInChildren<EntityStorageLabel>(true);
            if (label == null)
            {
                throw new InvalidOperationException("the copy has no label component");
            }

            label.enabled = false;
            var renderer = label.IllustrationSpriteRenderer;
            var action = clone.GetComponentInChildren<EntityInteractableAction>(true);
            if (renderer == null || action == null)
            {
                throw new InvalidOperationException("the copy has no picture or no interaction");
            }

            stand?.Apply(clone.transform);
            clone.transform.SetParent(Holder.parent, false);
            clone.transform.localPosition = Holder.localPosition;
            clone.transform.localRotation = Holder.localRotation;
            clone.transform.localScale = Holder.localScale;
            var (right, up) = SlotLayout.Offset(slot, _resolved, _width, _height, spacing);
            clone.transform.position = Holder.position + _right * right + _up * up;
            _copies[slot] = new Copy(clone, renderer, action.Pointer);
            clone = null;
        }
        catch (Exception exception)
        {
            log.Error($"Creating extra label {slot} for label {LabelId} ({Holder.name}) failed", exception);
        }
        finally
        {
            if (clone != null)
            {
                UnityEngine.Object.DestroyImmediate(clone);
            }

            UnityEngine.Object.DestroyImmediate(holder);
        }
    }

    private static void Strip(GameObject clone)
    {
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
            UnityEngine.Object.DestroyImmediate(component);
        }
    }

    private void Measure()
    {
        var picture = Original.IllustrationSpriteRenderer;
        var pictureTransform = picture == null ? Holder : picture.transform;
        _right = pictureTransform.right;
        _up = pictureTransform.up;
        _normal = pictureTransform.forward;

        var minRight = float.MaxValue;
        var maxRight = float.MinValue;
        var minUp = float.MaxValue;
        var maxUp = float.MinValue;
        foreach (var renderer in Holder.GetComponentsInChildren<Renderer>(false))
        {
            if (renderer == null || !renderer.enabled)
            {
                continue;
            }

            var bounds = renderer.localBounds;
            var min = bounds.min;
            var max = bounds.max;
            for (var corner = 0; corner < 8; corner++)
            {
                var local = new Vector3((corner & 1) == 0 ? min.x : max.x, (corner & 2) == 0 ? min.y : max.y, (corner & 4) == 0 ? min.z : max.z);
                var offset = renderer.transform.TransformPoint(local) - Holder.position;
                var alongRight = Vector3.Dot(offset, _right);
                var alongUp = Vector3.Dot(offset, _up);
                minRight = Mathf.Min(minRight, alongRight);
                maxRight = Mathf.Max(maxRight, alongRight);
                minUp = Mathf.Min(minUp, alongUp);
                maxUp = Mathf.Max(maxUp, alongUp);
            }
        }

        if (minRight > maxRight)
        {
            _width = 0.3f;
            _height = 0.15f;
            return;
        }

        _width = maxRight - minRight;
        _height = maxUp - minUp;
        MeasurePicture(pictureTransform);
    }

    private void MeasurePicture(Transform pictureTransform)
    {
        _standCut = float.NaN;
        Sprite sprite = Original.IllustrationSpriteRenderer == null ? null : Original.IllustrationSpriteRenderer.sprite;
        var sprites = Original.PossibleSprites;
        for (var index = 0; sprite == null && sprites != null && index < sprites.Length; index++)
        {
            sprite = sprites[index];
        }

        if (sprite == null)
        {
            return;
        }

        var bounds = sprite.bounds;
        var minRight = float.MaxValue;
        var maxRight = float.MinValue;
        var bottom = float.MaxValue;
        for (var corner = 0; corner < 4; corner++)
        {
            var local = new Vector3((corner & 1) == 0 ? bounds.min.x : bounds.max.x, (corner & 2) == 0 ? bounds.min.y : bounds.max.y, 0f);
            var offset = pictureTransform.TransformPoint(local) - Holder.position;
            minRight = Mathf.Min(minRight, Vector3.Dot(offset, _right));
            maxRight = Mathf.Max(maxRight, Vector3.Dot(offset, _right));
            bottom = Mathf.Min(bottom, Vector3.Dot(offset, _up));
        }

        var border = Mathf.Max(0.005f, (_width - (maxRight - minRight)) * 0.5f);
        _standCut = bottom - border * 1.25f - StandMargin;
    }

    private StandPlan PlanStand(CatLogger log)
    {
        var plan = new StandPlan();
        if (float.IsNaN(_standCut))
        {
            plan.Problem = "the picture of the label could not be measured";
            return plan;
        }

        foreach (var filter in Holder.GetComponentsInChildren<MeshFilter>(true))
        {
            var mesh = filter == null ? null : filter.sharedMesh;
            if (mesh == null)
            {
                continue;
            }

            var transform = filter.transform;
            var bounds = mesh.bounds;
            var lowest = float.MaxValue;
            var highest = float.MinValue;
            for (var corner = 0; corner < 8; corner++)
            {
                var local = new Vector3((corner & 1) == 0 ? bounds.min.x : bounds.max.x, (corner & 2) == 0 ? bounds.min.y : bounds.max.y, (corner & 4) == 0 ? bounds.min.z : bounds.max.z);
                var height = Vector3.Dot(transform.TransformPoint(local) - Holder.position, _up);
                lowest = Mathf.Min(lowest, height);
                highest = Mathf.Max(highest, height);
            }

            var path = PathOf(transform);
            if (highest < _standCut)
            {
                plan.Hide.Add(path);
                plan.Notes.Add($"{path}: hidden as a separate part");
                continue;
            }

            if (lowest >= _standCut)
            {
                continue;
            }

            Mesh cut;
            string result;
            try
            {
                cut = StandCutter.Cut(mesh, transform, Holder.position, _up, _standCut, out result);
            }
            catch (Exception exception)
            {
                cut = null;
                result = $"{(mesh.isReadable ? "readable" : "not readable")} mesh with {mesh.vertexCount} vertices failed: {exception.GetType().Name}: {exception.Message}";
            }

            plan.Notes.Add($"{path}: mesh {mesh.name}, {result}");
            if (cut != null)
            {
                plan.Replace.Add((path, cut));
            }
        }

        if (plan.Hide.Count == 0 && plan.Replace.Count == 0)
        {
            plan.Problem = plan.Notes.Count == 0 ? "no part of the label reaches below the frame" : string.Join("; ", plan.Notes);
        }

        plan.Summary = $"{Holder.name}: cut {_standCut:0.000} m under the holder, {string.Join("; ", plan.Notes)}";
        return plan;
    }

    private string PathOf(Transform transform)
    {
        var names = new List<string>();
        for (var current = transform; current != null && current.Pointer != Holder.Pointer; current = current.parent)
        {
            names.Insert(0, current.name);
        }

        return string.Join("/", names);
    }

    private sealed class StandPlan
    {
        public List<string> Hide { get; } = new();

        public List<(string Path, Mesh Mesh)> Replace { get; } = new();

        public List<string> Notes { get; } = new();

        public string Problem { get; set; }

        public string Summary { get; set; }

        public void Apply(Transform root)
        {
            if (Problem != null)
            {
                return;
            }

            foreach (var path in Hide)
            {
                var part = string.IsNullOrEmpty(path) ? root : root.Find(path);
                var renderer = part == null ? null : part.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.enabled = false;
                }
            }

            foreach (var (path, mesh) in Replace)
            {
                var part = string.IsNullOrEmpty(path) ? root : root.Find(path);
                var filter = part == null ? null : part.GetComponent<MeshFilter>();
                if (filter != null && mesh != null && !mesh.WasCollected)
                {
                    filter.sharedMesh = mesh;
                }
            }
        }
    }

    private Placement Resolve(Placement placement, float spacing)
    {
        if (placement != Placement.Auto)
        {
            return placement;
        }

        var preferred = SlotLayout.NameSide(Holder.name);
        var other = preferred == Placement.Left ? Placement.Right : Placement.Left;
        return SlotLayout.ChooseSide(preferred, Blocked(preferred, spacing), Blocked(other, spacing));
    }

    private int Blocked(Placement side, float spacing)
    {
        var (right, up) = SlotLayout.Offset(1, side, _width, _height, spacing);
        var centre = Holder.position + _right * right + _up * up;
        var half = new Vector3(_width * ProbeShrink, _height * ProbeShrink, Mathf.Max(_width, _height) * ProbeShrink * 0.5f);
        var hits = Physics.OverlapBox(centre, half, Quaternion.LookRotation(_normal, _up), Physics.AllLayers, QueryTriggerInteraction.Ignore);
        var blocked = 0;
        foreach (var hit in hits)
        {
            if (hit != null && !hit.transform.IsChildOf(Holder))
            {
                blocked++;
            }
        }

        return blocked;
    }

    private sealed class Copy
    {
        public Copy(GameObject root, SpriteRenderer renderer, IntPtr action)
        {
            Root = root;
            Renderer = renderer;
            Action = action;
        }

        public GameObject Root { get; }

        public SpriteRenderer Renderer { get; }

        public IntPtr Action { get; }
    }
}
