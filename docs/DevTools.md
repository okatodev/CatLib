# Developer menu

`CatLib.DevTools` gives mods one in-game panel for developer commands instead of a function key per tool.
The panel only opens when some mod has registered a command, so players without developer plugins never see it.

## Using it

The `` ` `` key (the key left of 1, `MenuHotkey` in section `[DevTools]` of CatLib's config) opens and closes the menu.

| Key | Action |
|---|---|
| Up, Down | select a command |
| Home, End | the first or the last command |
| Enter | run the selected command |
| 1-9 | run the command with that number |
| Left, Right or Page Up, Page Down | previous or next group |
| Esc | close |

The groups are listed on the left with the number of their commands, the commands of the selected group on the right,
toggles with their state. A long list scrolls with the selection and says how many lines are above and below.
Under the lists: the hint of the selected command, then the result of the last command with its time, green when it worked
and red when it failed.

The menu works without the mouse, so the camera keeps looking where it did: a dump of what the camera looks at
captures exactly that. Where the cursor is free, as in the menus, a click selects a group or runs a command.
The menu stays open after a command so several can be run in a row.

## Adding commands

```csharp
DevMenu.Command("Boat", "Save the deck", () =>
{
    var name = SaveDeck();
    return "saved as " + name;
}, "Saves the deck of the boat at the dock as an own layout.");

DevMenu.Toggle("Boat", "Show deck grid", () => showGrid, value => showGrid = value);
```

- The first argument is the group; groups appear in the order they were first used.
- A command may return a short text that is shown as its result; returning nothing shows "done".
- An exception in a command is caught, logged and shown as the result; the menu keeps working.
- A toggle shows its state next to its label and flips it when run.
- `DevMenu.Remove(item)` removes a command, for example when a tool is turned off.

Commands run on the main thread, in the frame the key was pressed. Long work should write its result to a file
and return where the file is.

## Drawing

The panel is drawn with the game's IMGUI and scaled with `GUI.matrix` to the screen height. The game build strips a lot of IMGUI:
`GUI.DrawTexture`, windows and the setters of font size, font style, word wrap and clipping are missing. The panel only uses what is left:
filled areas are `GUI.Box` with an empty `GUIStyle` whose background is `Texture2D.whiteTexture`, tinted with `GUI.color`
(a copy of the skin's box style keeps its borders and draws thin lines thick);
texts are `GUI.Label` with copies of the label style, aligned with `alignment` and coloured with `normal.textColor`.
Lines that do not fit are cut with "…", measured with `GUIStyle.CalcMinMaxWidth`.
Commands started with a click run in the next `Update`, not while the panel is drawn.
