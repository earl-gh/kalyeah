"""Trims and downscales the owner's character card pictures for the Characters tab.

Run: python3 tools/art/make_character_cards.py <folder with the originals>
Input: the owner's transparent WebP cards (1049 x 1499), each a complete card (its own worn-paper frame,
comic-burst background and the kid). They are NOT kept in the repo (the APK budget is tight); SOURCES says
which file is which kid.
Output: assets/ui/character_cards/<id>.webp, all the same size: ONE common crop box (the union of the visible
parts of every picture, so the frames keep the same size and sit in the same place), Lanczos to WIDTH px,
lossy WebP with alpha. The art is only trimmed and scaled, never repainted.
Adding a kid later is data only: add its file to SOURCES and re-run the script (the crop box is the union of
the files, so keep the earlier originals in the folder too), then the game finds <id>.webp by the kid's id.
"""
import os
import sys

from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "assets", "ui", "character_cards")
# the owner's file name -> the kid it shows (every kid has a picture)
SOURCES = {
    "85.webp": "junjun",
    "86.webp": "toni",
    "87.webp": "migo",
    "88.webp": "inday",
    "89.webp": "ligaya",
    "90.webp": "popoy",
}
WIDTH = 480
QUALITY = 85
# alpha at or under this counts as empty when looking for the visible part (ignores stray soft pixels)
EMPTY_ALPHA = 8


def visible_box(image):
    alpha = image.getchannel("A").point(lambda a: 255 if a > EMPTY_ALPHA else 0)
    return alpha.getbbox()


def main(folder):
    images = {}
    box = None
    for name, kid in SOURCES.items():
        image = Image.open(os.path.join(folder, name)).convert("RGBA")
        found = visible_box(image)
        box = found if box is None else (
            min(box[0], found[0]), min(box[1], found[1]), max(box[2], found[2]), max(box[3], found[3]))
        images[kid] = image
    print("common crop box", box)
    os.makedirs(OUT, exist_ok=True)
    for kid, image in images.items():
        card = image.crop(box)
        height = round(card.height * WIDTH / card.width)
        card = card.resize((WIDTH, height), Image.LANCZOS)
        path = os.path.join(OUT, kid + ".webp")
        card.save(path, "WEBP", quality=QUALITY, method=6, alpha_quality=100, exact=True)
        print(kid, card.size, os.path.getsize(path), "bytes")


if __name__ == "__main__":
    main(sys.argv[1])
