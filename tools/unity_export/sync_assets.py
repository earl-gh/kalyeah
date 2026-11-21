#!/usr/bin/env python3
"""Cross-platform Unity asset, data, i18n, app-icon, and shared C# package sync.

Mirrors authored assets (including Android app icons), canonical JSON + i18n CSV data,
and engine-free Kalyeah.Sim / Kalyeah.Net sources into unity/Game (converting *.webp -> *.png
with RGBA preserved via Pillow so Unity Resources.Load<Texture2D/Sprite> works).
"""
from __future__ import annotations

import fnmatch
import os
import re
import shutil
import subprocess
import sys
from pathlib import Path

try:
    from PIL import Image
except ImportError:
    Image = None  # type: ignore[assignment]

ROOT = Path(__file__).resolve().parents[2]
GAME = ROOT / "unity" / "Game"
IMPORTED = GAME / "Assets" / "Imported" / "Resources"
APP_ICON_DEST = GAME / "Assets" / "Imported" / "AppIcon"
SIM_SRC = ROOT / "unity" / "Kalyeah.Sim"
NET_SRC = ROOT / "unity" / "Kalyeah.Net"
SIM_PKG = GAME / "Packages" / "com.kalyeah.sim"

ASSET_DIRS = (
    "models/characters",
    "models/barangay",
    "models/props",
    "models/weapons",
    "textures",
    "audio",
    "fonts",
    "ui",
    "icons",
    "portraits",
)

GODOT_ONLY = (
    "*.import",
    "*.uid",
    "*.tres",
    "*.res",
    "*.tscn",
    "*.gd",
    "*.gdshader",
    "*.godot",
    ".gdignore",
)

CHARACTER_EXCLUDES = GODOT_ONLY + (
    "*.blend",
    "*.blend1",
    "*_base.jpg",
    "*_normal.png",
    "*.tmp",
)

CS_EXCLUDES = ("bin", "obj", "*.csproj", "*.csproj.user")
MATCH_SIM_RE = re.compile(r"\bclass\s+MatchSim\b")


def _matches_any(name: str, patterns: tuple[str, ...]) -> bool:
    return any(fnmatch.fnmatch(name, pat) for pat in patterns)


def _copy_if_needed(src: Path, dest: Path) -> None:
    src_stat = src.stat()
    if dest.exists():
        dest_stat = dest.stat()
        if dest_stat.st_size == src_stat.st_size and dest_stat.st_mtime >= src_stat.st_mtime:
            return
    dest.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(src, dest)


def _convert_webp_to_png(src: Path, dest: Path) -> None:
    dest.parent.mkdir(parents=True, exist_ok=True)
    if Image is not None:
        with Image.open(src) as img:
            img.convert("RGBA").save(dest, format="PNG")
        return
    for cmd in (
        ["ffmpeg", "-y", "-loglevel", "error", "-i", str(src), str(dest)],
        ["dwebp", "-quiet", str(src), "-o", str(dest)],
    ):
        if shutil.which(cmd[0]):
            subprocess.run(cmd, check=True)
            return
    raise RuntimeError(
        f"Cannot convert {src} -> {dest}: install Pillow (`pip install Pillow`) or ffmpeg/dwebp."
    )


def mirror_tree(
    src: Path,
    dest: Path,
    excludes: tuple[str, ...] = (),
    *,
    include_globs: tuple[str, ...] | None = None,
    convert_webp: bool = False,
    extra_files: dict[Path, Path] | None = None,
) -> tuple[int, int]:
    """Mirror src -> dest, keeping Unity .meta files and pruning stale non-.meta files.

    Returns (webp_total_seen, webp_reencoded).
    """
    dest.mkdir(parents=True, exist_ok=True)
    expected: set[Path] = set()
    webp_seen = 0
    webp_encoded = 0

    if src.is_dir():
        for dirpath, dirnames, filenames in os.walk(src, topdown=True):
            dirnames[:] = [d for d in dirnames if not _matches_any(d, excludes)]
            cur = Path(dirpath)
            for fname in filenames:
                if fname.endswith(".meta") or _matches_any(fname, excludes):
                    continue
                if include_globs is not None and not _matches_any(fname, include_globs):
                    continue
                src_file = cur / fname
                rel = src_file.relative_to(src)
                if convert_webp and src_file.suffix.lower() == ".webp":
                    webp_seen += 1
                    rel = rel.with_suffix(".png")
                    dest_file = dest / rel
                    expected.add(rel)
                    if not dest_file.exists() or dest_file.stat().st_mtime < src_file.stat().st_mtime:
                        _convert_webp_to_png(src_file, dest_file)
                        webp_encoded += 1
                else:
                    dest_file = dest / rel
                    expected.add(rel)
                    _copy_if_needed(src_file, dest_file)

    if extra_files:
        for rel_dest, extra_src in extra_files.items():
            if extra_src.is_file():
                expected.add(rel_dest)
                _copy_if_needed(extra_src, dest / rel_dest)
            else:
                print(f"sync_assets: {extra_src.relative_to(ROOT)} is missing, skipped", file=sys.stderr)

    for dirpath, _dirnames, filenames in os.walk(dest, topdown=False):
        cur = Path(dirpath)
        for fname in filenames:
            if fname.endswith(".meta"):
                continue
            rel = (cur / fname).relative_to(dest)
            if rel not in expected:
                (cur / fname).unlink()
        if cur != dest and not any(cur.iterdir()):
            cur.rmdir()

    return webp_seen, webp_encoded


