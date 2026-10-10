# Custom Stamps

Put your own stamps on parcels. Drop PNG or JPG images into a folder, and they show up in the game's stamp menu,
next to the decorative and weight stamps.

## Make a pack

1. Open Settings → Mods → Custom Stamps and press **Create** next to "New stamp pack".
   A folder opens with two sample stamps in it: two cats of the game.
2. Put your PNG or JPG images into `decorative` for decorative stamps and into `weight` for weight stamps.
   A weight stamp counts as one unit of weight, like the game's own.
3. Press **Reload** next to "Reload packs". Your pack gets its own card on the Mods tab with every stamp in it.

Transparent edges are cut off, and each stamp gets a white border like the game's stickers.
Write `outline: no` in `stamps.txt` to keep your images without it.

The pack's name, version, author and description are in `stamps.txt`:

```
name: My Stamps
version: 1.0.0
author: Me
description: My stamps for Cat Mail Co.
outline: yes
```

## Share it

Press **Build** on the pack's card. A zip ready for Thunderstore appears in `BepInEx/CatLib/Packages`:
upload it on thunderstore.io under your team. Other players install it with a mod manager like any mod.
The sample cats are pictures of the game: delete them from your pack before you publish it.

## Playing with friends

Every player needs the same pack to see its stamps. If someone in the game does not have it, its stamps
are hidden from the menu for that game, and the stamps already on parcels are seen only by those who have the pack.
Turn a pack off on its card if you do not want to use it for a while: its stamps leave the stamp menu,
and the stamps already on parcels stay, so your save does not lose them.

## Saves

Stamps stay on parcels in your save. A stamp is found by its pack and file name:
if you remove the pack, rename it or rename the image, that stamp disappears from the parcel the next time the save loads.

## Limits

- PNG and JPG images, up to 4096×4096; bigger stamps are made smaller to 512 pixels.
- A JPG has no transparent parts, so it becomes a rectangular stamp. Use a PNG with a transparent background for a stamp of any shape.
- Up to 64 decorative and 64 weight stamps in one pack.
- Custom stamps are available from the start of the game.

## Found a bug?

Report it on [GitHub](https://github.com/okatodev/CatLib/issues) with `BepInEx/LogOutput.log` and, if you can, the pack.
