#!/usr/bin/env python3
"""Builds unity/Data/i18n__strings.csv from Unity C# UI/Sim sources and unity/Data/*.json.

    python3 tools/i18n/extract.py            # merge extracted English keys, preserving existing keys & translations
    python3 tools/i18n/extract.py --check    # exit 1 if unity/Data/i18n__strings.csv is out of date
    python3 tools/i18n/extract.py --prune    # drop untranslated keys no longer found in C# or JSON data

Keys are the English strings themselves, loaded at runtime by `Kalyeah.Game.I18n` from `Resources/Data/i18n__strings`.
"""
from __future__ import annotations

import csv
import io
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "unity" / "Data" / "i18n__strings.csv"
DATA_DIR = ROOT / "unity" / "Data"
CS_ROOT = ROOT / "unity" / "Game" / "Assets" / "Kalyeah"
STATUS_CS = ROOT / "unity" / "Kalyeah.Sim" / "Match" / "StatusEffects.cs"

# Default extra language columns (preserved alongside any additional columns already in OUT).
LANGUAGES: list[str] = ["tl"]

STRING = r'"((?:[^"\\\n]|\\.)*)"'
MAKE_TEXT_RE = re.compile(r'Ui\.MakeText\(\s*"(?:[^"\\\n]|\\.)*"\s*,\s*[^,]+,\s*' + STRING)
TR_CALL_RE = re.compile(r"\b(?:I18n\.)?Tr\(\s*" + STRING)
FIL_TERNARY_RE = re.compile(r"\bfil\s*\?\s*" + STRING + r"\s*:\s*" + STRING)
TEXT_ASSIGN_RE = re.compile(r"\.text\s*=\s*" + STRING)
LITERAL_RE = re.compile(STRING)

# Internal node/asset/shader/debug lines whose string literals are not player-facing copy.
NON_UI_LINE_RE = re.compile(
    r"(?:\bUi\.(?:Panel|Fill|MakeImage|MakeCanvas)\(|\bnew\s+GameObject\(|\.Find\(|Resources\.Load|"
    r"Shader\.Find|PlayerPrefs\.|Debug\.Log|\[MenuItem|\[Header|\[SerializeField|SceneManager\.|"
    r"GetString\(|SetString\(|GetInt\(|SetInt\(|GetFloat\(|SetFloat\()"
)

EXCLUDE = {"Choice", "Heading", "DISPLAY", "One", "Two", "Disabled", "Initial", "Label", "Title", "Body", "Text"}
SKIP = re.compile(
    r"^(?:res://|user://|https?://|wss?://|[0-9a-f]{6,8}$|[a-z][a-z0-9_]*$|"
    r"[A-Za-z0-9]*[_/][A-Za-z0-9_./\-]*$|[#%0-9. ·:\-+/\[\]()×°]+$|\w+\.\w+$|_[A-Z]\w*$)"
)


def _unescape(raw: str) -> str:
    return raw.replace("\\n", "\n").replace('\\"', '"').replace("\\\\", "\\")


def keep(text: str) -> bool:
    cleaned = text.strip()
    return len(cleaned) >= 2 and re.search(r"[A-Za-z]{2}", cleaned) is not None and not SKIP.match(cleaned)


def from_code() -> tuple[set[str], dict[str, str]]:
    """Scan Unity C# UI/View/Scripts for player-facing English strings (and inline `fil ? tl : en` pairs)."""
    found: set[str] = set()
    inline_tl: dict[str, str] = {}
    if not CS_ROOT.is_dir():
        return found, inline_tl

    for path in sorted(CS_ROOT.rglob("*.cs")):
        rel_parts = path.relative_to(CS_ROOT).parts
        if any(part in ("Editor", "Tests") for part in rel_parts):
            continue
        is_hud_or_menu = any(part in ("View", "Hud", "Menus", "Street", "Net") for part in rel_parts)
        for line in path.read_text(encoding="utf-8").splitlines():
            code = line.strip()
            if not code or code.startswith("//") or code.startswith("///") or code.startswith("*"):
                continue

            for m in TR_CALL_RE.finditer(code):
                text = _unescape(m.group(1))
                if len(text.strip()) >= 2:
                    found.add(text)

            for m in MAKE_TEXT_RE.finditer(code):
                text = _unescape(m.group(1))
                if keep(text) and text not in EXCLUDE:
                    found.add(text)

            has_fil_ternary = False
            for m in FIL_TERNARY_RE.finditer(code):
                has_fil_ternary = True
                tl_txt, en_txt = _unescape(m.group(1)), _unescape(m.group(2))
                if keep(en_txt) and en_txt not in EXCLUDE:
                    found.add(en_txt)
                    if tl_txt.strip():
                        inline_tl.setdefault(en_txt, tl_txt)

            if not has_fil_ternary:
                for m in TEXT_ASSIGN_RE.finditer(code):
                    text = _unescape(m.group(1))
                    if keep(text) and text not in EXCLUDE:
                        found.add(text)

            if is_hud_or_menu and not has_fil_ternary and "Ui.MakeText(" not in code and not NON_UI_LINE_RE.search(code):
                for m in LITERAL_RE.finditer(code):
                    text = _unescape(m.group(1))
                    if keep(text) and text not in EXCLUDE and text[:1].isupper() and (" " in text or text.endswith("!") or text.endswith("?")):
                        found.add(text)

    return found, inline_tl


