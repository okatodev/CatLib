using System.Collections.Generic;
using System.Linq;
using System;
using BoatTweaks.Logic;
using BoatTweaks.Scene;
using BoatTweaks.Settings;
using CatLib.Core;
using CatLib.Il2Cpp;
using CatLib.Localization;
using CatLib.Logging;
using CatLib.Net;
using CatLib.UI;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace BoatTweaks;

public sealed class BoatController
{
    public const double ScanIntervalSeconds = 1;
    public const double DecisionDelaySeconds = 30;
    public const double BuildTimeoutSeconds = 20;
    public const string FallbackPoolKey = "fallback";

    private readonly CatLogger _log;
    private string _lastCatalog;
    private readonly TextCatalog _texts;
    private readonly PatternLibrary _library;
    private readonly DeckBuilder _builder;
    private readonly DeckEnforcer _enforcer;
    private readonly System.Random _random = new();
    private readonly Dictionary<IntPtr, Pool> _pools = new();
    private readonly Dictionary<IntPtr, PrefabState> _prefabs = new();
    private readonly Dictionary<string, int> _lastVariant = new(StringComparer.Ordinal);
    private readonly Dictionary<string, LayoutPlan> _plans = new(StringComparer.Ordinal);
    private readonly InstanceTracker _manager = new();
    private bool _pruneAfterCollect;
    private bool _needDecision = true;
    private double _decideAt = double.PositiveInfinity;
    private int _sequence;
    private string _lastLayout;
    private string _lastNote;
    private string _planText;
    private DeckPlan _plan = DeckPlan.Game;
    private EntityInteractableStore _pendingStore;
    private DeckPlan _pendingPlan;
    private double _pendingUntil;
    private int _blockerLayer = -2;
    private IntPtr _lastBoatStore;
    private bool _dirty = true;
    private double _nextScan;
    private string _lastSummary;
    private bool _suspended;
    private bool _wasActive = true;
    private readonly HashSet<string> _freedReported = new(StringComparer.Ordinal);

    public BoatController(CatLogger log, TextCatalog texts, PatternLibrary library)
    {
        _log = log;
        _texts = texts;
        _library = library;
        _builder = new DeckBuilder(log);
        _enforcer = new DeckEnforcer(log);
    }

    public BoatSettings Settings { get; set; }

    public IReadOnlyDictionary<string, LayoutPlan> Plans => _plans;

    public void RequestApply() => _dirty = true;

    public void RequestDecision()
    {
        _needDecision = true;
        _dirty = true;
    }

    public bool IsAuthority => Settings != null && CatNetwork.IsAuthority;

    public bool IsActive => CatNetwork.IsActive(PluginMeta.Guid);

    public LayoutMode EffectiveMode => IsActive ? Settings.Mode.Value : LayoutMode.Game;

    public float ApprovedScale => IsActive ? Settings.ApprovedHeightScale.Value : 1f;

    public float MaximumScale => IsActive ? Settings.MaximumHeightScale.Value : 1f;

    public void OnActiveModsChanged()
    {
        var active = IsActive;
        if (active == _wasActive)
        {
            return;
        }

        _wasActive = active;
        _log.Info(active
            ? "Boat Tweaks is active again in this session"
            : "Boat Tweaks is paused in this session because not every player has it, boats come like in the game");
        RequestDecision();
    }

    public DeckPlan CurrentPlan
    {
        get
        {
            if (!IsActive || (!IsAuthority && !Settings.Plan.IsOverridden))
            {
                return DeckPlan.Game;
            }

            var text = Settings.Plan.Value;
            if (text != _planText)
            {
                _planText = text;
                _plan = DeckPlan.Decode(text);
            }

            return _plan;
        }
    }

    public void SuspendForRestart()
    {
        _suspended = true;
        _pendingStore = null;
        _enforcer.Clear();
        _log.Info("The game is restarting, the boats are left alone until the next level");
    }

    public void OnLevelLoadStarted()
    {
        _suspended = false;
        _manager.Forget();
        _freedReported.Clear();
    }

    public void Update()
    {
        if (Settings == null || _suspended)
        {
            return;
        }

        try
        {
            TrackManager();
            _enforcer.Update();
            HandleHotkey();
            BuildPending();
            if (_needDecision || FrameLoop.Realtime >= _decideAt)
            {
                Decide();
            }

            if (!_dirty && FrameLoop.Realtime < _nextScan)
            {
                return;
            }

            _nextScan = FrameLoop.Realtime + ScanIntervalSeconds;
            Scan();
        }
        catch (Exception exception)
        {
            _log.Error("Updating the boat failed", exception);
            _nextScan = FrameLoop.Realtime + ScanIntervalSeconds * 10;
        }
    }

