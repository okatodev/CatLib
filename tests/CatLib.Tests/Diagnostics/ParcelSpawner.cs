using System;
using System.Collections.Generic;
using System.Linq;
using CatLib.Il2Cpp;
using CatLib.Logging;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace CatLib.Tests.Diagnostics;

public sealed class ParcelSpawner
{
    public const float Distance = 1.6f;
    public const float WallGap = 0.45f;
    public const float NearestDistance = 0.4f;

    public static readonly IReadOnlyList<(PackageSize Size, string Label)> Sizes = new[]
    {
        (PackageSize.Small, "Small"),
        (PackageSize.FlatLetter, "Flat letter"),
        (PackageSize.Flat, "Flat"),
        (PackageSize.LongFlat, "Long flat"),
        (PackageSize.Standard, "Standard"),
        (PackageSize.StandardLong, "Standard long"),
        (PackageSize.Medium, "Medium"),
        (PackageSize.Tall, "Tall"),
        (PackageSize.VeryLong, "Very long"),
        (PackageSize.OddCube, "Odd cube"),
        (PackageSize.Giant, "Giant"),
        (PackageSize.GiantPlus, "Giant plus")
    };

    public static readonly IReadOnlyList<(StorageConstraint? Value, string Text)> StorageChoices = new (StorageConstraint?, string)[]
    {
        (null, "random"),
        (StorageConstraint.None, "none"),
        (StorageConstraint.Cold, "cold"),
        (StorageConstraint.Hot, "hot"),
        (StorageConstraint.Dark, "dark"),
        (StorageConstraint.Bright, "bright"),
        (StorageConstraint.Frozen, "frozen")
    };

    public static readonly IReadOnlyList<(BehaviorConstraint? Value, string Text)> BehaviorChoices = new (BehaviorConstraint?, string)[]
    {
        (null, "random"),
        (BehaviorConstraint.None, "none"),
        (BehaviorConstraint.Fragile, "fragile"),
        (BehaviorConstraint.Heavy, "heavy"),
        (BehaviorConstraint.ContactForbidden, "no contact"),
        (BehaviorConstraint.Lover, "lover"),
        (BehaviorConstraint.Corrupted, "cursed")
    };

    public static readonly IReadOnlyList<(ParcelRegion? Value, string Text)> RegionChoices = new (ParcelRegion?, string)[]
    {
        (null, "random"),
        (ParcelRegion.CatsIsland, "Cats Island"),
        (ParcelRegion.FoggyMountains, "Foggy Mountains"),
        (ParcelRegion.SunnyShores, "Sunny Shores"),
        (ParcelRegion.PortWindy, "Port Windy"),
        (ParcelRegion.Hazelton, "Hazelton"),
        (ParcelRegion.CrescentBay, "Crescent Bay"),
        (ParcelRegion.TropicalDunes, "Tropical Dunes")
    };

    private readonly CatLogger _log;
    private readonly System.Random _random = new();
    private readonly Dictionary<uint, SpawnConditions> _pending = new();
    private Il2CppEventBindings _bindings;
    private IntPtr _boundManager;

    public ParcelSpawner(CatLogger log)
    {
        _log = log;
    }

    public StorageConstraint? Storage { get; set; }

    public BehaviorConstraint? Behavior { get; set; }

    public ParcelRegion? Region { get; set; }

    public SpawnConditions Conditions => new(Storage, Behavior, Region);

    public static string TextOf<T>(IReadOnlyList<(T Value, string Text)> choices, T value)
    {
        foreach (var (known, text) in choices)
        {
            if (EqualityComparer<T>.Default.Equals(known, value))
            {
                return text;
            }
        }

        return value?.ToString() ?? "random";
    }

    public static string LabelOf(PackageSize size)
    {
        foreach (var (known, label) in Sizes)
        {
            if (known == size)
            {
                return label;
            }
        }

        return size.ToString();
    }

    public static float SpawnDistance(float? wallDistance)
    {
        if (wallDistance == null || wallDistance.Value - WallGap >= Distance)
        {
            return Distance;
        }

        return Math.Max(NearestDistance, wallDistance.Value - WallGap);
    }

