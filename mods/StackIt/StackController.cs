using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CatLib.Game;
using CatLib.Logging;
using StackIt.Logic;
using StackIt.Scene;
using StackIt.Settings;
using UnityEngine;

namespace StackIt;

public sealed class StackController
{
    public const float TipSpeed = 4f;
    public const float SlideSpeed = 0.6f;
    public const float CellOverlap = StoreGrid.CellSize - 0.02f;
    public const float SlideSeconds = 0.25f;
    public const float StayBehindDistance = 0.05f;
    public const float PoseKeptSeconds = 1f;
    public const int PlanEveryFrames = 10;

    private readonly CatLogger _log;
    private readonly BridgeRegistry _registry = new();
    private readonly GridCache _grids = new();
    private readonly SupportFinder _finder;
    private readonly List<Vector3> _cells = new();
    private readonly List<CellSupport> _supports = new();
    private readonly List<HeldCell> _lost = new();
    private readonly List<HeldState> _states = new();
    private readonly List<Slide> _slides = new();
    private readonly Research.FallTrace _trace;
    private readonly Dictionary<(IntPtr Store, IntPtr Entity, long Anchor, int Yaw), BridgeVerdict> _verdicts = new();
    private readonly Dictionary<IntPtr, (MarkGraph Graph, Dictionary<IntPtr, int> Ids)> _graphs = new();
    private int _verdictFrame = -1;
    private int _graphFrame = -1;
    private float _startedAt = -1f;

    public StackController(CatLogger log)
    {
        _trace = new Research.FallTrace(log);
        _log = log;
        _finder = new SupportFinder(_grids, _registry);
        _registry.Rebuilt += () =>
        {
            _verdicts.Clear();
            _graphs.Clear();
        };
    }

    public StackSettings Settings { get; set; }

    public BridgeRegistry Registry => _registry;

    public int Placed { get; private set; }

    public int Dropped { get; private set; }

    public bool IsLevelStarted => _startedAt >= 0f;

