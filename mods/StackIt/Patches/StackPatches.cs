using System;
using CatLib.Logging;
using CatLib.Patching;
using UnityEngine;

namespace StackIt.Patches;

public static class StackPatches
{
    private static StackController _controller;

    public static CatPatches Group { get; private set; }

    public static CatPatches Install(string ownerId, StackController controller, CatLogger log)
    {
        _controller = controller;
        Group = new CatPatches(ownerId, log)
            .Postfix(typeof(EntityInteractableStore), nameof(EntityInteractableStore.IsEntityPositionValid),
                new[] { typeof(Entity), typeof(Vector2Int), typeof(int) }, typeof(StackPatches), nameof(PositionValidPostfix))
            .Postfix(typeof(EntityInteractableStore), nameof(EntityInteractableStore.StoreEntityLocal),
                new[] { typeof(Entity), typeof(Vector2Int), typeof(Il2CppSystem.Collections.Generic.List<Vector2Int>), typeof(int), typeof(bool) },
                typeof(StackPatches), nameof(StoredPostfix))
            .Postfix(typeof(EntityInteractableStore), nameof(EntityInteractableStore.RemoveEntityLocal),
                new[] { typeof(Entity) }, typeof(StackPatches), nameof(RemovedPostfix))
            .Postfix(typeof(EntityProperties), nameof(EntityProperties.CheckBehaviorConstraint),
                Type.EmptyTypes, typeof(StackPatches), nameof(BehaviorPostfix));
        Group.Apply();
        return Group;
    }

    private static void PositionValidPostfix(EntityInteractableStore __instance, Entity __0, Vector2Int __1, int __2, ref bool __result)
    {
        if (Group == null || !Group.IsActive)
        {
            return;
        }

        var vanilla = __result;
        try
        {
            __result = _controller.IsPositionValid(__instance, __0, __1, __2, vanilla);
        }
        catch (Exception exception)
        {
            __result = vanilla;
            Group.Fault(nameof(PositionValidPostfix), exception);
        }
    }

    private static void StoredPostfix(EntityInteractableStore __instance, Entity __0, Vector2Int __1, int __3)
    {
        if (Group == null || !Group.IsActive)
        {
            return;
        }

        try
        {
            _controller.OnStored(__instance, __0, __1, __3);
        }
        catch (Exception exception)
        {
            Group.Fault(nameof(StoredPostfix), exception);
        }
    }

    private static void RemovedPostfix(EntityInteractableStore __instance, Entity __0)
    {
        if (Group == null || !Group.IsActive)
        {
            return;
        }

        try
        {
            _controller.OnRemoved(__instance, __0);
        }
        catch (Exception exception)
        {
            Group.Fault(nameof(RemovedPostfix), exception);
        }
    }

    private static void BehaviorPostfix(EntityProperties __instance)
    {
        if (Group == null || !Group.IsActive)
        {
            return;
        }

        try
        {
            _controller.OnBehaviorChecked(__instance);
        }
        catch (Exception exception)
        {
            Group.Fault(nameof(BehaviorPostfix), exception);
        }
    }
}