    public string Spawn(PackageSize size)
    {
        var problem = LevelProblem();
        if (problem != null)
        {
            return problem;
        }

        var label = LabelOf(size).ToLowerInvariant();
        var prefabs = PrefabsOf(size);
        if (prefabs.Count == 0)
        {
            _log.Info($"No {label} parcel to spawn, the game has {Describe(AllPrefabs())}");
            return $"the game has no {label} parcel to spawn, see the log";
        }

        var camera = FindCamera();
        if (camera == null)
        {
            return "no camera to spawn in front of";
        }

        var conditions = Conditions;
        var isHost = Singleton<NetworkManager>.Instance.IsServer;
        if (conditions.IsSet && !isHost)
        {
            return "only the host can choose the conditions, set them back to random";
        }

        var prefab = prefabs[_random.Next(prefabs.Count)];
        var view = camera.transform;
        var forward = view.forward;
        var position = view.position + forward * SpawnDistance(WallDistance(view.position, forward));
        var flat = new Vector3(forward.x, 0f, forward.z);
        var rotation = flat.sqrMagnitude < 0.0001f ? Quaternion.identity : Quaternion.LookRotation(flat.normalized, Vector3.up);
        var spawner = Singleton<NetworkSpawnerManager>.Instance;
        if (conditions.IsSet && !Bind(spawner))
        {
            return "the game's spawn event could not be followed, see the log";
        }

        var request = spawner.RequestSpawn(prefab, position, rotation, Vector3.zero, string.Empty);
        if (conditions.IsSet)
        {
            _pending[request] = conditions;
        }

        _log.Info($"Requested a {label} parcel {prefab.name} ({prefab.AddressableKey}) at {position}, request {request}, {prefabs.Count} prefab(s) of this size, conditions: {conditions}");
        return conditions.IsSet ? $"{prefab.name} in front of you, {conditions}" : $"{prefab.name} in front of you";
    }

    public string OpenGameSpawner()
    {
        var problem = LevelProblem();
        if (problem != null)
        {
            return problem;
        }

        var spawner = Singleton<InterfaceManager>.HasInstance() ? Singleton<InterfaceManager>.Instance.SpawnerInterface : null;
        if (spawner == null)
        {
            return "this build of the game has no spawner window";
        }

        if (Singleton<PauseManager>.HasInstance())
        {
            Singleton<PauseManager>.Instance.PauseGame();
        }

        spawner.Show();
        _log.Info("Opened the game's own spawner window");
        return "opened, close the developer menu to use it; Esc closes it";
    }

    private bool Bind(NetworkSpawnerManager spawner)
    {
        if (_bindings != null && _boundManager == spawner.Pointer)
        {
            return true;
        }

        _bindings?.Clear(_log);
        _pending.Clear();
        _bindings = new Il2CppEventBindings();
        _boundManager = spawner.Pointer;
        if (_bindings.Add<NetworkSpawnerManager.EntitySpawnedHandler>("NetworkSpawnerManager.EntitySpawned",
                new Action<ulong, uint, Entity, string>(OnEntitySpawned), spawner.add_EntitySpawned, spawner.remove_EntitySpawned))
        {
            return true;
        }

        _log.Warning("Following the game's spawn event failed: " + string.Join("; ", _bindings.Failures));
        _bindings = null;
        _boundManager = IntPtr.Zero;
        return false;
    }

    private void OnEntitySpawned(ulong client, uint request, Entity entity, string arguments)
    {
        try
        {
            if (!_pending.TryGetValue(request, out var conditions) || !Singleton<NetworkManager>.HasInstance() || client != Singleton<NetworkManager>.Instance.ClientId)
            {
                return;
            }

            _pending.Remove(request);
            var properties = entity == null ? null : entity.Properties;
            if (properties == null && entity != null)
            {
                properties = entity.GetComponent<EntityProperties>();
            }

            if (properties == null)
            {
                _log.Warning($"Spawn request {request} gave no parcel, its conditions were not set");
                return;
            }

            properties.InitializeFromStorage(conditions.Storage ?? StorageConstraint.None, conditions.Behavior ?? BehaviorConstraint.None,
                StorageType.Other, conditions.Region ?? ParcelRegion.None, true);
            if (conditions.Storage == null)
            {
                properties._isStorageConstraintSet = false;
            }

            if (conditions.Behavior == null)
            {
                properties._isBehaviorConstraintSet = false;
            }

            _log.Info($"Spawn request {request}: {entity.name} gets {conditions}");
        }
        catch (Exception exception)
        {
            _log.Error($"Setting the conditions of spawn request {request} failed", exception);
        }
    }

