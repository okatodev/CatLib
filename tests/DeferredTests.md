# Deferred tests

Checks that are not done yet because they need something we do not have now. Remove an entry once it is checked.

| Area | What to check | Why it waits |
|---|---|---|
| Too Late | 5 to 8 players: the limit setting lets the fifth in, the lobby shows "+1 player", the CatLib mods list in the lobby lists everyone | Needs five game copies |
| Too Late, real copy | A player who joins late with Too Late and number 5 or more starts at their own spawn point, one without it at a point of players 1 to 4; player stubs set before the join are seen by that player; customers already waiting at a counter are there at once with their looks, request and the parcels they brought, and can be served by the late player; what players carry at the join is in their hands for the late player | Needs a second game copy |
| Mods tab, Steam Deck | A text setting opens the virtual keyboard in Big Picture and on Steam Deck, the keyboard is titled with the setting's name, the typed text is saved | No Steam Deck |
| Mods tab, gamepad | Moving through the mod list, the settings and the reset button with a real gamepad, sliders and text fields included | No gamepad; the automatic test passes |
| Developer menu, multiplayer | Spawn from a player: the parcel appears for everyone with a destination. Next step and Clear the piles on the host: the player sees the same piles and the open doors, the removed parcels disappear for them too | No second player now |
| Thunderstore | CatLib started from a mod manager runs the crash watcher from `plugins/CatLib-CrashWatcher` and its window shows after a crash | The mods load from a mod manager, the watcher is not checked yet |
