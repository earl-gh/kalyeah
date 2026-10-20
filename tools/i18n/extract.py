#!/usr/bin/env python3
"""Builds data/i18n/strings.csv: every English word a player reads, with an empty column for each language to fill in.

    python3 tools/i18n/extract.py            # rewrite the English column, keep every translation already written
    python3 tools/i18n/extract.py --check    # exit 1 if the file is out of date (for CI or a quick look)

Godot translates a Label or Button's text by itself when the text is a key in a loaded translation, so the keys are the
English strings themselves. Text that is *drawn* (a weapon's tag, the word over a kid) goes through `UiText.t()`.
Strings with a % in them are listed too: translate the whole sentence and keep the %d or %s where it belongs, and the code
must call `UiText.f(key, args)` for it to be translated (the extractor lists only the ones that already do).
"""
import csv
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "data" / "i18n" / "strings.csv"
# Add a language code here (and run this script) to start its column; the game loads data/i18n/strings.<code>.translation
LANGUAGES: list[str] = []

# the calls whose string arguments a player reads
CALLS = [
    r"\.text\s*=\s*", r"make_button\(", r"UiKit\.text\(", r"LobbyWidgets\.label\(", r"LobbyWidgets\.button\(", r"LobbyWidgets\.header\(",
    r"LobbyWidgets\.field_label\(", r"add_label\(", r"_set_status\(", r"show_banner\(", r"show_intro\(", r"UiToast\.say\(\w+,\s*",
    r"UiKit\.chip\(", r"UiKit\.section_label\(", r"UiKit\.page_header\(", r"UiKit\.setting_row\(", r"MenuDialog\.create\(",
    r"set_connection\(", r"UiStatusStrip\.strip\(", r"tooltip_text\s*=\s*", r"accessibility_name\s*=\s*", r"accessibility_description\s*=\s*",
    r"UiText\.t\(", r"UiText\.f\(", r'"(?:label|title|text|heading|phrase|caption|hint|subtitle)"\s*:\s*',
]
STRING = r'"((?:[^"\\\n]|\\.)*)"'
FOLDERS = ["scripts/ui", "scripts/view"]
# names of nodes and of the UI gallery's examples, not words a player reads
EXCLUDE = {"Choice", "Heading", "DISPLAY", "One", "Two", "Disabled"}
DATA = {
    "data/weapons": ["display_name", "description"],
    "data/characters": ["display_name", "description"],
    "data/rules": ["blurb", "description"],
}
# not words: ids, paths, formats of data
SKIP = re.compile(r"^(res://|user://|[0-9a-f]{6}$|[a-z][a-z0-9_]*$|[A-Za-z0-9]*[_%][A-Za-z0-9_%]*$|[#%0-9. ·:\-+/]+$|\w+\.\w+$)")


def keep(text: str) -> bool:
    text = text.replace("\\n", "\n").strip()
    return len(text) >= 2 and re.search(r"[A-Za-z]{2}", text) is not None and not SKIP.match(text)


def from_code() -> set[str]:
    found: set[str] = set()
    trigger = re.compile("(?:" + "|".join(CALLS) + ")")
    literal = re.compile(STRING)
    for folder in FOLDERS:
        for path in sorted((ROOT / folder).glob("*.gd")):
            for line in path.read_text().splitlines():
                code = line.strip()
                if code.startswith("#") or not trigger.search(code):
                    continue
                for match in literal.finditer(code):
                    text = match.group(1).replace("\\n", "\n").replace('\\"', '"')
                    if (keep(text) and text not in EXCLUDE) or ("UiText." in code and len(text) >= 2):
                        found.add(text)
    return found


def from_hud() -> set[str]:
    """The match HUD builds words in variables (a banner, a capture line), so take its capitalised phrases whole."""
    found: set[str] = set()
    literal = re.compile(STRING)
    for folder in ["scripts/ui/hud", "scripts/view"]:
        for path in sorted((ROOT / folder).glob("*.gd")):
            for line in path.read_text().splitlines():
                code = line.strip()
                if code.startswith("#") or "preload(" in code or "load(" in code or "name = " in code or "StringName" in code or "&\"" in code:
                    continue
                for match in literal.finditer(code):
                    text = match.group(1).replace("\\n", "\n")
                    if "UiText." in code and len(text) >= 2:
                        found.add(text)
                    elif keep(text) and text not in EXCLUDE and text[:1].isupper() and (" " in text or text.isupper() or text.endswith("!")):
                        found.add(text)
    return found


def from_status() -> set[str]:
    """The status words (Stunned, Slowed, ...) are data in the sim."""
    found: set[str] = set()
    text = (ROOT / "scripts/sim/status_effects.gd").read_text()
    for match in re.finditer(r'Type\.\w+:\s*"([^"]+)"', text):
        found.add(match.group(1))
    return found


def from_data() -> set[str]:
    found: set[str] = set()
    for folder, keys in DATA.items():
        for path in sorted((ROOT / folder).glob("*.tres")):
            for key in keys:
                for match in re.finditer(r'^%s = "(.*)"$' % key, path.read_text(), re.M):
                    if keep(match.group(1)):
                        found.add(match.group(1))
    return found


def read_existing() -> dict[str, dict[str, str]]:
    if not OUT.exists():
        return {}
    with OUT.open(newline="") as handle:
        return {row["keys"]: row for row in csv.DictReader(handle)}


def main() -> int:
    existing = read_existing()
    keys = sorted(from_code() | from_hud() | from_status() | from_data(), key=lambda text: (text.lower(), text))
    rows = []
    for key in keys:
        old = existing.get(key, {})
        rows.append([key, key] + [old.get(lang, "") for lang in LANGUAGES])
    text_lines = []
    import io
    buffer = io.StringIO()
    writer = csv.writer(buffer, lineterminator="\n")
    writer.writerow(["keys", "en"] + LANGUAGES)
    writer.writerows(rows)
    new = buffer.getvalue()
    if "--check" in sys.argv:
        old_text = OUT.read_text() if OUT.exists() else ""
        if old_text != new:
            print("data/i18n/strings.csv is out of date: run tools/i18n/extract.py")
            return 1
        return 0
    OUT.write_text(new)
    translated = sum(1 for row in rows if any(cell for cell in row[2:]))
    print(f"{len(rows)} strings, {translated} translated -> {OUT.relative_to(ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
