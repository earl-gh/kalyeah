# Translating Kalyeah

Every word a player reads is in `data/i18n/strings.csv`, one row per string. The first column (`keys`) and the `en` column are
the English words and are made by a script; each other language is one more column that a person fills in.

## Add a language (for example Filipino)

1. Open `tools/i18n/extract.py` and add the code to `LANGUAGES` (`["fil"]`).
2. Run `python3 tools/i18n/extract.py`. The CSV now has an empty `fil` column. Rows you have already translated are kept every
   time you run it again.
3. Fill the `fil` column. Keep any `%d` or `%s` in the sentence (where the number or the name goes). A line with a `\n` is two lines.
4. Run `godot --headless --import`. Godot makes `data/i18n/strings.fil.translation` from the CSV. Commit the CSV and its
   `.import` file (the `.translation` files are made by every import, in CI too, and are in `.gitignore`).
5. The game offers **Language** in Settings once a language has at least 30 translated messages
   (`Settings.MIN_TRANSLATED`), so a half-finished file is never shown to players by accident.

## How the words get translated

- A Label's or a Button's text is translated by Godot itself when it is a key (the English string) of a loaded translation.
- Text that is *drawn* (the weapon tag, the word over a kid, an arrow's label) goes through `UiText.t()` or `UiText.f()`.
- A sentence with a place in it is translated by code only when it is written `UiText.f("First to %d", [3])`. Most of the
  game's formatted sentences are not yet, and say so in the CSV by a `%` in the key: convert them one at a time.

Run `python3 tools/i18n/extract.py --check` to see whether the CSV is out of date (a new string was added in the code).
