using System;
using CatLib.Logging;
using HarmonyLib;

namespace ShelfLabels;

public static class LabelClickPatch
{
    private static Func<IntPtr, int, bool, bool> _handler;
    private static CatLogger _log;

    public static bool Install(string harmonyId, Func<IntPtr, int, bool, bool> handler, CatLogger log)
    {
        _handler = handler;
        _log = log;
        try
        {
            var harmony = new Harmony(harmonyId);
            harmony.Patch(AccessTools.Method(typeof(EntityInteractableAction), nameof(EntityInteractableAction.Interact)),
                prefix: new HarmonyMethod(typeof(LabelClickPatch), nameof(InteractPrefix)));
            harmony.Patch(AccessTools.Method(typeof(EntityInteractableAction), nameof(EntityInteractableAction.InteractSecondary)),
                prefix: new HarmonyMethod(typeof(LabelClickPatch), nameof(InteractSecondaryPrefix)));
            return true;
        }
        catch (Exception exception)
        {
            log.Error("Patching label clicks failed, extra labels cannot be changed", exception);
            return false;
        }
    }

    private static bool InteractPrefix(EntityInteractableAction __instance, bool is_from_network) => Handle(__instance, 1, is_from_network);

    private static bool InteractSecondaryPrefix(EntityInteractableAction __instance, bool is_from_network) => Handle(__instance, -1, is_from_network);

    private static bool Handle(EntityInteractableAction action, int step, bool fromNetwork)
    {
        try
        {
            return action == null || _handler == null || !_handler(action.Pointer, step, fromNetwork);
        }
        catch (Exception exception)
        {
            _log?.Error("Handling a click on an extra label failed", exception);
            return true;
        }
    }
}
