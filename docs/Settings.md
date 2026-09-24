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

## Resetting from code

```csharp
var changed = settings.ResetToDefaults(setting => !setting.IsHiddenInMenu);
```

All matching settings return to their defaults, appliers run as usual and the file is saved once.

## Messages to the player

Restart-only changes, rejected values and adjusted values are shown to the player:
in the status line of the Mods tab while the settings menu is open, and as the game's own notifications during a level.
Messages are available in English and Russian and follow the game language.

## Diagnostics

`CatConfig` exposes `FileReloaded`, `ValueRejected`, `ValueAdjusted` and `RestartRequired` for tools and UI.

## In-game menu

Every mod with at least one visible setting gets an entry on the Mods tab of the game's settings menu.
The mod list shows the plugin name, the header shows the name and version.
Settings are grouped by section, in declaration order.

| Setting type | Control |
|---|---|
| `bool` | Toggle |
| Number with `AcceptableValueRange` | Slider. Whole numbers for integer types, two decimals for fractional types |
| `enum` or any type with `AcceptableValueList` | Dropdown |
| Anything else | Text field. Invalid input is rejected and the field shows the current value again |

Toggles and dropdowns apply immediately, text fields when editing ends, sliders 250 ms after the handle stops.
Edits to the file while the menu is open show up in the menu.

```csharp
settings.Local("General", "Seed", 42, "World seed.").Label("World seed");
settings.Local("Debug", "InternalCounter", 0, "Used by the mod itself.").HiddenInMenu();
```

The top of the settings pane shows the mod name, its version and how many settings wait for a restart.
Below the settings, a context line shows the description of the setting under the pointer or the one selected with a gamepad,
together with its default value, its range or its options.
The "Revert settings to default" button resets the selected mod after the game's own confirmation popup; hidden settings are not touched.

`Label` replaces the label generated from the key (`ExtraFov` becomes "Extra Fov").
`HiddenInMenu` keeps a setting out of the menu; it still lives in the file and reloads live.
Settings that require a restart are marked with "(restart)".
