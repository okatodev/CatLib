# Deferred tests

Checks that are not done yet because they need something we do not have now. Remove an entry once it is checked.

| Area | What to check | Why it waits |
|---|---|---|
| Too Late | 5 to 8 players: the limit setting lets the fifth in, the lobby shows "+1 player", the CatLib mods list in the lobby lists everyone | Needs five game copies |
| Mods tab, Steam Deck | A text setting opens the virtual keyboard in Big Picture and on Steam Deck, the keyboard is titled with the setting's name, the typed text is saved | No Steam Deck |
| Mods tab, gamepad | Moving through the mod list, the settings and the reset button with a real gamepad, sliders and text fields included | No gamepad; the automatic test passes |
| Thunderstore | CatLib started from a mod manager runs the crash watcher from `plugins/CatLib-CrashWatcher` and its window shows after a crash | The mods load from a mod manager, the watcher is not checked yet |
