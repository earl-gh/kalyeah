#!/usr/bin/env python3
"""Build the 4 expansion Filipino kids (Kiko, Nene, Buknoy, Tala) for the 10-kid roster.

Clones the normalized rigged base GLBs (migo, ligaya, junjun, toni) WITHOUT modifying
any source files, paints custom 90s Filipino Komiks outfit textures in 3D-mapped UV
space (with charcoal comic seam/fold ink lines and max cloth RGB <= 0.78), writes
VRAM-compressed Godot .import files, and renders cohesive standing portraits,
3:4 90s komiks profile cards, and 96x96 minimap faces via Blender 5.2 + Pillow.

Usage:
    python tools/art/build_10_kids.py
"""
from __future__ import annotations

import hashlib
import io
import json
import math
import os
import shutil
import struct
import subprocess
import sys
import tempfile
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageEnhance, ImageFilter
from scipy.ndimage import (
    binary_dilation,
    binary_erosion,
    distance_transform_edt,
    gaussian_filter,
    label as nd_label,
)

ROOT = Path(__file__).resolve().parents[2]
CHAR_DIR = ROOT / "assets" / "models" / "characters"
PORTRAIT_DIR = ROOT / "assets" / "portraits"
CARDS_DIR = PORTRAIT_DIR / "cards"
FACES_DIR = PORTRAIT_DIR / "faces"

BASE_COLOR_SIZE = 1024
AUX_MAP_SIZE = 512
INK_RGB = np.array([23.0, 19.0, 26.0], dtype=np.float32)  # #17131A warm komiks charcoal
MAX_CLOTH_VAL = 198.0  # 198 / 255 = 0.776 <= 0.78 PBR anti-blowout ceiling

KIDS_SPEC = [
    {
        "id": "kiko",
        "base": "migo",
        "idle_frame": 11,
        "idle_yaw": -25.0,
        "card_clip": "Idle",
        "card_frame": 11,
        "card_yaw": -25.0,
        "card_pitch": -1.5,
        "card_dist": 1.34,
        "head_rot": (-14.0, -11.5, 5.5),
        "arm_lift": (-28.0, 38.0),
        "cam_roll": -2.0,
        "key_side": -1,
        "head_fill": 0.72,
        "face_eye_bias": 0.52,
        "palette": {
            "sky_top": (198, 92, 34),
            "sky_bot": (246, 174, 72),
            "ray": (255, 214, 112),
            "halftone": (136, 44, 30),
            "rim": (255, 196, 78),
            "bokeh_leaf": (76, 108, 42),
            "bokeh_wood": (124, 58, 28),
            "bunting": [(196, 46, 48), (52, 98, 168), (232, 174, 48), (48, 138, 108)],
        },
    },
    {
        "id": "nene",
        "base": "ligaya",
        "idle_frame": 20,
        "idle_yaw": 24.0,
        "card_clip": "Idle",
        "card_frame": 20,
        "card_yaw": 24.0,
        "card_pitch": -1.5,
        "card_dist": 1.32,
        "head_rot": (-13.5, 11.0, -6.0),
        "arm_lift": (-32.0, -34.0),
        "cam_roll": 2.0,
        "key_side": 1,
        "head_fill": 0.72,
        "face_eye_bias": 0.54,
        "palette": {
            "sky_top": (188, 98, 44),
            "sky_bot": (248, 188, 88),
            "ray": (255, 224, 128),
            "halftone": (128, 56, 38),
            "rim": (255, 204, 88),
            "bokeh_leaf": (82, 118, 48),
            "bokeh_wood": (118, 62, 34),
            "bunting": [(232, 162, 42), (218, 78, 96), (82, 152, 216), (92, 176, 126)],
        },
    },
    {
        "id": "buknoy",
        "base": "junjun",
        "idle_frame": 9,
        "idle_yaw": 25.0,
        "card_clip": "Idle",
        "card_frame": 9,
        "card_yaw": 25.0,
        "card_pitch": -1.5,
        "card_dist": 1.28,
        "head_rot": (-11.5, 11.5, 5.0),
        "arm_lift": (-26.0, -30.0),
        "cam_roll": -1.8,
        "key_side": -1,
        "head_fill": 0.72,
        "face_eye_bias": 0.55,
        "palette": {
            "sky_top": (176, 84, 32),
            "sky_bot": (242, 172, 74),
            "ray": (255, 212, 108),
            "halftone": (114, 48, 26),
            "rim": (255, 192, 74),
            "bokeh_leaf": (72, 106, 38),
            "bokeh_wood": (108, 52, 26),
            "bunting": [(184, 68, 42), (44, 118, 98), (224, 164, 48), (198, 52, 52)],
        },
    },
    {
        "id": "tala",
        "base": "toni",
        "idle_frame": 16,
        "idle_yaw": -24.0,
        "card_clip": "Idle",
        "card_frame": 16,
        "card_yaw": -24.0,
        "card_pitch": -1.5,
        "card_dist": 1.30,
        "head_rot": (-12.5, -10.5, -5.0),
        "arm_lift": (-28.0, 34.0),
        "cam_roll": 1.8,
        "key_side": 1,
        "head_fill": 0.72,
        "face_eye_bias": 0.55,
        "palette": {
            "sky_top": (168, 62, 48),
            "sky_bot": (244, 166, 72),
            "ray": (255, 208, 106),
            "halftone": (108, 34, 38),
            "rim": (255, 190, 72),
            "bokeh_leaf": (78, 104, 44),
            "bokeh_wood": (112, 46, 32),
            "bunting": [(138, 34, 52), (226, 166, 44), (44, 128, 118), (214, 84, 52)],
        },
    },
]


def sha256_file(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def find_blender() -> Path:
    env_bin = os.environ.get("BLENDER_BIN")
    if env_bin and Path(env_bin).exists():
        return Path(env_bin)
    candidates = [
        Path(r"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe"),
        Path(r"C:\Program Files\Blender Foundation\Blender 5.1\blender.exe"),
        Path(r"C:\Program Files\Blender Foundation\Blender 5.0\blender.exe"),
        Path(r"C:\Program Files\Blender Foundation\Blender 4.3\blender.exe"),
    ]
    for c in candidates:
        if c.exists():
            return c
    which = shutil.which("blender")
    if which:
        return Path(which)
    raise FileNotFoundError("Blender executable not found.")


# ---------------------------------------------------------------------------
# glTF Binary Parsing, UV->3D Rasterization & Binary Chunk Rebuilding
# ---------------------------------------------------------------------------


def parse_glb(path: Path) -> tuple[dict, bytes]:
    raw = path.read_bytes()
    magic, version, _total_len = struct.unpack_from("<4sII", raw, 0)
    if magic != b"glTF" or version != 2:
        raise ValueError(f"{path} is not a glTF 2.0 binary")
    json_len, json_type = struct.unpack_from("<II", raw, 12)
    if json_type != 0x4E4F534A:
        raise ValueError(f"{path} missing JSON chunk")
    gltf = json.loads(raw[20 : 20 + json_len].decode("utf-8"))
    bin_header = 20 + json_len
    bin_len, bin_type = struct.unpack_from("<II", raw, bin_header)
    if bin_type != 0x004E4942:
        raise ValueError(f"{path} missing BIN chunk")
    bin_chunk = raw[bin_header + 8 : bin_header + 8 + bin_len]
    return gltf, bin_chunk


def read_accessor(gltf: dict, bin_chunk: bytes, acc_idx: int) -> np.ndarray:
    acc = gltf["accessors"][acc_idx]
    bv = gltf["bufferViews"][acc["bufferView"]]
    offset = bv.get("byteOffset", 0) + acc.get("byteOffset", 0)
    comp_dtype = {
        5120: np.int8,
        5121: np.uint8,
        5122: np.int16,
        5123: np.uint16,
        5125: np.uint32,
        5126: np.float32,
    }[acc["componentType"]]
    num_comp = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4, "MAT4": 16}[acc["type"]]
    elem_bytes = np.dtype(comp_dtype).itemsize * num_comp
    stride = bv.get("byteStride", elem_bytes)
    count = acc["count"]
    sliced = np.frombuffer(bin_chunk, dtype=np.uint8, count=stride * count, offset=offset)
    sliced = sliced.reshape(count, stride)[:, :elem_bytes]
    arr = np.frombuffer(sliced.tobytes(), dtype=comp_dtype)
    return arr.reshape(count) if num_comp == 1 else arr.reshape(count, num_comp)


