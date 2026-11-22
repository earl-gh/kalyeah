#!/usr/bin/env python3
"""Assemble crisp labeled BEFORE vs AFTER character comparison composites & 4-kid showcase grid.

Reads before_<kid>.png / after_<kid>.png (1600x900) and before_readability_<kid>.png /
after_readability_<kid>.png (1600x600) in --dir and outputs:
  - before_after_<kid>.png      (1600x1860) Stacked full turnaround BEFORE (top) vs AFTER (bottom)
  - before_after_showcase.png   (1600x1680) 4-kid Close-Up + Front Ortho BEFORE vs AFTER executive grid

Usage:
  python tools/blender/kids/compose_char_comparisons.py --dir C:/path/to/char_polish
"""
from __future__ import annotations

import argparse
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont


def load_font(size: int):
    for cand in (
        "C:/Windows/Fonts/segoeuib.ttf",
        "C:/Windows/Fonts/arialbd.ttf",
        "C:/Windows/Fonts/consolab.ttf",
    ):
        if Path(cand).is_file():
            return ImageFont.truetype(cand, size)
    return ImageFont.load_default()


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--dir", required=True)
    ap.add_argument("--kids", default="karding,narda,jumong,tere")
    args = ap.parse_args()

    out_dir = Path(args.dir)
    kids = [k.strip().lower() for k in args.kids.split(",") if k.strip()]
    f_title = load_font(28)
    f_badge = load_font(20)
    f_sub = load_font(17)

    # 1) Per-kid full stacked BEFORE vs AFTER comparison (1600x1860)
    for kid in kids:
        im_b = Image.open(out_dir / f"before_{kid}.png").convert("RGB")
        im_a = Image.open(out_dir / f"after_{kid}.png").convert("RGB")
        w, h = im_b.size
        hdr = 56
        gap = 8
        comp = Image.new("RGB", (w, hdr + h + gap + h), (22, 24, 29))
        draw = ImageDraw.Draw(comp)
        draw.text(
            (20, 12),
            f"KALYEAH AAA CHARACTER POLISH (PHASES 1-4)  |  {kid.upper()}  —  BEFORE (TOP) vs AFTER (BOTTOM)",
            fill=(248, 226, 152),
            font=f_title,
        )
        comp.paste(im_b, (0, hdr))
        draw.rectangle([0, hdr + h, w, hdr + h + gap], fill=(240, 196, 92))
        comp.paste(im_a, (0, hdr + h + gap))
        comp.save(out_dir / f"before_after_{kid}.png", format="PNG")
        print(f"[compose] saved before_after_{kid}.png")

    # 2) 4-kid Executive Showcase Grid (100% true 1:1 aspect ratio, zero stretching!)
    # Each kid row (608px tall): [BEFORE Front (396x604) | BEFORE Bust (396x604) | divider | AFTER Front | AFTER Bust]
    # Source panels are 400px wide; cropping vertically to 610px height matches 396:604 = 1.000 exact aspect ratio!
    row_h = 608
    hdr_h = 74
    grid_w = 1600
    grid_h = hdr_h + len(kids) * row_h
    grid = Image.new("RGB", (grid_w, grid_h), (20, 22, 27))
    draw = ImageDraw.Draw(grid)
    draw.text(
        (22, 14),
        "KALYEAH 4-KID FLAGSHIP POLISH SHOWCASE  |  BEFORE (LEFT)  vs  AFTER (RIGHT)",
        fill=(248, 226, 152),
        font=f_title,
    )
    draw.text((22, 46), "BEFORE — RAW RETOPO / BLEED / CLIPPING", fill=(235, 130, 120), font=f_sub)
    draw.text((826, 46), "AFTER — CHISELED PLANES / PAINTERLY PBR / ZERO CLIP / 4 FACIAL MORPHS", fill=(130, 235, 175), font=f_sub)

    for idx, kid in enumerate(kids):
        im_b = Image.open(out_dir / f"before_{kid}.png").convert("RGB")
        im_a = Image.open(out_dir / f"after_{kid}.png").convert("RGB")
        y0 = hdr_h + idx * row_h

        # Exact 400x610 crops preserving full character head-to-toe in Front Ortho & full head+torso in Bust
        crops = (
            (im_b.crop((0, 155, 400, 765)),    0),
            (im_b.crop((1200, 95, 1600, 705)), 398),
            (im_a.crop((0, 155, 400, 765)),     804),
            (im_a.crop((1200, 95, 1600, 705)), 1202),
        )
        for crop_im, x0 in crops:
            resized = crop_im.resize((396, row_h - 4), Image.Resampling.LANCZOS)
            grid.paste(resized, (x0, y0 + 2))

        # Gold center divider + kid badge
        draw.rectangle([797, y0, 803, y0 + row_h], fill=(240, 196, 92))
        draw.rectangle([0, y0, grid_w, y0 + 2], fill=(42, 46, 54))
        draw.rectangle([10, y0 + 10, 195, y0 + 42], fill=(20, 22, 27))
        draw.text((18, y0 + 14), f"{kid.upper()} BEFORE", fill=(245, 150, 140), font=f_badge)
        draw.rectangle([814, y0 + 10, 995, y0 + 42], fill=(20, 22, 27))
        draw.text((822, y0 + 14), f"{kid.upper()} AFTER", fill=(140, 240, 180), font=f_badge)

    grid.save(out_dir / "before_after_showcase.png", format="PNG")
    print("[compose] saved before_after_showcase.png")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
