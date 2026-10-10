# Custom Stamps

Packs of stamp images that players add to the game's decorative and weight stamps. The mod itself is `ClientOnly`;
every pack is `RequiredOnAll`, so a pack that not every player has is paused for the session and its stamps are hidden.
What players read is in the [Thunderstore README](Thunderstore/README.md); this page is about how it works.
The packs are [content packs](../../docs/Assets.md#content-packs) of CatLib: their cards, previews and packages come from there.

## A pack

```
BepInEx/plugins/My_Stamps/
├── stamps.txt        name, version, author, description, outline
├── icon.png          optional; without it the icon is a collage of the first four stamps
├── decorative/       PNG or JPG images, also loose images next to stamps.txt
└── weight/           PNG or JPG images, each one unit of weight
```

Images are read on a background thread: cut to their visible part, made at most 512 pixels with a margin,
outlined in white unless `outline: no`, and padded to a square. Other image files (GIF, BMP, WebP…), more than 64 of a kind
or two images with the same name are listed on the pack's card.

A new pack gets two sample stamps, the cats `sp_Cat_Intro_01` and `sp_Cat_Intro_02` read from the game with `GameImages`:
as sprites, or as their part of the atlas `sp_UI_Elements_01`, which the main menu keeps loaded without these sprites.
When neither works, the copies in `Template` are used.

Every custom stamp shows its picture on a copy of the game's stamp mesh: the game's mesh shows only the part of the stamp atlas
that holds its own picture, so the copy stretches that part to the whole custom picture. The mesh is not readable by the CPU in the game,
so its vertices are read from the video card, like Shelf Labels does. When that fails, a square of the same size with two sides is used,
and the log says why.

## How the game's stamps work

Observed on CMC 1.01.00.1737.

- `GameManager` holds the stamps of each category: `DestinationStamps`, `WeightStamps`, `ConstraintStamps`, `DecorativeStamps`.
  A `StampData` is a ScriptableObject with a prefab (`StampHelper`, a mesh with a material), a preview sprite for the menu and a random tilt.
  `StampDataWeight` (its `StampType` is `Weight`) makes a stamp count as one unit of weight.
- The stamp menu has a `StampSelectionCategoryInterface` per category. `Populate()` clears it and adds the unlocked stamps
  with `InstantiateStampItem(StampData)`.
- A player's stamp is placed with `EntityInteractableStamp.AddStamp` and sent to the others as the name of its `StampData`,
  with its local position and rotation. A save keeps the same name. The others and a loaded save find the stamp again with
  `Addressables.LoadAssetAsync<StampData>(name)` in `AddStampFromNetwork`.

## What the mod does

- **Stamps.** The first time a level needs them, every custom stamp becomes a copy of the first decorative or weight `StampData`
  of the game, named `CustomStamps/<pack>/<kind>/<image>`, with a copy of its prefab kept in an inactive object that survives scene loads.
  On the copy's materials every texture that holds the game stamp's image is replaced with the custom one,
  in the start material, the feedback material of the stamping animation and every renderer of the prefab.
  `LogOutput.log` names the material and the texture properties the first time.
- **Menu.** A postfix of `Populate()` adds the stamps of active packs to the decorative and weight categories.
  When packs are read again, turned on or off, or paused for a session, both categories are filled again,
  and a selected stamp of a paused pack is cleared.
- **Network and saves.** A prefix of `AddStampFromNetwork` takes names that start with `CustomStamps/`: the stamp is placed
  the way the game places a loaded one, with its feedback, identifier and events, and the game's Addressables lookup is skipped.
  A stamp of a pack this game does not have is skipped quietly, with one log line per name.

## Developer menu

- **Game stamps in the log**: the prefab, materials and textures of the game's decorative and weight stamps; open a level first.
- **Custom stamps in the log**: every custom stamp, its size and whether it is active.