def build_uv_3d_maps(
    gltf: dict, bin_chunk: bytes, size: int = BASE_COLOR_SIZE
) -> tuple[np.ndarray, np.ndarray]:
    """Map 3D rest-pose POSITION (x,y,z) and NORMAL (nx,ny,nz) onto every texel of a size x size UV grid."""
    prim = gltf["meshes"][0]["primitives"][0]
    pos = read_accessor(gltf, bin_chunk, prim["attributes"]["POSITION"]).astype(np.float32)
    nrm = read_accessor(gltf, bin_chunk, prim["attributes"]["NORMAL"]).astype(np.float32)
    uv = read_accessor(gltf, bin_chunk, prim["attributes"]["TEXCOORD_0"]).astype(np.float32)
    idx = read_accessor(gltf, bin_chunk, prim["indices"]).astype(np.int64).reshape(-1, 3)

    uv = np.mod(uv, 1.0)
    pos_grid = np.zeros((size, size, 3), dtype=np.float32)
    nrm_grid = np.zeros((size, size, 3), dtype=np.float32)
    valid = np.zeros((size, size), dtype=bool)

    u0, u1, u2 = uv[idx[:, 0]], uv[idx[:, 1]], uv[idx[:, 2]]
    p0, p1, p2 = pos[idx[:, 0]], pos[idx[:, 1]], pos[idx[:, 2]]
    n0, n1, n2 = nrm[idx[:, 0]], nrm[idx[:, 1]], nrm[idx[:, 2]]

    bary_weights = [
        (1.0, 0.0, 0.0),
        (0.0, 1.0, 0.0),
        (0.0, 0.0, 1.0),
        (0.5, 0.5, 0.0),
        (0.0, 0.5, 0.5),
        (0.5, 0.0, 0.5),
        (1.0 / 3.0, 1.0 / 3.0, 1.0 / 3.0),
        (0.6, 0.2, 0.2),
        (0.2, 0.6, 0.2),
        (0.2, 0.2, 0.6),
        (0.75, 0.25, 0.0),
        (0.25, 0.75, 0.0),
        (0.0, 0.75, 0.25),
        (0.0, 0.25, 0.75),
        (0.75, 0.0, 0.25),
        (0.25, 0.0, 0.75),
    ]
    for w0, w1, w2 in bary_weights:
        su = w0 * u0 + w1 * u1 + w2 * u2
        sp = w0 * p0 + w1 * p1 + w2 * p2
        sn = w0 * n0 + w1 * n1 + w2 * n2
        px = np.clip((su[:, 0] * (size - 1)).astype(np.int32), 0, size - 1)
        py = np.clip((su[:, 1] * (size - 1)).astype(np.int32), 0, size - 1)
        pos_grid[py, px] = sp
        nrm_grid[py, px] = sn
        valid[py, px] = True

    t_px = np.stack([u0, u1, u2], axis=1) * (size - 1)
    span = t_px.max(axis=1) - t_px.min(axis=1)
    big_ids = np.where(((span[:, 0] > 0.35) | (span[:, 1] > 0.35)) & (span[:, 0] < size * 0.45) & (span[:, 1] < size * 0.45))[0]
    # Sort back-to-front (+Z front face/eyes write last and win over any overlapping back-of-head UVs)
    big_ids = big_ids[np.argsort(p0[big_ids, 2] + p1[big_ids, 2] + p2[big_ids, 2])]
    for ti in big_ids:
        tri_uv = t_px[ti]
        min_x = max(int(math.floor(tri_uv[:, 0].min())), 0)
        max_x = min(int(math.ceil(tri_uv[:, 0].max())), size - 1)
        min_y = max(int(math.floor(tri_uv[:, 1].min())), 0)
        max_y = min(int(math.ceil(tri_uv[:, 1].max())), size - 1)
        if max_x < min_x or max_y < min_y:
            continue
        ax, ay = tri_uv[0]
        bx, by = tri_uv[1]
        cx, cy = tri_uv[2]
        denom = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy)
        if abs(denom) < 1e-5:
            continue
        gx, gy = np.meshgrid(
            np.arange(min_x, max_x + 1, dtype=np.float32),
            np.arange(min_y, max_y + 1, dtype=np.float32),
        )
        w0 = ((by - cy) * (gx - cx) + (cx - bx) * (gy - cy)) / denom
        w1 = ((cy - ay) * (gx - cx) + (ax - cx) * (gy - cy)) / denom
        w2 = 1.0 - w0 - w1
        inside = (w0 >= -0.02) & (w1 >= -0.02) & (w2 >= -0.02)
        if not inside.any():
            continue
        iy, ix = np.where(inside)
        bw0, bw1, bw2 = w0[inside][:, None], w1[inside][:, None], w2[inside][:, None]
        pos_grid[min_y + iy, min_x + ix] = bw0 * p0[ti] + bw1 * p1[ti] + bw2 * p2[ti]
        nrm_grid[min_y + iy, min_x + ix] = bw0 * n0[ti] + bw1 * n1[ti] + bw2 * n2[ti]
        valid[min_y + iy, min_x + ix] = True

    nearest_idx = distance_transform_edt(~valid, return_distances=False, return_indices=True)
    pos_grid = pos_grid[nearest_idx[0], nearest_idx[1]]
    nrm_grid = nrm_grid[nearest_idx[0], nearest_idx[1]]
    n_len = np.maximum(np.linalg.norm(nrm_grid, axis=-1, keepdims=True), 1e-5)
    nrm_grid = nrm_grid / n_len
    return pos_grid, nrm_grid


def extract_embedded_image(gltf: dict, bin_chunk: bytes, img_index: int) -> Image.Image:
    bv = gltf["bufferViews"][gltf["images"][img_index]["bufferView"]]
    off = bv.get("byteOffset", 0)
    length = bv["byteLength"]
    return Image.open(io.BytesIO(bin_chunk[off : off + length]))


def encode_png(img: Image.Image) -> bytes:
    buf = io.BytesIO()
    img.save(buf, format="PNG", optimize=False, compress_level=6)
    return buf.getvalue()


def resize_normal_map(img: Image.Image, size: int) -> Image.Image:
    resized = img.convert("RGB").resize((size, size), Image.Resampling.LANCZOS)
    arr = np.asarray(resized, dtype=np.float32) / 255.0 * 2.0 - 1.0
    length = np.maximum(np.linalg.norm(arr, axis=-1, keepdims=True), 1e-5)
    arr = arr / length
    out = np.clip((arr * 0.5 + 0.5) * 255.0, 0.0, 255.0).astype(np.uint8)
    return Image.fromarray(out, mode="RGB")


def rebuild_glb_with_images(gltf: dict, bin_chunk: bytes, png_bytes_list: list[bytes]) -> bytes:
    gltf_copy = json.loads(json.dumps(gltf))
    img_bv_map: dict[int, bytes] = {}
    for img_idx, png_bytes in enumerate(png_bytes_list):
        bv_idx = gltf_copy["images"][img_idx]["bufferView"]
        img_bv_map[bv_idx] = png_bytes
        gltf_copy["images"][img_idx]["mimeType"] = "image/png"

    order = sorted(
        range(len(gltf_copy["bufferViews"])),
        key=lambda idx: gltf_copy["bufferViews"][idx].get("byteOffset", 0),
    )
    out_bin = bytearray()
    for bv_idx in order:
        bv = gltf_copy["bufferViews"][bv_idx]
        if bv_idx in img_bv_map:
            chunk = img_bv_map[bv_idx]
        else:
            old_off = bv.get("byteOffset", 0)
            old_len = bv["byteLength"]
            chunk = bin_chunk[old_off : old_off + old_len]
        while len(out_bin) % 4 != 0:
            out_bin.append(0)
        bv["byteOffset"] = len(out_bin)
        bv["byteLength"] = len(chunk)
        out_bin.extend(chunk)
    while len(out_bin) % 4 != 0:
        out_bin.append(0)

    gltf_copy["buffers"][0]["byteLength"] = len(out_bin)
    json_bytes = json.dumps(gltf_copy, separators=(",", ":")).encode("utf-8")
    while len(json_bytes) % 4 != 0:
        json_bytes += b" "

    total_len = 12 + 8 + len(json_bytes) + 8 + len(out_bin)
    header = struct.pack("<4sII", b"glTF", 2, total_len)
    json_hdr = struct.pack("<II", len(json_bytes), 0x4E4F534A)
    bin_hdr = struct.pack("<II", len(out_bin), 0x004E4942)
    return header + json_hdr + json_bytes + bin_hdr + bytes(out_bin)


# ---------------------------------------------------------------------------
# 90s Filipino Komiks Outfit Texture Painting (Calibrated Chibi 3D Anatomy)
# ---------------------------------------------------------------------------


def luminance(rgb: np.ndarray) -> np.ndarray:
    return 0.299 * rgb[..., 0] + 0.587 * rgb[..., 1] + 0.114 * rgb[..., 2]


def front_face_oval(pos: np.ndarray) -> np.ndarray:
    """Front face zone (eyes, brows, nose, cheeks, mouth): y in [1.13, 1.345], |x| < 0.175, z > 0.065."""
    x, y, z = pos[..., 0], pos[..., 1], pos[..., 2]
    return (np.abs(x) < 0.175) & (y > 1.13) & (y < 1.345) & (z > 0.065)


def detect_skin(rgb: np.ndarray, pos: np.ndarray) -> np.ndarray:
    """Detect warm peach skin texels while strictly excluding sclera, irises, lashes, and eyebrows."""
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    skin_hue = (r > 152.0) & (g > 88.0) & (b > 54.0) & ((r - g) > 36.0) & ((g - b) > 16.0)
    in_face = front_face_oval(pos)
    eye_or_brow = in_face & ((b > 152.0) | (r < 152.0))
    return skin_hue & (~eye_or_brow)


def protect_facial_features(rgb: np.ndarray, pos: np.ndarray) -> np.ndarray:
    """Protect eyes, sclera, irises, lashes, eyebrows, and mouth interior across the head."""
    in_face = front_face_oval(pos)
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    not_skin = ~((r > 162.0) & ((r - g) > 38.0) & ((g - b) > 18.0))
    # Protect neutral white/grey eyeball sclera anywhere on the head (even if UV islands touch hair/bandana)
    head_sclera = (
        (pos[..., 1] > 1.08)
        & (r > 165.0)
        & (g > 165.0)
        & (b > 165.0)
        & (np.abs(r - g) < 16.0)
        & (np.abs(g - b) < 16.0)
    )
    return (in_face & not_skin) | head_sclera


def woven_grain(pos: np.ndarray, scale: float = 190.0, strength: float = 0.035) -> np.ndarray:
    w1 = np.sin((pos[..., 0] + pos[..., 2]) * scale) * np.cos(pos[..., 1] * scale * 1.15)
    w2 = np.sin((pos[..., 0] - pos[..., 2]) * scale * 0.7)
    return 1.0 + strength * (0.65 * w1 + 0.35 * w2)


def normal_cloth_shade(nrm: np.ndarray) -> np.ndarray:
    return 0.83 + 0.19 * np.clip(nrm[..., 1] * 0.55 + nrm[..., 2] * 0.55, -0.65, 1.0)


def clamp_cloth(arr: np.ndarray, mask: np.ndarray) -> None:
    arr[mask] = np.clip(arr[mask], 8.0, MAX_CLOTH_VAL)


