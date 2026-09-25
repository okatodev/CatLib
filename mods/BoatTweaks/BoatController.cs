using System;
using System.Collections.Generic;
using System.Linq;
using CatLib.Core;
using CatLib.Logging;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace BoatTweaks;

public sealed class BoatController
{
    public const double ScanIntervalSeconds = 1;
    public const string FallbackPoolKey = "fallback";

    private readonly CatLogger _log;
    private readonly System.Random _random = new();
    private readonly Dictionary<IntPtr, Pool> _pools = new();
    private readonly Dictionary<IntPtr, PrefabState> _prefabs = new();
    private readonly Dictionary<string, int> _lastVariant = new(StringComparer.Ordinal);
    private readonly Dictionary<string, LayoutPlan> _plans = new(StringComparer.Ordinal);
    private readonly InstanceTracker _manager = new();
    private bool _pruneAfterCollect;
    private int _blockerLayer = -2;
    private IntPtr _lastBoatStore;
    private bool _dirty = true;
    private double _nextScan;
    private string _lastSummary;

    public BoatController(CatLogger log)
    {
        _log = log;
    }

    public BoatSettings Settings { get; set; }

    public IReadOnlyDictionary<string, LayoutPlan> Plans => _plans;

    public void RequestApply() => _dirty = true;

    public void Update()
    {
        if (Settings == null)
        {
            return;
        }

        try
        {
            TrackManager();
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
        if (manager == null)
        {
            return;
        }

        var current = manager.CurrentDelivererStore;
        if (current != null && Settings.Mode.Value != LayoutMode.Game)
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

        var current = manager.CurrentDelivererStore;
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
        if (current != null)
        {
            var state = pools.SelectMany(pool => pool.Prefabs).Select(prefab => _prefabs[prefab.Pointer]).FirstOrDefault(prefab => prefab.Key == KeyOf(current));
            ApplyHeight(current, state);
        }
    }

    private void Apply(IReadOnlyList<Pool> pools)
    {
        var mode = Settings.Mode.Value;
        var allowed = VariantList.Parse(Settings.AllowedVariants.Value);
        var clearDeck = mode == LayoutMode.Empty;

        foreach (var pool in pools)
        {
            var last = _lastVariant.TryGetValue(pool.Key, out var index) ? index : -1;
            var plan = LayoutPlanner.Choose(mode, pool.Prefabs.Count, allowed, Settings.FixedVariant.Value, last, Settings.AvoidRepeats.Value, _random);
            _plans[pool.Key] = plan;
            pool.Data._DelivererStores_k__BackingField = plan.Kind == LayoutPlanKind.Variant
                ? new Il2CppReferenceArray<EntityInteractableStore>(new[] { pool.Prefabs[plan.VariantIndex] })
                : pool.OriginalArray;

            foreach (var prefab in pool.Prefabs)
            {
                var state = _prefabs[prefab.Pointer];
                ApplyHeight(prefab, state);
                state.SetDeckCleared(clearDeck);
            }
        }

        var summary = $"Boat: mode {mode}, {pools.Count} pool(s), {_prefabs.Count} boat storage prefab(s), " +
                      $"{_prefabs.Values.Sum(state => state.BlockerCount)} blocker(s) and {_prefabs.Values.Sum(state => state.PropCount)} prop(s) {(clearDeck ? "hidden" : "shown")}, " +
                      $"height x{Settings.ApprovedHeightScale.Value:0.##} approved, x{Settings.MaximumHeightScale.Value:0.##} maximum";
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

    private void ApplyHeight(EntityInteractableStore store, PrefabState state)
    {
        if (state == null)
        {
            return;
        }

        var (approved, maximum) = HeightRule.Apply(state.Approved, state.Maximum, Settings.ApprovedHeightScale.Value, Settings.MaximumHeightScale.Value);
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
            _log.Info($"Boat arrived: {key}, variant {position + 1} of pool {pool.Key}, planned {plan}, " +
                      $"approved height {store._StorageMaximumApprovedHeight_k__BackingField:0.##}, maximum {store._StorageMaximumHeight_k__BackingField:0.##}");
            return;
        }

        _log.Warning($"Boat arrived with {key}, which is not in any known boat pool");
    }

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

        if (_blockerLayer == -2)
        {
            _blockerLayer = LayerMask.NameToLayer(DeckDecor.BlockerLayerName);
        }

        var state = new PrefabState(prefab, KeyOf(prefab), prefab._StorageMaximumApprovedHeight_k__BackingField, prefab._StorageMaximumHeight_k__BackingField);
        foreach (var transform in prefab.GetComponentsInChildren<Transform>(true))
        {
            var gameObject = transform.gameObject;
            var parentName = transform.parent == null ? null : transform.parent.name;
            if (_blockerLayer >= 0 && gameObject.layer == _blockerLayer)
            {
                state.Blockers.Add((gameObject, gameObject.activeSelf));
            }
            else if (DeckDecor.IsProp(parentName, gameObject.name))
            {
                state.Props.Add((gameObject, gameObject.activeSelf));
            }
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
        public PrefabState(EntityInteractableStore prefab, string key, float approved, float maximum)
        {
            Prefab = prefab;
            Key = key;
            Approved = approved;
            Maximum = maximum;
        }

        public EntityInteractableStore Prefab { get; }

        public string Key { get; }

        public float Approved { get; }

        public float Maximum { get; }

        public List<(GameObject Object, bool WasActive)> Blockers { get; } = new();

        public List<(GameObject Object, bool WasActive)> Props { get; } = new();

        public int BlockerCount => Blockers.Count;

        public int PropCount => Props.Count;

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