    private void TrackManager()
    {
        var manager = Singleton<ParcelDeliveryManager>.HasInstance() ? Singleton<ParcelDeliveryManager>.Instance : null;
        if (!_manager.Observe(manager == null ? IntPtr.Zero : manager.Pointer))
        {
            return;
        }

        _pools.Clear();
        _lastBoatStore = IntPtr.Zero;
        _dirty = true;
        _pruneAfterCollect = true;
        _needDecision = true;
        _pendingStore = null;
        _enforcer.Clear();
        if (manager == null)
        {
            return;
        }

        var current = CurrentStore(manager);
        if (current != null && EffectiveMode != LayoutMode.Game)
        {
            _log.Warning($"The boat {KeyOf(current)} was chosen before Boat Tweaks could apply its settings to this level");
        }
        else
        {
            _log.Info("New level, applying boat settings before the first boat");
        }
    }

    private void Scan()
    {
        if (!Singleton<ParcelDeliveryManager>.HasInstance())
        {
            _lastBoatStore = IntPtr.Zero;
            return;
        }

        var manager = Singleton<ParcelDeliveryManager>.Instance;
        var pools = CollectPools(manager);
        if (pools.Count == 0)
        {
            return;
        }

        if (_pruneAfterCollect)
        {
            _pruneAfterCollect = false;
            Prune(pools);
        }

        var current = CurrentStore(manager);
        var currentPointer = current == null ? IntPtr.Zero : current.Pointer;
        if (currentPointer != _lastBoatStore)
        {
            _lastBoatStore = currentPointer;
            if (current != null)
            {
                OnBoatArrived(current, pools);
            }

            _dirty = true;
        }

        if (!_dirty)
        {
            return;
        }

        _dirty = false;
        Apply(pools);
        ApplyHeightToScene();
    }

    private void ApplyHeightToScene()
    {
        var states = _prefabs.Values.GroupBy(state => state.Key).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        foreach (var store in UnityEngine.Object.FindObjectsOfType<EntityInteractableStore>())
        {
            if (store == null || !states.TryGetValue(KeyOf(store), out var state))
            {
                continue;
            }

            var before = store._StorageMaximumApprovedHeight_k__BackingField;
            ApplyHeight(store, state);
            MoveApprovedVisual(store, before, store._StorageMaximumApprovedHeight_k__BackingField);
            store.UpdateApprovedHeight();
        }
    }

    private static string DescribeVisual(EntityInteractableStore store)
    {
        var visual = store._ApprovedHeightVisual_k__BackingField;
        if (visual == null)
        {
            return "no approved height visual";
        }

        var transform = visual.transform;
        return $"approved height visual at local y {transform.localPosition.y:0.###}, scale y {transform.localScale.y:0.###}, parent {(transform.parent == null ? "none" : transform.parent.name)}";
    }

    private void MoveApprovedVisual(EntityInteractableStore store, float before, float after)
    {
        if (before <= 0f || after <= 0f || Math.Abs(before - after) < 0.001f)
        {
            return;
        }

        var visual = store._ApprovedHeightVisual_k__BackingField;
        if (visual == null)
        {
            return;
        }

        var transform = visual.transform;
        var position = transform.localPosition;
        var moved = HeightRule.VisualHeight(position.y, before, after);
        transform.localPosition = new Vector3(position.x, moved, position.z);
        _log.Debug($"Approved height visual of {KeyOf(store)} moved from {position.y:0.###} to {moved:0.###} for approved height {before:0.##} -> {after:0.##}");
    }