def paint_kiko(rgb: np.ndarray, pos: np.ndarray, nrm: np.ndarray) -> np.ndarray:
    """Kiko (from migo.glb): crimson kalye bandana, sleeveless maong denim vest over cream tee,
    rolled khaki shorts, leather trumpo belt pouch, brown tsinelas, espresso curls.
    Migo anatomy: shoes y<0.12, shins y[0.12..0.45], shorts/pouch y[0.45..0.81],
    tee/vest y[0.81..1.17] (+ waist knot y>=0.73), face y[1.15..1.35], bandana y[1.33..1.55], hair y[1.08..1.70]."""
    out = rgb.copy()
    x, y, z = pos[..., 0], pos[..., 1], pos[..., 2]
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    skin = detect_skin(rgb, pos)
    face_oval = front_face_oval(pos)
    face_feat = protect_facial_features(rgb, pos)
    n_shade = normal_cloth_shade(nrm)

    # Slight warm kalye tan on skin
    skin_edit = skin & (~face_feat)
    out[skin_edit, 0] = np.clip(r[skin_edit] * 0.95, 0, 240)
    out[skin_edit, 1] = np.clip(g[skin_edit] * 0.92, 0, 222)
    out[skin_edit, 2] = np.clip(b[skin_edit] * 0.88, 0, 200)

    # 1. Forehead Bandana -> full crimson kalye bandana (#B2282E) including temple wrap above ears
    is_bandana = (
        ((y > 1.34) | ((y > 1.22) & (b > 115.0)))
        & (y < 1.56)
        & (~skin)
        & (~face_oval)
        & (~face_feat)
        & ((b > 105.0) | (g > 105.0) | (r > 155.0))
    )
    b_shade = np.clip(n_shade * woven_grain(pos, 220.0, 0.03), 0.68, 1.05)
    out[is_bandana] = np.array([178.0, 40.0, 46.0], dtype=np.float32) * b_shade[is_bandana, None]
    diamond = np.abs(np.sin((x * 34.0 + z * 34.0))) + np.abs((y - 1.425) * 44.0)
    band_trim = is_bandana & ((diamond < 0.72) | (np.abs(y - 1.365) < 0.008))
    out[band_trim] = np.array([196.0, 184.0, 156.0], dtype=np.float32) * b_shade[band_trim, None]
    band_ink = is_bandana & ((np.abs(y - 1.352) < 0.006) | (np.abs(y - 1.495) < 0.006))
    out[band_ink] = INK_RGB
    clamp_cloth(out, is_bandana)

    # 2. Curly Hair -> rich espresso-black curls
    is_hair = (y > 1.08) & (~skin) & (~face_oval) & (~face_feat) & (~is_bandana) & (r < 168.0) & (b < 105.0)
    h_lum = np.clip(luminance(rgb) / 72.0, 0.35, 1.45)
    out[is_hair, 0] = np.clip(34.0 * h_lum[is_hair], 12.0, 66.0)
    out[is_hair, 1] = np.clip(26.0 * h_lum[is_hair], 9.0, 48.0)
    out[is_hair, 2] = np.clip(25.0 * h_lum[is_hair], 9.0, 46.0)

    # 3. Torso -> sleeveless faded maong denim vest over cream kalye tee (y in [0.81..1.17] + pastel waist knot)
    pastel_shirt_knot = (y >= 0.72) & (y < 0.82) & (r > 152.0) & (g > 130.0) & ((b > 145.0) | (g > 165.0))
    is_top = (((y >= 0.815) & (y <= 1.175)) | pastel_shirt_knot) & (~skin) & (~face_oval) & (~is_hair) & (~is_bandana)
    t_shade = np.clip(n_shade * woven_grain(pos, 250.0, 0.04), 0.68, 1.05)

    # Sleeves (|x| > 0.145), collar rim (y > 1.095 & |x| < 0.095), and center chest strip (|x| < 0.046 & z > 0.02) -> cream tee
    is_tee = is_top & (
        (np.abs(x) > 0.145)
        | ((y > 1.095) & (np.abs(x) < 0.095))
        | ((np.abs(x) < 0.046) & (z > 0.02) & (y > 0.83))
    )
    is_vest = is_top & (~is_tee)
    out[is_tee] = np.array([194.0, 184.0, 162.0], dtype=np.float32) * t_shade[is_tee, None]
    out[is_vest] = np.array([56.0, 88.0, 134.0], dtype=np.float32) * t_shade[is_vest, None]

    # Left chest pocket, copper stitching, brass buttons & charcoal ink lapels
    pocket = is_vest & (x > 0.060) & (x < 0.122) & (y > 0.93) & (y < 1.01) & (z > 0.04)
    out[pocket] = np.array([44.0, 72.0, 112.0], dtype=np.float32) * t_shade[pocket, None]
    stitch = is_vest & (
        ((np.abs(np.abs(x) - 0.054) < 0.004) & (z > 0.02))
        | ((np.abs(y - 1.005) < 0.004) & (x > 0.060) & (x < 0.122) & (z > 0.04))
    )
    out[stitch] = np.array([196.0, 124.0, 52.0], dtype=np.float32)
    buttons = is_vest & (np.abs(x + 0.062) < 0.007) & (z > 0.04) & (
        (np.abs(y - 0.89) < 0.007) | (np.abs(y - 0.96) < 0.007) | (np.abs(y - 1.03) < 0.007)
    )
    out[buttons] = np.array([198.0, 152.0, 56.0], dtype=np.float32)
    vest_ink = is_top & (
        ((np.abs(np.abs(x) - 0.046) < 0.0045) & (z > 0.02) & (y > 0.83))
        | (np.abs(np.abs(x) - 0.145) < 0.005)
        | ((np.abs(y - 1.095) < 0.004) & (np.abs(x) < 0.095))
    )
    out[vest_ink] = INK_RGB
    clamp_cloth(out, is_top)

    # 4. Shorts & Saddle-Brown Leather Trumpo Belt Pouch (y in [0.43..0.815])
    is_lower = (y >= 0.43) & (y < 0.82) & (~skin) & (~is_top)
    is_belt = is_lower & (r < 52.0) & (g < 52.0) & (b < 60.0)
    is_pouch = is_lower & (~is_belt) & (
        ((b > g + 22.0) & (r > 68.0) & (z > 0.04))
        | ((r > 170.0) & (g > 145.0) & (b < 140.0) & (z > 0.05))
    )
    is_shorts = is_lower & (~is_belt) & (~is_pouch)
    s_shade = np.clip(n_shade * woven_grain(pos, 200.0, 0.03), 0.68, 1.05)
    out[is_shorts] = np.array([146.0, 124.0, 86.0], dtype=np.float32) * s_shade[is_shorts, None]
    is_cuff = is_shorts & (y < 0.565)
    out[is_cuff] = np.array([172.0, 150.0, 108.0], dtype=np.float32) * s_shade[is_cuff, None]
    shorts_ink = is_shorts & (
        (np.abs(y - 0.565) < 0.005)
        | ((np.abs(x) < 0.005) & (y > 0.59) & (z > 0.04))
    )
    out[shorts_ink] = INK_RGB

    if is_pouch.any():
        out[is_pouch] = np.array([118.0, 64.0, 34.0], dtype=np.float32) * s_shade[is_pouch, None]
        is_buckle = is_pouch & (r > 170.0) & (g > 145.0) & (b < 140.0)
        out[is_buckle] = np.array([196.0, 148.0, 54.0], dtype=np.float32)
        pouch_ink = binary_dilation(is_pouch, iterations=1) & (~is_pouch) & is_shorts
        out[pouch_ink] = INK_RGB
    out[is_belt] = np.array([40.0, 26.0, 20.0], dtype=np.float32)
    clamp_cloth(out, is_lower)

    # 5. Footwear -> warm brown leather kalye tsinelas (using normal shade so blue strap is 100% replaced)
    is_shoe = (y < 0.12) & (~skin)
    out[is_shoe] = np.array([92.0, 58.0, 36.0], dtype=np.float32) * np.clip(n_shade[is_shoe, None], 0.72, 1.05)
    sole = is_shoe & (y < 0.024)
    out[sole] = np.array([178.0, 130.0, 52.0], dtype=np.float32)
    clamp_cloth(out, is_shoe)
    return out


def paint_nene(rgb: np.ndarray, pos: np.ndarray, nrm: np.ndarray) -> np.ndarray:
    """Nene (from ligaya.glb): mango-gold flower scrunchie, espresso wavy hair,
    90s sky-blue chambray pinafore jumper over cream blouse, striped knee socks, dark plum flats.
    Ligaya anatomy: flats y<0.12, shins y[0.06..0.38], skirt y[0.39..0.77], bodice/straps y[0.77..1.17],
    T-pose bare arms |x|>0.145 & y[0.82..1.15], flower clip y>1.44."""
    out = rgb.copy()
    x, y, z = pos[..., 0], pos[..., 1], pos[..., 2]
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    skin = detect_skin(rgb, pos)
    face_oval = front_face_oval(pos)
    face_feat = protect_facial_features(rgb, pos)
    n_shade = normal_cloth_shade(nrm)

    bare_arm = (np.abs(x) > 0.142) & (y > 0.80) & (y < 1.16) & skin
    bare_neck = (y > 1.04) & (np.abs(x) < 0.070) & skin
    bare_forehead = (y >= 1.13) & (y < 1.46) & (z > 0.07) & skin
    bare_leg = (y < 0.415) & skin
    protected_skin = face_oval | face_feat | bare_arm | bare_neck | bare_forehead | bare_leg

    # 1. Hair Flower Scrunchie Clip (strictly y > 1.45 & non-skin so forehead/eyelids are never touched)
    is_clip = (y > 1.45) & (z > 0.0) & (~protected_skin) & (~skin) & (r > 180.0) & ((b > 120.0) | (g > 155.0))
    is_clip_center = is_clip & (g > 165.0) & (b < 155.0)
    out[is_clip] = np.array([198.0, 144.0, 38.0], dtype=np.float32) * np.clip(n_shade[is_clip, None], 0.76, 1.05)
    out[is_clip_center] = np.array([188.0, 68.0, 52.0], dtype=np.float32)
    clip_border = binary_dilation(is_clip, iterations=1) & (~is_clip) & (y > 1.42)
    out[clip_border] = INK_RGB

    # 2. Wavy Hair -> glossy espresso-black Filipino waves (strictly outside face_oval)
    is_hair = (
        (y > 0.78)
        & (~protected_skin)
        & (~is_clip)
        & (r < 176.0)
        & (b < 90.0)
        & ((r - b) > 18.0)
        & ((y > 1.15) | (z < -0.01) | (np.abs(x) > 0.138))
    )
    h_lum = np.clip(luminance(rgb) / 82.0, 0.35, 1.40)
    out[is_hair, 0] = np.clip(32.0 * h_lum[is_hair], 12.0, 62.0)
    out[is_hair, 1] = np.clip(25.0 * h_lum[is_hair], 10.0, 48.0)
    out[is_hair, 2] = np.clip(30.0 * h_lum[is_hair], 12.0, 56.0)

    # 3. Dress -> 90s Sky-Blue Chambray Pinafore Jumper over Cream Blouse (y in [0.39..1.17])
    is_dress = (
        (y >= 0.395)
        & (y <= 1.175)
        & (~protected_skin)
        & (~is_hair)
        & (~is_clip)
        & ((np.abs(x) < 0.31) | (y < 0.79))
    )
    d_shade = np.clip(n_shade * woven_grain(pos, 230.0, 0.032), 0.68, 1.04)

    # Cream blouse in upper center chest/back neckline; sky-blue jumper straps, bib & pleated skirt
    is_blouse = is_dress & (y > 0.945) & (np.abs(x) < 0.066)
    is_jumper = is_dress & (~is_blouse)
    out[is_blouse] = np.array([196.0, 188.0, 168.0], dtype=np.float32) * d_shade[is_blouse, None]
    out[is_jumper] = np.array([84.0, 134.0, 188.0], dtype=np.float32) * d_shade[is_jumper, None]

    waist = is_jumper & (y >= 0.745) & (y <= 0.790)
    out[waist] = np.array([64.0, 108.0, 162.0], dtype=np.float32) * d_shade[waist, None]
    hem_trim = is_jumper & (y >= 0.415) & (y <= 0.442)
    out[hem_trim] = np.array([196.0, 152.0, 48.0], dtype=np.float32) * d_shade[hem_trim, None]
    bib_buttons = is_jumper & (z > 0.04) & (np.abs(y - 0.922) < 0.010) & (np.abs(np.abs(x) - 0.068) < 0.010)
    out[bib_buttons] = np.array([198.0, 154.0, 44.0], dtype=np.float32)

    angle = np.arctan2(x, z)
    pleat_line = (y < 0.745) & (y > 0.442) & (np.abs(np.sin(angle * 8.0)) < 0.050)
    dress_ink = is_dress & (
        ((np.abs(y - 0.945) < 0.005) & (np.abs(x) < 0.070))
        | ((y > 0.945) & (np.abs(np.abs(x) - 0.066) < 0.005))
        | (np.abs(y - 0.790) < 0.0045)
        | (np.abs(y - 0.745) < 0.0045)
        | (np.abs(y - 0.442) < 0.0035)
        | pleat_line
    )
    out[dress_ink] = INK_RGB
    clamp_cloth(out, is_dress)

    # 4. Ribbed Cream Knee Socks with Navy & Gold Varsity Stripes (y in [0.055..0.305], leaving bare knees above!)
    is_socks = (y >= 0.055) & (y < 0.305) & skin
    sock_rib = 1.0 + 0.03 * np.sin(np.arctan2(x, z) * 28.0)
    sk_shade = np.clip(n_shade * sock_rib, 0.74, 1.02)
    out[is_socks] = np.array([194.0, 188.0, 176.0], dtype=np.float32) * sk_shade[is_socks, None]
    stripe_navy = is_socks & (y >= 0.278) & (y < 0.293)
    stripe_gold = is_socks & (y >= 0.260) & (y < 0.274)
    sock_top_ink = is_socks & (y >= 0.298)
    out[stripe_navy] = np.array([42.0, 72.0, 118.0], dtype=np.float32) * sk_shade[stripe_navy, None]
    out[stripe_gold] = np.array([196.0, 146.0, 42.0], dtype=np.float32) * sk_shade[stripe_gold, None]
    out[sock_top_ink] = INK_RGB
    clamp_cloth(out, is_socks)

    # 5. Footwear -> dark plum-navy Mary Jane flats
    is_shoe = (y < 0.118) & (~skin)
    out[is_shoe] = np.array([42.0, 34.0, 54.0], dtype=np.float32) * np.clip(n_shade[is_shoe, None], 0.70, 1.05)
    sole = is_shoe & (y < 0.022)
    out[sole] = np.array([178.0, 170.0, 156.0], dtype=np.float32)
    clamp_cloth(out, is_shoe)
    return out


