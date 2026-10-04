# Writing a mod

This guide walks through a gameplay mod built on CatLib, from an empty project to a release.
[Boat Tweaks](../mods/BoatTweaks/README.md) is a complete example of everything described here.

## Project

A mod in this repository lives in `mods/<Name>/` and only declares what is its own:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <AssemblyName>MyMod</AssemblyName>
    <RootNamespace>MyMod</RootNamespace>
    <Version>0.1.0</Version>
    <PluginGuid>catlib.mymod</PluginGuid>
    <PluginMetaName>My Mod</PluginMetaName>
    <CatLibDeployFolder>MyMod</CatLibDeployFolder>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\CatLib\CatLib.csproj" Private="false" />
  </ItemGroup>
</Project>
```

Game and BepInEx references, the generated `PluginMeta` class, the texts in `Lang/*.json`
and copying into `BepInEx/plugins/<CatLibDeployFolder>` come from the shared build files. Add the project to `CatLib.sln` with `dotnet sln add`.

A mod outside this repository references `CatLib.dll` directly and writes its own `BepInPlugin` values.

### Icon

Put a 256x256 `icon.png` with a transparent background into `mods/<Name>/`, the same file Thunderstore needs.
The build copies it next to the mod's DLL and into the game, and the Mods tab shows it left of the mod's name and at the top of its settings.
A project without `icon.png` builds with warning CATLIB001. Set `CatLibIcon` in the project file to take the icon from another path,
or `CatLibIconCheck` to `false` to turn the check off.
CatLib looks for `icon.png` next to the DLL and up to two folders above it, but never in the shared `plugins` folder,
so the layout of a Thunderstore package installed by a mod manager works too. `settings.IconPath` sets another file.
Without an icon the Mods tab shows a question mark in its place. The log line `[Icons]` names the icon of every mod
or the folder where it was looked for, and warns when the icon is not 256x256.

The author shown under the version comes from the Thunderstore package folder (`Author-ModName`) when the mod is installed
by a mod manager, otherwise from `Authors` in the project file. `settings.Author` sets it from code.

### Folders

Every mod in this repository keeps the same layout. The namespace follows the folder, for example `BoatTweaks.Logic`.

```
mods/<Name>/
  <Name>Plugin.cs     entry point: declarations, settings, events, developer commands
  <Name>Controller.cs ties the parts together while the game runs
  <Name>.csproj
  README.md           what the mod does, settings, how it works, for GitHub
  CHANGELOG.md        changes per version, for GitHub and the Thunderstore package
  icon.png            256x256, for the Mods tab and the Thunderstore package
  Thunderstore/       manifest.json and README.md of the Thunderstore package
  Lang/               en.json, ru.json and other languages
  Settings/           the mod's settings
  Logic/              rules, plans and state without game objects; covered by offline tests
  Scene/              code that reads or changes game objects
  Sync/               multiplayer: mod messages and shared state
  Data/               mod data in game saves
  Patches/            Harmony patches
```

A folder appears only when the mod needs it. Code that can live in `Logic` goes there,
so most of the mod is tested without starting the game.

## Plugin

```csharp
[BepInPlugin(PluginMeta.Guid, PluginMeta.Name, PluginMeta.Version)]
[BepInDependency("catlib.core")]
public sealed class MyModPlugin : BasePlugin
{
    public override void Load()
    {
        var log = CatLogger.From(Log);
        var settings = CatSettings.For(this);
        CatNetwork.Declare(this, SessionPolicy.RequiredOnAll);

        var controller = new MyController(log, settings);
        FrameLoop.Update += controller.Update;
    }
}
```

- `BepInDependency` makes BepInEx load CatLib first.
- `CatSettings.For(this)` also loads the mod's texts from `Lang/*.json` and the players' translation files.
- `CatNetwork.Declare` puts the mod into the multiplayer handshake. See [Multiplayer compatibility](Network.md) for the policies.
- `FrameLoop.Update` runs every frame on the main thread; an exception in one subscriber does not stop the others.

## Settings

```csharp
var height = settings.Session("Height", "Scale", 1f, "Multiplier for the height limit.", new AcceptableValueRange<float>(0.5f, 4f));
height.Apply(value => controller.RequestApply());
```

- `Local` settings belong to each player, `Session` settings take the host's value in multiplayer.
- `Apply` runs immediately and after every change, from the menu, the file or code.
- Settings appear in the Mods tab without any UI code. Details: [Live settings](Settings.md).

## Texts

Every text a player sees comes from the mod's catalog: the name on the Mods tab, sections, labels, descriptions,
dropdown values and the mod's own messages. Put `Lang/en.json` and a file for every other language next to the project,
the build embeds them and CatLib loads them by itself:

```json
{
  "mod": { "name": "My Mod" },
  "section": { "Height": "Height" },
  "setting": {
    "Height.Scale": "Height limit",
    "Height.Scale.description": "Multiplier for the height limit."
  },
  "message": { "saved": "Saved: {0}" },
  "parcels": { "one": "{0} parcel", "other": "{0} parcels" }
}
```

```csharp
Notifications.Show(settings.Texts.Format("message.saved", name));
Notifications.Show(settings.Texts.Plural("parcels", count));
```

Key rules, plural forms, translation files of players and the checks are in [Localization](Localization.md).

## Reacting to the game

`CatLib.Game.Events` exposes the game's own events as plain .NET events, for example
`BootstrapEvents.LevelLoadFinalized`, `BootstrapEvents.GameRestartStarted`, `NetworkEvents.ClientConnected`
or `PlayerEvents.LocalPlayerSpawned`. Their observed order and meaning are in [Game events](GameEvents.md).

### Parcels and the screen

`CatParcels.Read()` lists the parcels of the level with their destinations, marks and sizes, and `HudLayer` with `CountList`
draws on the screen during a level, out of the way of the game's menus. See [HUD and parcels](Hud.md).

### Storages and patches

`StoreGrid` reads the grids of storages and parcels the way the game uses them, see [Storages](Storages.md).
A mod that changes the game's own behaviour patches its methods with `CatPatches`, which installs all patches or none
and keeps errors in handlers away from the game, see [Patching the game](Patching.md).

### Game build

`GameCompatibility.Current` tells whether the running game is a build this CatLib is made for: `Supported`, `GameNewer`,
`GameOlder` or `Unknown`, with the running and the expected `GameBuild` (version, build date, Steam build).
The build is read from the game version: its last two numbers are the build date. A mod that relies on fragile game internals
can stay quiet when `GameCompatibility.Status` is not `Supported`. The main menu shows the CatLib version in the bottom right
corner and warns there when the game is newer or older.

## Data in saves

Data that belongs to a playthrough, such as a picture a player chose, is stored per game save with `CatSaves.For(this)`.
It is written only when the game saves and only by the host. Rules and examples are in [Mod data in game saves](Saves.md).

## Multiplayer

- Decisions that must be the same for everyone are made where `CatNetwork.IsAuthority` is true
  (single player and the host) and sent to clients in a hidden session setting.
- A client acts on such a value only when `IsOverridden` is true, that is after the host accepted it.
- Random choices are sent as a seed, and clients rebuild the same result from it.

The pattern with code is in [Session role](Network.md#session-role).
For actions of players, such as a click on an object the mod added, use [mod messages](Network.md#mod-messages):
the player asks the host, the host checks and applies the action and tells everyone the result.

## Working with game objects

Lessons from the mods so far:

- **Managers are recreated.** Every `Singleton<T>` of the game is destroyed and created again on each level load and restart.
  Keep the instance pointer you worked with and redo the work when `Singleton<T>.Instance` changes; never keep a list across levels.
- **Change prefabs for what comes next, instances for what exists now.** The game builds new objects from prefabs,
  so a value written to a prefab reaches every future copy. Remember the original value and write it back when the mod is turned off.
- **Create objects only inside the object that owns them.** Objects created in one scene and left alive while that scene unloads
  corrupt Unity's scene lists and crash the game later. Destroy temporary objects right away.
- **Read fields, not computed properties.** Auto properties of game classes are backed by fields named `_Name_k__BackingField`;
  reading those never runs game code. Computed getters may.
- **Two-dimensional arrays** such as `bool[,]` come through interop as a bare `Il2CppObjectBase`.
  Read and write them with `Il2CppArrays`, which checks the rank, the element size and the bounds and returns `false` instead of touching foreign memory.
- **Some state is read once.** The boat storage, for example, finds its blocked cells when it is created and never again,
  so a mod that adds blockers later has to keep those cells taken itself. When a change seems to be ignored, measure what the game reads and when.

## Finding things in the game

The developer build of `CatLib.Tests` has an entity dump on F4: it writes what the camera looks at with its components,
fields and object tree, and the nearest objects of configurable game types. Take a dump with a screenshot of the same view,
change one thing in the game, take another dump and compare.

## Tests

In this repository, a mod's pure logic is tested in `tests/CatLib.Tests/Suites/<Mod>/`. Tests run in the game when the main menu loads and on F10.
Keep the game-independent parts — parsing, planning, generation — in classes without Unity types so they are easy to test,
and check each rule once with a deliberately broken implementation to see that the test catches it.

## Releasing

- Follow semantic versioning for the mod's `Version`. The default network rule `SameMinor` treats a minor bump as incompatible,
  so players in one lobby need the same minor version.
- Describe settings, modes and multiplayer behaviour in the mod's `README.md`, and the changes of every version in `CHANGELOG.md`.

### Thunderstore package

`dotnet build -c Release -p:CatLibThunderstore=true` packs every project that has `Thunderstore/manifest.json`
into `Thunderstore-build/<name>/` and `Thunderstore-build/<name>-<version>.zip`, the file to upload:

```
manifest.json      from Thunderstore/manifest.json
icon.png           the project's icon.png
README.md          from Thunderstore/, written for players
CHANGELOG.md       the project's CHANGELOG.md, when there is one
plugins/<Name>.dll
```

Every other file in `Thunderstore/` goes to the root of the package as well. A mod manager installs the package
into `BepInEx/plugins/<Team>-<name>/`, where CatLib finds the icon and the author.
`Thunderstore-build/` is not in git, and each build replaces the package folder.

`manifest.json` is written for Thunderstore with placeholders: `{version}` becomes the project's `Version`,
`{catlib_version}` the version of CatLib the mod is built against, and `{version:<Project>}` the version of a referenced project,
for example `{version:BoatTweaks}` in the package of `CatLib.Tests`.

```json
{
  "name": "MyMod",
  "version_number": "{version}",
  "website_url": "https://github.com/you/MyMod",
  "description": "What the mod does, 250 characters at most.",
  "dependencies": [
    "Team-CatLib-{catlib_version}"
  ]
}
```

The build checks the package the way Thunderstore does and fails with CATLIB004 when it would be rejected:
the name only has `a-z A-Z 0-9 _`, the version is `Major.Minor.Patch`, the description is at most 250 characters,
`website_url` is there even when empty, every dependency is `Team-Package-1.2.3`, and `icon.png` is a PNG of exactly 256x256.
A placeholder that names no referenced project fails as well. A missing `Thunderstore/README.md` fails with CATLIB002.
An empty `Thunderstore/README.md`, a `TODO` left in the manifest or a version that differs from the project give warning CATLIB003.

Thunderstore shows the README and the changelog on the package page, where links relative to the repository do not open.
The build turns them into links to the repository on GitHub, `CatLibRepositoryUrl` and `CatLibRepositoryBranch` in `Directory.Build.props`,
so `[Crash reports](docs/CrashReports.md)` in the root changelog becomes `https://github.com/okatodev/CatLib/blob/main/docs/CrashReports.md`.
The files in the repository keep their relative links. The changelog must start with a section named after the package version:
`## Not released yet` or `## Next version` gives warning CATLIB003, so rename it when the version is set.

Properties: `CatLibThunderstoreDir` for the output folder, `CatLibThunderstoreSource` for the template folder,
`CatLibThunderstoreChangelog` for another changelog. Items `CatLibThunderstoreFile` and `CatLibThunderstoreProject`
add files or the output of other projects to `plugins/`. `CatLib.CrashWatcher.exe` is not added to CatLib this way: it has a package of its own.
