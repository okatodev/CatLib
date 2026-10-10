# Deferred tests

Checks that are not done yet because they need something we do not have now. Remove an entry once it is checked.

| Area | What to check | Why it waits |
|---|---|---|
| Too Late | 5 to 8 players: the limit setting lets the fifth in, the lobby shows "+1 player", the CatLib mods list in the lobby lists everyone | Needs five game copies |
| Too Late | After the fix for the game without the Steam network: with the internet on, a Steam player still joins a game in progress, the host's log has `Your game started, players who connect from now on join the game in progress` | Multiplayer tests |
| Mods tab, Steam Deck | A text setting opens the virtual keyboard in Big Picture and on Steam Deck, the keyboard is titled with the setting's name, the typed text is saved | No Steam Deck |
| Mods tab, gamepad | Moving through the mod list, the settings and the reset button with a real gamepad, sliders and text fields included | No gamepad; the automatic test passes |
| Thunderstore | CatLib started from a mod manager runs the crash watcher from `plugins/CatLib-CrashWatcher` and its window shows after a crash | The mods load from a mod manager, the watcher is not checked yet |
| Crash watcher | In normal play no `looks hung` line appears in `watcher.log` after the fix of the quit hang | Needs time in normal play |
| Game, multiplayer | A client joins from the lobby into a big save (about 780 entities): no stack overflow. Once the client crashed with 0xC00000FD on the game thread a second after the second entity sync, inside the game's runtime; the dump was lost, the crash watcher 1.0.2 keeps it now | Rare, seen once; send the crash folder with `crash.dmp` if it happens again |
| Game, multiplayer | The host quits a second or two after a player left, five times with mods: no crash in `steamclient64.dll`, the log has `Before Steam shuts down` and `Steam shuts down after … of waiting` with the session state. The same ten times without BepInEx, to see whether the game itself crashes | Once crashed with CatLib 0.7.0 after the fixed 5 s wait, in the WebRTC thread of Steam |
