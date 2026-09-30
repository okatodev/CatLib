# Localization

`CatLib.Localization` gives every mod a catalog of translated texts, loads it by itself and uses it for the mod's entry on the Mods tab.
Players can add or fix a translation with a file, without rebuilding the mod.

## Texts of a mod

Put one JSON file per language into `Lang/` next to the project file: `Lang/en.json`, `Lang/ru.json`, `Lang/de.json`.
The build embeds them, and `CatSettings.For(this)` or `CatLocalization.For(this)` loads them. No code is needed.

```csharp
var texts = settings.Texts;
Notifications.Show(texts.Format("message.saved", fileName));
Notifications.Show(texts.Plural("parcels", count));
var title = texts.Text("window.title");
label.text = title.Value;
```

A file is a JSON object named after the language. Nested objects become dotted keys, so these two files are equal:

```json
{ "setting": { "Height.Scale": "Height" } }
{ "setting.Height.Scale": "Height" }
```

Comments (`//`) and trailing commas are allowed.

| Call | Gives |
|---|---|
| `Get(key)` | text in the game language, the key itself when nothing matches |
| `Format(key, args)` | text with `{0}`, `{1}` filled in |
| `Plural(key, count, args)` | the plural form for `count`, `{0}` is the count and `{1}` the first of `args` |
| `Text(key)` | a `LocalText` handle to keep in a field; `Value` is always the current language |
| `Find(key, language)` | text with fallbacks, `null` when nothing matches |
| `FindExact(key, language)` | text of that one language, no fallbacks |
| `Has(key, language)` | whether that one language has the key |
| `FormatFor`, `PluralFor` | the same in a given language |

Lookups try the exact language, then its base language, then English: `pt-br` → `pt` → `en`.
A translation that cannot be filled in (a `{2}` where English has only two values, a stray brace) never throws:
the English text is shown instead and the log names the text once.

## Plural forms

A plural text is an object with a form per plural category of the language:

```json
{ "parcels": { "one": "{0} parcel", "other": "{0} parcels" } }
{ "parcels": { "one": "{0} посылка", "few": "{0} посылки", "many": "{0} посылок" } }
{ "parcels": { "other": "小包{0}個" } }
```

The categories follow the Unicode plural rules for whole numbers. A language needs these forms:
English, German, French, Spanish, Italian, Portuguese, Turkish `one` and `other`; Russian, Ukrainian and Polish
`one`, `few` and `many`; Czech and Slovak `one`, `few`, `other`; Japanese, Chinese, Korean, Thai and Vietnamese only `other`;
Arabic all six. `PluralRules.Categories(language)` lists them.
A missing form falls back to `other`, then to the next language in the chain.

## The game language

- `CatLanguage.Current` is the game's language code, `CatLanguage.GameLanguages` every language the game offers.
- `CatLanguage.Changed` fires when the player switches the language; the Mods tab updates by itself.
- `CatLanguage.Game(term)` gives the game's own translation of one of its terms, for example `Settings/Revert`.
  Words the game already has need no translation of their own.

## Translation files of players

Files in `BepInEx/config/CatLib/Translations/<mod id>/<language>.json` win over the texts built into the mod and can add
languages the mod does not have. `<mod id>` is the mod's plugin GUID, for example `catlib.boattweaks`.

The developer menu (the `` ` `` key) has a Translations group:

- **Translation report** writes `BepInEx/CatLib/Translations/report_*.txt`: for every mod and every game language
  what is translated, what is missing, what would break and which texts drop a value.
- **Export for translators** writes every text of every mod in the current game language to
  `BepInEx/CatLib/Translations/Export/<mod id>/<language>.json`, English where a translation is missing, with the missing keys
  listed at the top. Translate it, move the folder into `BepInEx/config/CatLib/Translations` and press
- **Reload translations**: the Mods tab shows the new texts at once.

At the main menu the log gets one line per mod with its languages, and a warning for missing or broken texts.

CatLib's own texts (the Mods tab, the lobby list, notifications) are the catalog `catlib.core` with keys under `ui.`,
so `BepInEx/config/CatLib/Translations/catlib.core/<language>.json` fixes or adds them the same way.

## Checking translations

`TranslationCheck.Compare(catalog, language)` compares a language with English and returns
`Missing`, `Unknown` (keys English does not have), `Broken` (would not fill in) and `Lost` (drops a value).
Plural forms are checked by the categories of the language. A mod test that runs it for every file keeps
translations complete when texts are added:

```csharp
foreach (var language in texts.Languages)
{
    var report = TranslationCheck.Compare(texts, language);
    Assert.True(report.IsComplete, string.Join(", ", report.Missing.Concat(report.Broken)));
}
```

## Keys used by the Mods tab

| Key | Used for | Without a translation |
|---|---|---|
| `mod.name` | name in the mod list and on the card | plugin name |
| `section.<Section>` | section tape | section name split into words |
| `setting.<Section>.<Key>` | row label | `Label(...)` from code, else the key split into words |
| `setting.<Section>.<Key>.description` | context line | description from the declaration |
| `enum.<EnumType>.<Value>` | dropdown option | value name split into words |

## Build

`build/Lang.targets` embeds `Lang/*.json` of every project as `<AssemblyName>.Lang.<language>.json`.
Set `<CatLibLang>false</CatLibLang>` in a project to embed them another way.
