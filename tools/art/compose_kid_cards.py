#!/usr/bin/env python3
"""Turn the renders of tools/blender/kids/render_kid_art.py into the roster's UI pictures (same sizes as before):

  assets/portraits/<id>.png               600x800 head and shoulders (transparent)
  assets/portraits/faces/<id>.png         96x96 HUD face (transparent)
  assets/portraits/cards/<id>.webp        600x800 card portrait on the card backdrop
  assets/ui/character_cards/<id>.webp     480x737 full body on the backdrop with a frame
  assets/ui/avatars/<id>.webp             512x512 nametag face on the backdrop

usage: python tools/art/compose_kid_cards.py <render dir> id [id ...]
"""
from __future__ import annotations

import sys
from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
ASSETS = ROOT / "assets"
NAVY, NAVY_DARK, RED, RED_LIGHT, FRAME = (52, 62, 92), (30, 38, 60), (226, 46, 42), (255, 84, 80), (235, 238, 245)


def backdrop(w: int, h: int) -> Image.Image:
    """The kid-card backdrop: two navy planes and two red slashes (the look of the existing character cards)."""
    img = Image.new("RGBA", (w, h), NAVY + (255,))
    d = ImageDraw.Draw(img)
    d.polygon([(0, h * 0.35), (w, h * 0.55), (w, h), (0, h)], fill=NAVY_DARK + (255,))
    d.polygon([(w * 0.55, 0), (w * 0.92, 0), (w * 0.38, h), (w * 0.02, h)], fill=RED + (255,))
    d.polygon([(w * 0.95, h * 0.08), (w * 1.0, h * 0.08), (w * 0.5, h), (w * 0.44, h)], fill=RED_LIGHT + (255,))
    return img


def framed(img: Image.Image) -> Image.Image:
    d = ImageDraw.Draw(img)
    w, h = img.size
    d.rounded_rectangle([1, 1, w - 2, h - 2], radius=14, outline=FRAME + (255,), width=3)
    return img


def compose(render_dir: Path, kid: str) -> None:
    portrait = Image.open(render_dir / f"{kid}_portrait.png").convert("RGBA")
    face = Image.open(render_dir / f"{kid}_face.png").convert("RGBA")
    body = Image.open(render_dir / f"{kid}_body.png").convert("RGBA")

    portrait.save(ASSETS / "portraits" / f"{kid}.png")
    face.resize((96, 96), Image.LANCZOS).save(ASSETS / "portraits" / "faces" / f"{kid}.png")

    card = backdrop(600, 800)
    card.alpha_composite(portrait)
    card.convert("RGB").save(ASSETS / "portraits" / "cards" / f"{kid}.webp", quality=90)

    char_card = backdrop(480, 737)
    char_card.alpha_composite(body)
    framed(char_card).save(ASSETS / "ui" / "character_cards" / f"{kid}.webp", quality=90)

    avatar = backdrop(512, 512)
    avatar.alpha_composite(face)
    avatar.convert("RGB").save(ASSETS / "ui" / "avatars" / f"{kid}.webp", quality=90)
    print("composed", kid)


def main() -> int:
    render_dir = Path(sys.argv[1])
    for kid in sys.argv[2:]:
        compose(render_dir, kid)
    return 0


if __name__ == "__main__":
    sys.exit(main())
