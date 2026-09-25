# Localization

`CatLib.Localization` gives every mod a catalog of translated texts and uses it for the mod's entry on the Mods tab.

## Catalogs

```csharp
var texts = CatLocalization.For(this);
texts.LoadEmbedded(typeof(MyPlugin).Assembly, "MyMod.Lang.");
texts.LoadDirectory(Path.Combine(Paths.ConfigPath, "MyMod", "lang"));
var message = texts.Format("message.saved", fileName);
var russian = texts.FormatFor("ru", "message.saved", fileName);
```

`CatSettings.Texts` is the same catalog as `CatLocalization.For(ownerId)`.

A language file is a JSON object named after the language, for example `ru.json`.
Nested objects become dotted keys, so these two files are equal:

```json
{ "setting": { "Height.Scale": "Height" } }
{ "setting.Height.Scale": "Height" }
```

Lookups try the exact language, then its base language, then English: `ru-RU` → `ru` → `en`.
`Find` returns `null` when nothing matches, `Get` returns the key, `Has` checks one language without falling back.
The current language follows the game's language setting (`CatLanguage.Current`).

Embedded files are added to the mod project like this:

```xml
<EmbeddedResource Include="Lang\*.json" LogicalName="MyMod.Lang.%(Filename).json" />
```

## Keys used by the Mods tab

| Key | Used for | Without a translation |
|---|---|---|
| `mod.name` | name in the mod list and on the card | plugin name |
| `section.<Section>` | section tape | section name split into words |
| `setting.<Section>.<Key>` | row label | `Label(...)` from code, else the key split into words |
| `setting.<Section>.<Key>.description` | context line | description from the declaration |
| `enum.<EnumType>.<Value>` | dropdown option | value name split into words |

A test that declares the mod's settings in a sandbox and checks every key with `Has` for each language
keeps translations complete when settings are added.
