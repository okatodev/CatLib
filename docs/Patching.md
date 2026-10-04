# Patching the game

`CatPatches` in `CatLib.Patching` is a group of Harmony patches of one mod that is installed whole or not at all.

```csharp
var patches = new CatPatches(PluginMeta.Guid, log)
    .Postfix(typeof(EntityInteractableStore), nameof(EntityInteractableStore.IsEntityPositionValid),
        new[] { typeof(Entity), typeof(Vector2Int), typeof(int) }, typeof(MyPatches), nameof(MyPatches.PositionValidPostfix))
    .Prefix(typeof(EntityInteractableAction), nameof(EntityInteractableAction.Interact),
        null, typeof(MyPatches), nameof(MyPatches.InteractPrefix));
if (!patches.Apply())
{
    log.Warning("Running without patches");
}
```

- `Apply` first looks up every method and every handler. If one is missing (the game changed, a parameter type differs),
  each problem is written to the log, `Problems` lists them, and nothing is patched. Parameter types pick one overload;
  `null` means the method has exactly one overload.
- If Harmony fails on one patch, the patches installed before it are removed again.
- Handlers are static. A prefix returns nothing or `bool`.
- `IsActive` is true while the group works. `Remove` takes all patches off.

## Errors in handlers

A handler that throws inside the game's code can break the game's own work, so every handler runs its code through the group:

```csharp
public static void PositionValidPostfix(EntityInteractableStore __instance, Entity __0, Vector2Int __1, int __2, ref bool __result)
{
    if (!Patches.IsActive)
    {
        return;
    }

    var vanilla = __result;
    try
    {
        __result = Controller.Check(__instance, __0, __1, __2, vanilla);
    }
    catch (Exception exception)
    {
        __result = vanilla;
        Patches.Fault(nameof(PositionValidPostfix), exception);
    }
}
```

`Run(name, action)` and `Run(name, func, fallback)` do the same for short handlers; they allocate a closure, so hot handlers
use `try` and `Fault` directly. The first 3 errors of every handler are written with their stack, later ones are only counted.
After 50 errors in all the group turns itself off: `IsActive` becomes false, `TurnedOff` fires, and the handlers leave the game
to work as without the mod until it restarts.

## Patches and multiplayer

A patch that changes what the game allows (where a parcel may stand, what breaks) must run on every player the same way,
so such a mod is `RequiredOnAll`. Methods the game runs only on the host, like the end-of-day damage checks or the tipping over of stacks,
are only patched in effect on the host. See [Storages](Storages.md) for how the game stacks parcels.
