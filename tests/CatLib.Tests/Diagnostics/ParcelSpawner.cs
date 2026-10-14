using System;
using System.Collections.Generic;
using System.Linq;
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

    private readonly CatLogger _log;
    private readonly System.Random _random = new();

    public ParcelSpawner(CatLogger log)
    {
        _log = log;
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

        var prefab = prefabs[_random.Next(prefabs.Count)];
        var view = camera.transform;
        var forward = view.forward;
        var position = view.position + forward * SpawnDistance(WallDistance(view.position, forward));
        var flat = new Vector3(forward.x, 0f, forward.z);
        var rotation = flat.sqrMagnitude < 0.0001f ? Quaternion.identity : Quaternion.LookRotation(flat.normalized, Vector3.up);
        var request = Singleton<NetworkSpawnerManager>.Instance.RequestSpawn(prefab, position, rotation, Vector3.zero, string.Empty);
        _log.Info($"Requested a {label} parcel {prefab.name} ({prefab.AddressableKey}) at {position}, request {request}, {prefabs.Count} prefab(s) of this size");
        return $"{prefab.name} in front of you";
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
