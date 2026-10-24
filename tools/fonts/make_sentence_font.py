#!/usr/bin/env python3
"""Builds the game's typeface from the owner's Bungee: every lowercase letter becomes a smaller capital.

Bungee has capitals only, so "Create room" is drawn as "CREATE ROOM" at one size. For a sentence-case look (owner: the next
letters smaller) this tool writes assets/fonts/bungee/Bungee-SentenceCase.ttf in which each lowercase letter is its capital scaled
by SMALL, with a touch of extra spacing for the thinner strokes. Text typed in capitals stays full size, so titles typed in
capitals ("VICTORY!") keep their weight and ordinary sentences ("Create room") read as a capital then smaller capitals.

    pip install fonttools
    python3 tools/fonts/make_sentence_font.py

Bungee is licensed under the SIL Open Font License 1.1 (assets/fonts/bungee/OFL.txt, no reserved font name); the derived font
keeps the notice and takes its own name.
"""
import sys
from pathlib import Path

from fontTools.pens.transformPen import TransformPen
from fontTools.pens.ttGlyphPen import TTGlyphPen
from fontTools.ttLib import TTFont

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "assets/fonts/bungee/Bungee-Regular.ttf"
TARGET = ROOT / "assets/fonts/bungee/Bungee-SentenceCase.ttf"
## How big a lowercase letter is against a capital.
SMALL = 0.82
## Extra advance for a small letter, in font units (the strokes are thinner, so the letters want a little more air).
EXTRA = 12
FAMILY = "Kalyeah Sentence Case"
POSTSCRIPT = "KalyeahSentenceCase-Regular"


def main() -> int:
    font = TTFont(SOURCE)
    glyph_set = font.getGlyphSet()
    cmap = font.getBestCmap()
    order = list(font.getGlyphOrder())
    glyf, hmtx = font["glyf"], font["hmtx"]
    vmtx = font["vmtx"] if "vmtx" in font else None

    # one small glyph per capital glyph, and every lowercase code point that has such a capital points at it
    wanted: dict[int, str] = {}
    for code in sorted(cmap):
        letter = chr(code)
        upper = letter.upper()
        if letter.islower() and len(upper) == 1 and upper != letter and ord(upper) in cmap:
            wanted[code] = cmap[ord(upper)]
    made: dict[str, str] = {}
    for capital in sorted(set(wanted.values())):
        name = capital + ".sc"
        pen = TTGlyphPen(glyph_set)
        glyph_set[capital].draw(TransformPen(pen, (SMALL, 0, 0, SMALL, 0, 0)))
        glyph = pen.glyph()
        glyf[name] = glyph
        order.append(name)
        advance, _ = hmtx[capital]
        glyph.recalcBounds(glyf)
        hmtx[name] = (round(advance * SMALL) + EXTRA, getattr(glyph, "xMin", 0))
        if vmtx is not None and capital in vmtx.metrics:
            vmtx[name] = vmtx[capital]
        made[capital] = name
    font.setGlyphOrder(order)
    glyf.glyphOrder = order
    for table in font["cmap"].tables:
        if table.isUnicode():
            for code, capital in wanted.items():
                table.cmap[code] = made[capital]

    # a ligature would put a full-size glyph back inside a run of small letters
    if "GSUB" in font:
        for record in font["GSUB"].table.FeatureList.FeatureRecord:
            if record.FeatureTag in ("liga", "clig", "dlig", "rlig"):
                record.Feature.LookupListIndex = []
                record.Feature.LookupCount = 0

    font["post"].formatType = 3.0
    for record in font["name"].names:
        if record.nameID in (1, 4, 16):
            record.string = FAMILY
        elif record.nameID == 6:
            record.string = POSTSCRIPT
        elif record.nameID == 3:
            record.string = "%s;derived from Bungee" % POSTSCRIPT
    font.save(TARGET)
    print("wrote %s: %d small letters from %d capitals" % (TARGET.relative_to(ROOT), len(wanted), len(made)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
