# Folding lists

`CatLib.UI.FoldoutList` is a list that folds into one header, built from the game's own UI pieces.
CatLib uses it for the players' mods in the lobby; a mod can use it for its own lists.

```csharp
var style = new FoldoutStyle
{
    TextTemplate = someGameText,
    HeaderSprite = paperSprite,
    BodySprite = cardSprite,
    ArrowSprite = arrowSprite
};
var list = FoldoutList.Create(parentTransform, style, "MyMod_List", new Vector2(0f, -44f));

list.Show(new FoldoutContent("Parcels", "12 in the scene", FoldoutTone.Normal,
    new[] { new FoldoutRow("Sort", null, "by size", onClick: ChangeSort) },
    new[]
    {
        new FoldoutSection("big", "Big parcels", "4", "fits", FoldoutTone.Good, new[]
        {
            new FoldoutRow("Giant", "2", "on shelves"),
            new FoldoutRow("Very long", "2", "on the floor", FoldoutTone.Warning)
        })
    }));
```

- The root is anchored to the top center of the parent, `offset` moves it down from there. Collapsed, only the header shows;
  a click on it opens the body below, a click on a section header folds that section. `Expanded` and `ExpandedChanged` control it from code.
- `Show` rebuilds only when the content differs, so it can be called every few frames with fresh data.
- A row has three columns: left, middle and right. `FoldoutTone` colors the right column and section statuses:
  `Good`, `Warning`, `Bad`, `Muted` or `Normal`. A row with `onClick` becomes clickable.
- With `ButtonTemplate` set to one of the game's buttons, a clickable row shows its right text on a copy of that button,
  with the game's own click sound and press effect. Without it, a clickable row gets an arrow on the left.
- `HeaderReferenceWidth` and `BodyReferenceWidth` are the widths at which the header and body sprites look right in the game.
  The sprites are then drawn nine-sliced: the edges keep their size and only the middle grows, so paper edges stay sharp.
- The body scrolls with the mouse wheel when it is taller than `MaxBodyHeight`.
- Sizes in `FoldoutStyle` are in the parent canvas units; the defaults fit a 1920x1080 reference canvas.
- `Destroy` removes it. Objects under a scene that unloads are removed by the game.
