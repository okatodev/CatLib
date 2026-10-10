using System;
using CatLib.Logging;
using CatLib.Patching;
using CustomStamps.Game;
using UnityEngine;

namespace CustomStamps.Patches;

public static class StampPatches
{
    private static StampSelection _selection;

    public static CatPatches Group { get; private set; }

    public static CatPatches Install(string ownerId, StampSelection selection, CatLogger log)
    {
        _selection = selection;
        Group = new CatPatches(ownerId, log)
            .Postfix(typeof(StampSelectionCategoryInterface), nameof(StampSelectionCategoryInterface.Populate), Type.EmptyTypes,
                typeof(StampPatches), nameof(PopulatePostfix))
            .Prefix(typeof(EntityInteractableStamp), nameof(EntityInteractableStamp.AddStampFromNetwork),
                new[] { typeof(string), typeof(int), typeof(Vector3), typeof(Quaternion), typeof(bool) },
                typeof(StampPatches), nameof(AddFromNetworkPrefix));
        Group.Apply();
        return Group;
    }

    private static void PopulatePostfix(StampSelectionCategoryInterface __instance)
    {
        if (Group == null || !Group.IsActive)
        {
            return;
        }

        try
        {
            _selection.AfterPopulate(__instance);
        }
        catch (Exception exception)
        {
            Group.Fault(nameof(PopulatePostfix), exception);
        }
    }

    private static bool AddFromNetworkPrefix(EntityInteractableStamp __instance, string __0, int __1, Vector3 __2, Quaternion __3, bool __4)
    {
        if (Group == null || !Group.IsActive)
        {
            return true;
        }

        try
        {
            return !_selection.PlaceFromNetwork(__instance, __0, __1, __2, __3, __4);
        }
        catch (Exception exception)
        {
            Group.Fault(nameof(AddFromNetworkPrefix), exception);
            return !CustomStamps.Packs.StampFiles.IsCustomKey(__0);
        }
    }
}
