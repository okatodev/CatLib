# Developer menu

`CatLib.DevTools` gives mods one in-game panel for developer commands instead of a function key per tool.
The panel only opens when some mod has registered a command, so players without developer plugins never see it.

## Using it

The `` ` `` key (the key left of 1, `MenuHotkey` in section `[DevTools]` of CatLib's config) opens and closes the menu.

| Key | Action |
|---|---|
| Up, Down | select a command |
| Enter | run the selected command |
| 1-9 | run the command with that number |
| Left, Right | previous or next group |
| Esc | close |

The menu works without the mouse, so the camera keeps looking where it did: a dump of what the camera looks at
captures exactly that. The menu stays open after a command so several can be run in a row.
The line under the list shows the hint of the selected command, the line below it the result of the last one.

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

The panel is drawn with the game's IMGUI through `GUI.Box` and `GUI.Label` and scaled with `GUI.matrix` to the screen height.
Windows, textures and font setters of IMGUI are stripped from the game build and are not used.