    private static string LevelProblem()
    {
        if (!Singleton<NetworkManager>.HasInstance() || !Singleton<NetworkSpawnerManager>.HasInstance())
        {
            return "only in a level";
        }

        var boot = Singleton<BootstrapManager>.HasInstance() ? Singleton<BootstrapManager>.Instance : null;
        if (boot == null || !boot.LoadFinalized || !Singleton<GameManager>.HasInstance() || !Singleton<GameManager>.Instance.IsGameInitialized)
        {
            return "only in a level";
        }

        return null;
    }

    private static List<EntityParcel> PrefabsOf(PackageSize size)
    {
        return AllPrefabs().Where(item => item.Size == size).Select(item => item.Prefab).ToList();
    }

    private static List<(EntityParcel Prefab, PackageSize Size)> AllPrefabs()
    {
        var found = new List<(EntityParcel Prefab, PackageSize Size)>();
        var seen = new HashSet<IntPtr>();
        var groups = Singleton<GameManager>.Instance.SpawnableParcels;
        if (groups != null)
        {
            foreach (var group in groups)
            {
                if (group?.Parcels == null)
                {
                    continue;
                }

                foreach (var parcel in group.Parcels)
                {
                    Add(found, seen, parcel);
                }
            }
        }

        foreach (var item in Resources.FindObjectsOfTypeAll(Il2CppType.Of<EntityParcel>()))
        {
            var parcel = item.TryCast<EntityParcel>();
            if (parcel != null && !parcel.gameObject.scene.IsValid() && !string.IsNullOrEmpty(parcel.AddressableKey))
            {
                Add(found, seen, parcel);
            }
        }

        return found;
    }

    private static void Add(List<(EntityParcel Prefab, PackageSize Size)> found, HashSet<IntPtr> seen, EntityParcel parcel)
    {
        if (parcel == null || !seen.Add(parcel.Pointer))
        {
            return;
        }

        var properties = parcel.GetComponent<EntityProperties>();
        if (properties == null)
        {
            properties = parcel.GetComponentInChildren<EntityProperties>(true);
        }

        if (properties != null)
        {
            found.Add((parcel, properties.PackageSize));
        }
    }

    private static string Describe(List<(EntityParcel Prefab, PackageSize Size)> prefabs)
    {
        if (prefabs.Count == 0)
        {
            return "no parcel prefab loaded";
        }

        return string.Join(", ", prefabs.GroupBy(item => item.Size).OrderBy(group => (int)group.Key)
            .Select(group => $"{LabelOf(group.Key).ToLowerInvariant()} {group.Count()}"));
    }

    private static float? WallDistance(Vector3 origin, Vector3 direction)
    {
        float? nearest = null;
        foreach (var hit in Physics.RaycastAll(origin, direction, Distance + WallGap, Physics.AllLayers, QueryTriggerInteraction.Ignore))
        {
            var collider = hit.collider;
            if (collider == null || collider.GetComponentInParent<PlayerEntityBase>() != null)
            {
                continue;
            }

            if (nearest == null || hit.distance < nearest.Value)
            {
                nearest = hit.distance;
            }
        }

        return nearest;
    }

    private static Camera FindCamera()
    {
        var main = Camera.main;
        if (main != null)
        {
            return main;
        }

        Camera best = null;
        foreach (var camera in Camera.allCameras)
        {
            if (camera != null && camera.isActiveAndEnabled && camera.targetTexture == null && (best == null || camera.depth > best.depth))
            {
                best = camera;
            }
        }

        return best;
    }
}

public readonly struct SpawnConditions
{
    public SpawnConditions(StorageConstraint? storage, BehaviorConstraint? behavior, ParcelRegion? region)
    {
        Storage = storage;
        Behavior = behavior;
        Region = region;
    }

    public StorageConstraint? Storage { get; }

    public BehaviorConstraint? Behavior { get; }

    public ParcelRegion? Region { get; }

    public bool IsSet => Storage != null || Behavior != null || Region != null;

    public override string ToString()
    {
        if (!IsSet)
        {
            return "chosen by the game";
        }

        var parts = new List<string>();
        if (Storage != null)
        {
            parts.Add(Storage == StorageConstraint.None ? "no storage condition" : ParcelSpawner.TextOf(ParcelSpawner.StorageChoices, Storage));
        }

        if (Behavior != null)
        {
            parts.Add(Behavior == BehaviorConstraint.None ? "no behavior condition" : ParcelSpawner.TextOf(ParcelSpawner.BehaviorChoices, Behavior));
        }

        if (Region != null)
        {
            parts.Add("to " + ParcelSpawner.TextOf(ParcelSpawner.RegionChoices, Region));
        }

        return string.Join(", ", parts);
    }
}
