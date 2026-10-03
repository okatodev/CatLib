# Deferred tests

Checks that are not done yet because they need something we do not have now. Remove an entry once it is checked.

| Area | What to check | Why it waits |
|---|---|---|
| Better Repair, multiplayer | On a new day the cardboard stock is refilled for the host and the client by the setting | Not run yet |
| Game, multiplayer | The client quits to the menu while the end-of-day summary is open; the host closes the summary and plays on | Not run yet |
| Late join | A client joins a game in progress and gets the labels, placements, stands, the cardboard stock and the next boat deck of the host | Late join does not exist yet |
| Mods tab, Steam Deck | A text setting opens the virtual keyboard in Big Picture and on Steam Deck, the keyboard is titled with the setting's name, the typed text is saved | No Steam Deck |
| Mods tab, gamepad | Moving through the mod list, the settings and the reset button with a real gamepad, sliders and text fields included | No gamepad; the automatic test passes |
| Boat Tweaks | A generated deck "anywhere" gets a big 3x3 crate on its piece | Rare, not seen in a few boats yet |
| Boat Tweaks | An own layout with a 3x3 block gets one big crate there, not smaller ones | Not run yet |
| Thunderstore | A package from `Thunderstore-build` installed by a mod manager loads; the Mods tab shows its icon and the author from the package folder; CatLib starts the crash watcher from `plugins/CatLib-CrashWatcher` | Packages are not uploaded yet |
| Crash watcher | In normal play no `looks hung` line appears in `watcher.log` after the fix of the quit hang | Needs time in normal play |
| Parcel Board | In a level with Giant parcels, **Parcel sizes in the log** gives their footprint and box; the drawn Giant box (guessed 4x4x4 cells) matches it | No Giant parcels in the levels played so far |
| Parcel Board | The test run started in a level: `BoardSceneTest` checks the board, the size icons and that the sizes table is as tall as the destinations | The last runs were started in the menu, where the test only notes that it needs a level |
