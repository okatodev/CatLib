# Live settings

`CatLib.Config` lets a mod declare settings that apply while the game is running.
Players edit the mod's `.cfg` file in `BepInEx/config`, save it, and the change takes effect within a fraction of a second.

## Declaring settings

```csharp
public override void Load()
{
    var settings = CatSettings.For(this);

    settings.Local("Camera", "ExtraFov", 0f,
            "Additional field of view in degrees.", new AcceptableValueRange<float>(0f, 30f))
        .Apply(value => CameraTweaks.ExtraFov = value);

    settings.Session("Gameplay", "CarryLimit", 3,
            "How many parcels a player can carry.", new AcceptableValueRange<int>(1, 10))
        .Apply(value => CarryRules.Limit = value);

    var fastSorting = settings.Local("Advanced", "FastSorting", true, "Speeds up sorting.").RequiresRestart();
    if (fastSorting.Value)
    {
        Harmony.CreateAndPatchAll(typeof(FastSortingPatch));
    }
}
```

Settings are stored in the plugin's regular BepInEx config file, so the file format does not change and other tools keep working.

## Scopes

| Scope | Use for | Behaviour |
|---|---|---|
| `Local` | Graphics, audio, controls, UI | Always the player's own value. |
| `Session` | Anything that affects gameplay for everyone | Currently behaves like `Local`. Once network sync lands, clients in someone else's session use the host's value. |

Declare gameplay settings as `Session` now. Mods will not need changes when synchronisation arrives.

## Reading values

- `Value` is the value in effect. Mod code should always read this one.
- `LocalValue` is the value from the local file.
- They differ for settings that require a restart after an edit, and later for session settings while playing as a client.

## Applying values

`Apply(action)` calls `action` immediately with the current value and again after every change.
It always runs on the main thread, so it is safe to touch game objects inside it.
It returns an `IDisposable`; dispose it to stop receiving values.
An exception thrown by one applier is logged and does not affect other appliers or other mods.

Prefer `Apply` over caching `Value` in `Load`. A cached value never updates.

## Settings that need a restart

Mark settings that are read only once, such as whether a Harmony patch is installed, with `RequiresRestart()`.
After an edit, `Value` keeps the startup value, `LocalValue` shows the new one, `IsRestartPending` is true,
and the player sees a message that the change applies after a restart. Appliers are not called.

## What players see

| Situation | Result |
|---|---|
| A valid edit | Applied, `Reloaded <file>: 1 changed` in the log. |
| A value that cannot be parsed | The previous value stays, a warning names the setting and the rejected text. |
| A value outside the accepted range | The nearest accepted value is used, a warning names both values. |
| An edit to a restart-only setting | A message says it will apply after a restart. |
| The file is deleted | Current values stay. The file is recreated with defaults on the next launch. |

The file itself is never rewritten while the player is editing it.

## Diagnostics

`CatConfig` exposes `FileReloaded`, `ValueRejected`, `ValueAdjusted` and `RestartRequired` for tools and UI.
