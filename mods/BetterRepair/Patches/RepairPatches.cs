using System;
using System.Collections.Generic;
using CatLib.Logging;
using CatLib.Patching;

namespace BetterRepair.Patches;

public static class RepairPatches
{
    private static BetterRepairController _controller;
    private static IntPtr _store;
    private static List<Entity> _damaged;
    private static int _parcels;
    private static int _indexBefore;
    private static bool _taken;

    public static CatPatches Group { get; private set; }

    public static CatPatches Install(string ownerId, BetterRepairController controller, CatLogger log)
    {
        _controller = controller;
        Group = new CatPatches(ownerId, log)
            .Prefix(typeof(EntityRepairWorkstation), nameof(EntityRepairWorkstation.LinkedStore_OnEntityStoreChanged), Type.EmptyTypes,
                typeof(RepairPatches), nameof(StoreChangedPrefix))
            .Postfix(typeof(EntityRepairWorkstation), nameof(EntityRepairWorkstation.LinkedStore_OnEntityStoreChanged), Type.EmptyTypes,
                typeof(RepairPatches), nameof(LeavePostfix))
            .Prefix(typeof(EntityRepairWorkstation), nameof(EntityRepairWorkstation.RepairParcel), Type.EmptyTypes,
                typeof(RepairPatches), nameof(RepairPrefix))
            .Postfix(typeof(EntityRepairWorkstation), nameof(EntityRepairWorkstation.RepairParcel), Type.EmptyTypes,
                typeof(RepairPatches), nameof(RepairPostfix))
            .Prefix(typeof(EntityInteractableStore), nameof(EntityInteractableStore.CountChildParcels), Type.EmptyTypes,
                typeof(RepairPatches), nameof(CountPrefix))
            .Prefix(typeof(EntityInteractableStore), nameof(EntityInteractableStore.GetFirstEntity), Type.EmptyTypes,
                typeof(RepairPatches), nameof(FirstPrefix));
        Group.Apply();
        return Group;
    }

    private static void StoreChangedPrefix(EntityRepairWorkstation __instance) => Enter(__instance, nameof(StoreChangedPrefix));

    private static void LeavePostfix() => Leave();

    private static void RepairPrefix(EntityRepairWorkstation __instance)
    {
        Enter(__instance, nameof(RepairPrefix));
        _indexBefore = __instance == null ? 0 : __instance._currentRepairIndex;
    }

    private static void RepairPostfix(EntityRepairWorkstation __instance)
    {
        try
        {
            if (Group != null && Group.IsActive && _taken && __instance != null && __instance._currentRepairIndex > _indexBefore)
            {
                _controller.OnBatchRepaired(__instance, _damaged, _parcels);
            }
        }
        catch (Exception exception)
        {
            Group?.Fault(nameof(RepairPostfix), exception);
        }
        finally
        {
            Leave();
        }
    }

    private static bool CountPrefix(EntityInteractableStore __instance, ref int __result)
    {
        if (!_taken || __instance == null || __instance.Pointer != _store)
        {
            return true;
        }

        __result = 1;
        return false;
    }

    private static bool FirstPrefix(EntityInteractableStore __instance, ref Entity __result)
    {
        if (!_taken || __instance == null || __instance.Pointer != _store || _damaged == null || _damaged.Count == 0)
        {
            return true;
        }

        __result = _damaged[0];
        return false;
    }

    private static void Enter(EntityRepairWorkstation table, string handler)
    {
        Leave();
        if (Group == null || !Group.IsActive || table == null)
        {
            return;
        }

        try
        {
            var plan = _controller.PlanBatch(table);
            if (plan == null)
            {
                return;
            }

            _store = table.LinkedStore.Pointer;
            _damaged = plan.Damaged;
            _parcels = plan.Parcels;
            _taken = true;
        }
        catch (Exception exception)
        {
            Leave();
            Group.Fault(handler, exception);
        }
    }

    private static void Leave()
    {
        _taken = false;
        _store = IntPtr.Zero;
        _damaged = null;
        _parcels = 0;
    }
}
