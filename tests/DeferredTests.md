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
| Thunderstore | A package from `Thunderstore-build` installed by a mod manager loads; the Mods tab shows its icon and the author from the package folder | Manifests are templates yet |
| Crash watcher | In normal play no `looks hung` line appears in `watcher.log` after the fix of the quit hang | Needs time in normal play |