    public static bool IsServer
    {
        get
        {
            try
            {
                return Singleton<NetworkManager>.HasInstance() && Singleton<NetworkManager>.Instance.IsServer;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    public void OnGameStarted() => _startedAt = Time.realtimeSinceStartup;

    public void OnLevelReady()
    {
        if (_startedAt < 0f && !IsServer)
        {
            _startedAt = Time.realtimeSinceStartup;
        }
    }

    public void Forget()
    {
        _startedAt = -1f;
        _slides.Clear();
        _registry.Clear();
        Invalidate();
    }

    public bool IsPositionValid(EntityInteractableStore store, Entity entity, Vector2Int anchor, int yaw, bool vanilla)
    {
        if (vanilla)
        {
            return !OverlapsReserved(store, entity, anchor, yaw) && !ClipsHanging(store, entity, anchor, yaw);
        }

        return Settings != null && Settings.Enabled.Value && Judge(store, entity, anchor, yaw) == BridgeVerdict.Bridge
               && !ClipsHanging(store, entity, anchor, yaw);
    }

    public BridgeVerdict Judge(EntityInteractableStore store, Entity entity, Vector2Int anchor, int yaw)
    {
        if (Time.frameCount != _verdictFrame)
        {
            _verdicts.Clear();
            _verdictFrame = Time.frameCount;
        }

        if (store == null || entity == null)
        {
            return BridgeVerdict.Blocked;
        }

        var key = (store.Pointer, entity.Pointer, CellKey.Of(anchor.x, anchor.y), yaw);
        if (_verdicts.TryGetValue(key, out var known))
        {
            return known;
        }

        var verdict = JudgeNow(store, entity, anchor, yaw);
        _verdicts[key] = verdict;
        return verdict;
    }

    public void OnStored(EntityInteractableStore store, Entity entity, Vector2Int anchor, int yaw)
    {
        Invalidate();
        if (!StoreGrid.IsParcel(store) || !_grids.TryView(store, out var view) || !Footprint(view, entity, anchor, yaw))
        {
            return;
        }

        var outside = _cells.Where(point => !view.Contains(store.GetGridPosition(point, false))).ToList();
        if (outside.Count == 0)
        {
            _registry.Remove(entity);
            return;
        }

        var root = StoreGrid.Root(store);
        var bridge = new Bridge(entity, store, anchor, yaw, root == null ? IntPtr.Zero : root.Pointer, Time.realtimeSinceStartup) { Height = HeightOf(entity) };
        var excluded = StoreGrid.StoreOf(entity);
        var main = store.transform;
        foreach (var point in outside)
        {
            if (_finder.TryFind(root, point, store, excluded, out var support, out var cell))
            {
                bridge.Held.Add(new HeldCell(support, cell, ToLocal(main, CellWorld(support, cell, point))));
            }
            else
            {
                bridge.Hanging.Add(new HangingCell(ToLocal(main, point), bridge.Since, false));
            }
        }

        _registry.Add(bridge);
        Placed++;
        var supports = bridge.Held.Select(held => held.Store.Pointer).Distinct().Count();
        _log.Info(Format("{0} stands across {1} more parcel(s) next to {2}, {3} of {4} cell(s) on them{5}",
            Name(entity), supports, Name(store.LinkedEntity), bridge.Held.Count, _cells.Count,
            bridge.Hanging.Count == 0 ? string.Empty : Format(", {0} cell(s) wait for their parcel", bridge.Hanging.Count)));
    }

    public Research.FallTrace Trace => _trace;

    public void OnRemoved(EntityInteractableStore store, Entity entity)
    {
        Invalidate();
        TraceSupportTaken(entity);
        DropAround(StoreGrid.StoreOf(entity));
        var bridge = _registry.Find(entity);
        if (bridge == null || store == null || !StoreGrid.IsAlive(bridge.Main) || bridge.Main.Pointer != store.Pointer)
        {
            return;
        }

        var root = StoreGrid.Root(bridge.Main);
        var falling = !IsServer || bridge.Waiting || root == null || root.Pointer != bridge.Root;
        _registry.Remove(bridge);
        if (falling && !IsCarried(entity))
        {
            if (bridge.HasPose)
            {
                StayBehind(bridge, store.transform);
            }
            else
            {
                Detach(entity.transform, store.transform);
            }

            Loosen(entity);
            SettleGhost(entity);
            _trace.Start(entity, "was let fall by the host");
            _log.Info($"{Name(entity)} was let fall by the host");
            return;
        }

        _log.Info($"{Name(entity)} left the joint it stood across");
    }

    public void OnBehaviorChecked(EntityProperties properties)
    {
        if (_registry.Count == 0 || properties == null)
        {
            return;
        }

        var store = StoreGrid.StoreOf(properties.LinkedEntity);
        var root = store == null ? null : StoreGrid.Root(store);
        if (root == null || !_registry.AnyUnder(root))
        {
            return;
        }

        var (graph, ids) = Graph(root);
        if (!ids.TryGetValue(store.Pointer, out var id))
        {
            return;
        }

        string reason = null;
        if (properties.BehaviorConstraint == BehaviorConstraint.Fragile && HasSettings(properties.BehaviorConstraint) && graph.FragileBroken(id))
        {
            reason = "a parcel stands across it";
        }
        else if (graph.HeavyAbove(id))
        {
            reason = "a heavy parcel stands above it across a joint";
        }

        if (reason == null)
        {
            return;
        }

        var wasDamaged = properties.IsDamaged;
        properties.SetDamaged(true);
        if (!wasDamaged)
        {
            _log.Info($"{Name(properties.LinkedEntity)} is damaged: {reason}");
        }
    }

    public void Update()
    {
        _trace.Update();
        if (_slides.Count > 0)
        {
            MoveSlides(Time.realtimeSinceStartup);
        }

        if (_registry.Count == 0 || !IsLevelStarted)
        {
            return;
        }

        var isServer = IsServer;
        var keepBalanced = Settings != null && Settings.KeepBalanced.Value;
        var now = Time.realtimeSinceStartup;
        foreach (var bridge in _registry.All.ToList())
        {
            Check(bridge, isServer, keepBalanced, now);
        }
    }

    public string Describe()
    {
        _log.Info(Format("{0} bridge(s), {1} held cell(s), {2} placed and {3} dropped since the level started, {4}",
            _registry.Count, _registry.ReservedCells, Placed, Dropped, IsServer ? "this player decides drops" : "the host decides drops"));
        foreach (var bridge in _registry.All)
        {
            var supports = bridge.Held.Where(held => StoreGrid.IsAlive(held.Store)).GroupBy(held => held.Store.Pointer)
                .Select(group => Name(group.First().Store.LinkedEntity) + " " + string.Join(" ", group.Select(held => held.Cell.x + ":" + held.Cell.y)));
            _log.Info(Format("{0} on {1} at {2}:{3} turned {4}, also on [{5}]{6}{7}", Name(bridge.Entity),
                StoreGrid.IsAlive(bridge.Main) ? Name(bridge.Main.LinkedEntity) : "a gone parcel",
                bridge.Anchor.x, bridge.Anchor.y, bridge.Yaw, string.Join("; ", supports),
                bridge.Hanging.Count == 0 ? string.Empty : Format(", {0} cell(s) over nothing", bridge.Hanging.Count),
                bridge.Waiting ? ", waiting for the host" : string.Empty));
        }

        return _registry.Count == 0 ? "no bridges" : "see the log";
    }

    private BridgeVerdict JudgeNow(EntityInteractableStore store, Entity entity, Vector2Int anchor, int yaw)
    {
        if (!StoreGrid.IsParcel(store) || !StoreGrid.IsAlive(store.ParentStore) || !_grids.TryView(store, out var view) || !view.Contains(anchor))
        {
            return BridgeVerdict.Blocked;
        }

        if (!Footprint(view, entity, anchor, yaw))
        {
            return BridgeVerdict.Blocked;
        }

        var root = StoreGrid.Root(store);
        var excluded = StoreGrid.StoreOf(entity);
        _supports.Clear();
        foreach (var point in _cells)
        {
            var index = store.GetGridPosition(point, false);
            if (view.Contains(index))
            {
                _supports.Add(view.IsFree(index) && !_registry.IsReserved(store, index) ? CellSupport.Main : CellSupport.Taken);
                continue;
            }

            if (!_finder.TryFind(root, point, store, excluded, out _, out _))
            {
                return BridgeVerdict.Blocked;
            }

            _supports.Add(CellSupport.Other);
        }

        return BridgeRules.Judge(_supports);
    }

    private void Check(Bridge bridge, bool isServer, bool keepBalanced, float now)
    {
        var entity = bridge.Entity;
        if (entity == null || entity.WasCollected || entity.IsDisposed || !StoreGrid.IsAlive(bridge.Main) || !bridge.Main.ContainsEntity(entity))
        {
            _registry.Remove(bridge);
            return;
        }

        var main = bridge.Main.transform;
        var root = StoreGrid.Root(bridge.Main);
        var rootChanged = root == null || root.Pointer != bridge.Root;
        var changed = false;
        if (rootChanged && bridge.Held.Count == 0 && root != null)
        {
            bridge.Root = root.Pointer;
            rootChanged = false;
        }

        if (bridge.Hanging.Count > 0 && !rootChanged)
        {
            changed = Resolve(bridge, root, main);
        }

        _states.Clear();
        foreach (var held in bridge.Held)
        {
            var world = Vector3.zero;
            var present = root != null && StoreGrid.IsAlive(held.Store) && StoreGrid.IsInside(held.Store, root)
                          && _grids.TryView(held.Store, out var view) && view.TryGetCellWorld(held.Cell, out world);
            if (present)
            {
                held.Local = ToLocal(main, world);
            }

            _states.Add(new HeldState(ToNumerics(held.Local), ToNumerics(held.Local), present));
        }

        var report = SupportCheck.Evaluate(rootChanged, _states);
        if ((report.MovedTogether || report.MainLost) && root != null)
        {
            bridge.Root = root.Pointer;
        }

        if (report.Lost.Count > 0)
        {
            _lost.Clear();
            foreach (var index in report.Lost)
            {
                _lost.Add(bridge.Held[index]);
            }

            foreach (var held in _lost)
            {
                bridge.Held.Remove(held);
                bridge.Hanging.Add(new HangingCell(held.Local, now, true));
            }

            changed = true;
            _log.Info(report.MainLost
                ? $"{Name(entity)} left its side parcels with {Name(bridge.Main.LinkedEntity)}"
                : $"{Name(entity)} lost the parcel under {_lost.Count} of its cell(s)");
        }

        var due = false;
        foreach (var hanging in bridge.Hanging)
        {
            due |= LossRules.IsDue(hanging.Lost, hanging.Since, _startedAt, now);
        }

        if (changed)
        {
            _registry.Changed();
        }

        switch (LossRules.Decide(keepBalanced, report.MainLost, due, isServer))
        {
            case LossAction.Drop:
                _registry.Remove(bridge);
                Drop(bridge, report.MainLost, null);
                return;
            case LossAction.Wait:
                if (!bridge.Waiting)
                {
                    bridge.Waiting = true;
                    _log.Info($"{Name(entity)} hangs over nothing, the host lets it fall");
                }

                break;
            default:
                bridge.Waiting = false;
                break;
        }

        if (!CarriedRoot(bridge.Main))
        {
            bridge.CarriedSince = -1f;
            bridge.RememberPose(entity.transform);
            if (Time.frameCount - bridge.PlannedAt >= PlanEveryFrames)
            {
                bridge.PlannedAt = Time.frameCount;
                bridge.FallPlan = PlanSlide(bridge, true, null);
                bridge.HasFallPlan = true;
            }
        }
        else if (bridge.CarriedSince < 0f)
        {
            bridge.CarriedSince = now;
        }
        else if (now - bridge.CarriedSince > PoseKeptSeconds)
        {
            bridge.ForgetPose();
        }
    }

    private bool Resolve(Bridge bridge, EntityInteractableStore root, Transform main)
    {
        var excluded = StoreGrid.StoreOf(bridge.Entity);
        var found = false;
        for (var index = bridge.Hanging.Count - 1; index >= 0; index--)
        {
            var point = ToWorld(main, bridge.Hanging[index].Local);
            if (_finder.TryFind(root, point, bridge.Main, excluded, out var support, out var cell))
            {
                bridge.Held.Add(new HeldCell(support, cell, ToLocal(main, CellWorld(support, cell, point))));
                bridge.Hanging.RemoveAt(index);
                _registry.Changed();
                found = true;
            }
        }

        if (found)
        {
            _log.Info($"{Name(bridge.Entity)} found parcels under its cells, {bridge.Hanging.Count} cell(s) still over nothing");
        }

        return found;
    }

    private void DropAround(EntityInteractableStore removed)
    {
        if (removed == null || _registry.Count == 0 || !IsLevelStarted || Settings == null || Settings.KeepBalanced.Value || !IsServer)
        {
            return;
        }

        foreach (var bridge in _registry.All.ToList())
        {
            if (!_registry.All.Contains(bridge) || !StoreGrid.IsAlive(bridge.Main))
            {
                continue;
            }

            if (bridge.Main.Pointer == removed.Pointer)
            {
                _registry.Remove(bridge);
                Drop(bridge, true, null);
            }
            else if (bridge.Held.Any(held => StoreGrid.IsAlive(held.Store) && held.Store.Pointer == removed.Pointer))
            {
                _registry.Remove(bridge);
                Drop(bridge, false, removed);
            }
        }
    }

    private void Drop(Bridge bridge, bool mainLost, EntityInteractableStore removed)
    {
        var entity = bridge.Entity;
        var main = bridge.Main.transform;
        var carriedAway = mainLost && bridge.HasPose;
        var plan = carriedAway && bridge.HasFallPlan && CarriedRoot(bridge.Main) ? bridge.FallPlan : PlanSlide(bridge, mainLost, removed);
        try
        {
            bridge.Main.RemoveEntity(entity);
            Dropped++;
        }
        catch (Exception exception)
        {
            _log.Error($"Letting {Name(entity)} fall failed", exception);
            return;
        }

        if (carriedAway)
        {
            StayBehind(bridge, main);
        }

        Loosen(entity);
        TakeAuthority(entity);
        SettleGhost(entity);
        var how = StartSlide(entity, plan);
        _trace.Start(entity, mainLost ? "falls, the parcel under its centre was taken" : "falls, part of it hangs over nothing");
        _log.Info(mainLost
            ? $"{Name(entity)} falls: {Name(bridge.Main.LinkedEntity)} under its centre was taken, {how}"
            : $"{Name(entity)} falls: part of it hangs over nothing, {how}");
    }

    private void TakeAuthority(Entity entity)
    {
        try
        {
            var network = entity.Network;
            if (network == null || !Singleton<NetworkedEntityManager>.HasInstance())
            {
                return;
            }

            Singleton<NetworkedEntityManager>.Instance.SetLocalClientAuthoritative(network);
        }
        catch (Exception exception)
        {
            _log.Warning($"Taking {Name(entity)} over from the player who last held it failed: {exception.Message}");
        }
    }

    private void TraceSupportTaken(Entity entity)
    {
        if (!_trace.IsEnabled || _registry.Count == 0)
        {
            return;
        }

        var taken = StoreGrid.StoreOf(entity);
        if (taken == null)
        {
            return;
        }

        foreach (var bridge in _registry.All)
        {
            if (StoreGrid.IsAlive(bridge.Main) && bridge.Main.Pointer == taken.Pointer)
            {
                _trace.Start(bridge.Entity, $"stands on {Name(entity)}, which was taken from its storage{(IsCarried(entity) ? " and is carried already" : string.Empty)}");
            }
        }
    }

    private void StayBehind(Bridge bridge, Transform main)
    {
        try
        {
            var transform = bridge.Entity.transform;
            Detach(transform, main);
            if ((transform.position - bridge.Position).sqrMagnitude > StayBehindDistance * StayBehindDistance)
            {
                _log.Info($"{Name(bridge.Entity)} moved with {Name(bridge.Main.LinkedEntity)} as it was picked up, it falls from where it stood");
            }

            transform.SetPositionAndRotation(bridge.Position, bridge.Rotation);
            var body = Body(bridge.Entity);
            if (body != null)
            {
                body.position = bridge.Position;
                body.rotation = bridge.Rotation;
            }
        }
        catch (Exception exception)
        {
            _log.Warning($"Keeping {Name(bridge.Entity)} where it stood failed: {exception.Message}");
        }
    }

    private static void Detach(Transform transform, Transform main)
    {
        if (main != null && transform.IsChildOf(main))
        {
            transform.SetParent(null, true);
        }
    }

    private void SettleGhost(Entity entity)
    {
        try
        {
            var network = entity.Network;
            if (network == null)
            {
                return;
            }

            network.ResetTransformAsGhost();
            network._targetVelocityAsGhost = Vector3.zero;
            network.SetMustUpdatePositionAndRotationAsGhost(false);
        }
        catch (Exception exception)
        {
            _log.Warning($"Clearing where {Name(entity)} last moved over the network failed: {exception.Message}");
        }
    }

    private SlidePlan PlanSlide(Bridge bridge, bool mainLost, EntityInteractableStore removed)
    {
        try
        {
            if (!_grids.TryView(bridge.Main, out var view) || !Footprint(view, bridge.Entity, bridge.Anchor, bridge.Yaw))
            {
                return default;
            }

            var main = bridge.Main.transform;
            var remaining = new List<System.Numerics.Vector2>();
            var lost = new List<System.Numerics.Vector2>();
            foreach (var point in _cells)
            {
                bool supported;
                if (view.Contains(bridge.Main.GetGridPosition(point, false)))
                {
                    supported = !mainLost;
                }
                else
                {
                    var local = ToLocal(main, point);
                    supported = bridge.Held.Any(held => StoreGrid.IsAlive(held.Store) && (removed == null || held.Store.Pointer != removed.Pointer)
                                                         && (held.Local - local).sqrMagnitude < BridgeRules.AlignTolerance * BridgeRules.AlignTolerance);
                }

                (supported ? remaining : lost).Add(new System.Numerics.Vector2(point.x, point.z));
            }

            var center = bridge.Entity.transform.position;
            return SlidePlanner.Plan(new System.Numerics.Vector2(center.x, center.z), remaining, lost, StoreGrid.CellSize);
        }
        catch (Exception exception)
        {
            _log.Warning($"Planning the fall of {Name(bridge.Entity)} failed: {exception.Message}");
            return default;
        }
    }

    private string StartSlide(Entity entity, SlidePlan plan)
    {
        if (!plan.HasDirection)
        {
            return "nothing to push";
        }

        var direction = new Vector3(plan.Direction.X, 0f, plan.Direction.Y);
        var body = Body(entity);
        if (body == null)
        {
            return "no body to move";
        }

        if (!plan.Slides)
        {
            return Tip(entity, direction) ? "pushed towards the open side" : "nothing to push";
        }

        body.isKinematic = true;
        var from = entity.transform.position;
        _slides.Add(new Slide(entity, body, from, from + direction * plan.Distance, direction, Time.realtimeSinceStartup));
        return Format("slides {0:0.00} m towards the open side", plan.Distance);
    }

    private void MoveSlides(float now)
    {
        for (var index = _slides.Count - 1; index >= 0; index--)
        {
            var slide = _slides[index];
            try
            {
                if (slide.Entity == null || slide.Entity.WasCollected || slide.Body == null || slide.Body.WasCollected || IsCarried(slide.Entity)
                    || StoreGrid.IsAlive(StoreGrid.StoreOf(slide.Entity)?.ParentStore))
                {
                    _slides.RemoveAt(index);
                    continue;
                }

                var progress = Mathf.Clamp01((now - slide.Start) / SlideSeconds);
                slide.Entity.transform.position = Vector3.Lerp(slide.From, slide.To, progress);
                if (progress < 1f)
                {
                    continue;
                }

                _slides.RemoveAt(index);
                slide.Body.isKinematic = false;
                Tip(slide.Entity, slide.Direction);
            }
            catch (Exception exception)
            {
                _slides.RemoveAt(index);
                _log.Warning($"Moving {Name(slide.Entity)} off its parcel failed: {exception.Message}");
            }
        }
    }

    private bool ClipsHanging(EntityInteractableStore store, Entity entity, Vector2Int anchor, int yaw)
    {
        if (!_registry.All.Any(bridge => bridge.Hanging.Count > 0) || !_grids.TryView(store, out var view) || !Footprint(view, entity, anchor, yaw))
        {
            return false;
        }

        var height = HeightOf(entity);
        if (height <= 0f)
        {
            return false;
        }

        foreach (var bridge in _registry.All)
        {
            if (bridge.Hanging.Count == 0 || !StoreGrid.IsAlive(bridge.Main) || bridge.Pointer == entity.Pointer || CarriedRoot(bridge.Main))
            {
                continue;
            }

            var main = bridge.Main.transform;
            foreach (var hanging in bridge.Hanging)
            {
                var world = ToWorld(main, hanging.Local);
                foreach (var point in _cells)
                {
                    if (Math.Abs(point.x - world.x) < CellOverlap && Math.Abs(point.z - world.z) < CellOverlap
                        && LossRules.Clips(point.y, point.y + height, world.y, world.y + bridge.Height))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static float HeightOf(Entity entity)
    {
        var pickable = entity == null || entity.Interactable == null ? null : entity.Interactable.Pickable;
        var collider = pickable == null ? null : pickable.MainCollider;
        return collider == null ? 0f : collider.size.y * collider.transform.lossyScale.y;
    }

    private bool Tip(Entity entity, Vector3 direction)
    {
        try
        {
            var body = Body(entity);
            if (body == null || body.isKinematic)
            {
                return false;
            }

            body.WakeUp();
            body.angularVelocity += Vector3.Cross(Vector3.up, direction) * TipSpeed;
            body.linearVelocity += direction * SlideSpeed;
            return true;
        }
        catch (Exception exception)
        {
            _log.Warning($"Pushing {Name(entity)} failed: {exception.Message}");
            return false;
        }
    }

    private void Loosen(Entity entity)
    {
        try
        {
            var pickable = entity.Interactable == null ? null : entity.Interactable.Pickable;
            if (pickable != null)
            {
                pickable.OverrideNotCarryable = false;
            }

            var physics = entity.Physics;
            if (physics != null)
            {
                physics.SetNonTriggerCollidersActive(true);
                physics.SetRigidbodyKinematic(false, true);
            }

            var store = StoreGrid.StoreOf(entity);
            if (store != null)
            {
                store.HandleDrop();
            }
        }
        catch (Exception exception)
        {
            _log.Warning($"Letting {Name(entity)} touch the world again failed: {exception.Message}");
        }
    }

    private static Rigidbody Body(Entity entity)
    {
        var physics = entity.Physics;
        var body = physics == null ? null : physics.GetComponent<Rigidbody>();
        return body != null ? body : entity.GetComponentInChildren<Rigidbody>();
    }

    private static bool CarriedRoot(EntityInteractableStore store)
    {
        var root = StoreGrid.Root(store);
        var entity = root == null ? null : root.LinkedEntity;
        return entity != null && !entity.WasCollected && IsCarried(entity);
    }

    private static bool IsCarried(Entity entity)
    {
        var pickable = entity.Interactable == null ? null : entity.Interactable.Pickable;
        return pickable != null && (pickable.CarryingEntity != null || pickable.IsInInventory);
    }

    private bool OverlapsReserved(EntityInteractableStore store, Entity entity, Vector2Int anchor, int yaw)
    {
        if (!_registry.HasReservations(store) || !_grids.TryView(store, out var view) || !Footprint(view, entity, anchor, yaw))
        {
            return false;
        }

        foreach (var point in _cells)
        {
            if (_registry.IsReserved(store, store.GetGridPosition(point, false)))
            {
                return true;
            }
        }

        return false;
    }

    private bool Footprint(GridView view, Entity entity, Vector2Int anchor, int yaw)
    {
        var offsets = _grids.Offsets(entity);
        return offsets != null && StoreGrid.TryGetFootprint(view, offsets, anchor, yaw, _cells);
    }

    private Vector3 CellWorld(EntityInteractableStore store, Vector2Int cell, Vector3 fallback) =>
        _grids.TryView(store, out var view) && view.TryGetCellWorld(cell, out var world) ? world : fallback;

    private void Invalidate()
    {
        _grids.Invalidate();
        _finder.Invalidate();
        _verdicts.Clear();
        _graphs.Clear();
    }

    private (MarkGraph Graph, Dictionary<IntPtr, int> Ids) Graph(EntityInteractableStore root)
    {
        if (Time.frameCount != _graphFrame)
        {
            _graphs.Clear();
            _graphFrame = Time.frameCount;
        }

        if (_graphs.TryGetValue(root.Pointer, out var cached))
        {
            return cached;
        }

        var stores = new List<EntityInteractableStore>();
        StoreGrid.CollectChildStores(root, stores);
        var ids = new Dictionary<IntPtr, int>();
        foreach (var store in stores)
        {
            ids[store.Pointer] = ids.Count;
        }

        var graph = new MarkGraph();
        foreach (var store in stores)
        {
            var parent = store.ParentStore;
            var properties = StoreGrid.IsParcel(store) ? store.LinkedEntity.Properties : null;
            graph.Add(ids[store.Pointer], StoreGrid.IsAlive(parent) && ids.TryGetValue(parent.Pointer, out var parentId) ? parentId : -1,
                properties == null ? BehaviorConstraint.None : properties.BehaviorConstraint);
        }

        foreach (var bridge in _registry.All)
        {
            var bridgeStore = StoreGrid.StoreOf(bridge.Entity);
            if (bridgeStore == null || !ids.TryGetValue(bridgeStore.Pointer, out var bridgeId))
            {
                continue;
            }

            graph.AddBridge(bridgeId, bridge.Held.Where(held => StoreGrid.IsAlive(held.Store) && ids.ContainsKey(held.Store.Pointer))
                .Select(held => ids[held.Store.Pointer]));
        }

        var result = (graph, ids);
        _graphs[root.Pointer] = result;
        return result;
    }

    private static bool HasSettings(BehaviorConstraint constraint)
    {
        try
        {
            return Singleton<EntityPropertiesManager>.HasInstance() && Singleton<EntityPropertiesManager>.Instance.GetBehaviorConstraintSettings(constraint) != null;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static Vector3 ToLocal(Transform main, Vector3 world) => Quaternion.Inverse(main.rotation) * (world - main.position);

    private static Vector3 ToWorld(Transform main, Vector3 local) => main.position + main.rotation * local;

    private static System.Numerics.Vector3 ToNumerics(Vector3 value) => new(value.x, value.y, value.z);

    public static string Name(Entity entity)
    {
        if (entity == null || entity.WasCollected)
        {
            return "a parcel";
        }

        var properties = entity.IsDisposed ? null : entity.Properties;
        var id = entity.Network == null ? 0u : entity.Network.NetworkIdentifier;
        return properties == null ? entity.name : properties.PackageSize + "#" + id.ToString(CultureInfo.InvariantCulture);
    }

    private static string Format(string format, params object[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
}
