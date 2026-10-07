using System;
using System.Collections.Generic;
using CatLib.Localization;
using CatLib.Logging;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace CatLib.Game;

public static class CatParcels
{
    public static readonly IReadOnlyList<ParcelRegion> Regions = new[]
    {
        ParcelRegion.CatsIsland, ParcelRegion.FoggyMountains, ParcelRegion.SunnyShores, ParcelRegion.PortWindy,
        ParcelRegion.Hazelton, ParcelRegion.CrescentBay, ParcelRegion.TropicalDunes
    };

    public static readonly IReadOnlyList<StorageConstraint> StorageConstraints = new[]
    {
        StorageConstraint.Frozen, StorageConstraint.Cold, StorageConstraint.Hot, StorageConstraint.Dark, StorageConstraint.Bright
    };

    public static readonly IReadOnlyList<BehaviorConstraint> BehaviorConstraints = new[]
    {
        BehaviorConstraint.Fragile, BehaviorConstraint.Heavy, BehaviorConstraint.ContactForbidden, BehaviorConstraint.Lover, BehaviorConstraint.Corrupted
    };

    private static readonly Dictionary<string, Sprite> Sprites = new(StringComparer.Ordinal);
    private static CatLogger _log;
    private static int _failures;

    public static bool IsAvailable => Singleton<ParcelManager>.HasInstance();

    internal static void Initialize(CatLogger log) => _log = log;

