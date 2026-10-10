# Assets: images and content packs

CatLib loads files that players and mod authors put next to the game: images, and **content packs**,
folders of files with a small text file that a mod reads, like the stamp packs of [Custom Stamps](../mods/CustomStamps/README.md).
A pack gets its own card on the Mods tab with previews of its images, and one button builds a Thunderstore package of it.

| I want to… | Use |
|---|---|
| read a PNG or JPG, trim, scale, pad or outline it | `ImageData` |
| take a picture of the game, a sprite or a texture | `GameImages` |
| make a texture or a sprite from it | `ImageData.ToTexture`, `ImageData.ToSprite` |
| read folders of a kind of pack from `BepInEx/plugins` | `ContentPackKind`, `ContentPacks.Load` |
| let a player make a new pack | `ContentPacks.Create` |
| make a Thunderstore package of a pack | the Build button, or `ContentPacks.Build` |
| show images or a button on a mod's card | `CatSettings.Gallery`, `CatSettings.Button` |
| write a package of any folder | `ThunderstorePackage.Build` |

## Images

`ImageData` is an image in memory: `Width`, `Height` and `Rgba`, four bytes a pixel, rows from the top.
Everything on it is plain .NET, so it runs on any thread; only `ToTexture` and `ToSprite` need the main thread.

```csharp
if (!ImageData.TryRead(path, out var image, out var problem))
{
    log.Warning($"{path} is skipped: {problem}");
    return;
}

var stamp = image.Trimmed().Scaled(512).Outlined(12).PaddedToAspect(1f);
var texture = stamp.ToTexture("My stamp");
var sprite = ImageData.ToSprite(texture);
```

| Member | Does |
|---|---|
| `TryRead(path, out image, out problem)`, `TryDecode(bytes, out image, out problem)` | Reads a PNG (any color type, 8 or 16 bits, palettes, interlaced) or a JPG (baseline and progressive, gray, color and CMYK, turned by its EXIF orientation); at most 4096×4096 and 16 MiB |
| `VisibleBounds()`, `Trimmed(margin)` | The part that is not transparent; the image cut to it |
| `Scaled(maxSide)`, `Resized(w, h)`, `Fitted(w, h)` | Smaller with an area average, larger with bilinear filtering, alpha kept clean at the edges |
| `Padded(w, h)`, `PaddedToAspect(aspect)` | Clear space around the image, which stays in the middle |
| `Outlined(thickness)` | A border around the shape, white by default, like the game's stickers |
| `Collage(images, side, gap)` | Up to nine images in a grid on a square, for icons |
| `ToPng()` | A PNG file of the image |
| `ToTexture(name, mipmaps)` | A `Texture2D` with mipmaps, clamped, not unloaded between scenes |

> [!NOTE]
> The game cannot decode images itself in this build, so CatLib reads PNG and JPG with its own decoders. GIF, BMP, WebP and other formats are not read.
> A JPG has no transparent pixels, so `Trimmed` keeps it whole.

### Images of the game

