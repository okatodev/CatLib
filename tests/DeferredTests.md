# Deferred tests

Checks that are not done yet because they need something we do not have now. Remove an entry once it is checked.

| Area | What to check | Why it waits |
|---|---|---|
| Too Late | A client joins a game in progress and gets the labels, placements, stands, the cardboard stock and the next boat deck of the host | Not run yet |
| Too Late | A second player joins during the day: sees the warehouse as on the host, parcels moved while they loaded are where the host has them, their counter opens for everyone | Not run yet |
| Too Late | A player connects during the day's results: waits in the lobby, loads when the next part of the day starts | Not run yet |
| Too Late | A player joins, leaves and joins again in the same day; a player leaves while loading | Not run yet |
| Too Late | 5 to 8 players: the limit setting lets the fifth in, the lobby shows "+1 player", the CatLib mods list in the lobby lists everyone | Needs five game copies |
| Too Late | A player without mods joins a host with Too Late | Not run yet |
| Too Late | The host quits or goes to the menu a second or two after a player left: no crash in `steamclient64.dll`, the log says CatLib waits for Steam | Checked without Too Late only |
| Mods tab, Steam Deck | A text setting opens the virtual keyboard in Big Picture and on Steam Deck, the keyboard is titled with the setting's name, the typed text is saved | No Steam Deck |
| Mods tab, gamepad | Moving through the mod list, the settings and the reset button with a real gamepad, sliders and text fields included | No gamepad; the automatic test passes |
| Thunderstore | A package from `Thunderstore-build` installed by a mod manager loads; the Mods tab shows its icon and the author from the package folder; CatLib starts the crash watcher from `plugins/CatLib-CrashWatcher` | Packages are not uploaded yet |
| Crash watcher | In normal play no `looks hung` line appears in `watcher.log` after the fix of the quit hang | Needs time in normal play |
| Game, multiplayer | A client joins from the lobby into a big save (about 780 entities): no stack overflow. Once the client crashed with 0xC00000FD on the game thread a second after the second entity sync, inside the game's runtime; the dump was lost, the crash watcher 1.0.2 keeps it now | Rare, seen once; send the crash folder with `crash.dmp` if it happens again |