    public static List<ParcelInfo> Read()
    {
        var result = new List<ParcelInfo>();
        if (!IsAvailable)
        {
            return result;
        }

        var stamps = Singleton<EntityPropertiesManager>.HasInstance() ? Singleton<EntityPropertiesManager>.Instance : null;
        if (!IsHost())
        {
            var found = UnityEngine.Object.FindObjectsByType(Il2CppType.Of<EntityParcel>(), FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (found != null)
            {
                foreach (var item in found)
                {
                    Add(result, item?.TryCast<EntityParcel>(), stamps);
                }
            }

            return result;
        }

        var parcels = Singleton<ParcelManager>.Instance.GetAllParcels();
        if (parcels == null)
        {
            return result;
        }

        for (var index = 0; index < parcels.Count; index++)
        {
            Add(result, parcels[index], stamps);
        }

        return result;
    }

    private static bool IsHost()
    {
        var network = Singleton<NetworkManager>.HasInstance() ? Singleton<NetworkManager>.Instance : null;
        return network == null || network.IsServer;
    }

    private static void Add(List<ParcelInfo> result, EntityParcel parcel, EntityPropertiesManager stamps)
    {
        if (parcel == null || parcel.WasCollected || parcel.IsDisposed)
        {
            return;
        }

        try
        {
            var info = Describe(parcel, stamps);
            if (info != null)
            {
                result.Add(info);
            }
        }
        catch (Exception exception)
        {
            if (_failures++ < 5)
            {
                _log?.Warning($"Reading a parcel failed: {exception.Message}");
            }
        }
    }

    public static ParcelInfo Describe(Entity parcel) =>
        Describe(parcel, Singleton<EntityPropertiesManager>.HasInstance() ? Singleton<EntityPropertiesManager>.Instance : null);

    public static string RegionName(ParcelRegion region)
    {
        try
        {
            if (Singleton<EntityPropertiesManager>.HasInstance())
            {
                var term = Singleton<EntityPropertiesManager>.Instance.GetRegionTerm(region);
                var text = string.IsNullOrEmpty(term) ? null : CatLanguage.Game(term);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }
        }
        catch (Exception)
        {
        }

        return region.ToString();
    }

    public static Sprite RegionIcon(ParcelRegion region) =>
        CachedSprite("region." + region, manager => manager.GetPackageRegionStamp(region));

    public static Sprite ConstraintIcon(StorageConstraint constraint) =>
        CachedSprite("storage." + constraint, manager => manager.GetStorageConstraintSettings(constraint)?.Stamp);

    public static Sprite ConstraintIcon(BehaviorConstraint constraint) =>
        CachedSprite("behavior." + constraint, manager => manager.GetBehaviorConstraintSettings(constraint)?.Stamp);

    private static ParcelInfo Describe(Entity parcel, EntityPropertiesManager stamps)
    {
        var properties = parcel.Properties;
        if (properties == null)
        {
            return null;
        }

        var region = properties.Region;
        var interactable = parcel.Interactable;
        return new ParcelInfo(
            parcel.Network == null ? 0u : parcel.Network.NetworkIdentifier,
            region,
            properties.StorageConstraint,
            properties.BehaviorConstraint,
            properties.PackageSize,
            properties.WeightClass,
            properties.IsDamaged,
            MissingStamps(properties, interactable == null ? null : interactable.Stamp, stamps),
            PlaceOf(interactable),
            FootprintOf(interactable));
    }

    private static ParcelFootprint FootprintOf(EntityInteractable interactable)
    {
        var pickable = interactable == null ? null : interactable.Pickable;
        if (pickable == null)
        {
            return default;
        }

        var size = pickable.Size;
        return new ParcelFootprint(size.x, size.y);
    }

    private static ParcelStamps MissingStamps(EntityProperties properties, EntityInteractableStamp stamp, EntityPropertiesManager stamps)
    {
        var region = properties.Region;
        if (stamp == null || region == ParcelRegion.None || region == ParcelInfo.HomeRegion)
        {
            return ParcelStamps.None;
        }

        var destinations = new HashSet<ParcelRegion>();
        var placed = new HashSet<IntPtr>();
        var storage = StorageConstraint.None;
        var behavior = BehaviorConstraint.None;
        var weights = 0;
        var instances = stamp.StampInstances;
        for (var index = 0; instances != null && index < instances.Length; index++)
        {
            var instance = instances[index];
            var data = instance == null || instance.IsBeingRemoved ? null : instance.StampData;
            if (data == null)
            {
                continue;
            }

            placed.Add(data.Pointer);
            var destination = data.TryCast<StampDataDestination>();
            if (destination != null)
            {
                destinations.Add(destination.Destination);
                continue;
            }

            if (data.TryCast<StampDataWeight>() != null)
            {
                weights++;
                continue;
            }

            var storageStamp = data.TryCast<StampDataStorageConstraint>();
            if (storageStamp != null)
            {
                storage |= storageStamp.Constraint;
                continue;
            }

            var behaviorStamp = data.TryCast<StampDataBehaviorConstraint>();
            if (behaviorStamp != null)
            {
                behavior |= behaviorStamp.Constraint;
            }
        }

        var missing = ParcelStamps.None;
        if (!destinations.Contains(region))
        {
            missing |= ParcelStamps.Destination;
        }

        if (properties.StorageConstraint != StorageConstraint.None && (storage & properties.StorageConstraint) != properties.StorageConstraint)
        {
            missing |= ParcelStamps.Storage;
        }

        if (properties.BehaviorConstraint != BehaviorConstraint.None && (behavior & properties.BehaviorConstraint) != properties.BehaviorConstraint && HasStamp(stamps, properties))
        {
            missing |= ParcelStamps.Behavior;
        }

        if (weights < RequiredWeightStamps(stamps, properties.WeightClass))
        {
            missing |= ParcelStamps.Weight;
        }

        return missing;
    }

    public static int RequiredWeightStamps(ParcelWeightClass weight)
    {
        var manager = Singleton<EntityPropertiesManager>.HasInstance() ? Singleton<EntityPropertiesManager>.Instance : null;
        return RequiredWeightStamps(manager, weight);
    }

    private static int RequiredWeightStamps(EntityPropertiesManager manager, ParcelWeightClass weight)
    {
        var required = manager == null ? null : manager.StampAmountRequiredByWeightClass;
        var index = (int)weight;
        return required == null || index < 0 || index >= required.Length ? 0 : required[index];
    }

    private static bool HasStamp(EntityPropertiesManager manager, EntityProperties properties)
    {
        try
        {
            var settings = manager == null ? null : manager.GetBehaviorConstraintSettings(properties.BehaviorConstraint);
            return settings != null && settings.Stamp != null;
        }
        catch (Exception)
        {
            return true;
        }
    }

    private static ParcelPlace PlaceOf(EntityInteractable interactable)
    {
        var pickable = interactable == null ? null : interactable.Pickable;
        if (pickable != null && pickable.IsInInventory)
        {
            return ParcelPlace.Carried;
        }

        var store = interactable == null ? null : interactable.Store;
        var parent = store == null ? null : store.ParentStore;
        if (parent == null)
        {
            return pickable != null && pickable.IsStored ? ParcelPlace.Stored : ParcelPlace.Loose;
        }

        var root = parent;
        for (var depth = 0; depth < 32 && root.ParentStore != null; depth++)
        {
            root = root.ParentStore;
        }

        return root.StorageType switch
        {
            StorageType.Boat => ParcelPlace.Boat,
            StorageType.Customer => ParcelPlace.Customer,
            _ => ParcelPlace.Stored
        };
    }

    private static Sprite CachedSprite(string key, Func<EntityPropertiesManager, StampData> find)
    {
        if (Sprites.TryGetValue(key, out var cached) && cached != null && !cached.WasCollected)
        {
            return cached;
        }

        if (!Singleton<EntityPropertiesManager>.HasInstance())
        {
            return null;
        }

        try
        {
            var data = find(Singleton<EntityPropertiesManager>.Instance);
            var sprite = data == null ? null : data.StampPreviewSprite;
            if (sprite != null)
            {
                Sprites[key] = sprite;
            }

            return sprite;
        }
        catch (Exception exception)
        {
            _log?.Debug($"No icon for {key}: {exception.Message}");
            return null;
        }
    }
}