def paint_buknoy(rgb: np.ndarray, pos: np.ndarray, nrm: np.ndarray) -> np.ndarray:
    """Buknoy (from junjun.glb): sun-tanned kayumanggi skin, jet-black spiky hair,
    open terracotta/ochre/teal checkered provincial camisa over cream tee,
    frayed indigo shorts with gagamba matchbox, leaf-green tsinelas.
    Junjun anatomy: tsinelas y<0.11, shins y[0.11..0.42], shorts y[0.42..0.755],
    sando y[0.755..1.185], T-pose shoulders y[0.98..1.15]."""
    out = rgb.copy()
    x, y, z = pos[..., 0], pos[..., 1], pos[..., 2]
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    skin = detect_skin(rgb, pos)
    face_oval = front_face_oval(pos)
    face_feat = protect_facial_features(rgb, pos)
    n_shade = normal_cloth_shade(nrm)

    # 1. Sun-tanned provincial kayumanggi skin
    skin_edit = skin & (~face_feat)
    out[skin_edit, 0] = np.clip(r[skin_edit] * 0.84, 0, 224)
    out[skin_edit, 1] = np.clip(g[skin_edit] * 0.77, 0, 190)
    out[skin_edit, 2] = np.clip(b[skin_edit] * 0.68, 0, 158)

    # 2. Spiky Hair -> jet-black with subtle slate-blue comic sheen
    is_hair = (y > 1.18) & (~skin) & (~face_oval) & (r < 128.0) & (b < 72.0)
    h_lum = np.clip(luminance(rgb) / 48.0, 0.35, 1.50)
    out[is_hair, 0] = np.clip(24.0 * h_lum[is_hair], 10.0, 46.0)
    out[is_hair, 1] = np.clip(24.0 * h_lum[is_hair], 10.0, 46.0)
    out[is_hair, 2] = np.clip(32.0 * h_lum[is_hair], 13.0, 58.0)

    # 3. Open Provincial Checkered Camisa (full sando y[0.755..1.185] + rolled upper-shoulder sleeves)
    is_sando = (y >= 0.755) & (y <= 1.185) & (~skin) & (~face_oval) & (~is_hair) & (np.abs(x) < 0.19)
    is_sleeve = (y >= 0.985) & (y <= 1.155) & (np.abs(x) >= 0.115) & (np.abs(x) <= 0.225) & skin
    is_top = is_sando | is_sleeve
    c_shade = np.clip(n_shade * woven_grain(pos, 220.0, 0.038), 0.68, 1.04)

    is_inner_tee = is_sando & (np.abs(x) < 0.038) & (z > 0.01)
    is_camisa = is_top & (~is_inner_tee)
    out[is_inner_tee] = np.array([194.0, 184.0, 156.0], dtype=np.float32) * c_shade[is_inner_tee, None]

    u_plaid = np.mod((x + 0.55 * z) * 26.0, 1.0)
    v_plaid = np.mod(y * 26.0, 1.0)
    band_u = u_plaid < 0.46
    band_v = v_plaid < 0.46
    thin_line = (np.abs(u_plaid - 0.73) < 0.045) | (np.abs(v_plaid - 0.73) < 0.045)

    plaid_rgb = np.zeros_like(out)
    plaid_rgb[:] = np.array([188.0, 138.0, 56.0], dtype=np.float32)
    plaid_rgb[band_u ^ band_v] = np.array([174.0, 68.0, 44.0], dtype=np.float32)
    plaid_rgb[band_u & band_v] = np.array([42.0, 88.0, 80.0], dtype=np.float32)
    plaid_rgb[thin_line] = np.array([196.0, 178.0, 138.0], dtype=np.float32)

    out[is_camisa] = plaid_rgb[is_camisa] * c_shade[is_camisa, None]
    sleeve_cuff = is_sleeve & (np.abs(x) > 0.200)
    out[sleeve_cuff] = INK_RGB
    camisa_ink = is_top & (
        ((np.abs(np.abs(x) - 0.038) < 0.005) & (z > 0.01))
        | (np.abs(y - 0.762) < 0.005)
    )
    out[camisa_ink] = INK_RGB
    buttons = is_camisa & (np.abs(x - 0.050) < 0.006) & (z > 0.04) & (
        (np.abs(y - 0.82) < 0.006) | (np.abs(y - 0.90) < 0.006) | (np.abs(y - 0.98) < 0.006)
    )
    out[buttons] = np.array([62.0, 40.0, 26.0], dtype=np.float32)
    clamp_cloth(out, is_top)

    # 4. Frayed Indigo Shorts (y in [0.41..0.755]) + Gagamba Matchbox at Hip
    is_shorts = (y >= 0.41) & (y < 0.755) & (~skin) & (~is_top)
    s_shade = np.clip(n_shade * woven_grain(pos, 240.0, 0.04), 0.68, 1.04)
    out[is_shorts] = np.array([46.0, 72.0, 106.0], dtype=np.float32) * s_shade[is_shorts, None]
    frayed_hem = is_shorts & (y < 0.465 + 0.008 * np.sin(x * 95.0))
    out[frayed_hem] = np.array([96.0, 122.0, 152.0], dtype=np.float32) * s_shade[frayed_hem, None]

    matchbox = is_shorts & (x > 0.062) & (x < 0.132) & (y > 0.61) & (y < 0.695) & (z > 0.03)
    if matchbox.any():
        out[matchbox] = np.array([194.0, 138.0, 46.0], dtype=np.float32) * s_shade[matchbox, None]
        striker = matchbox & (y > 0.668)
        out[striker] = np.array([156.0, 42.0, 38.0], dtype=np.float32)
        mb_ink = binary_dilation(matchbox, iterations=1) & (~matchbox) & is_shorts
        out[mb_ink] = INK_RGB
    shorts_ink = is_shorts & (
        (np.abs(y - 0.465) < 0.004)
        | ((np.abs(x) < 0.005) & (y > 0.54) & (z > 0.03))
    )
    out[shorts_ink] = INK_RGB
    clamp_cloth(out, is_shorts)

    # 5. Footwear -> earthy leaf-green & ochre rubber tsinelas
    is_shoe = (y < 0.11) & (~skin)
    out[is_shoe] = np.array([44.0, 108.0, 68.0], dtype=np.float32) * np.clip(n_shade[is_shoe, None], 0.72, 1.05)
    sole = is_shoe & (y < 0.022)
    out[sole] = np.array([178.0, 132.0, 50.0], dtype=np.float32)
    clamp_cloth(out, is_shoe)
    return out


