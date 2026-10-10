# Changelog

## 0.1.0

Needs CatLib 0.7.1.

- Packs of your own stamps: a folder in `BepInEx/plugins` with `stamps.txt` and PNG or JPG images in `decorative` and `weight`.
  The stamps join the game's decorative and weight stamps in the stamp menu; a weight stamp counts as one unit of weight.
- Images are cut to their visible part (a JPG has no transparency and stays a rectangle), made at most 512 pixels and get a white border like the game's stickers; `outline: no` keeps them as they are.
- Every pack has its own card on the Mods tab with its stamps, a toggle, its folder and a button that builds a Thunderstore package of it.
- A button makes a new pack with two sample stamps, two cats of the game, and opens its folder; another reads the packs again without restarting the game.
- Every player needs the same packs: a pack that someone in the game does not have is paused and its stamps are hidden.
- Stamps stay on parcels in saves and are seen by every player who has the pack.
