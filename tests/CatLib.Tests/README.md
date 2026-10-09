# CatLib Tests

[![Thunderstore](https://img.shields.io/badge/Thunderstore-CatLib%20Tests-23a6d5)](https://thunderstore.io/c/cat-mail-co/p/CatLib/CatLibTests/)

In-game tests and developer tools for CatLib and the mods of this repository. For developers only: players do not need it.
A build of the repository deploys it into `BepInEx/plugins/CatLib.Tests`; without the sources, install
[CatLib Tests from Thunderstore](https://thunderstore.io/c/cat-mail-co/p/CatLib/CatLibTests/) with a mod manager.

> [!WARNING]
> `CatLib.Tests` declares a demo network mod. Players without it show up as missing a mod in your lobby,
> so do not keep it installed when you play with friends who do not develop mods.

## Tests

About 160 tests run inside the real game: settings, the Mods tab, translations, saves, multiplayer, patches and every mod.

| When | How |
|---|---|
| The first time the main menu loads | automatically, unless `OnMainMenu` in `[Run]` of `catlib.tests.cfg` is off |
| Any time | developer menu → **Tests** → **Run all tests** |

- Reports go to `BepInEx/CatLib.Tests/Reports`, timelines of game events to `BepInEx/CatLib.Tests/Timelines`.
- Tests of a mod that is not installed are skipped and counted apart; the run still passes.
- Tests that need a level or the main menu say so in their notes and pass without checking anything elsewhere.

<details>
<summary><b>Test suites</b></summary>

| Suite | Checks |
|---|---|
| `Settings`, `Presentation`, `Ui` | live settings, the Mods tab, the version label, the developer menu panel |
| `Localization`, `Formatting` | catalogs of every mod, plural forms, the game build texts |
| `Saves` | mod data in saves: atomic writes, backups, newer data |
| `Network` | the handshake, roster, session settings, mod messages, the Steam channel |
| `Events`, `Bridge`, `Scheduling`, `Platform` | game events, singletons, the frame loop, game info |
| `Patching`, `Interop`, `Native` | `CatPatches`, `CodePatch`, IL2CPP arrays and events |
| `Crash` | crash texts, the session file, the watcher package, stack unwinding and method names of the watcher, stacks in the report |
| `Inspection`, `DevTools`, `Runner` | the entity dump, the developer menu model, the test runner |
| `BoatTweaks`, `ShelfLabels`, `BetterRepair`, `ParcelBoard`, `StackIt`, `TooLate` | the logic of every mod, and their scenes in a level |

</details>

## Developer menu

The `` ` `` key (left of 1) opens it. How the menu works and how a mod adds its own commands is in [Developer menu](../../docs/DevTools.md).
Groups come from `CatLib.Tests`, from CatLib itself and from the mods; a group appears only when its plugin is installed.

### From CatLib.Tests

| Group | Command | What it does |
|---|---|---|
| Tests | Run all tests | Starts the whole run; the report is written when it finishes |
| | Run on main menu | Toggle: run the tests the first time the main menu loads |
| Game | Serve the waiting customers | Host only. Every customer at a counter gets their request as if given the right parcel |
| | Next part of the day | Host only. The game's own cheat: day, evening, results, night, dawn, results; every player follows |
| | Skip to the day's results | Host only. One part of the day per frame until the evening or dawn results show |
| Inspect | Entity dump | What the camera looks at: components, fields, object tree, entity counts, storages and the nearest objects of the types in `EntityDumpTypes` |
| | Settings menu dump | The tree of the game's settings menu |
| | Lobby dump | The tree of the multiplayer lobby |
| | Sprite export | Every texture with loaded UI sprites as PNG, with `index.txt` naming each sprite; run it in the menu and in a level |
| | Texture export | The textures named in `TextureExportNames` as PNG; run it in a level |
| | Label clone experiment | Toggle: the experiment with copied shelf labels that Shelf Labels grew from; results in the log |
| Network | Session roster | The roster of the session in the log |
| | Mod message probe | A ping to the host, which answers every player |
| | Visible players | The players Steam sees, with their rich presence; on one computer these are the other game copies |
| | Join the first host | Joins the first visible player that offers a game, like accepting a Steam invite |
| | Steam self check | Checks the CatLib channel of Steam on this computer |
| UI | Notification preview | A sample message as a game notification |
| | Version badge preview | The version label as if the game were newer, then older, then the real check |
| | Stress mods on or off | Adds many fake mods to the Mods tab |
| Crash | Native crash, game thread | Access violation on the game thread. Press twice within 3 s |
| | Native crash, worker thread | Access violation on a new native thread. Press twice |
| | Managed crash | Unhandled .NET exception on a new thread. Press twice |
| | Hang on quit | Toggle: a 90 s hang while the game quits, to check the hang report |

All dumps go to `BepInEx/CatLib.Tests/Dumps`.

### From CatLib and the mods

| Group | Command | What it does |
|---|---|---|
| Translations | Translation report | Which texts every mod misses in every game language, and texts that would break |
| | Export for translators | Every text of every mod in the current language, ready to translate |
| | Reload translations | Reads the players' translation files again |
| Better Repair | Cardboard state | Stock, time of day and every repair table in the log |
| | Refill now | Refills the cardboard as a new day would. Host only |
| Boat Tweaks | Boat heights | Height limits of every boat and whether its stack is approved |
| Parcel Board | Parcels in the log | Every parcel with destination, marks, missing stamps, size and place, and every count |
| | Parcel sizes in the log | Shelf footprint and box size of one parcel of every size |
| Stack it! | Trace falls | Toggle: every fall of a parcel across a joint for 1.5 s; turn it on on every game |
| | Bridges in the log | Every parcel across a joint, the parcels under it and the cells it holds |
| | Parcel grids in the log | The grid on top of one parcel of every size and the height of its top |
| | Storages in the log | Every storage with its grid, limits and the tree of parcels on it |
| | Level tops in the log | The parcels of every storage grouped by the height of their tops |
| Too Late | Record game messages | Toggle: every network message of the game in the log |
| | Joining players in the log | Whether players can join now, the player limit and who is joining |
| | Snapshot in the log | The warehouse as a joining player would get it, in `BepInEx/cache/TooLate` |

## Settings

`BepInEx/config/catlib.tests.cfg`, or the Mods tab:

| Setting | Meaning |
|---|---|
| `[Run] OnMainMenu` | Run the tests the first time the main menu loads |
| `[Diagnostics] EntityDumpTypes` | Game types the entity dump looks for around the camera |
| `[Diagnostics] TextureExportNames` | Textures the texture export writes |
| `[Demo] …` | Demo settings to try live settings, and `NetworkModVersion` of the demo network mod: change it on one copy to see a mismatch in the lobby |

## Writing a test

```csharp
public sealed class MyRuleTest : TestCase
{
    public override string Suite => "MyMod";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        Assert.Equal(3, MyRules.Limit(2), "The limit grows by one");
        yield return Wait.Frames(1);
        context.Note("Anything worth reading in the report");
    }
}
```

- A test is found by type; `Suite` groups it in the report. A suite named after a mod is skipped when the mod is not installed.
- `Wait.Frames`, `Wait.Seconds` and `Wait.Until` let a test wait inside the game.
- Keep the rules of a mod in classes without Unity types, so most tests need no level.
  Check each rule once with a deliberately broken implementation to see that the test catches it.
