# Core helpers

Small things almost every mod uses: logging, running code every frame and on the main thread, safe events,
what the game is doing right now and which build it is. All of them are static and ready once CatLib has loaded,
which `[BepInDependency("catlib.core")]` guarantees before your `Load` runs.

| I want to… | Use | Namespace |
|---|---|---|
| write to the BepInEx log with a prefix per part of my mod | `CatLogger` | `CatLib.Logging` |
| run code every frame | `FrameLoop.Update` | `CatLib.Core` |
| get back to the main thread from a callback or a task | `MainThread.Post` | `CatLib.Threading` |
| raise my own event without one handler breaking the others | `SafeInvoker.Invoke` | `CatLib.Events` |
| know the save, the level or whether I am the server | `GameInfo` | `CatLib.Game` |
| know whether this game build is the one CatLib was made for | `GameCompatibility` | `CatLib.Game` |
| tell the player something | `Notifications.Show` | `CatLib.UI` |
| read a two-dimensional array of the game | `Il2CppArrays` | `CatLib.Il2Cpp` |

## Logging

```csharp
var log = CatLogger.From(Log);
var sync = log.Scope("Sync");

log.Info("Loaded");
sync.Warning("The host sent an unknown value");
sync.Error("Applying the plan failed", exception);
```

```
[Info   :     My Mod] Loaded
[Warning:     My Mod] [Sync] The host sent an unknown value
```

- `CatLogger.From(Log)` writes under your plugin's name; `CatLogger.Create("Name")` makes a source of its own.
- `Scope` adds a `[Scope]` prefix and can be nested: `log.Scope("Sync").Scope("Host")` writes `[Sync] [Host]`.
- `Error(message, exception)` writes the full exception with its stack.

> [!TIP]
> Keep log lines in English and full sentences that say what happened and what the mod does about it:
> *"The stored stock 12 is out of range and is ignored"* is worth more than *"bad value"* when a player sends you their log.

## Every frame

```csharp
FrameLoop.Update += () =>
{
    if (FrameLoop.Realtime - _last < 0.25)
    {
        return;
    }

    _last = FrameLoop.Realtime;
    Refresh();
};
```

- `Update` runs once per frame on the main thread, after CatLib's own work: posted actions, settings reloads, the Mods tab and the network.
- An exception in one handler is logged and does not stop the other handlers or other mods.
- `FrameCount` counts frames since CatLib started, `Realtime` is seconds since then, unaffected by the game's time scale.

You do not need a `MonoBehaviour` of your own for per-frame work.

## The main thread

Unity objects may only be touched on the main thread. Steam callbacks, timers, tasks and file watchers often run elsewhere.

```csharp
Task.Run(() =>
{
    var plan = BuildPlan();
    MainThread.Post(() => Apply(plan));
});
```

| Member | Does |
|---|---|
| `Post(action)` | runs `action` on the main thread at the start of the next frame, in the order posted |
| `RunOrPost(action)` | runs it right away when already on the main thread, otherwise posts it |
| `IsCurrent` | whether the calling code is on the main thread |
| `PendingCount` | actions waiting for the next frame |

Settings appliers, game event handlers, mod messages and developer commands already run on the main thread.

## Safe events

```csharp
public static event Action<int> StockChanged;

SafeInvoker.Invoke(StockChanged, stock, "MyMod.StockChanged", log);
```

Every handler runs even when an earlier one throws; each failure is logged with the event name and the handler,
and `Invoke` returns how many failed. All events of CatLib are raised this way.

## Game state

`GameInfo` reads the game without changing it. Every value is `null` while the game has not created the manager it comes from.

| Property | Gives |
|---|---|
| `GameVersion` | the game's version string, for example `CMC 1.01.00.1737.9770.21112` |
| `IsServer` | whether this game runs the server: single player and the host |
| `IsSingleplayer`, `IsDemo` | the game mode and the demo build |
| `LoadedLevelKey` | the level that is loaded |
| `SaveFileName`, `SaveFilePath`, `SaveDirectory` | the selected save; `.bin` before one is selected |
| `IsNewSave`, `IsLoadingSave` | the state of the selected save |
| `UnityVersion`, `ApplicationVersion` | Unity's own versions |

For who is the host in multiplayer use `CatNetwork.Role` and `CatNetwork.IsAuthority`, see [Multiplayer compatibility](Network.md#session-role).

## Game build

`GameCompatibility` tells whether the running game is a build this CatLib is made for.

```csharp
if (GameCompatibility.Status != GameBuildStatus.Supported)
{
    log.Warning($"Untested game build {GameCompatibility.Current?.Running}, the fragile part stays off");
    return;
}
```

| Status | Means |
|---|---|
| `Supported` | the build is one CatLib was checked with |
| `GameNewer` | the game was updated after this CatLib; the main menu asks the player to look for a CatLib update |
| `GameOlder` | the game is older than CatLib expects; the main menu asks to update the game |
| `Unknown` | the version cannot be read yet, or has no build date |

`Current` holds the status with the running and the expected `GameBuild`: the version, the build date read from the last two
numbers of the version, and the Steam build. A mod that relies on fragile internals of the game, such as byte patterns
of [CodePatch](Patching.md#changing-a-few-bytes-of-the-games-code), can stay off when the status is not `Supported`.

## Messages to the player

```csharp
Notifications.Show(texts.Format("message.saved", name), texts.Get("message.saved.brief"));
```

During a level the text appears as the game's own notification; while the settings are open it goes to the status line
of the Mods tab instead, where the full text fits. The second argument is the short form for the notification:
two lines of at most 34 characters, split after the first `": "`. Without it the full text is fitted into those two lines.

## Two-dimensional arrays

Interop cannot type arrays like `bool[,]`, which the game uses for storage grids; they come through as a bare `Il2CppObjectBase`.

```csharp
if (Il2CppArrays.TryGetBounds(grid, out var rows, out var columns)
    && Il2CppArrays.TryGet<bool>(grid, 0, 0, out var taken))
{
    log.Info($"{rows}x{columns}, first cell {(taken ? "taken" : "free")}");
}
```

`TryGetRank`, `TryGetBounds`, `TryGet`, `TrySet` and `TryRead2D` check the rank, the element size and the bounds first
and return `false` instead of touching memory that is not the array. For storage grids `StoreGrid` does all of this already,
see [Storages](Storages.md).