def from_status() -> set[str]:
    """Status words (`Stunned`, `Slowed`, ...) from `Kalyeah.Sim.StatusEffects.DisplayNames`."""
    found: set[str] = set()
    if not STATUS_CS.is_file():
        return found
    text = STATUS_CS.read_text(encoding="utf-8")
    for match in re.finditer(r'\{\s*Type\.\w+,\s*"([^"]+)"\s*\}', text):
        found.add(match.group(1))
    return found


def from_data() -> set[str]:
    """Player-facing weapon, kid, and street/donate rule strings in `unity/Data/*.json`."""
    found: set[str] = set()
    if not DATA_DIR.is_dir():
        return found

    for path in sorted(DATA_DIR.glob("*.json")):
        payload = json.loads(path.read_text(encoding="utf-8"))
        data = payload.get("data", payload)
        if not isinstance(data, dict):
            continue
        name = path.name
        if name.startswith(("weapons__", "characters__")):
            fields = ("display_name", "short_name", "description")
        else:
            fields = ("blurb", "description", "thanks", "grown_ups")
        for key in fields:
            val = data.get(key)
            if isinstance(val, str) and keep(val):
                found.add(val)
        blurbs = data.get("blurbs")
        if isinstance(blurbs, dict):
            for val in blurbs.values():
                if isinstance(val, str) and keep(val):
                    found.add(val)

    return found


def read_existing(csv_path: Path = OUT) -> tuple[list[str], dict[str, dict[str, str]]]:
    if not csv_path.is_file():
        return list(LANGUAGES), {}
    with csv_path.open(encoding="utf-8", newline="") as handle:
        reader = csv.DictReader(handle)
        header_langs = [c for c in (reader.fieldnames or []) if c not in ("keys", "en") and c]
        langs = list(dict.fromkeys(header_langs + LANGUAGES))
        rows = {row["keys"]: row for row in reader if row.get("keys")}
        return langs, rows


def build_csv(out_path: Path = OUT, *, prune: bool = False) -> tuple[str, int, int]:
    langs, existing = read_existing(out_path)
    code_keys, inline_tl = from_code()
    extracted = code_keys | from_status() | from_data()

    if prune:
        keep_existing = {k for k, row in existing.items() if any(row.get(lang, "").strip() for lang in langs)}
        all_keys = extracted | keep_existing
    else:
        all_keys = extracted | set(existing.keys())

    sorted_keys = sorted(all_keys, key=lambda text: (text.lower(), text))
    rows: list[list[str]] = []
    for key in sorted_keys:
        old = existing.get(key, {})
        cells = [key, old.get("en") or key]
        for lang in langs:
            val = old.get(lang, "")
            if not val and lang == "tl" and key in inline_tl:
                val = inline_tl[key]
            cells.append(val)
        rows.append(cells)

    buffer = io.StringIO()
    writer = csv.writer(buffer, lineterminator="\n")
    writer.writerow(["keys", "en"] + langs)
    writer.writerows(rows)
    translated = sum(1 for row in rows if any(cell for cell in row[2:]))
    return buffer.getvalue(), len(rows), translated


def main(argv: list[str] | None = None) -> int:
    args = sys.argv[1:] if argv is None else argv
    prune = "--prune" in args
    new_csv, count, translated = build_csv(OUT, prune=prune)
    if "--check" in args:
        old_text = OUT.read_text(encoding="utf-8") if OUT.is_file() else ""
        if old_text != new_csv:
            print(f"{OUT.relative_to(ROOT)} is out of date: run tools/i18n/extract.py")
            return 1
        return 0
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(new_csv, encoding="utf-8", newline="\n")
    print(f"{count} strings, {translated} translated -> {OUT.relative_to(ROOT)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