def paint_tala(rgb: np.ndarray, pos: np.ndarray, nrm: np.ndarray) -> np.ndarray:
    """Tala (from toni.glb): warm morena skin, maroon & woven habi backward cap,
    maroon barangay track singlet (#7 in barrio-gold), plum-charcoal sprint shorts,
    woven friendship bracelets on wrists, maroon/gold track shoes.
    Toni anatomy: shoes y<0.115, shins y[0.115..0.34], shorts y[0.34..0.665] (|x|<0.245),
    singlet y[0.665..1.155], A-pose wrists |x|>0.235 & y[0.705..0.760], cap y>1.36."""
    out = rgb.copy()
    x, y, z = pos[..., 0], pos[..., 1], pos[..., 2]
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    skin = detect_skin(rgb, pos)
    face_oval = front_face_oval(pos)
    face_feat = protect_facial_features(rgb, pos)
    n_shade = normal_cloth_shade(nrm)

    # 1. Warm golden morena skin + woven habi wrist friendship bands (on forearms y[0.705..0.760], above hands!)
    skin_edit = skin & (~face_feat)
    out[skin_edit, 0] = np.clip(r[skin_edit] * 0.90, 0, 232)
    out[skin_edit, 1] = np.clip(g[skin_edit] * 0.83, 0, 200)
    out[skin_edit, 2] = np.clip(b[skin_edit] * 0.76, 0, 170)

    wrist_band = skin_edit & (np.abs(x) > 0.235) & (y >= 0.705) & (y <= 0.758)
    if wrist_band.any():
        band_phase = np.mod((y - 0.705) / 0.053 * 3.0, 1.0)
        wb_col = np.zeros_like(out)
        wb_col[:] = np.array([182.0, 42.0, 48.0], dtype=np.float32)
        wb_col[band_phase > 0.33] = np.array([196.0, 144.0, 40.0], dtype=np.float32)
        wb_col[band_phase > 0.66] = np.array([38.0, 114.0, 106.0], dtype=np.float32)
        out[wrist_band] = wb_col[wrist_band] * np.clip(n_shade[wrist_band, None], 0.76, 1.04)
        wb_ink = skin_edit & (np.abs(x) > 0.235) & ((np.abs(y - 0.705) < 0.004) | (np.abs(y - 0.758) < 0.004))
        out[wb_ink] = INK_RGB

    # 2. Backward Cap (y > 1.36) & Espresso-Black Hair (100% clean, zero red bounce flecks)
    is_strap = (y > 1.36) & (y < 1.49) & (~skin) & (~face_oval) & (~face_feat) & (np.abs(r - g) < 24.0) & (r > 35.0) & (r < 120.0) & (z > 0.04)
    cap_body = (y > 1.36) & (~skin) & (~face_oval) & (~face_feat) & (~is_strap) & (r > 125.0) & ((r - g) > 42.0)
    is_cap = cap_body | is_strap

    cap_shade = np.clip(n_shade * woven_grain(pos, 210.0, 0.03), 0.68, 1.05)
    out[cap_body] = np.array([112.0, 30.0, 44.0], dtype=np.float32) * cap_shade[cap_body, None]
    # Woven habi geometric diamond headband around lower rim of backward cap (y in [1.39..1.455])
    habi_band = cap_body & (y >= 1.390) & (y <= 1.455)
    angle = np.arctan2(x, z)
    dia = np.abs(np.sin(angle * 14.0)) + np.abs((y - 1.422) * 34.0)
    habi_gold = habi_band & (dia < 0.82)
    habi_teal = habi_band & (dia < 0.42)
    out[habi_gold] = np.array([196.0, 142.0, 40.0], dtype=np.float32) * cap_shade[habi_gold, None]
    out[habi_teal] = np.array([38.0, 108.0, 98.0], dtype=np.float32) * cap_shade[habi_teal, None]
    top_btn = cap_body & (y > 1.685) & ((x * x + z * z) < 0.0025)
    out[top_btn] = np.array([196.0, 142.0, 40.0], dtype=np.float32)
    cap_ink = cap_body & ((np.abs(y - 1.390) < 0.004) | (np.abs(y - 1.455) < 0.004))
    out[cap_ink] = INK_RGB
    out[is_strap] = np.array([38.0, 32.0, 42.0], dtype=np.float32)
    clamp_cloth(out, is_cap)

    is_hair = (y > 1.12) & (~skin) & (~face_oval) & (~face_feat) & (~is_cap)
    h_lum = np.clip(luminance(rgb) / 58.0, 0.35, 1.35)
    out[is_hair, 0] = np.clip(28.0 * h_lum[is_hair], 10.0, 48.0)
    out[is_hair, 1] = np.clip(22.0 * h_lum[is_hair], 9.0, 40.0)
    out[is_hair, 2] = np.clip(28.0 * h_lum[is_hair], 10.0, 46.0)

    # 3. Maroon Barangay Track Singlet (y in [0.665..1.155], erasing #11 completely!)
    is_jersey = (
        (y >= 0.665)
        & (y <= 1.155)
        & (~skin)
        & (~face_oval)
        & (~is_hair)
        & (~is_cap)
        & (np.abs(x) < 0.19)
    )
    j_shade = np.clip(n_shade * woven_grain(pos, 250.0, 0.032), 0.70, 1.05)
    out[is_jersey] = np.array([110.0, 28.0, 44.0], dtype=np.float32) * j_shade[is_jersey, None]

    # Collar & armhole trim ONLY near outer neckline/armholes (never on center chest where #11 lived!)
    border_zone = (y > 1.045) | (np.abs(x) > 0.112)
    was_trim = is_jersey & (
        (y < 0.702)
        | (border_zone & ((r > 135.0) | ((r > 120.0) & (g < 70.0))))
    )
    out[was_trim] = np.array([196.0, 144.0, 40.0], dtype=np.float32) * j_shade[was_trim, None]
    bottom_cream = is_jersey & (y < 0.684)
    out[bottom_cream] = np.array([194.0, 186.0, 168.0], dtype=np.float32) * j_shade[bottom_cream, None]

    # Crisp barrio-gold "07" varsity sprinter number centered on front chest (y in [0.80..0.94], z > 0.05)
    # Viewer-left has x < 0 ('0' at x = -0.032), viewer-right has x > 0 ('7' at x in [0.006, 0.056])
    in_chest = is_jersey & (z > 0.05) & (y >= 0.80) & (y <= 0.94)
    ox = (x + 0.032) / 0.023
    oy = (y - 0.87) / 0.062
    ring_0 = in_chest & (np.abs(ox) <= 1.05) & (np.abs(oy) <= 1.05) & ((ox * ox + oy * oy) <= 1.08) & ((ox * ox + oy * oy) >= 0.30)
    top_7 = in_chest & (x >= 0.006) & (x <= 0.056) & (y >= 0.912) & (y <= 0.936)
    diag_7 = in_chest & (y >= 0.805) & (y <= 0.932) & (np.abs(x - (0.016 + (y - 0.805) * 0.26)) < 0.012)
    digit_07 = ring_0 | top_7 | diag_7
    digit_outline = binary_dilation(digit_07, iterations=2) & (~digit_07) & is_jersey
    out[digit_outline] = INK_RGB
    out[digit_07] = np.array([198.0, 150.0, 42.0], dtype=np.float32) * j_shade[digit_07, None]
    jersey_ink = is_jersey & (np.abs(y - 0.684) < 0.004)
    out[jersey_ink] = INK_RGB
    clamp_cloth(out, is_jersey)

    # 4. Dark Plum-Charcoal Athletic Runner Shorts (y in [0.33..0.665], |x| < 0.245)
    is_shorts = (y >= 0.33) & (y < 0.665) & (~skin) & (~is_jersey) & (np.abs(x) < 0.245)
    s_shade = np.clip(n_shade * woven_grain(pos, 220.0, 0.03), 0.70, 1.05)
    out[is_shorts] = np.array([40.0, 34.0, 48.0], dtype=np.float32) * s_shade[is_shorts, None]
    side_stripe = is_shorts & (np.abs(x) > 0.145) & (np.abs(x) < 0.172)
    out[side_stripe] = np.array([196.0, 144.0, 40.0], dtype=np.float32) * s_shade[side_stripe, None]
    hem_maroon = is_shorts & (y < 0.375)
    out[hem_maroon] = np.array([110.0, 28.0, 44.0], dtype=np.float32) * s_shade[hem_maroon, None]
    clamp_cloth(out, is_shorts)

    # 5. Track Shoes -> maroon & barrio-gold sprint shoes
    is_shoe = (y < 0.115) & (~skin)
    out[is_shoe] = np.array([82.0, 26.0, 38.0], dtype=np.float32) * np.clip(n_shade[is_shoe, None], 0.72, 1.06)
    shoe_gold = is_shoe & (y >= 0.024) & (y < 0.044)
    shoe_sole = is_shoe & (y < 0.020)
    out[shoe_gold] = np.array([196.0, 144.0, 40.0], dtype=np.float32)
    out[shoe_sole] = np.array([186.0, 180.0, 168.0], dtype=np.float32)
    clamp_cloth(out, is_shoe)
    return out


PAINTERS = {
    "kiko": paint_kiko,
    "nene": paint_nene,
    "buknoy": paint_buknoy,
    "tala": paint_tala,
}


# ---------------------------------------------------------------------------
# Godot .import File Writers (Matching Existing Repository Settings)
# ---------------------------------------------------------------------------


def write_glb_import(glb_path: Path, kid_id: str) -> None:
    content = f"""[remap]

importer="scene"
importer_version=1
type="PackedScene"

[deps]

source_file="res://assets/models/characters/{kid_id}.glb"

[params]

nodes/root_type=""
nodes/root_name=""
nodes/root_script=null
nodes/apply_root_scale=true
nodes/root_scale=1.0
nodes/import_as_skeleton_bones=false
nodes/use_name_suffixes=true
nodes/use_node_type_suffixes=true
meshes/ensure_tangents=true
meshes/generate_lods=false
meshes/create_shadow_meshes=true
meshes/light_baking=1
meshes/lightmap_texel_size=0.2
meshes/force_disable_compression=false
skins/use_named_skins=true
animation/import=true
animation/fps=30
animation/trimming=false
animation/remove_immutable_tracks=true
animation/import_rest_as_RESET=false
import_script/path=""
materials/extract=0
materials/extract_format=0
materials/extract_path=""
_subresources={{}}
gltf/naming_version=2
gltf/embedded_image_handling=1
"""
    glb_path.with_name(glb_path.name + ".import").write_text(content, encoding="utf-8", newline="\n")


def write_model_tex_import(tex_path: Path, res_path: str, is_normal: bool) -> None:
    normal_flag = 1 if is_normal else 0
    content = f"""[remap]

importer="texture"
type="CompressedTexture2D"

[deps]

source_file="{res_path}"

[params]

compress/mode=2
compress/high_quality=false
compress/lossy_quality=0.7
compress/uastc_level=0
compress/rdo_quality_loss=0.0
compress/hdr_compression=1
compress/normal_map={normal_flag}
compress/channel_pack=0
mipmaps/generate=true
mipmaps/limit=-1
roughness/mode=0
roughness/src_normal=""
process/channel_remap/red=0
process/channel_remap/green=1
process/channel_remap/blue=2
process/channel_remap/alpha=3
process/fix_alpha_border=true
process/premult_alpha=false
process/normal_map_invert_y=false
process/hdr_as_srgb=false
process/hdr_clamp_exposure=false
process/size_limit=0
detect_3d/compress_to=0
"""
    tex_path.with_name(tex_path.name + ".import").write_text(content, encoding="utf-8", newline="\n")


