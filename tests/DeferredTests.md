# Deferred tests

Checks that are not done yet because they need something we do not have now. Remove an entry once it is checked.

| Area | What to check | Why it waits |
|---|---|---|
| Too Late | 5 to 8 players: the limit setting lets the fifth in, the lobby shows "+1 player", the CatLib mods list in the lobby lists everyone | Needs five game copies |
| Mods tab, Steam Deck | A text setting opens the virtual keyboard in Big Picture and on Steam Deck, the keyboard is titled with the setting's name, the typed text is saved | No Steam Deck |
| Mods tab, gamepad | Moving through the mod list, the settings and the reset button with a real gamepad, sliders and text fields included | No gamepad; the automatic test passes |
| Quitting with players | The host quits from a level while a player is still in it, 3 to 5 times: no crash report, the log says `Steam is not shut down by the game`, the game closes at once and Steam shows it as not running any more. The player first and then the host was checked in 0.7.4: no crash, both closed at once instead of after 4 to 5 seconds | Needs a second game copy |
| Crash watcher 1.2.0, Steam | Turn on "Let the game shut Steam down" in the developer menu on the host and a second copy, quit from a level on each side a few times: when Steam crashes, the report says "Steam crashed while the game shut it down". Turn it off and quit again: no crash | Needs a second game copy |
| Purr!, 3 players | Three players with Purr!: the host hears both players, each player hears the other through the host, a player who leaves stops talking at once, a player without the mod hears nothing and nothing breaks | Needs three game copies with different Steam accounts |
