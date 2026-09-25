# CatLib

Foundation library for modding Cat Mail Co. with BepInEx 6 (Unity IL2CPP).

## Layout

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
tests/CatLib.Tests/          in-game test plugin, developers only
```

## Namespaces

| Namespace | Purpose |
|---|---|
| `CatLib` | Plugin entry point, `PluginMeta` |
| `CatLib.Core` | Runtime bootstrap, frame loop, injected behaviour |
| `CatLib.Logging` | `CatLogger`, scoped logging on top of BepInEx |
| `CatLib.Threading` | `MainThread` dispatcher |
| `CatLib.Events` | `SafeInvoker`, exception-isolated event invocation |
| `CatLib.Il2Cpp` | Helpers for binding managed code to IL2CPP events |
| `CatLib.Game` | `GameInfo`, read-only game state |
| `CatLib.Game.Events` | Game events re-exposed as plain .NET events, `GameEventStream` |
| `CatLib.Game.Bridge` | Tracks game singletons and binds their events |
| `CatLib.Config` | Live settings: `CatSettings`, `Setting<T>`, `CatConfig` |
| `CatLib.UI` | Mods tab injected into the game's settings menu |
| `CatLib.Net` | Mod compatibility handshake and session settings sync |
| `CatLib.Localization` | Translation catalogs for mods and the game language |
| `CatLib.Tests.Framework` | In-game test runner |
| `CatLib.Tests.Timeline` | Game event timeline recorder |
| `CatLib.Tests.Diagnostics` | Developer tools such as the UI hierarchy dump (F9) |
| `CatLib.Tests.Suites.*` | Test cases grouped by suite |

## Building

1. Install the .NET SDK 8 or newer.
2. Install BepInEx 6 (IL2CPP) into the game and launch the game once so `BepInEx/interop` is generated.
3. Copy `GamePaths.props.example` to `GamePaths.props` and set `GameDir` to the folder that contains the game executable.
4. Run `dotnet build` in the repository root.

Each build copies `CatLib.dll` to `BepInEx/plugins/CatLib` and `CatLib.Tests.dll` to `BepInEx/plugins/CatLib.Tests`.
Pass `-p:CatLibDeploy=false` to build without copying.

## Tests

`CatLib.Tests` runs all tests the first time the main menu loads and on demand with F10.
Reports are written to `BepInEx/CatLib.Tests/Reports`, game event timelines to `BepInEx/CatLib.Tests/Timelines`.
Configuration: `BepInEx/config/catlib.tests.cfg`.

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

## Developer hotkeys (CatLib.Tests)

| Key | Tool |
|---|---|
| F4 | Entity dump: what the camera looks at, entity counts and the focus types, written to `BepInEx/CatLib.Tests/Dumps` |
| F5 | Sample network message shown as a game notification |
| F6 | Steam channel self check |
| F7 | Network probes |
| F8 | Stress mods for the Mods tab |
| F9 | Settings menu UI dump |
| F10 | Run the in-game tests |

All keys can be changed in `catlib.tests.cfg`, section `[Diagnostics]`. The focus types of the entity dump are set with `EntityDumpTypes`.

## Mods in this repository

Gameplay mods built on CatLib live in `mods/`, one project per mod, released separately from CatLib:

- each mod is its own BepInEx plugin with a `BepInDependency` on `catlib.core`;
- each declares its network policy with `CatNetwork.Declare`;
- shared build settings come from the repository's `Directory.Build.props`, so a mod project only sets its GUID, name, version and deploy folder.

### Mods

| Mod | Folder |
|---|---|
| Boat Tweaks | [mods/BoatTweaks](mods/BoatTweaks/README.md) |