def write_ui_tex_import(tex_path: Path, res_path: str, compress_mode: int, mipmaps: bool) -> None:
    mip_str = "true" if mipmaps else "false"
    content = f"""[remap]

importer="texture"
type="CompressedTexture2D"

[deps]

source_file="{res_path}"

[params]

compress/mode={compress_mode}
compress/high_quality=false
compress/lossy_quality=0.7
compress/uastc_level=0
compress/rdo_quality_loss=0.0
compress/hdr_compression=1
compress/normal_map=0
compress/channel_pack=0
mipmaps/generate={mip_str}
mipmaps/limit=-1
roughness/mode=0
roughness/src_normal=""
process/channel_remap/red=0
process/channel_remap/green=1
process/channel_remap/blue=2
process/channel_remap/alpha=3
process/fix_alpha_border=true
process/premult_alpha=false
process/normal_map_invert_y=false
process/hdr_as_srgb=false
process/hdr_clamp_exposure=false
process/size_limit=0
detect_3d/compress_to=1
"""
    tex_path.with_name(tex_path.name + ".import").write_text(content, encoding="utf-8", newline="\n")


# ---------------------------------------------------------------------------
# Blender 5.2 Posed Portrait & Action-Bust Renderer + 90s Komiks Finish
# ---------------------------------------------------------------------------

BLENDER_SCRIPT = r'''
import json
import math
import os
import sys
import bpy
from bpy_extras.object_utils import world_to_camera_view
from mathutils import Euler, Vector

argv = sys.argv[sys.argv.index("--") + 1:]
manifest_path = argv[0]
with open(manifest_path, "r", encoding="utf-8") as f:
    jobs = json.load(f)

results = {}

def tame_materials():
    for mat in bpy.data.materials:
        if not mat.node_tree:
            continue
        for node in mat.node_tree.nodes:
            if node.type != "BSDF_PRINCIPLED":
                continue
            for name in ("Metallic", "Roughness"):
                for link in list(node.inputs[name].links):
                    mat.node_tree.links.remove(link)
            node.inputs["Metallic"].default_value = 0.0
            node.inputs["Roughness"].default_value = 0.88
            if "Specular IOR Level" in node.inputs:
                node.inputs["Specular IOR Level"].default_value = 0.12
            if "Coat Weight" in node.inputs:
                node.inputs["Coat Weight"].default_value = 0.0

def add_sun(scene, name, energy, rot, color, angle_deg=14.0):
    light = bpy.data.lights.new(name, "SUN")
    light.energy = energy
    light.color = color
    light.angle = math.radians(angle_deg)
    obj = bpy.data.objects.new(name, light)
    obj.rotation_euler = Euler([math.radians(a) for a in rot])
    scene.collection.objects.link(obj)
    return obj

def activate_clip(rig, clip_name, frame):
    if rig.animation_data:
        for track in rig.animation_data.nla_tracks:
            track.mute = True
    chosen = None
    for act in bpy.data.actions:
        if act.name == clip_name or act.name.startswith(clip_name):
            chosen = act
            break
    if chosen is not None:
        slot = next((sl for sl in chosen.slots if sl.target_id_type == "OBJECT"), None)
        rig.animation_data_create()
        rig.animation_data.action = chosen
        if slot is not None:
            rig.animation_data.action_slot = slot
    bpy.context.scene.frame_set(frame)
    bpy.context.view_layer.update()

def apply_head_pose(rig, head_rot):
    rx, ry, rz = [math.radians(float(a)) for a in head_rot]
    neck = rig.pose.bones.get("mixamorig:Neck") or rig.pose.bones.get("mixamorig_Neck") or rig.pose.bones.get("Neck")
    head = rig.pose.bones.get("mixamorig:Head") or rig.pose.bones.get("mixamorig_Head") or rig.pose.bones.get("Head")
    if neck is not None:
        neck.rotation_mode = "QUATERNION"
        neck.rotation_quaternion = neck.rotation_quaternion @ Euler((rx * 0.38, ry * 0.38, rz * 0.35)).to_quaternion()
    if head is not None:
        head.rotation_mode = "QUATERNION"
        head.rotation_quaternion = head.rotation_quaternion @ Euler((rx * 0.62, ry * 0.62, rz * 0.65)).to_quaternion()
    bpy.context.view_layer.update()

def aim_sun_from_dir(sun_obj, pitch_deg, yaw_deg):
    p = math.radians(pitch_deg)
    y = math.radians(yaw_deg)
    sun_obj.rotation_euler = Euler((math.radians(90.0) - p, 0.0, y))

for job in jobs:
    kid_id = job["id"]
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=job["glb"])
    rig = next(o for o in bpy.data.objects if o.type == "ARMATURE")
    tame_materials()

    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 36
    scene.cycles.use_denoising = True
    scene.render.film_transparent = True
    scene.render.image_settings.color_mode = "RGBA"

    world = bpy.data.worlds.new("W")
    scene.world = world
    bg = world.node_tree.nodes["Background"]
    bg.inputs["Color"].default_value = (1.0, 0.76, 0.48, 1.0)
    bg.inputs["Strength"].default_value = 0.56

    cyaw_deg = float(job["card_yaw"])
    cpitch_deg = float(job["card_pitch"])
    key_side = float(job["key_side"])

    key_sun = add_sun(scene, "Key", 3.55, (0, 0, 0), (1.0, 0.83, 0.56), angle_deg=10.5)
    aim_sun_from_dir(key_sun, 26.0, cyaw_deg + key_side * 38.0)

    fill_sun = add_sun(scene, "Fill", 0.95, (0, 0, 0), (1.0, 0.78, 0.55), angle_deg=28.0)
    aim_sun_from_dir(fill_sun, 8.0, cyaw_deg - key_side * 52.0)

    mango_rim = add_sun(scene, "MangoRim", 4.65, (0, 0, 0), (1.0, 0.67, 0.20), angle_deg=7.5)
    aim_sun_from_dir(mango_rim, 20.0, cyaw_deg - key_side * 142.0)

    gold_kicker = add_sun(scene, "GoldKicker", 2.55, (0, 0, 0), (1.0, 0.74, 0.30), angle_deg=10.0)
    aim_sun_from_dir(gold_kicker, 24.0, cyaw_deg + key_side * 142.0)

    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    scene.collection.objects.link(cam)
    scene.camera = cam

    activate_clip(rig, job["card_clip"], int(job["card_frame"]))
    apply_head_pose(rig, job["head_rot"])

    head_pb = (
        rig.pose.bones.get("mixamorig:Head")
        or rig.pose.bones.get("mixamorig_Head")
        or rig.pose.bones.get("Head")
    )
    if head_pb is not None:
        chin_world = rig.matrix_world @ head_pb.head
    else:
        chin_world = Vector((0.0, -0.04, 1.12))

    eye_world = Vector((chin_world.x, chin_world.y - 0.12, chin_world.z + 0.145))
    look_at = Vector((chin_world.x, chin_world.y, chin_world.z + 0.18))

    RW, RH = 768, 1024
    scene.render.resolution_x, scene.render.resolution_y = RW, RH
    cam.data.type = "PERSP"
    cam.data.lens = 56.0
    cam.data.sensor_fit = "VERTICAL"
    cam.data.sensor_height = 36.0

    cpitch = math.radians(cpitch_deg)
    cyaw = math.radians(cyaw_deg)
    croll = math.radians(float(job["cam_roll"]))
    cdist = float(job["card_dist"]) + 0.24
    coff = Euler((0.0, 0.0, cyaw)).to_matrix() @ (Vector((0.0, -math.cos(cpitch), math.sin(cpitch))) * cdist)
    cam.location = look_at + coff
    cam.rotation_euler = Euler((math.radians(90.0) - cpitch, croll, cyaw))
    bpy.context.view_layer.update()

    chin_ndc = world_to_camera_view(scene, cam, chin_world)
    eye_ndc = world_to_camera_view(scene, cam, eye_world)
    results[kid_id] = {
        "chin_px": [float(chin_ndc.x * RW), float((1.0 - chin_ndc.y) * RH)],
        "eye_px": [float(eye_ndc.x * RW), float((1.0 - eye_ndc.y) * RH)],
        "anim_count": len(bpy.data.actions),
    }
    scene.render.filepath = job["card_raw"]
    bpy.ops.render.render(write_still=True)

with open(argv[1], "w", encoding="utf-8") as f:
    json.dump(results, f, indent=2)
'''


def auto_frame_head_and_shoulders(
    raw_path: Path,
    chin_px: list[float],
    eye_px: list[float],
    head_fill: float = 0.72,
) -> tuple[Image.Image, tuple[float, float], tuple[float, float], float]:
    """Supersampled auto-framing: crops the 768x1024 RGBA render so head+hair fills ~72% height (600x800)."""
    img = Image.open(raw_path).convert("RGBA")
    rw, rh = img.size
    arr = np.asarray(img, dtype=np.float32)
    alpha = arr[..., 3] / 255.0

    ys, xs = np.where(alpha > 0.25)
    if len(ys) == 0:
        raise RuntimeError(f"Empty portrait render: {raw_path}")

    y_crown = float(ys.min())
    y_chin = float(np.clip(chin_px[1] + 10.0, y_crown + 360.0, rh - 110.0))
    h_head = float(np.clip(y_chin - y_crown, 420.0, 680.0))

    crop_h = int(round(min(h_head / head_fill, float(rh))))
    crop_w = int(round(crop_h * 0.75))
    if crop_w > rw:
        crop_w = rw
        crop_h = int(round(crop_w / 0.75))

    top = int(round(np.clip(y_crown - 0.026 * crop_h, 0.0, float(rh - crop_h))))
    head_band = (alpha > 0.25) & (np.arange(rh)[:, None] <= int(round(y_chin)))
    _, hxs = np.where(head_band)
    cx_raw = float(0.5 * (hxs.min() + hxs.max())) if len(hxs) > 0 else float(eye_px[0])
    left = int(round(np.clip(cx_raw - 0.5 * crop_w, 0.0, float(rw - crop_w))))

    cropped = img.crop((left, top, left + crop_w, top + crop_h)).resize(
        (600, 800), Image.Resampling.LANCZOS
    )
    sx = 600.0 / float(crop_w)
    sy = 800.0 / float(crop_h)
    eye_600 = ((eye_px[0] - left) * sx, (eye_px[1] - top) * sy)
    chin_600 = ((chin_px[0] - left) * sx, (y_chin - top) * sy)
    actual_fill = ((y_chin - y_crown) * sy) / 800.0
    return cropped, eye_600, chin_600, actual_fill


