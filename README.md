# CatLib

Foundation library for modding Cat Mail Co. with BepInEx 6 (Unity IL2CPP), and gameplay mods built on it.

## Repository

```
CatLib.sln
Directory.Build.props        shared compiler settings and game path resolution
Directory.Build.targets      imports everything in build/
GamePaths.props.example      template for your local game path
build/
  GameReferences.targets     references to BepInEx core and generated interop assemblies
  PluginMeta.targets         generates PluginMeta (Guid, Name, Version) from the project file
  Deploy.targets             copies the built plugin into BepInEx/plugins/<folder>
src/CatLib/                  the library, shipped to players
mods/                        gameplay mods built on CatLib, one project per mod
tests/CatLib.Tests/          in-game test plugin and developer tools, developers only
docs/                        documentation
```

## Namespaces

| Namespace | Purpose |
|---|---|
| `CatLib` | Plugin entry point, `PluginMeta` |
| `CatLib.Core` | Runtime bootstrap, `FrameLoop` for per-frame updates |
| `CatLib.Logging` | `CatLogger`, scoped logging on top of BepInEx |
| `CatLib.Threading` | `MainThread` dispatcher |
| `CatLib.Events` | `SafeInvoker`, exception-isolated event invocation |
| `CatLib.Il2Cpp` | Binding managed code to IL2CPP events; `Il2CppArrays` reads and writes two-dimensional IL2CPP arrays that interop cannot type |
| `CatLib.Game` | `GameInfo`, read-only game state |
| `CatLib.Game.Events` | Game events as plain .NET events, `GameEventStream` |
| `CatLib.Game.Bridge` | Tracks game singletons and binds their events |
| `CatLib.Config` | Live settings: `CatSettings`, `Setting<T>`, `CatConfig` |
| `CatLib.Localization` | Translation catalogs for mods, `CatLanguage` follows the game language |
| `CatLib.UI` | Mods tab in the game's settings menu; `Notifications.Show` for messages to the player |
| `CatLib.Net` | Mod compatibility handshake and session settings sync |

## Mods

Each mod in `mods/` is its own BepInEx plugin, released separately from CatLib:

- it has a `BepInDependency` on `catlib.core`;
- it declares its network policy with `CatNetwork.Declare`;
- it only sets its GUID, name, version and deploy folder; everything else comes from the shared build files.

| Mod | Description |
|---|---|
| [Boat Tweaks](mods/BoatTweaks/README.md) | Deck blockers and stack height limits of the boat |

## Building

1. Install the .NET SDK 8 or newer.
2. Install BepInEx 6 (IL2CPP) into the game and launch the game once so `BepInEx/interop` is generated.
3. Copy `GamePaths.props.example` to `GamePaths.props` and set `GameDir` to the folder that contains the game executable.
4. Run `dotnet build` in the repository root.

Each build copies every plugin into its own folder in `BepInEx/plugins`: `CatLib`, `CatLib.Tests` and one folder per mod.
Pass `-p:CatLibDeploy=false` to build without copying.

## Tests

`CatLib.Tests` runs all tests the first time the main menu loads and on demand with F10.
Reports are written to `BepInEx/CatLib.Tests/Reports`, game event timelines to `BepInEx/CatLib.Tests/Timelines`.
Configuration: `BepInEx/config/catlib.tests.cfg`.

### Developer hotkeys

| Key | Tool |
|---|---|
| F4 | Entity dump: what the camera looks at with its object tree, entity counts, storages and the focus types, written to `BepInEx/CatLib.Tests/Dumps` |
| F5 | Sample network message shown as a game notification |
| F6 | Steam channel self check |
| F7 | Network probes |
| F8 | Stress mods for the Mods tab |
| F9 | Settings menu UI dump |
| F10 | Run the in-game tests |

All keys can be changed in `catlib.tests.cfg`, section `[Diagnostics]`. The focus types of the entity dump are set with `EntityDumpTypes`.

## Documentation

- [Live settings](docs/Settings.md)
- [Multiplayer compatibility](docs/Network.md)
- [Localization](docs/Localization.md)
- [Game events: observed behaviour](docs/GameEvents.md)

## Conventions

- No comments in code.
- Log messages are in English.
- Numbers and dates in logs and reports are formatted with the invariant culture.
- One public type per file, folder structure mirrors namespaces.

## License

MIT, see [LICENSE](LICENSE).
