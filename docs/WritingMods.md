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
    <EmbeddedResource Include="Lang\*.json" LogicalName="MyMod.Lang.%(Filename).json" />
  </ItemGroup>
</Project>
```

Game and BepInEx references, the generated `PluginMeta` class and copying into `BepInEx/plugins/<CatLibDeployFolder>`
come from the shared build files. Add the project to `CatLib.sln` with `dotnet sln add`.

A mod outside this repository references `CatLib.dll` directly and writes its own `BepInPlugin` values.

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
        settings.Texts.LoadEmbedded(typeof(MyModPlugin).Assembly, "MyMod.Lang.");
        CatNetwork.Declare(this, SessionPolicy.RequiredOnAll);

        var controller = new MyController(log, settings);
        FrameLoop.Update += controller.Update;
    }
}
```

- `BepInDependency` makes BepInEx load CatLib first.
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
dropdown values and the mod's own messages. Put `Lang/en.json` and `Lang/ru.json` next to the project:

```json
{
  "mod": { "name": "My Mod" },
  "section": { "Height": "Height" },
  "setting": {
    "Height.Scale": "Height limit",
    "Height.Scale.description": "Multiplier for the height limit."
  },
  "message": { "saved": "Saved: {0}" }
}
```

```csharp
Notifications.Show(settings.Texts.Format("message.saved", name));
```

Key rules and the lookup order are in [Localization](Localization.md).
A test that walks the declared settings and checks every key with `TextCatalog.Has` for each language keeps the files complete.

## Reacting to the game

`CatLib.Game.Events` exposes the game's own events as plain .NET events, for example
`BootstrapEvents.LevelLoadFinalized`, `BootstrapEvents.GameRestartStarted`, `NetworkEvents.ClientConnected`
or `PlayerEvents.LocalPlayerSpawned`. Their observed order and meaning are in [Game events](GameEvents.md).

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
- Describe settings, modes and multiplayer behaviour in the mod's `README.md`.
