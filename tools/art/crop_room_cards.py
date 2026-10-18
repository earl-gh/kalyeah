"""Crops each character's face out of the boot screen key art (assets/ui/boot/boot_bg.webp) as a 3:4
profile card for the hotspot room (assets/portraits/cards/<id>.png), at the key art's own zoom (no
scaling up: the crop is the same size as the face box, 3 wide by 4 tall).

usage: python tools/art/crop_room_cards.py
Needs Pillow. The face boxes are the ones used for the minimap faces (crop_minimap_faces.py).
"""
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "assets/ui/boot/boot_bg.webp"
OUT = ROOT / "assets/portraits/cards"
# the face box of each kid (left, top, right, bottom) in the 941 x 1672 key art
FACES: dict[str, tuple[int, int, int, int]] = {
    "junjun": (5, 765, 215, 975),
    "popoy": (165, 775, 365, 975),
    "ligaya": (285, 870, 485, 1070),
    "migo": (400, 805, 600, 1005),
    "toni": (560, 850, 770, 1060),
    "inday": (741, 800, 941, 1000),
}
# the crop is this many times the face box wide; the face sits a little above the middle of the card
WIDTH_FACTOR = 1.0
LIFT = 0.04


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    art = Image.open(SOURCE).convert("RGB")
    for name, (left, top, right, bottom) in FACES.items():
        width = round((right - left) * WIDTH_FACTOR)
        height = round(width * 4 / 3)
        cx = (left + right) / 2
        cy = (top + bottom) / 2 + height * 0.5 * (0.5 - LIFT) - (bottom - top) / 2 + (bottom - top) * 0.0
        x0 = round(cx - width / 2)
        y0 = round(cy - height * (0.5 - LIFT))
        x0 = max(0, min(art.width - width, x0))
        y0 = max(0, min(art.height - height, y0))
        art.crop((x0, y0, x0 + width, y0 + height)).save(OUT / f"{name}.png", optimize=True)
        print("wrote", OUT / f"{name}.png", width, height)


if __name__ == "__main__":
    main()