def paint_eye_catchlights_and_warmth(
    framed_rgba: Image.Image,
    eye_600: tuple[float, float],
    key_side: int,
) -> tuple[np.ndarray, np.ndarray]:
    """Add warm golden-hour skin/cloth grading, mango rim boost, honey iris glow, and crisp dual eye catchlights."""
    arr = np.asarray(framed_rgba, dtype=np.float32)
    rgb = arr[..., :3].copy()
    alpha = arr[..., 3] / 255.0
    h, w = alpha.shape
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)

    ex, ey = eye_600
    in_eye_band = (
        (yy > max(140.0, ey - 135.0))
        & (yy < min(620.0, ey + 135.0))
        & (xx > 45.0)
        & (xx < 565.0)
        & (alpha > 0.90)
    )
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    lum = 0.299 * r + 0.587 * g + 0.114 * b

    # Detect white/cream eyeball sclera before warm grading
    sclera = in_eye_band & (r > 162.0) & (g > 152.0) & (b > 142.0) & (np.abs(r - b) < 44.0)
    near_sclera = binary_dilation(sclera, iterations=10)

    # Isolate round dark/brown irises (including key-lit warm brown irises; gaussian_filter erases thin lashes)
    dark_eye = in_eye_band & near_sclera & (lum < 108.0) & (b < 96.0)
    iris_core = gaussian_filter(dark_eye.astype(np.float32), sigma=3.8) > 0.48
    labeled, n_comp = nd_label(iris_core)

    catchlight_mask = np.zeros((h, w), dtype=bool)
    comps: list[tuple[int, int]] = []
    for cid in range(1, n_comp + 1):
        area = int((labeled == cid).sum())
        if area >= 230:
            comps.append((area, cid))
    comps.sort(reverse=True)

    kdir = -1.0 if key_side < 0 else 1.0
    for area, cid in comps[:2]:
        cmask = labeled == cid
        cys, cxs = np.where(cmask)
        icx = float(cxs.mean())
        icy = float(cys.mean())
        irad = max(math.sqrt(area / math.pi) * 1.18, 11.0)

        # Warm honey-amber lower iris crescent so dark eyes glow warmly like junjun..inday
        dx = (xx - icx) / irad
        dy = (yy - icy) / irad
        r_norm = np.hypot(dx, dy)
        crescent = (
            dark_eye
            & (r_norm < 0.92)
            & (r_norm > 0.28)
            & (dy > 0.05)
        )
        c_weight = np.clip((dy - 0.05) * (1.0 - np.abs(r_norm - 0.62) * 1.8), 0.0, 1.0)
        rgb[crescent, 0] = np.clip(rgb[crescent, 0] + 46.0 * c_weight[crescent], 0.0, 255.0)
        rgb[crescent, 1] = np.clip(rgb[crescent, 1] + 22.0 * c_weight[crescent], 0.0, 255.0)
        rgb[crescent, 2] = np.clip(rgb[crescent, 2] + 5.0 * c_weight[crescent], 0.0, 255.0)

        # Primary golden-cream catchlight (upper key side) + secondary pinpoint glint (lower opposite)
        p1x = icx + kdir * irad * 0.28
        p1y = icy - irad * 0.28
        rad1 = float(np.clip(irad * 0.27, 4.4, 7.4))
        w1 = np.clip((rad1 + 0.8 - np.hypot(xx - p1x, yy - p1y)) / 1.4, 0.0, 1.0)

        p2x = icx - kdir * irad * 0.27
        p2y = icy + irad * 0.27
        rad2 = float(np.clip(irad * 0.12, 2.0, 3.5))
        w2 = np.clip((rad2 + 0.7 - np.hypot(xx - p2x, yy - p2y)) / 1.3, 0.0, 1.0)

        glint = np.maximum(w1, w2 * 0.92)
        g_active = glint > 0.02
        catchlight_mask |= glint > 0.35
        glint_col = np.array([255.0, 251.0, 238.0], dtype=np.float32)
        rgb[g_active] = rgb[g_active] * (1.0 - glint[g_active, None]) + glint_col * glint[g_active, None]

    # Golden-hour sunset warmth & contrast on skin/hair/clothes while keeping sclera & catchlights crisp
    pil_rgb = Image.fromarray(np.clip(rgb, 0.0, 255.0).astype(np.uint8), mode="RGB")
    pil_rgb = ImageEnhance.Color(pil_rgb).enhance(1.18)
    pil_rgb = ImageEnhance.Contrast(pil_rgb).enhance(1.08)
    graded = np.asarray(pil_rgb, dtype=np.float32)

    warm_zone = (alpha > 0.2) & (~sclera) & (~catchlight_mask)
    graded[warm_zone, 0] = np.clip(graded[warm_zone, 0] * 1.06 + 4.0, 0.0, 255.0)
    graded[warm_zone, 1] = np.clip(graded[warm_zone, 1] * 0.99 + 1.0, 0.0, 255.0)
    graded[warm_zone, 2] = np.clip(graded[warm_zone, 2] * 0.89, 0.0, 255.0)

    #Crisp mango-gold inner rim boost along the backlit silhouette edge
    fg = alpha > 0.38
    rim_band = fg & (~binary_erosion(fg, iterations=5)) & (~binary_erosion(fg, iterations=1) ^ fg)
    inner_band = binary_erosion(fg, iterations=2) & (~binary_erosion(fg, iterations=6))
    side_x = (xx - 300.0) / 300.0
    rim_dir_w = np.clip((-kdir * side_x) * 0.65 + (1.0 - yy / 800.0) * 0.45, 0.0, 1.0)
    mango_col = np.array([255.0, 194.0, 68.0], dtype=np.float32)
    rw_mask = inner_band & (~sclera) & (~catchlight_mask)
    blend_m = 0.34 * rim_dir_w[rw_mask, None]
    graded[rw_mask] = graded[rw_mask] * (1.0 - blend_m) + mango_col * blend_m
    _ = rim_band

    # Keep sclera clean warm-ivory white & catchlights sparkling
    graded[sclera] = np.clip(rgb[sclera] * np.array([1.01, 0.99, 0.96], dtype=np.float32), 0.0, 252.0)
    graded[catchlight_mask] = rgb[catchlight_mask]
    return graded, alpha


def make_komiks_backdrop(width: int, height: int, pal: dict, center: tuple[float, float]) -> Image.Image:
    """Paint a lush golden-hour barangay bokeh + 90s Filipino Komiks sunburst/halftone backdrop (600x800)."""
    yy, xx = np.mgrid[0:height, 0:width].astype(np.float32)
    t = yy / max(height - 1, 1)

    top = np.array(pal["sky_top"], dtype=np.float32)
    bot = np.array(pal["sky_bot"], dtype=np.float32)
    bg = top[None, None, :] * (1.0 - t[..., None]) + bot[None, None, :] * t[..., None]

    cx, cy = center
    ang = np.arctan2(yy - cy, xx - cx)
    dist = np.hypot(xx - cx, yy - cy) / max(width, height)
    rays = (np.sin(ang * 14.0) > 0.15).astype(np.float32) * np.clip(1.0 - dist * 0.85, 0.0, 1.0)
    ray_col = np.array(pal["ray"], dtype=np.float32)
    bg = bg * (1.0 - 0.20 * rays[..., None]) + ray_col[None, None, :] * (0.20 * rays[..., None])

    base_img = Image.fromarray(np.clip(bg, 0, 255).astype(np.uint8), mode="RGB")
    overlay = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    draw = ImageDraw.Draw(overlay)

    # Warm sari-sari / bahay timber eaves & sunlit mango foliage bokeh matching junjun..inday depth
    wood_c = pal["bokeh_wood"]
    leaf_c = pal["bokeh_leaf"]
    draw.rectangle((-20, int(height * 0.42), int(width * 0.24), height + 20), fill=(wood_c[0], wood_c[1], wood_c[2], 135))
    draw.rectangle((int(width * 0.78), int(height * 0.36), width + 20, height + 20), fill=(wood_c[0], wood_c[1], wood_c[2], 125))
    for bx, by, br in [(-25, 55, 165), (width + 15, 75, 175), (95, -20, 135), (width - 105, -15, 140), (35, 290, 110), (width - 30, 270, 115)]:
        draw.ellipse((bx - br, by - br, bx + br, by + br), fill=(leaf_c[0], leaf_c[1], leaf_c[2], 125))
    for gx_b, gy_b, gr in [(65, 130, 52), (width - 70, 150, 58), (42, 420, 44), (width - 48, 395, 48)]:
        draw.ellipse((gx_b - gr, gy_b - gr, gx_b + gr, gy_b + gr), fill=(255, 214, 104, 92))

    bunting_cols = pal["bunting"]
    for s_idx, (x0, y0, x1, y1) in enumerate([(0, 26, int(width * 0.58), 112), (int(width * 0.44), 12, width, 98)]):
        draw.line([(x0, y0), (x1, y1)], fill=(42, 28, 24, 155), width=4)
        steps = 7
        for i in range(steps):
            u0 = i / steps
            u1 = (i + 0.78) / steps
            px0 = x0 + (x1 - x0) * u0
            py0 = y0 + (y1 - y0) * u0
            px1 = x0 + (x1 - x0) * u1
            py1 = y0 + (y1 - y0) * u1
            midx = 0.5 * (px0 + px1) - 7
            midy = 0.5 * (py0 + py1) + 40
            c = bunting_cols[(i + s_idx * 2) % len(bunting_cols)]
            draw.polygon([(px0, py0), (px1, py1), (midx, midy)], fill=(c[0], c[1], c[2], 185))

    blurred_fg = overlay.filter(ImageFilter.GaussianBlur(radius=11.0))
    comp = Image.alpha_composite(base_img.convert("RGBA"), blurred_fg).convert("RGB")
    comp_arr = np.asarray(comp, dtype=np.float32)

    rot_x = xx * 0.866 - yy * 0.5
    rot_y = xx * 0.5 + yy * 0.866
    pitch = 13.0
    gx = (np.mod(rot_x, pitch) - pitch * 0.5) / (pitch * 0.5)
    gy = (np.mod(rot_y, pitch) - pitch * 0.5) / (pitch * 0.5)
    dot_r = np.hypot(gx, gy)
    vig = np.clip(dist * 1.12 - 0.28, 0.0, 0.72)
    dots = (dot_r < vig).astype(np.float32) * 0.24
    ht_col = np.array(pal["halftone"], dtype=np.float32)
    comp_arr = comp_arr * (1.0 - dots[..., None]) + ht_col[None, None, :] * dots[..., None]

    return Image.fromarray(np.clip(comp_arr, 0, 255).astype(np.uint8), mode="RGB")


