# HUD and parcels

Mods that show something on the screen during a level, and mods that need to know which parcels are in the level.
Parcel Board is built on both.

## Parcels

`CatParcels.Read()` in `CatLib.Game` returns every parcel the game has registered, as plain `ParcelInfo` records:

| Field | Meaning |
|---|---|
| `Region` | destination; `CatsIsland` is the home region, `IsInbound` and `IsOutbound` tell arrivals from parcels to send |
| `Storage`, `Behavior` | the storage marks (Frozen, Cold, Hot, Dark, Bright) and behaviour marks (Fragile, Heavy, ContactForbidden, Lover, Corrupted); `Has(...)` checks one |
| `Size`, `Weight` | `PackageSize` and `ParcelWeightClass` |
| `Footprint` | the cells the parcel takes on a shelf, `Width` by `Depth`; `Text()` gives `2×2` |
| `IsDamaged` | damaged, needs the repair table |
| `MissingStamps`, `NeedsStamps` | for a parcel to send, the stamps it still misses: `Destination`, `Weight` (fewer weight stamps than its weight class asks for), `Storage`, `Behavior` (the stamp of its mark) |
| `Place` | `Carried`, `Stored`, `Boat`, `Customer` or `Loose`, from the root of the stack the parcel stands in |

`Read()` builds a fresh list, so call it a few times a second, not every frame. It works on the host and on clients:
the game sends every parcel's destination, marks and size to every player. Only the host's `ParcelManager` lists the parcels
(`EntityParcel.Start` registers them on the server only), so on the other players `Read()` finds the parcels in the scene.
`RegionIcon`, `ConstraintIcon` and `RegionName` give the game's own stamp sprites and the translated region names.

Missing stamps are read from the stamps on the parcel, not from the game's own checks: `IsDestinationValid` shows the wrong-stamp feedback
on the parcel, and the mark and weight checks only decide the bonus of the day's recap.

## HUD layer

`HudLayer.Root` is a full-screen `RectTransform` on the game's HUD canvas during a level, drawn over the HUD,
or `null` in the main menu. It fades out while the pause menu, the settings, the day recap, credits or the debug menus are open
(`HudLayer.IsGameMenuOpen`). It never takes clicks. `HudLayer.Created` fires for every new level.
`HudLayer.CreateText` makes a text in the game's font.

## Count tables

`CountTable` is the same idea as a table: a column per list with an icon (or a title) and a total, a row per item with an icon
or a label, and a count in every cell. Empty cells stay empty. Parcel Board draws its whole board with it.
A row can have both an icon and a label; with `mutedLabel` the label is smaller and fainter, for a hint next to the icon.

Two tables side by side line up row by row: `HeightOf(content)` gives the height a content needs, and `Show(content, growUp, minHeight)`
stretches the plate to the taller one with the rows kept next to the header. A long table can wrap into columns of `rowsPerBlock` rows
(the last argument of `CountTableContent`); a single header then stands over all of them. Parcel Board wraps its sizes this way
to the height of the destinations.

```csharp
var table = CountTable.Create(HudLayer.Root, "MyMod_Table");
table.Show(new CountTableContent(
    new[] { new TableColumn(allIcon, string.Empty, "12"), new TableColumn(fragileIcon, string.Empty, "3") },
    new[] { new TableRow(regionIcon, string.Empty, new[] { "7", "2" }), new TableRow(otherIcon, string.Empty, new[] { "5", "1" }, separated: true) },
    expanded: true));
```

## Count lists

`CountList` in `CatLib.UI` is a small list on a rounded plate: a header with an icon, a name and a count, and rows below it.

```csharp
var list = CountList.Create(HudLayer.Root, "MyMod_List", new CountListStyle { PlateAlpha = 0.5f });
list.Show(new CountListContent(
    new CountRow(icon, "Fragile", "4"),
    new[] { new CountRow(stampIcon, string.Empty, "3"), new CountRow(otherIcon, string.Empty, "1", separated: true) },
    expanded: true));
```

- `Show` rebuilds only when the content changes. With `growUp` the rows open above the header, for lists at the bottom of the screen.
- The size follows the texts; `Size` gives it after `Show`. Place `Rect` yourself.
- `CountListStyle`, shared by lists and tables, holds the sizes, the colours, `PlateAlpha` and `Scale`; call `ApplyPlate` after changing the plate.
  By default texts are light with a dark outline, like the game's own hints, on a dark plate that is almost clear.
- `UiSprites.RoundedPlate(radius)` is the plate sprite, nine-sliced, for other widgets too.
  `UiSprites.FromPng` and `UiSprites.FromResource(assembly, name)` turn a PNG, for example one embedded in a mod, into a sprite.