    public string DescribeBoats()
    {
        var keys = new HashSet<string>(_prefabs.Values.Select(state => state.Key), StringComparer.Ordinal);
        var lines = new List<string>();
        foreach (var store in UnityEngine.Object.FindObjectsOfType<EntityInteractableStore>())
        {
            if (store == null || !keys.Contains(KeyOf(store)))
            {
                continue;
            }

            var visual = store._ApprovedHeightVisual_k__BackingField;
            lines.Add($"{KeyOf(store)}: approved {store._StorageMaximumApprovedHeight_k__BackingField:0.##}, maximum {store._StorageMaximumHeight_k__BackingField:0.##}, " +
                      $"current {store.CurrentHeight:0.##}, approved now {store.IsHeightApproved}, " +
                      (visual == null ? "no approved height visual" : $"visual {(visual.activeInHierarchy ? "shown" : "hidden")} at local height {visual.transform.localPosition.y:0.##}"));
        }

        var role = CatNetwork.Role;
        _log.Info($"Boats in the scene as {role}: {(lines.Count == 0 ? "none" : string.Join("; ", lines))}; scales approved x{ApprovedScale:0.##}, maximum x{MaximumScale:0.##}{(IsActive ? string.Empty : ", paused in this session")}");
        return lines.Count == 0 ? "no boat in the scene" : $"{lines.Count} boat(s), see the log";
    }

    private void Apply(IReadOnlyList<Pool> pools)
    {
        var mode = EffectiveMode;
        var allowed = VariantList.Parse(Settings.AllowedVariants.Value);
        var clearDeck = CurrentPlan.ClearsGameDeck;

        foreach (var pool in pools)
        {
            var last = _lastVariant.TryGetValue(pool.Key, out var index) ? index : -1;
            var plan = LayoutPlanner.Choose(mode, pool.Prefabs.Count, allowed, Settings.FixedVariant.Value, last, Settings.AvoidRepeats.Value, _random);
            _plans[pool.Key] = plan;
            var fitting = FittingPrefabs(pool);
            pool.Data._DelivererStores_k__BackingField = plan.Kind == LayoutPlanKind.Variant
                ? new Il2CppReferenceArray<EntityInteractableStore>(new[] { pool.Prefabs[plan.VariantIndex] })
                : fitting != null
                    ? new Il2CppReferenceArray<EntityInteractableStore>(fitting.ToArray())
                    : pool.OriginalArray;

            foreach (var prefab in pool.Prefabs)
            {
                var state = _prefabs[prefab.Pointer];
                ApplyHeight(prefab, state);
                state.SetDeckCleared(clearDeck);
            }
        }

        var summary = $"Boat: mode {mode}, next deck {CurrentPlan.Describe()}, {pools.Count} pool(s), {_prefabs.Count} boat storage prefab(s), " +
                      $"{_prefabs.Values.Sum(state => state.BlockerCount)} blocker(s) and {_prefabs.Values.Sum(state => state.PropCount)} prop(s) {(clearDeck ? "hidden" : "shown")}, " +
                      $"height x{ApprovedScale:0.##} approved, x{MaximumScale:0.##} maximum{(IsActive ? string.Empty : ", paused in this session")}" +
                      (EntityObjects > 0 ? $", {EntityObjects} object(s) with game entities left untouched" : string.Empty);
        var plans = string.Join(", ", pools.Where(pool => _plans[pool.Key].Kind != LayoutPlanKind.KeepGame).Take(3).Select(pool => pool.Key + ": " + _plans[pool.Key]));
        if (summary != _lastSummary)
        {
            _lastSummary = summary;
            _log.Info(summary);
        }

        if (plans.Length > 0)
        {
            _log.Debug("Planned boat variants: " + plans);
        }
    }

    private List<EntityInteractableStore> FittingPrefabs(Pool pool)
    {
        var plan = CurrentPlan;
        if (plan.Kind != DeckPlanKind.Pattern)
        {
            return null;
        }

        var arriving = pool.Prefabs.Select(prefab => (ISet<(int Row, int Column)>)_prefabs[prefab.Pointer].Arriving).ToList();
        var closest = LayoutFit.ClosestVariants(plan.Pattern, arriving);
        if (closest.FreedCells > 0)
        {
            _log.Debug($"Layout \"{plan.Name}\" meets arriving parcels on every boat of pool {pool.Key}; the closest one leaves {closest.FreedCells} cell(s) free");
        }

        if (closest.Variants.Count == 0 || closest.Variants.Count == pool.Prefabs.Count)
        {
            return null;
        }

        return closest.Variants.Select(index => pool.Prefabs[index]).ToList();
    }

    private void ApplyHeight(EntityInteractableStore store, PrefabState state)
    {
        if (state == null)
        {
            return;
        }

        var (approved, maximum) = HeightRule.Apply(state.Approved, state.Maximum, ApprovedScale, MaximumScale);
        store._StorageMaximumApprovedHeight_k__BackingField = approved;
        store._StorageMaximumHeight_k__BackingField = maximum;
    }

