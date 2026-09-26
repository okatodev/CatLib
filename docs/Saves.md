# Mod data in game saves

`CatLib.Saves` keeps a mod's data per game save. The game save itself is never opened or changed.

## Where the data lives

```
LocalLow/Maracas Studio/CatMailCo/
  GameSaves/                                   the game's saves, not touched
    GameSave_20260923_201740_Cat-Mail-Co.bin
  CatLibSaves/                                 mod data
    GameSave_20260923_201740_Cat-Mail-Co/
      catlib.labels.json                       current data of one mod
      catlib.labels.json.bak                   the previous version
```

The folder sits next to the game saves, not inside them, so the game's save list never sees foreign files,
and the data survives reinstalling mods or switching mod manager profiles.
Moving a save to another computer means moving its `CatLibSaves` folder too.

## Using it

```csharp
var save = CatSaves.For(this, dataVersion: 1);

save.Loaded += () =>
{
    var picture = save.Get("label/390/1", 0);
    ApplyPicture(picture);
};

save.Saving += () => save.Set("label/390/1", CurrentPicture());
```

- `CatSaves.For` is called once in `Load`. The handle stays valid for the whole game; it is attached to whichever save the player selects.
- `Loaded` runs when a save is selected in the main menu, before the level loads, and when a mod registers while a save is selected.
- `Set` and `Remove` change data in memory and return `false` when there is nothing to write to (`State` is not `Ready`).
- `Saving` runs when the game starts saving; set values that are only computed on demand there.
- `Get<T>(key, fallback)` returns the fallback when the key is missing or holds a value of another type.
- Values are anything `System.Text.Json` can write: numbers, text, lists, dictionaries, plain classes.
- `CatSaves.Committed` reports after every game save which mods were written and which failed.

## When data is written

Data follows the game's save, not the other way round:

| Game | Mod data |
|---|---|
| `GameSavingStarted` | `Saving` runs, a snapshot of every changed mod is taken |
| `SuccessfullySaved` | the snapshot is written |
| `UnsuccessfullySaved` | the snapshot is dropped, changes wait for the next save |
| player leaves without saving | changes since the last save are lost, like the game's own progress |

A mod without changes writes nothing, so mods that never store data leave no files.

## Safety rules

- **Atomic writes.** Each file is written to `.json.tmp` and swapped in, the previous version becomes `.json.bak`.
  A crash during writing leaves either the old or the new file, never half of one.
- **Damaged files are kept.** A file that is not valid JSON is renamed to `.json.corrupt-<time>` and the backup is used;
  the main file is rebuilt from it on the next save. The backup is one game save older, so changes of the last save are lost.
  If both are damaged the mod starts empty, and nothing is deleted.
- **Unreadable files are left alone.** A file that exists but cannot be read, for example while another program holds it,
  makes the mod read only for this session.
- **Newer data is never overwritten.** Each file records the CatLib format and the mod's `dataVersion`.
  If either is newer than the running code, the mod gets no values, `State` is `ReadOnly` and the file is not touched.
  Older data is loaded as usual and `StoredDataVersion` tells the mod which version it reads, so it can convert.
- **A new save does not inherit old data.** If the game starts a new save under a name that already has mod data,
  that data is not loaded, and on the first successful save its folder is renamed to `<save>.archived-<time>`.
- **Only the host writes.** On a client the handle is detached; the host's data reaches clients through the mod's own messages.
- **Mods are isolated.** An exception in one mod's handler or a failed write of one file does not affect other mods.

## States

| `State` | Meaning |
|---|---|
| `NoSave` | main menu or client, nothing to read or write |
| `Ready` | values can be read and changed |
| `ReadOnly` | the stored data is newer than the mod or cannot be read; nothing is written |

`LastRead` (`Missing`, `Loaded`, `LoadedFromBackup`, `Corrupt`, `Unreadable`, `TooNew`) and `LastReadDetail` say what happened when the data was loaded.
