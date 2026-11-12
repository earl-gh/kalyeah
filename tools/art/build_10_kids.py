#!/usr/bin/env python3
"""Entry-point wrapper for the Kalyeah Overhaul v3 Valorant 90s Filipino Street Kids pipeline.

Rebuilds all 10 playable 90s Filipino street kids from scratch in Blender 5.2
(`tools/blender/build_valorant_kids_v3.py`) with:
  - 5,500..9,000 clean deforming triangles per character (zero blocky primitives)
  - Canonical rest height 1.7000 m grounded at Y = 0.0000, material `"Original"`
  - 1024x1024 painterly Valorant Albedo (`_1.png`), Tangent-Space Normal (`_2.png`),
    and Roughness/AO ORM (`_0.png`) textures (preserving `.import` VRAM compression)
  - Full `mixamorig:` skeleton + 20 expressive 30fps animations
  - Optional Cycles rim-lit studio portraits, minimap faces & Valorant character cards

Usage:
    python tools/art/build_10_kids.py [--kids all] [--with-portraits] [--render-audit DIR]
"""
from __future__ import annotations

import argparse
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
V3_SCRIPT = ROOT / "tools" / "blender" / "build_valorant_kids_v3.py"


def main() -> int:
    parser = argparse.ArgumentParser(description="Build all 10 Valorant 90s Filipino Street Kids (Overhaul v3)")
    parser.add_argument("--kids", default="all", help="Comma-separated kid ids or 'all'")
    parser.add_argument("--with-portraits", action="store_true", help="Render 2D studio portraits, faces & character cards")
    parser.add_argument("--render-audit", default="", help="Optional output directory for 3-view studio audit sheets")
    parser.add_argument("--scratch", default="", help="Optional scratch directory for temporary render buffers")
    args = parser.parse_args()

    cmd: list[str] = [sys.executable, str(V3_SCRIPT), "--kids", args.kids]
    if args.with_portraits:
        cmd.append("--render-ui")
    if args.render_audit:
        cmd.extend(["--render-audit", args.render_audit])
    if args.scratch:
        cmd.extend(["--scratch", args.scratch])

    subprocess.run(cmd, check=True)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