    private void OnBoatArrived(EntityInteractableStore store, IReadOnlyList<Pool> pools)
    {
        var key = KeyOf(store);
        foreach (var pool in pools)
        {
            var position = pool.Prefabs.FindIndex(prefab => KeyOf(prefab) == key);
            if (position < 0)
            {
                continue;
            }

            var plan = _plans.TryGetValue(pool.Key, out var planned) ? planned : LayoutPlan.KeepGame;
            _lastVariant[pool.Key] = position;
            _log.Info($"Boat arrived: {key}, variant {position + 1} of pool {pool.Key}, planned {plan}, deck {CurrentPlan.Describe()}, " +
                      $"approved height {store._StorageMaximumApprovedHeight_k__BackingField:0.##}, maximum {store._StorageMaximumHeight_k__BackingField:0.##}, {DescribeVisual(store)}");
            StartBuild(store);
            return;
        }

        _log.Warning($"Boat arrived with {key}, which is not in any known boat pool");
        StartBuild(store);
    }

    private void StartBuild(EntityInteractableStore store)
    {
        if (IsAuthority)
        {
            _decideAt = FrameLoop.Realtime + DecisionDelaySeconds;
        }

        if (!CurrentPlan.BuildsDeck)
        {
            return;
        }

        _pendingStore = store;
        _pendingPlan = CurrentPlan;
        _pendingUntil = FrameLoop.Realtime + BuildTimeoutSeconds;
    }

    private void BuildPending()
    {
        if (_pendingStore == null)
        {
            return;
        }

        if (_pendingStore.WasCollected || _pendingStore == null)
        {
            _pendingStore = null;
            return;
        }

        var result = _builder.TryBuild(_pendingStore, KeyOf(_pendingStore), _pendingPlan, Settings.Decorate.Value ? DecorTemplates() : new DeckDecorTemplates(), FallbackBlocker(), BlockerLayer(), out var taken);
        if (result == DeckBuildResult.Done && taken != null && taken.Count > 0)
        {
            _enforcer.Hold(_pendingStore, KeyOf(_pendingStore), taken);
        }

        if (result == DeckBuildResult.Done && _pendingPlan.Kind == DeckPlanKind.Pattern && taken != null)
        {
            ReportFreed(_pendingPlan, KeyOf(_pendingStore), _pendingPlan.Pattern.BlockedCount - taken.Count);
        }

        if (result != DeckBuildResult.NotReady)
        {
            _pendingStore = null;
        }
        else if (FrameLoop.Realtime > _pendingUntil)
        {
            _log.Warning($"The deck of {KeyOf(_pendingStore)} was not ready within {BuildTimeoutSeconds} seconds; it stays empty");
            _pendingStore = null;
        }
    }

    private void ReportFreed(DeckPlan plan, string key, int freed)
    {
        if (freed <= 0 || !_freedReported.Add(plan.Name + "|" + key))
        {
            return;
        }

        _log.Info($"Layout \"{plan.Name}\" on {key}: {freed} cell(s) stay free under arriving parcels");
        Notifications.Show(_texts.Format("message.layoutFreed", freed));
    }

    private void Decide()
    {
        _needDecision = false;
        _decideAt = double.PositiveInfinity;
        if (!IsAuthority)
        {
            return;
        }

        var choices = Settings.Choices();
        var needsLayouts = choices.Mode == LayoutMode.Custom || choices.Mode == LayoutMode.Mixed;
        var layouts = needsLayouts ? _library.Load(warning => _log.Warning("Own layout " + warning)) : Array.Empty<(string, DeckPattern)>();
        var decision = DeckDecider.Decide(choices, layouts, _lastLayout, _sequence, _random);
        _sequence = decision.NextSequence;
        if (decision.Note != null && decision.Note != _lastNote)
        {
            _log.Warning("Next deck: " + decision.Note);
        }

        _lastNote = decision.Note;
        if (decision.Plan.Kind == DeckPlanKind.Pattern)
        {
            _lastLayout = decision.Plan.Name;
        }

        var encoded = decision.Plan.Encode();
        if (encoded != Settings.Plan.Value)
        {
            Settings.Plan.LocalValue = encoded;
            _log.Info("Next deck: " + decision.Plan.Describe());
        }
    }