def build_portrait_card_and_face(
    raw_card_path: Path,
    portrait_dst: Path,
    card_dst: Path,
    face_dst: Path,
    spec: dict,
    meta: dict,
) -> float:
    """Build tight ~72% head-fill RGBA portrait, 3:4 90s komiks card, and 96x96 minimap face."""
    framed_rgba, eye_600, _chin_600, actual_fill = auto_frame_head_and_shoulders(
        raw_card_path,
        meta["chin_px"],
        meta["eye_px"],
        head_fill=float(spec["head_fill"]),
    )
    graded_rgb, alpha = paint_eye_catchlights_and_warmth(
        framed_rgba,
        eye_600,
        key_side=int(spec["key_side"]),
    )

    w, h = framed_rgba.size
    mask = alpha > 0.35
    outer_ink_p = binary_dilation(mask, iterations=3) & (~mask)
    inner_ink = mask & (~binary_erosion(mask, iterations=2))

    # 1) Save tight 600x800 RGBA portrait (`assets/portraits/<id>.png`) with charcoal ink contour
    port_rgb = graded_rgb.copy()
    port_rgb[inner_ink] = port_rgb[inner_ink] * 0.42 + INK_RGB * 0.58
    port_rgb[outer_ink_p] = INK_RGB
    port_alpha = np.clip(alpha * 255.0, 0.0, 255.0)
    port_alpha[outer_ink_p] = np.maximum(port_alpha[outer_ink_p], 242.0)
    port_rgba = np.dstack([port_rgb, port_alpha]).astype(np.uint8)
    Image.fromarray(port_rgba, mode="RGBA").save(portrait_dst, format="PNG", compress_level=6)

    # 2) Save 600x800 3:4 90s Filipino Komiks profile card (`assets/portraits/cards/<id>.webp`)
    pal = spec["palette"]
    hx = float(np.clip(eye_600[0], w * 0.28, w * 0.72))
    hy = float(np.clip(eye_600[1], h * 0.25, h * 0.62))
    bg_img = make_komiks_backdrop(w, h, pal, (hx, hy))
    bg = np.asarray(bg_img, dtype=np.float32)

    glow_mask = gaussian_filter(mask.astype(np.float32), sigma=10.0) * (1.0 - alpha)
    rim_col = np.array(pal["rim"], dtype=np.float32)
    bg = np.clip(bg + (rim_col[None, None, :] - bg) * (glow_mask[..., None] * 0.64), 0.0, 255.0)

    outer_ink_c = binary_dilation(mask, iterations=4) & (~mask)
    fg_inked = graded_rgb.copy()
    fg_inked[inner_ink] = fg_inked[inner_ink] * 0.42 + INK_RGB * 0.58

    comp = bg * (1.0 - alpha[..., None]) + fg_inked * alpha[..., None]
    comp[outer_ink_c] = INK_RGB

    rng = np.random.default_rng(abs(hash(card_dst.stem)) % (2**32))
    grain = rng.normal(0.0, 2.4, size=(h, w, 1)).astype(np.float32)
    comp = np.clip(comp + grain, 0.0, 255.0).astype(np.uint8)

    card_final = Image.fromarray(comp, mode="RGB")
    card_final.save(card_dst, format="WEBP", quality=92, method=6)

    # 3) Save 96x96 square minimap/HUD face (`assets/portraits/faces/<id>.png`) tightly framed on head/eyes
    sq = 552
    bias = float(spec["face_eye_bias"])
    f_left = int(round(np.clip(hx - 0.50 * sq, 0.0, float(w - sq))))
    f_top = int(round(np.clip(hy - bias * sq, 0.0, float(h - sq))))
    face_crop = card_final.crop((f_left, f_top, f_left + sq, f_top + sq))
    face_96 = face_crop.resize((96, 96), Image.Resampling.LANCZOS)
    face_96 = face_96.filter(ImageFilter.UnsharpMask(radius=0.85, percent=68, threshold=2))
    face_96.save(face_dst, format="PNG", compress_level=6)
    return actual_fill


# ---------------------------------------------------------------------------
# Main Pipeline Execution
# ---------------------------------------------------------------------------


def main() -> None:
    CARDS_DIR.mkdir(parents=True, exist_ok=True)
    FACES_DIR.mkdir(parents=True, exist_ok=True)

    source_hashes: dict[Path, str] = {}
    for spec in KIDS_SPEC:
        src_glb = CHAR_DIR / f"{spec['base']}.glb"
        if not src_glb.exists():
            raise FileNotFoundError(f"Missing base GLB: {src_glb}")
        source_hashes[src_glb] = sha256_file(src_glb)

    print("[1/3] Cloning base GLBs & painting 3D-mapped 90s Filipino Komiks textures...")
    for spec in KIDS_SPEC:
        kid_id = spec["id"]
        base_id = spec["base"]
        src_glb = CHAR_DIR / f"{base_id}.glb"
        gltf, bin_chunk = parse_glb(src_glb)

        img_mr = extract_embedded_image(gltf, bin_chunk, 0).convert("RGB").resize(
            (AUX_MAP_SIZE, AUX_MAP_SIZE), Image.Resampling.LANCZOS
        )
        img_base = extract_embedded_image(gltf, bin_chunk, 1).convert("RGB").resize(
            (BASE_COLOR_SIZE, BASE_COLOR_SIZE), Image.Resampling.LANCZOS
        )
        img_norm = resize_normal_map(extract_embedded_image(gltf, bin_chunk, 2), AUX_MAP_SIZE)

        pos_grid, nrm_grid = build_uv_3d_maps(gltf, bin_chunk, size=BASE_COLOR_SIZE)
        base_arr = np.asarray(img_base, dtype=np.float32)
        painted_arr = PAINTERS[kid_id](base_arr, pos_grid, nrm_grid)
        img_painted = Image.fromarray(np.clip(painted_arr, 0.0, 255.0).astype(np.uint8), mode="RGB")

        png_0 = encode_png(img_mr)
        png_1 = encode_png(img_painted)
        png_2 = encode_png(img_norm)

        new_glb_bytes = rebuild_glb_with_images(gltf, bin_chunk, [png_0, png_1, png_2])
        dst_glb = CHAR_DIR / f"{kid_id}.glb"
        dst_glb.write_bytes(new_glb_bytes)
        write_glb_import(dst_glb, kid_id)

        for idx, png_bytes in enumerate([png_0, png_1, png_2]):
            tex_path = CHAR_DIR / f"{kid_id}_{idx}.png"
            tex_path.write_bytes(png_bytes)
            write_model_tex_import(
                tex_path,
                f"res://assets/models/characters/{kid_id}_{idx}.png",
                is_normal=(idx == 2),
            )
        print(f"  -> Built {kid_id}.glb ({len(new_glb_bytes) / (1024 * 1024):.2f} MiB) from {base_id}.glb")

    blender_bin = find_blender()
    print(f"[2/3] Rendering close-up 3/4 golden-hour portraits in {blender_bin.name}...")
    with tempfile.TemporaryDirectory(prefix="kalyeah_10kids_") as tmp:
        tmp_dir = Path(tmp)
        jobs = []
        for spec in KIDS_SPEC:
            kid_id = spec["id"]
            jobs.append(
                {
                    "id": kid_id,
                    "glb": str(CHAR_DIR / f"{kid_id}.glb"),
                    "card_clip": spec["card_clip"],
                    "card_frame": spec["card_frame"],
                    "card_yaw": spec["card_yaw"],
                    "card_pitch": spec["card_pitch"],
                    "card_dist": spec["card_dist"],
                    "head_rot": list(spec["head_rot"]),
                    "cam_roll": spec["cam_roll"],
                    "key_side": spec["key_side"],
                    "card_raw": str(tmp_dir / f"{kid_id}_card_raw.png"),
                }
            )
        manifest_path = tmp_dir / "jobs.json"
        results_path = tmp_dir / "results.json"
        script_path = tmp_dir / "render_jobs.py"
        manifest_path.write_text(json.dumps(jobs, indent=2), encoding="utf-8")
        script_path.write_text(BLENDER_SCRIPT, encoding="utf-8")

        cmd = [
            str(blender_bin),
            "--background",
            "--python",
            str(script_path),
            "--",
            str(manifest_path),
            str(results_path),
        ]
        proc = subprocess.run(cmd, capture_output=True, text=True, check=False)
        if proc.returncode != 0 or not results_path.exists():
            sys.stderr.write(proc.stdout[-3000:] + "\n" + proc.stderr[-3000:] + "\n")
            raise RuntimeError(f"Blender render failed with exit code {proc.returncode}")

        render_meta = json.loads(results_path.read_text(encoding="utf-8"))

        print("[3/3] Compositing golden-hour ~72% head-fill portraits, 3:4 cards & 96x96 faces...")
        for spec in KIDS_SPEC:
            kid_id = spec["id"]
            meta = render_meta[kid_id]
            if meta["anim_count"] < 8:
                raise RuntimeError(f"{kid_id}.glb missing animations (found {meta['anim_count']})")

            portrait_dst = PORTRAIT_DIR / f"{kid_id}.png"
            card_dst = CARDS_DIR / f"{kid_id}.webp"
            face_dst = FACES_DIR / f"{kid_id}.png"

            fill_pct = build_portrait_card_and_face(
                tmp_dir / f"{kid_id}_card_raw.png",
                portrait_dst,
                card_dst,
                face_dst,
                spec,
                meta,
            )

            write_ui_tex_import(portrait_dst, f"res://assets/portraits/{kid_id}.png", compress_mode=1, mipmaps=False)
            write_ui_tex_import(card_dst, f"res://assets/portraits/cards/{kid_id}.webp", compress_mode=0, mipmaps=False)
            write_ui_tex_import(face_dst, f"res://assets/portraits/faces/{kid_id}.png", compress_mode=0, mipmaps=True)
            print(
                f"  -> Generated portraits/{kid_id}.png, cards/{kid_id}.webp, faces/{kid_id}.png "
                f"(head fill = {fill_pct * 100.0:.1f}%)"
            )

    for src_glb, before_hash in source_hashes.items():
        if sha256_file(src_glb) != before_hash:
            raise RuntimeError(f"Integrity violation: {src_glb} changed!")
    print("Done! All 4 new kids' GLBs, textures, 90s komiks portraits/cards/faces & .import files built cleanly.")


if __name__ == "__main__":
    main()