`GameImages` reads what the game has loaded now, on the main thread: `FindSprite(name)` finds a sprite by its name,
`Read(sprite)` and `Read(texture)` copy its pixels from the video card into an `ImageData`, `TryRead(name, out image, out problem)` does both.
The names are in the sprite export of the [developer menu](../tests/CatLib.Tests/README.md#developer-menu); a sprite is found only while
its scene or menu keeps it loaded. An atlas texture is often loaded when only some of its sprites are:
`FindTexture(name)` and `TryReadArea(texture, x, y, width, height, out image, out problem)` read a part of it, `y` counted from the bottom
like in the export's `index.txt`.

```csharp
if (GameImages.TryRead("sp_Cat_Intro_01", out var cat, out var problem))
{
    File.WriteAllBytes(path, cat.Trimmed().ToPng());
}
```

## Content packs

A pack is a folder anywhere in `BepInEx/plugins`, up to three folders deep, with a file the mod names, `stamps.txt` for example.
Thunderstore and mod managers install packages into `BepInEx/plugins/<Team-Package>`, so a pack published there is found the same way.

```csharp
var kind = new ContentPackKind(PluginMeta.Guid, "stamps.txt", "customstamps")
{
    Policy = SessionPolicy.RequiredOnAll,
    Template = "name: {name}\nversion: 1.0.0\nauthor:\ndescription:\n"
};
kind.Dependencies.Add("CatLib-CustomStamps-" + PluginMeta.Version);
kind.Folders.Add("decorative");

foreach (var pack in ContentPacks.Load(kind, log))
{
    ReadImages(pack.Folder);
}
```

The pack file has a `key: value` per line; `key = value` works too, `#` starts a note, an indented line continues the value above.
Keys are found whatever their case.

| Key | Meaning |
|---|---|
| `name` | The name on the card. Without it the folder name |
| `version` | `Major.Minor.Patch`, `1.0.0` by default |
| `author` | Shown on the card and in the README of the package |
| `description` | The card's text and the package description, at most 250 characters on Thunderstore |
| `website` | The package's website, empty by default |
| `package` | The package name if `name` has no Latin letters; otherwise made from `name`, then from the folder |

Every other key is the mod's own, read from `pack.File`.

What `ContentPacks.Load` does for every pack:

- **A card on the Mods tab**, right below the mod that reads it, with the pack's icon, version, author and description,
  a toggle to turn the pack off, a button that opens its folder and one that builds its package.
  Problems, such as a wrong version or images the mod could not read, are added at the end of the description.
- **A place in the session check.** The pack is declared like a mod, with the id `<prefix>.<package name>` and the kind's policy.
  With `RequiredOnAll` a pack that not every player has is paused for the session: `pack.IsActive` is false, and the mod should hide its content.
  A pack the player turned off counts as not installed.
- **An icon.** `icon.png` in the folder is used as it is. Without it the mod can give one with `pack.UseGeneratedIcon(image)`,
  for example `ImageData.Collage` of the pack's images.

`ContentPacks.Load` again reads every pack anew and replaces the cards. `kind.PackChanged` comes when a pack is turned on or off;
for paused packs listen to `CatNetwork.ActiveModsChanged`.

Texts on the card: CatLib translates its own rows. The mod adds its own with `ContentPacks.CopyTexts(catalog, "pack.", pack)`,
which copies every key of the mod's translations that starts with `pack.` into the pack's card without the prefix,
so `pack.setting.Stamps.Weight` in `Lang/en.json` becomes the label of the gallery `Stamps`/`Weight`.

### New packs

`ContentPacks.Create(kind, name)` makes `BepInEx/plugins/<Name>` (a number is added if it exists), the kind's folders and the pack file
from `kind.Template` with `{name}` filled in, and returns the folder. Put sample files in it and call `Load` to show the new card.

### Packages

The Build button, or `ContentPacks.Build(pack)`, writes `BepInEx/CatLib/Packages/<Package>-<version>.zip` and shows it in Explorer:

- `manifest.json` with the name, version, website, description and the kind's dependencies;
- `README.md` with the name, description, the text of `kind.Readme` and the author;
- `icon.png`, 256×256, from the pack's own icon resized if needed, or from the generated one;
- every file of the folder, without hidden files and folders, old zips, `Thumbs.db` and `desktop.ini`.

The zip goes to Thunderstore as it is: Upload, pick the team and the community. A package is refused, with the reason on the Mods tab,
when its name has no Latin letters or digits, its version is not `1.0.0`-like, its description is empty or there is no icon.

`ThunderstorePackage` does the same for any folder: `PackageName` makes a valid name, `Manifest` writes the JSON,
`Check` lists problems, `Build` writes the zip.

## Mods tab items

Besides settings, a card can show images and buttons. They are not saved anywhere and do not count as settings.

```csharp
var gallery = settings.Gallery("Stamps", "Decorative");
gallery.SetImages(sprites);

settings.Button("Packs", "Reload", () => library.Load());
```

| Item | Shows |
|---|---|
| `MenuGallery` | The label and the images in rows of 72×72, at most 60; a text when it is empty |
| `MenuButton` | The label on the left and a button like the game's key binding buttons on the right, reachable with a gamepad like any row |

Items go into the sections of the card after the settings of the same section. Their texts come from the owner's translations,
like the texts of settings: `setting.<Section>.<Key>` for the label, `.description` for the hint at the bottom of the tab,
`.button` for the caption of a button and `.empty` for an empty gallery; without them `Label`, `Hint` and `Caption`, then the key.

A card is listed when it has a visible setting or an item. Two more properties shape the list:

- `ParentId` puts a card right after another one, as packs follow their mod;
- `Summary` replaces "3 settings" on the second line of the list and on the card, for example "12 stamps".