    private void HandleHotkey()
    {
        if (!Settings.SaveHotkey.Value.IsDown())
        {
            return;
        }

        var manager = Singleton<ParcelDeliveryManager>.HasInstance() ? Singleton<ParcelDeliveryManager>.Instance : null;
        var store = manager == null ? null : CurrentStore(manager);
        if (store == null)
        {
            Notifications.Show(_texts.Get("message.noBoat"));
            return;
        }

        if (!Il2CppArrays.TryRead2D<bool>(store._availableCells, out var available))
        {
            Notifications.Show(_texts.Get("message.noGrid"));
            return;
        }

        var layout = store._PrefilledStorageTextAsset_k__BackingField;
        var pattern = DeckPattern.FromAvailability(available, PrefillFootprint.Parse(layout == null ? null : layout.text), out var excluded);
        var name = _library.Save(pattern, new[] { "Boat Tweaks layout", "Saved from " + KeyOf(store) }, DateTime.Now);
        _log.Info($"Deck of {KeyOf(store)} saved as {name} with {pattern.BlockedCount} taken cell(s), {excluded} cell(s) of arriving parcels left out, in {_library.Directory}");
        Notifications.Show(_texts.Format("message.saved", name));
    }

    private static EntityInteractableStore CurrentStore(ParcelDeliveryManager manager)
    {
        var current = manager.CurrentDelivererStore;
        if (current != null)
        {
            return current;
        }

        var deliverer = manager.Deliverer;
        return deliverer == null ? null : deliverer._storage;
    }

    private int BlockerLayer()
    {
        if (_blockerLayer == -2)
        {
            _blockerLayer = LayerMask.NameToLayer(DeckDecor.BlockerLayerName);
        }

        return _blockerLayer;
    }

    private DeckDecorTemplates DecorTemplates()
    {
        var templates = new DeckDecorTemplates();
        var props = _prefabs.Values
            .SelectMany(state => state.Props.Select(prop => (prop.Object, Cell: CellSize(state.Prefab))))
            .Where(prop => prop.Object != null)
            .GroupBy(prop => prop.Object.name.Split(' ')[0], StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => group.First());

        foreach (var (prop, cell) in props)
        {
            var label = prop.name.Split(' ')[0];
            var footprint = DeckDecor.ByName(prop.name);
            var size = string.Empty;
            if (DeckBuilder.TryMeasure(prop, out var extent))
            {
                var measured = DeckDecor.Footprint(extent.x, extent.z, cell);
                var flat = DeckDecor.IsFlat(extent.y, cell);
                footprint = flat ? null : measured;
                size = $" {measured.Short}x{measured.Long} h{extent.y:0.##}{(flat ? " flat" : string.Empty)}";
            }

            if (footprint == null || DeckDecor.IsExcluded(prop.name) || !DeckPieces.IsKnown(footprint.Value))
            {
                templates.Unused.Add(label + size);
                continue;
            }

            templates.Add(prop, footprint.Value, label + size);
        }

        var catalog = templates.Describe();
        if (catalog != _lastCatalog)
        {
            _lastCatalog = catalog;
            _log.Info("Decoration of own and generated decks: " + catalog);
        }

        return templates;
    }

    private static float CellSize(EntityInteractableStore prefab)
    {
        var cell = prefab == null ? 0f : prefab._Padding_k__BackingField * 2f;
        return cell > 0f ? cell : DeckBuilder.DefaultCellSize;
    }

    private int EntityObjects => _prefabs.Values.Sum(state => state.EntityObjects);

    private GameObject FallbackBlocker() =>
        _prefabs.Values.SelectMany(state => state.Blockers).Select(blocker => blocker.Object).FirstOrDefault(blocker => blocker != null);

    private List<Pool> CollectPools(ParcelDeliveryManager manager)
    {
        var pools = new List<Pool>();
        var perPlayers = manager.DelivererStoresPerProgressionLevelsPerPlayerCount;
        for (var players = 0; perPlayers != null && players < perPlayers.Length; players++)
        {
            var levels = perPlayers[players]?.DelivererStoreDataPerProgressionLevels;
            for (var level = 0; levels != null && level < levels.Length; level++)
            {
                AddPool(pools, $"players{players + 1}/level{level + 1}", levels[level]);
            }
        }

        AddPool(pools, FallbackPoolKey, manager.DelivererStoresFallback);
        return pools;
    }