def _detect_sim_version(sim_src: Path) -> str:
    if not sim_src.is_dir():
        return "0.1.0"
    for cs_file in sim_src.rglob("*.cs"):
        if MATCH_SIM_RE.search(cs_file.read_text(encoding="utf-8", errors="ignore")):
            return "1.0.0"
    return "0.1.0"


def _write_lf_if_changed(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    if path.is_file() and path.read_bytes() == text.encode("utf-8"):
        return
    with path.open("w", encoding="utf-8", newline="\n") as f:
        f.write(text)


def _count_non_meta_files(*roots: Path) -> int:
    total = 0
    for root in roots:
        if not root.is_dir():
            continue
        for _dirpath, _dirnames, filenames in os.walk(root):
            total += sum(1 for f in filenames if not f.endswith(".meta"))
    return total


# Active Input Handling must be on disk before Unity starts: the editor compiles its scripts once with the
# ENABLE_INPUT_SYSTEM define it reads at launch, so switching it inside the build session (KalyeahBuild) makes the
# editor and the player disagree ("script class layout is incompatible") and the APK build fails.
INPUT_SYSTEM_ONLY = 1
PLAYER_SETTINGS = GAME / "ProjectSettings" / "ProjectSettings.asset"
_INPUT_HANDLER_RE = re.compile(r"^(\s*activeInputHandler:\s*)\d+\s*$", re.MULTILINE)


def ensure_input_system_only(path: Path = PLAYER_SETTINGS) -> None:
    if not path.is_file():
        _write_lf_if_changed(
            path,
            "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!129 &1\nPlayerSettings:\n"
            f"  activeInputHandler: {INPUT_SYSTEM_ONLY}\n",
        )
        return
    text = path.read_text(encoding="utf-8")
    if _INPUT_HANDLER_RE.search(text):
        patched = _INPUT_HANDLER_RE.sub(lambda m: f"{m.group(1)}{INPUT_SYSTEM_ONLY}", text)
    else:
        patched = text.replace("PlayerSettings:\n", f"PlayerSettings:\n  activeInputHandler: {INPUT_SYSTEM_ONLY}\n", 1)
    _write_lf_if_changed(path, patched)


def main() -> int:
    webp_seen_total = 0
    webp_encoded_total = 0

    for rel_dir in ASSET_DIRS:
        src_dir = ROOT / "assets" / rel_dir
        if src_dir.is_dir():
            excludes = CHARACTER_EXCLUDES if rel_dir == "models/characters" else GODOT_ONLY
            seen, encoded = mirror_tree(
                src_dir,
                IMPORTED / rel_dir,
                excludes,
                convert_webp=True,
            )
            webp_seen_total += seen
            webp_encoded_total += encoded
        else:
            print(f"sync_assets: assets/{rel_dir} is missing, skipped", file=sys.stderr)

    app_icon_src = ROOT / "assets" / "app_icon"
    if app_icon_src.is_dir():
        seen, encoded = mirror_tree(
            app_icon_src,
            APP_ICON_DEST,
            GODOT_ONLY,
            convert_webp=True,
        )
        webp_seen_total += seen
        webp_encoded_total += encoded
    else:
        print("sync_assets: assets/app_icon is missing, skipped", file=sys.stderr)

    mirror_tree(
        ROOT / "unity" / "Data",
        IMPORTED / "Data",
        include_globs=("*.json", "*.csv"),
    )

    mirror_tree(SIM_SRC, SIM_PKG / "Runtime", CS_EXCLUDES)
    mirror_tree(NET_SRC, SIM_PKG / "Net", CS_EXCLUDES)

    version = _detect_sim_version(SIM_SRC)
    package_json = (
        "{\n"
        '  "name": "com.kalyeah.sim",\n'
        '  "displayName": "Kalyeah Sim",\n'
        f'  "version": "{version}",\n'
        '  "unity": "6000.0",\n'
        '  "description": "The game\'s rules: engine-free C# shared with the server and the .NET tests. '
        'Written by tools/unity_export/sync_assets.py from unity/Kalyeah.Sim; edit the sim there, not here."\n'
        "}\n"
    )
    _write_lf_if_changed(SIM_PKG / "package.json", package_json)
    ensure_input_system_only()

    count = _count_non_meta_files(GAME / "Assets" / "Imported", SIM_PKG)
    chars_dir = IMPORTED / "models" / "characters"
    char_glbs = sum(1 for p in chars_dir.glob("*.glb") if not p.stem.endswith(("_lod1", "_lod2"))) if chars_dir.is_dir() else 0
    char_lods = sum(1 for p in chars_dir.glob("*_lod[12].glb")) if chars_dir.is_dir() else 0
    char_orms = sum(1 for p in chars_dir.glob("*_orm.png")) if chars_dir.is_dir() else 0
    char_rigs = sum(1 for p in chars_dir.glob("*_rig.json")) if chars_dir.is_dir() else 0
    print(
        f"sync_assets: {count} files in unity/Game/Assets/Imported and "
        f"unity/Game/Packages/com.kalyeah.sim "
        f"({webp_seen_total} webp->png [{webp_encoded_total} encoded], "
        f"chars: {char_glbs} lod0 / {char_lods} lod1+2 / {char_orms} orm / {char_rigs} rig, sim package {version})"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
