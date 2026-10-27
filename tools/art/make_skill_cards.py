"""Trims and downsizes the owner's full-card skill pictures for the match hand.

Each picture is a complete card (its own torn-cardboard frame, art and glow) on a transparent canvas, made by the owner at 1049 x 1499 px.
The hand draws it AS the card (`CardStyle.picture`, `HandCard`), so this only trims the transparent margins (the picture's own frame then
fills the card slot) and downsizes with Lanczos. Nothing is repainted. Every picture is cut with the SAME box (CROP: the union of the
visible parts of all fourteen), so the frames of all cards keep the same size and place in the card slot; a picture whose glow reaches
outside the box stops the script (widen CROP, then run it for all of them again).

Run:  python3 -I tools/art/make_skill_cards.py [source_dir]
Source (not committed, the originals are 1049 x 1499): one file per picture in `source_dir` (default tools/art/src/skill_cards/), named
as in SOURCES below, e.g. the owner's 71.webp (green healing cross with wings) is the card of the weapon `heal`.
Output: assets/ui/skill_cards/<weapon_id>.webp, WIDTH px wide, alpha kept. A weapon whose file is there is drawn as that picture in the
match; a weapon with none keeps its teks card with the painted icon. To add one: add its source name below, run this, and open the
project once so Godot writes the .import (same settings as the other files of that folder: lossy, mipmaps, no VRAM compression).
"""
import os
import sys

from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "assets", "ui", "skill_cards")
DEFAULT_SRC = os.path.join(ROOT, "tools", "art", "src", "skill_cards")
WIDTH = 512
CANVAS = (1049, 1499)
# left, top, right, bottom in the owner's canvas: 1038 x 1372 (aspect 0.757, the card slot is 0.79)
CROP = (11, 62, 1049, 1434)
# the faint tail of a glow counts as picture; dust at alpha 1 to 8 does not
TRIM_ALPHA = 8
QUALITY = 90
# source file -> weapon id
SOURCES = {
    "71.webp": "heal",
    "72.webp": "papel_shield",
    "73.webp": "papel_trap",
    "74.webp": "gunting_heavy",
    "75.webp": "langit_lupa",
    "76.webp": "jacks",
    "77.webp": "trumpo",
    "78.webp": "gunting_light",
    "79.webp": "bato_heavy",
    "80.webp": "bola",
    "81.webp": "tsinelas_heavy",
    "82.webp": "bato_light",
    "83.webp": "tsinelas_light",
    "84.webp": "lata",
}


def trimmed(img):
    if img.size != CANVAS:
        raise SystemExit("expected a %dx%d canvas, got %dx%d" % (CANVAS + img.size))
    alpha = img.getchannel("A").point(lambda a: 255 if a > TRIM_ALPHA else 0)
    left, top, right, bottom = alpha.getbbox()
    if left < CROP[0] or top < CROP[1] or right > CROP[2] or bottom > CROP[3]:
        raise SystemExit("picture reaches (%d, %d, %d, %d), outside CROP %s" % (left, top, right, bottom, CROP))
    return img.crop(CROP)


def make(path, weapon_id):
    img = trimmed(Image.open(path).convert("RGBA"))
    height = round(img.height * WIDTH / img.width)
    out = img.resize((WIDTH, height), Image.LANCZOS)
    target = os.path.join(OUT, weapon_id + ".webp")
    out.save(target, "WEBP", quality=QUALITY, method=6, alpha_quality=100)
    print("%-14s %dx%d -> %dx%d (aspect %.3f), %d bytes" % (weapon_id, img.width, img.height, out.width, out.height, out.width / out.height, os.path.getsize(target)))


def main():
    src = sys.argv[1] if len(sys.argv) == 2 else DEFAULT_SRC
    os.makedirs(OUT, exist_ok=True)
    for name, weapon_id in SOURCES.items():
        make(os.path.join(src, name), weapon_id)


if __name__ == "__main__":
    main()
