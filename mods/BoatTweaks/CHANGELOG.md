# Changelog

## 0.3.0

- Paused while not every player has Boat Tweaks: boats come like in the game with the game's heights. Needs CatLib 0.6.0.
- The red mark of the approved height follows the height multiplier, also on the boat at the dock.
- A piece of an own layout that an arriving parcel touches is left out whole, so a 2x2 square never becomes a pair and a single cell.
- Generated decks are made of whole pieces, mostly squares and pairs, like the game's decks.
- Every crate, bottle and lamp of the game's boats decorates blocked cells of its size, measured from its model: big 3x3 and 4x4 crates,
  long 2x5 crates, the giant 4x5 crate, squares, pairs and single cells. Rope coils and paddles are not used.
- Generated decks may have big 3x3 and long 2x5 crates where they fit; near the rails only long crates fit.
- Works with the game update of October 2026.

## 0.2.0

- Own layouts from files, picked at random, one after another or always one file.
- Saving the deck of the boat at the dock as an own layout (Ctrl+B); cells of arriving parcels are left out.
- Generated decks with a set density, near the rails or anywhere, rebuilt from the same seed on every player.
- Mixed mode picking the deck source for each boat by weight.
- Blockers of own and generated decks are decorated with the game's crates, bottles and lamps sized to their cells.
- For an own layout only boat variants whose arriving parcels miss it come; if none does, the closest ones, so the deck is the same every time.
- Nothing is changed while the game restarts.
- Blockers of own and generated decks are plain box colliders instead of copies of the game's objects.
- Decoration can be turned off per player.
- Height multipliers reach every boat in the scene, also on clients, and the game recomputes the approved height at once.
- The host decides the next deck and sends it to clients.

## 0.1.0

- Game layouts with allowed numbers and no repeats, one fixed game layout, an empty deck.
- Approved and maximum stack height multipliers.