    private void AddPool(List<Pool> pools, string key, DelivererStoresData data)
    {
        if (data == null)
        {
            return;
        }

        if (!_pools.TryGetValue(data.Pointer, out var pool))
        {
            var original = data._DelivererStores_k__BackingField;
            if (original == null || original.Length == 0)
            {
                return;
            }

            var prefabs = new List<EntityInteractableStore>();
            foreach (var store in original)
            {
                if (store != null)
                {
                    prefabs.Add(store);
                }
            }

            pool = new Pool(key, data, original, prefabs);
            _pools[data.Pointer] = pool;
            foreach (var prefab in prefabs)
            {
                Remember(prefab);
            }
        }

        if (pool.Prefabs.Count > 0)
        {
            pools.Add(pool);
        }
    }

    private void Prune(IReadOnlyList<Pool> pools)
    {
        var live = new HashSet<IntPtr>(pools.SelectMany(pool => pool.Prefabs).Select(prefab => prefab.Pointer));
        var stale = _prefabs.Where(pair => !live.Contains(pair.Key)).ToList();
        foreach (var (pointer, state) in stale)
        {
            state.Restore();
            _prefabs.Remove(pointer);
        }

        if (stale.Count > 0)
        {
            _log.Debug($"Forgot {stale.Count} boat storage prefab(s) that the new level no longer uses");
        }
    }

    private void Remember(EntityInteractableStore prefab)
    {
        if (_prefabs.ContainsKey(prefab.Pointer))
        {
            return;
        }

        BlockerLayer();
        var layout = prefab._PrefilledStorageTextAsset_k__BackingField;
        var state = new PrefabState(prefab, KeyOf(prefab), prefab._StorageMaximumApprovedHeight_k__BackingField, prefab._StorageMaximumHeight_k__BackingField,
            PrefillFootprint.Parse(layout == null ? null : layout.text));
        foreach (var transform in prefab.GetComponentsInChildren<Transform>(true))
        {
            var gameObject = transform.gameObject;
            var parentName = transform.parent == null ? null : transform.parent.name;
            var isBlocker = _blockerLayer >= 0 && gameObject.layer == _blockerLayer;
            var isProp = !isBlocker && DeckDecor.IsProp(parentName, gameObject.name);
            if (!isBlocker && !isProp)
            {
                continue;
            }

            if (gameObject.GetComponentsInChildren<Entity>(true).Length > 0)
            {
                state.EntityObjects++;
                continue;
            }

            (isBlocker ? state.Blockers : state.Props).Add((gameObject, gameObject.activeSelf));
        }

        _prefabs[prefab.Pointer] = state;
    }

    private static string KeyOf(EntityInteractableStore store)
    {
        var entity = store.GetComponent<Entity>();
        return entity == null ? store.name : entity.AddressableKey;
    }

    private sealed record Pool(string Key, DelivererStoresData Data, Il2CppReferenceArray<EntityInteractableStore> OriginalArray, List<EntityInteractableStore> Prefabs);

    private sealed class PrefabState
    {
        public PrefabState(EntityInteractableStore prefab, string key, float approved, float maximum, HashSet<(int Row, int Column)> arriving)
        {
            Arriving = arriving;
            Prefab = prefab;
            Key = key;
            Approved = approved;
            Maximum = maximum;
        }

        public EntityInteractableStore Prefab { get; }

        public HashSet<(int Row, int Column)> Arriving { get; }

        public string Key { get; }

        public float Approved { get; }

        public float Maximum { get; }

        public List<(GameObject Object, bool WasActive)> Blockers { get; } = new();

        public List<(GameObject Object, bool WasActive)> Props { get; } = new();

        public int BlockerCount => Blockers.Count;

        public int PropCount => Props.Count;

        public int EntityObjects { get; set; }

        public void Restore()
        {
            if (Prefab == null || Prefab.WasCollected)
            {
                return;
            }

            Prefab._StorageMaximumApprovedHeight_k__BackingField = Approved;
            Prefab._StorageMaximumHeight_k__BackingField = Maximum;
            SetDeckCleared(false);
        }

        public void SetDeckCleared(bool cleared)
        {
            foreach (var (gameObject, wasActive) in Blockers.Concat(Props))
            {
                if (gameObject == null)
                {
                    continue;
                }

                var active = !cleared && wasActive;
                if (gameObject.activeSelf != active)
                {
                    gameObject.SetActive(active);
                }
            }
        }
    }
}
