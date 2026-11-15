# Translating Kalyeah

Every word a player reads is in `unity/Data/i18n__strings.csv`, one row per string. The first column (`keys`) and the `en` column are
the English words extracted by `tools/i18n/extract.py`; each other language is one more column that a person fills in.

## Add a language (for example Filipino)

1. Open `tools/i18n/extract.py` and ensure the language code is in `LANGUAGES` (`["fil"]`).
2. Run `python3 tools/i18n/extract.py`. The CSV now has an empty `fil` column. Rows you have already translated are kept every
   time you run it again.
3. Fill the `fil` column. Keep any `%d` or `%s` placeholders in the sentence (where numbers or names go). A line with `\n` renders as two lines.
4. Run `python3 tools/unity_export/sync_assets.py` so `unity/Data/i18n__strings.csv` is mirrored into `unity/Game/Assets/Imported/Resources/Data/i18n__strings.csv`, and commit `unity/Data/i18n__strings.csv`.
5. The game offers **Language** in Settings once a language has enough translated entries, loaded at startup by `I18n.LoadCsv(...)` (`unity/Game/Assets/Kalyeah/Scripts/I18n.cs`).

## How the words get translated

- UI labels, buttons, HUD tags, and formatted messages resolve through `I18n.Tr(key)` (`unity/Game/Assets/Kalyeah/Scripts/I18n.cs`) using the active language from `SettingsStore`.
- Run `python3 tools/i18n/extract.py --check` to verify whether `unity/Data/i18n__strings.csv` is up to date with `unity/**/*.cs` and `unity/Data/*.json`.
