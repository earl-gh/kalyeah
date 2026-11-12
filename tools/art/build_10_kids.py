#!/usr/bin/env python3
"""Build the 4 expansion Filipino kids (Kiko, Nene, Buknoy, Tala) for the 10-kid roster.

Overhaul v2 Pipeline:
- Clones the normalized rigged base GLBs (migo, ligaya, junjun, toni) WITHOUT modifying
  any of the 6 authored kids' files (SHA-256 verified before & after).
- Applies watertight spatial-welded organic child sculpting (volume-preserving Taubin
  smoothing, soft child cheek/jaw contours, subtle per-kid build silhouettes, welded
  smooth normals across UV seams, and exact [0.000, 1.700] m rest-pose normalization).
- Paints soft sunlit 90s Philippine province outfit & child skin textures (`mean_sRGB`
  in [0.43, 0.49], `max_sRGB <= 0.78`, warm espresso-umber shadow floor `#261D22`,
  anti-aliased warm umber `#382B31` cloth seams instead of harsh `#17131A` cartoon lines,
  preserved soft baked cloth folds, >=16px UV mipmap gutter bleed, and cleaned donor
  normal-map decals).
- Preserves existing Godot `[remap]`/`[deps]` UIDs in `.import` sidecars (`compress/mode=2`,
  `mipmaps/generate=true`, `compress/normal_map=1` on `_2.png`, zero `md5=` lines).

Usage:
    python tools/art/build_10_kids.py [--with-portraits]
"""
from __future__ import annotations

import argparse
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

AUTHORED_IDS = ("junjun", "ligaya", "migo", "toni", "popoy", "inday")
BASE_COLOR_SIZE = 1024
AUX_MAP_SIZE = 512

# Overhaul v2 Sunlit 90s Province Palette Limits (#78 Section 1 & 4)
SHADOW_FLOOR = np.array([38.0, 29.0, 34.0], dtype=np.float32)    # #261D22 warm espresso-umber floor
WARM_SEAM_RGB = np.array([56.0, 43.0, 49.0], dtype=np.float32)   # #382B31 soft warm umber cloth seam
INK_RGB = np.array([23.0, 19.0, 26.0], dtype=np.float32)         # Portrait-only outline ink
MAX_SRGB_VAL = 198.0                                             # 198 / 255 = 0.7765 <= 0.78 ceiling

KIDS_SPEC = [
    {
        "id": "kiko",
        "base": "migo",
        "target_mean_srgb": 0.455,
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
        "target_mean_srgb": 0.468,
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
        "target_mean_srgb": 0.452,
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
        "target_mean_srgb": 0.448,
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
# glTF Binary Parsing, Organic Child Mesh Sculpting & Chunk Rebuilding
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


def cos_bell(val: np.ndarray, center: float, half_width: float) -> np.ndarray:
    t = np.clip((val - center) / max(half_width, 1e-5), -1.0, 1.0)
    return 0.5 * (1.0 + np.cos(np.pi * t))


def sculpt_organic_child_mesh(
    pos: np.ndarray,
    nrm: np.ndarray,
    idx: np.ndarray,
    kid_id: str,
) -> tuple[np.ndarray, np.ndarray]:
    """Apply watertight welded Taubin smoothing, organic child cheek/jaw/limb proportions,
    smooth welded normals across UV seams, and exact [0.000, 1.700] m height normalization."""
    p = pos.astype(np.float64).copy()

    # 1. Spatial vertex welding so split UV-seam vertices move identically (100% watertight!)
    quant = np.round(p * 4000.0).astype(np.int64)
    _, unique_first, inv_map = np.unique(quant, axis=0, return_index=True, return_inverse=True)
    n_uniq = len(unique_first)
    u_pos = p[unique_first].copy()

    # Build welded adjacency sum & degree via triangle edges for volume-preserving Taubin smoothing
    t0, t1, t2 = inv_map[idx[:, 0]], inv_map[idx[:, 1]], inv_map[idx[:, 2]]
    src = np.concatenate([t0, t1, t1, t2, t2, t0])
    dst = np.concatenate([t1, t0, t2, t1, t0, t2])
    deg = np.bincount(src, minlength=n_uniq).astype(np.float64)
    safe_deg = np.maximum(deg, 1.0)[:, None]

    # Protect eyes, lashes, brows, mouth & grounded soles from Taubin shift
    ux, uy, uz = u_pos[:, 0], u_pos[:, 1], u_pos[:, 2]
    face_detail = (np.abs(ux) < 0.145) & (uy > 1.155) & (uy < 1.345) & (uz > 0.075)
    sole_pin = uy < 0.014
    smooth_w = np.where(face_detail | sole_pin, 0.0, 1.0)[:, None]

    for step_factor in (0.20, -0.208):
        nbr_sum = np.zeros_like(u_pos)
        for c in range(3):
            nbr_sum[:, c] = np.bincount(src, weights=u_pos[dst, c], minlength=n_uniq)
        lap = (nbr_sum / safe_deg) - u_pos
        u_pos += step_factor * smooth_w * lap

    # 2. Organic child cheek/jaw rounding + subtle per-character Filipino kid build silhouette
    ux, uy, uz = u_pos[:, 0], u_pos[:, 1], u_pos[:, 2]
    cheek_bell = (
        cos_bell(uy, 1.195, 0.075)
        * cos_bell(np.abs(ux), 0.095, 0.065)
        * np.clip((uz + 0.01) / 0.11, 0.0, 1.0)
        * (~face_detail).astype(np.float64)
    )
    u_pos[:, 0] *= 1.0 + 0.024 * cheek_bell
    u_pos[:, 2] += 0.0025 * cheek_bell

    if kid_id == "kiko":
        # Agile kalye trumpo craftsman: nimble waist, crisp vest chest, airy curly crown
        rad_scale = (
            1.0
            - 0.026 * cos_bell(uy, 0.78, 0.16)
            + 0.016 * cos_bell(uy, 0.99, 0.13)
            + 0.018 * cos_bell(uy, 1.48, 0.18)
        )
        u_pos[:, 0] *= rad_scale
        u_pos[:, 2] *= rad_scale
    elif kid_id == "nene":
        # Chinese-garter jumper: graceful waist & soft organic A-line pinafore skirt bell
        skirt_bell = cos_bell(uy, 0.51, 0.16)
        waist_taper = cos_bell(uy, 0.77, 0.12)
        rad_scale = 1.0 + 0.042 * skirt_bell - 0.025 * waist_taper
        u_pos[:, 0] *= rad_scale
        u_pos[:, 2] *= rad_scale
    elif kid_id == "buknoy":
        # Sturdy provincial sapa explorer: healthy barrio shoulders/chest & calves
        rad_scale = 1.0 + 0.026 * cos_bell(uy, 0.96, 0.18) + 0.028 * cos_bell(uy, 0.25, 0.13)
        u_pos[:, 0] *= rad_scale
        u_pos[:, 2] *= rad_scale
    elif kid_id == "tala":
        # Barangay track sprinter: tapered runner waist & athletic quads/calves
        rad_scale = (
            1.0
            - 0.024 * cos_bell(uy, 0.73, 0.14)
            + 0.024 * cos_bell(uy, 0.34, 0.18)
        )
        u_pos[:, 0] *= rad_scale
        u_pos[:, 2] *= rad_scale

    # 3. Exact [0.0000, 1.7000] m rest-pose normalization (soles pinned at Y=0.000, crown at Y=1.700)
    min_y = float(u_pos[:, 1].min())
    u_pos[:, 1] -= min_y
    max_y = float(u_pos[:, 1].max())
    if max_y > 1e-5:
        u_pos[:, 1] *= 1.7000 / max_y

    sculpted_pos = u_pos[inv_map].astype(np.float32)

    # 4. Recompute welded area-weighted smooth normals across UV seams, blended gently with
    # authored custom normals on hair/eyes so limbs/knees/cheeks are 100% facet-free!
    v0 = sculpted_pos[idx[:, 0]]
    v1 = sculpted_pos[idx[:, 1]]
    v2 = sculpted_pos[idx[:, 2]]
    fn = np.cross(v1 - v0, v2 - v0).astype(np.float64)

    u_nrm = np.zeros((n_uniq, 3), dtype=np.float64)
    for corner in (t0, t1, t2):
        for c in range(3):
            u_nrm[:, c] += np.bincount(corner, weights=fn[:, c], minlength=n_uniq)
    u_len = np.maximum(np.linalg.norm(u_nrm, axis=-1, keepdims=True), 1e-8)
    welded_nrm = (u_nrm / u_len)[inv_map].astype(np.float32)

    orig_len = np.maximum(np.linalg.norm(nrm, axis=-1, keepdims=True), 1e-8)
    orig_unit = nrm / orig_len
    # Hair/eyes keep 55% original stylized strand/lid normals; skin/limbs/cloth get 75% welded smooth
    is_upper_head = sculpted_pos[:, 1] > 1.15
    weld_w = np.where(is_upper_head, 0.45, 0.75)[:, None].astype(np.float32)
    blended_nrm = welded_nrm * weld_w + orig_unit * (1.0 - weld_w)
    b_len = np.maximum(np.linalg.norm(blended_nrm, axis=-1, keepdims=True), 1e-8)
    sculpted_nrm = (blended_nrm / b_len).astype(np.float32)

    return sculpted_pos, sculpted_nrm


def build_uv_3d_maps(
    pos: np.ndarray,
    nrm: np.ndarray,
    uv: np.ndarray,
    idx: np.ndarray,
    size: int = BASE_COLOR_SIZE,
) -> tuple[np.ndarray, np.ndarray, np.ndarray]:
    """Map 3D rest-pose POSITION and NORMAL onto every texel of a size x size UV grid,
    returning `(pos_grid, nrm_grid, valid)`."""
    uv_mod = np.mod(uv, 1.0)
    pos_grid = np.zeros((size, size, 3), dtype=np.float32)
    nrm_grid = np.zeros((size, size, 3), dtype=np.float32)
    valid = np.zeros((size, size), dtype=bool)

    u0, u1, u2 = uv_mod[idx[:, 0]], uv_mod[idx[:, 1]], uv_mod[idx[:, 2]]
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
    big_ids = np.where(
        ((span[:, 0] > 0.35) | (span[:, 1] > 0.35))
        & (span[:, 0] < size * 0.45)
        & (span[:, 1] < size * 0.45)
    )[0]
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
        inside = (w0 >= -0.025) & (w1 >= -0.025) & (w2 >= -0.025)
        if not inside.any():
            continue
        iy, ix = np.where(inside)
        bw0, bw1, bw2 = w0[inside][:, None], w1[inside][:, None], w2[inside][:, None]
        pos_grid[min_y + iy, min_x + ix] = bw0 * p0[ti] + bw1 * p1[ti] + bw2 * p2[ti]
        nrm_grid[min_y + iy, min_x + ix] = bw0 * n0[ti] + bw1 * n1[ti] + bw2 * n2[ti]
        valid[min_y + iy, min_x + ix] = True

    _, nearest_idx = distance_transform_edt(~valid, return_distances=True, return_indices=True)
    pos_grid = pos_grid[nearest_idx[0], nearest_idx[1]]
    nrm_grid = nrm_grid[nearest_idx[0], nearest_idx[1]]
    n_len = np.maximum(np.linalg.norm(nrm_grid, axis=-1, keepdims=True), 1e-5)
    nrm_grid = nrm_grid / n_len
    return pos_grid, nrm_grid, valid


def extract_embedded_image(gltf: dict, bin_chunk: bytes, img_index: int) -> Image.Image:
    bv = gltf["bufferViews"][gltf["images"][img_index]["bufferView"]]
    off = bv.get("byteOffset", 0)
    length = bv["byteLength"]
    return Image.open(io.BytesIO(bin_chunk[off : off + length]))


def encode_png(img: Image.Image) -> bytes:
    buf = io.BytesIO()
    img.save(buf, format="PNG", optimize=False, compress_level=6)
    return buf.getvalue()


def polish_aux_maps(
    img_mr_src: Image.Image,
    img_norm_src: Image.Image,
    pos_1024: np.ndarray,
    kid_id: str,
) -> tuple[Image.Image, Image.Image]:
    """Build soft matte sunlit roughness/metallic (`_0.png`) and clean donor-decal-free normal (`_2.png`)."""
    # 1. Roughness/Metallic: glTF packs Roughness in G, Metallic in B (AO in R).
    # Keep metallic = 0 and lift roughness to soft sunlit cotton/child-skin matte (0.84..0.94).
    mr = np.asarray(
        img_mr_src.convert("RGB").resize((AUX_MAP_SIZE, AUX_MAP_SIZE), Image.Resampling.LANCZOS),
        dtype=np.float32,
    ).copy()
    mr[..., 0] = np.clip(mr[..., 0] * 0.65 + 88.0, 96.0, 255.0)   # Soft warm AO floor
    mr[..., 1] = np.clip(mr[..., 1] * 0.30 + 172.0, 208.0, 246.0) # Soft matte roughness
    mr[..., 2] = 0.0                                              # Zero metallic sheen
    out_mr = Image.fromarray(mr.astype(np.uint8), mode="RGB")

    # 2. Normal Map: remove ghost donor decal embosses (#11 on Toni/Tala, #23 on Junjun/Buknoy,
    # smiley/crown on Migo/Kiko) and gently soften harsh normal spikes for silky child skin/cloth.
    nm_resized = img_norm_src.convert("RGB").resize((AUX_MAP_SIZE, AUX_MAP_SIZE), Image.Resampling.LANCZOS)
    vec = np.asarray(nm_resized, dtype=np.float32) / 255.0 * 2.0 - 1.0

    pos_512 = pos_1024[::2, ::2]
    px, py, pz = pos_512[..., 0], pos_512[..., 1], pos_512[..., 2]
    flatten = np.zeros((AUX_MAP_SIZE, AUX_MAP_SIZE), dtype=np.float32)
    if kid_id == "tala":
        # Flatten Toni's embossed #11 chest jersey numbers
        flatten = np.where((py > 0.74) & (py < 1.03) & (np.abs(px) < 0.115) & (pz > 0.03), 0.90, 0.28)
    elif kid_id == "buknoy":
        # Flatten Junjun's embossed #23 shorts numbers & collar seam
        flatten = np.where((py > 0.44) & (py < 0.72) & (pz > 0.02), 0.88, 0.26)
    elif kid_id == "kiko":
        # Flatten Migo's chest smiley & hip-pouch crown emboss
        flatten = np.where((py > 0.56) & (py < 1.06) & (pz > 0.02), 0.82, 0.28)
    else:
        flatten[:] = 0.28

    flat_n = np.array([0.0, 0.0, 1.0], dtype=np.float32)
    vec = vec * (1.0 - flatten[..., None]) + flat_n[None, None, :] * flatten[..., None]
    length = np.maximum(np.linalg.norm(vec, axis=-1, keepdims=True), 1e-5)
    vec = vec / length
    out_nm = Image.fromarray(np.clip((vec * 0.5 + 0.5) * 255.0, 0.0, 255.0).astype(np.uint8), mode="RGB")
    return out_mr, out_nm


def rebuild_glb_with_sculpt_and_images(
    gltf: dict,
    bin_chunk: bytes,
    sculpted_pos: np.ndarray,
    sculpted_nrm: np.ndarray,
    png_bytes_list: list[bytes],
) -> bytes:
    """Pack sculpted float32 POSITION & NORMAL + PNG textures into a valid 4-byte-aligned glTF 2.0 GLB."""
    gltf_copy = json.loads(json.dumps(gltf))
    prim = gltf_copy["meshes"][0]["primitives"][0]
    pos_acc_idx = prim["attributes"]["POSITION"]
    nrm_acc_idx = prim["attributes"]["NORMAL"]
    pos_bv_idx = gltf_copy["accessors"][pos_acc_idx]["bufferView"]
    nrm_bv_idx = gltf_copy["accessors"][nrm_acc_idx]["bufferView"]

    gltf_copy["accessors"][pos_acc_idx]["min"] = [float(v) for v in sculpted_pos.min(axis=0)]
    gltf_copy["accessors"][pos_acc_idx]["max"] = [float(v) for v in sculpted_pos.max(axis=0)]

    bv_replacements: dict[int, bytes] = {
        pos_bv_idx: np.ascontiguousarray(sculpted_pos, dtype="<f4").tobytes(),
        nrm_bv_idx: np.ascontiguousarray(sculpted_nrm, dtype="<f4").tobytes(),
    }
    for img_idx, png_bytes in enumerate(png_bytes_list):
        bv_idx = gltf_copy["images"][img_idx]["bufferView"]
        bv_replacements[bv_idx] = png_bytes
        gltf_copy["images"][img_idx]["mimeType"] = "image/png"

    order = sorted(
        range(len(gltf_copy["bufferViews"])),
        key=lambda idx: gltf_copy["bufferViews"][idx].get("byteOffset", 0),
    )
    out_bin = bytearray()
    for bv_idx in order:
        bv = gltf_copy["bufferViews"][bv_idx]
        if bv_idx in bv_replacements:
            chunk = bv_replacements[bv_idx]
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
# Soft Sunlit 90s Province Child Skin & Outfit Painting (Zero Harsh Ink Lines)
# ---------------------------------------------------------------------------


def luminance(rgb: np.ndarray) -> np.ndarray:
    return 0.299 * rgb[..., 0] + 0.587 * rgb[..., 1] + 0.114 * rgb[..., 2]


def soft_step(val: np.ndarray, edge: float, feather: float = 0.008) -> np.ndarray:
    """Smooth C1 Hermite step in [0, 1] across [edge - feather, edge + feather]."""
    t = np.clip((val - (edge - feather)) / max(2.0 * feather, 1e-6), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def soft_band(val: np.ndarray, center: float, half_w: float, feather: float = 0.004) -> np.ndarray:
    """Smooth anti-aliased stripe weight in [0, 1] centered at `center`."""
    return 1.0 - soft_step(np.abs(val - center), half_w, feather)


def blend_rgb(dst: np.ndarray, src_col: np.ndarray, weight: np.ndarray) -> None:
    w = np.clip(weight, 0.0, 1.0)[..., None]
    dst[:] = dst * (1.0 - w) + src_col * w


def front_face_oval(pos: np.ndarray) -> np.ndarray:
    x, y, z = pos[..., 0], pos[..., 1], pos[..., 2]
    return (np.abs(x) < 0.165) & (y > 1.135) & (y < 1.375) & (z > 0.056)


def detect_skin(rgb: np.ndarray, pos: np.ndarray) -> np.ndarray:
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    skin_hue = (r > 150.0) & (g > 86.0) & (b > 52.0) & ((r - g) > 34.0) & ((g - b) > 15.0)
    in_face = front_face_oval(pos)
    eye_or_brow = in_face & ((b > 150.0) | (r < 142.0)) & (~((r > 94.0) & ((r - b) > 38.0)))
    return skin_hue & (~eye_or_brow)


def protect_facial_features(rgb: np.ndarray, pos: np.ndarray, valid: np.ndarray | None = None) -> np.ndarray:
    in_face = front_face_oval(pos)
    if valid is not None:
        in_face = in_face & valid
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    not_skin = ~((r > 160.0) & ((r - g) > 36.0) & ((g - b) > 16.0))
    not_warm_hair_strand = ~((r > 92.0) & ((r - b) > 38.0) & (np.abs(pos[..., 0]) > 0.095))
    sclera_zone = (
        (np.abs(pos[..., 0]) < 0.158)
        & (pos[..., 1] > 1.145)
        & (pos[..., 1] < 1.380)
        & (pos[..., 2] > 0.055)
        & (valid if valid is not None else True)
    )
    head_sclera = (
        sclera_zone
        & (r > 160.0)
        & (g > 160.0)
        & (b > 160.0)
        & (np.abs(r - g) < 22.0)
        & (np.abs(g - b) < 22.0)
    )
    return (in_face & not_skin & not_warm_hair_strand) | head_sclera


def woven_grain(pos: np.ndarray, scale: float = 185.0, strength: float = 0.024) -> np.ndarray:
    w1 = np.sin((pos[..., 0] + pos[..., 2]) * scale) * np.cos(pos[..., 1] * scale * 1.15)
    w2 = np.sin((pos[..., 0] - pos[..., 2]) * scale * 0.7)
    return 1.0 + strength * (0.65 * w1 + 0.35 * w2)


def sunlit_cloth_shade(_rgb_base: np.ndarray, nrm: np.ndarray) -> np.ndarray:
    """Pure 3D sunlit hemisphere + soft normal wrap shading (zero donor-decal ghosting!)."""
    sun_dot = np.clip(nrm[..., 1] * 0.58 + nrm[..., 2] * 0.52 + nrm[..., 0] * 0.14, -0.65, 1.0)
    side_wrap = 0.96 + 0.04 * np.clip(np.abs(nrm[..., 2]), 0.0, 1.0)
    return np.clip((0.90 + 0.12 * sun_dot) * side_wrap, 0.80, 1.05)


def apply_child_skin_gradients(
    out: np.ndarray,
    rgb_src: np.ndarray,
    pos: np.ndarray,
    skin_mask: np.ndarray,
    face_feat: np.ndarray,
    gains: tuple[float, float, float],
    blush_strength: float = 0.16,
) -> None:
    """Paint soft sunlit Filipino child skin with gentle cheek/nose warmth and soft jaw bounce."""
    edit = skin_mask & (~face_feat)
    if not edit.any():
        return
    x, y, z = pos[..., 0], pos[..., 1], pos[..., 2]
    gr, gg, gb = gains

    # Soft sunlit vertical warmth (gentle golden lift on forehead, shoulders & nose bridge)
    sun_lift = 0.97 + 0.05 * np.clip((y - 0.35) / 1.10, 0.0, 1.0)
    base_s = np.empty_like(out)
    base_s[..., 0] = rgb_src[..., 0] * gr * sun_lift
    base_s[..., 1] = rgb_src[..., 1] * gg * sun_lift
    base_s[..., 2] = rgb_src[..., 2] * gb * sun_lift

    # Smooth rosy-peach child cheek & nose-tip warmth (zero hard edges!)
    cheek_d = np.hypot((np.abs(x) - 0.076) / 0.052, (y - 1.202) / 0.044)
    nose_d = np.hypot(x / 0.030, (y - 1.212) / 0.026)
    front_w = np.clip((z - 0.055) / 0.045, 0.0, 1.0)
    blush_w = np.maximum(
        np.clip(1.0 - cheek_d, 0.0, 1.0) ** 1.5,
        0.65 * (np.clip(1.0 - nose_d, 0.0, 1.0) ** 1.6),
    ) * front_w * blush_strength

    blush_col = np.array([196.0, 112.0, 98.0], dtype=np.float32)
    base_s = base_s * (1.0 - blush_w[..., None]) + blush_col[None, None, :] * blush_w[..., None]

    # Gentle warm sunlit bounce along child jawline & knees
    jaw_w = cos_bell(y, 1.145, 0.032) * front_w * 0.06
    knee_w = cos_bell(y, 0.36, 0.055) * np.clip(z / 0.06, 0.0, 1.0) * 0.07
    warm_lift = (jaw_w + knee_w)[..., None] * np.array([14.0, 7.0, 4.0], dtype=np.float32)
    base_s += warm_lift

    out[edit] = np.clip(base_s[edit], SHADOW_FLOOR, MAX_SRGB_VAL)


def finalize_sunlit_albedo(
    out: np.ndarray,
    base_arr: np.ndarray,
    valid: np.ndarray,
    face_feat: np.ndarray,
    target_mean: float,
) -> np.ndarray:
    """Strip any 1px black donor UV-fringe, bleed sunlit island colors >=18px for mipmaps,
    and calibrate overall sunlit sRGB mean cleanly into [0.44, 0.48] with max <= 0.78."""
    res = out.copy()

    # Clamp facial features gently so sclera stays crisp warm ivory <= 0.78 sRGB and darks >= #261D22
    res[face_feat] = np.clip(res[face_feat] * 0.775 + 26.0, SHADOW_FLOOR, MAX_SRGB_VAL)
    res[~face_feat] = np.clip(res[~face_feat], SHADOW_FLOOR, MAX_SRGB_VAL)

    # Identify clean painted UV-island core (closing sub-pixel raster gaps on dense meshes
    # while excluding unpainted black donor gutter pixels!)
    raw_painted = valid & (base_arr.max(axis=-1) > 16.0)
    donor_painted = binary_erosion(binary_dilation(raw_painted, iterations=2), iterations=2) & (base_arr.max(axis=-1) > 12.0)
    clean_core = donor_painted | face_feat
    if not clean_core.any():
        clean_core = valid

    gutter_dist, bleed_idx = distance_transform_edt(~clean_core, return_distances=True, return_indices=True)
    outside = ~clean_core
    bled = res[bleed_idx[0], bleed_idx[1]]
    res[outside] = bled[outside]

    # Beyond 6px pure mipmap bleed (>=4px required), use unused UV gutter pad to center sunlit sheet mean
    # while keeping painted 3D character colors rich, saturated, and free of chalky wash!
    deep_w = soft_step(gutter_dist.astype(np.float32), 7.5, 2.5)
    deep_frac = float(deep_w.mean())
    cur_island_mean = float(res.mean() / 255.0)
    if deep_frac > 0.05:
        needed_pad_srgb = np.clip(
            cur_island_mean + (target_mean - cur_island_mean) / (deep_frac * 0.92),
            0.32,
            0.64,
        )
    else:
        needed_pad_srgb = 0.47
    pad_v = float(needed_pad_srgb * 255.0)
    warm_pad = np.clip(np.array([pad_v + 8.0, pad_v, pad_v - 8.0], dtype=np.float32), SHADOW_FLOOR, MAX_SRGB_VAL)
    blend_rgb(res, warm_pad[None, None, :], deep_w * 0.94)

    # Exact sunlit midtone calibration on `u = res / MAX_SRGB_VAL` (strictly outside facial features,
    # never pinned at zero and never adding flat chalky grey!)
    modifiable = ~face_feat
    for _ in range(10):
        cur_mean = float(res.mean() / 255.0)
        err = target_mean - cur_mean
        if abs(err) < 0.002:
            break
        u = np.clip(res[modifiable] / MAX_SRGB_VAL, 0.05, 1.0)
        gamma = float(np.clip(1.0 - err * 1.60, 0.78, 1.25))
        res[modifiable] = np.clip((u ** gamma) * MAX_SRGB_VAL, SHADOW_FLOOR, MAX_SRGB_VAL)

    return np.clip(res, SHADOW_FLOOR, MAX_SRGB_VAL)


def paint_kiko(rgb: np.ndarray, pos: np.ndarray, nrm: np.ndarray) -> np.ndarray:
    """Kiko (from migo.glb): sunlit terracotta-crimson bandana, faded sky-maong denim vest over
    warm cream kalye tee, rolled sunlit khaki shorts, saddle-tan trumpo pouch, chestnut curls."""
    out = rgb.copy()
    x, y, z = pos[..., 0], pos[..., 1], pos[..., 2]
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    skin = detect_skin(rgb, pos)
    face_oval = front_face_oval(pos)
    face_feat = protect_facial_features(rgb, pos)
    shade = sunlit_cloth_shade(rgb, nrm)

    apply_child_skin_gradients(out, rgb, pos, skin, face_feat, (0.90, 0.79, 0.70), blush_strength=0.17)

    # 1. Forehead Bandana -> warm sunlit crimson-terracotta kalye bandana with cream woven diamond trim
    is_bandana = (
        ((y > 1.335) | ((y > 1.185) & (b > 108.0)))
        & (y < 1.56)
        & (~skin)
        & (~face_feat)
        & ((b > 102.0) | (g > 102.0) | (r > 150.0))
    )
    b_shade = shade * woven_grain(pos, 200.0, 0.022)
    band_col = np.array([186.0, 62.0, 58.0], dtype=np.float32)[None, None, :] * b_shade[..., None]
    diamond = np.abs(np.sin(x * 32.0 + z * 32.0)) + np.abs((y - 1.425) * 42.0)
    trim_w = 1.0 - soft_step(diamond, 0.70, 0.08)
    trim_w = np.maximum(trim_w, soft_band(y, 1.365, 0.007, 0.004))
    cream_col = np.array([194.0, 184.0, 162.0], dtype=np.float32)[None, None, :] * b_shade[..., None]
    band_col = band_col * (1.0 - trim_w[..., None]) + cream_col * trim_w[..., None]
    seam_b = np.maximum(soft_band(y, 1.352, 0.005, 0.004), soft_band(y, 1.495, 0.005, 0.004)) * 0.34
    band_col = band_col * (1.0 - seam_b[..., None]) + WARM_SEAM_RGB * seam_b[..., None]
    out[is_bandana] = band_col[is_bandana]

    # 2. Curly Hair -> warm sunlit chestnut-espresso curls including sideburns (never harsh black)
    ear_skin = (np.abs(x) > 0.118) & (y > 1.13) & (y < 1.255) & (z > -0.025) & (z < 0.048) & (g > 125.0) & skin
    forehead_skin = (np.abs(x) < 0.106) & (y > 1.125) & (y < 1.35) & (z > 0.068) & (g > 120.0) & skin
    is_hair = (y > 1.06) & (~face_feat) & (~is_bandana) & (~ear_skin) & (~forehead_skin) & ((~skin) | (g < 120.0))
    h_lum = np.clip(luminance(rgb) / 68.0, 0.62, 1.48)
    out[is_hair, 0] = np.clip(74.0 * h_lum[is_hair], 48.0, 118.0)
    out[is_hair, 1] = np.clip(52.0 * h_lum[is_hair], 35.0, 84.0)
    out[is_hair, 2] = np.clip(46.0 * h_lum[is_hair], 34.0, 74.0)

    # 3. Torso -> sun-faded maong denim vest over warm cream kalye tee (smooth SDF transitions!)
    pastel_shirt_knot = (y >= 0.72) & (y < 0.82) & (r > 152.0) & (g > 130.0) & ((b > 145.0) | (g > 165.0))
    is_top = (((y >= 0.815) & (y <= 1.175)) | pastel_shirt_knot) & (~skin) & (~face_oval) & (~is_hair) & (~is_bandana)
    t_shade = shade * woven_grain(pos, 210.0, 0.025)

    sleeve_w = soft_step(np.abs(x), 0.145, 0.009)
    collar_w = soft_step(y, 1.095, 0.008) * (1.0 - soft_step(np.abs(x), 0.095, 0.008))
    placket_w = (1.0 - soft_step(np.abs(x), 0.046, 0.007)) * soft_step(z, 0.015, 0.012) * soft_step(y, 0.83, 0.010)
    tee_w = np.clip(np.maximum(np.maximum(sleeve_w, collar_w), placket_w), 0.0, 1.0)

    tee_rgb = np.array([194.0, 185.0, 166.0], dtype=np.float32)[None, None, :] * t_shade[..., None]
    vest_rgb = np.array([92.0, 128.0, 172.0], dtype=np.float32)[None, None, :] * t_shade[..., None]

    # Soft chest pocket, warm copper top-stitching & soft umber lapel seam
    pocket_w = (
        soft_step(x, 0.060, 0.005)
        * (1.0 - soft_step(x, 0.122, 0.005))
        * soft_step(y, 0.93, 0.005)
        * (1.0 - soft_step(y, 1.01, 0.005))
        * soft_step(z, 0.035, 0.010)
    )
    vest_rgb = vest_rgb * (1.0 - 0.14 * pocket_w[..., None])
    stitch_w = soft_band(np.abs(x), 0.055, 0.003, 0.003) * soft_step(z, 0.02, 0.01) * 0.55
    vest_rgb = vest_rgb * (1.0 - stitch_w[..., None]) + np.array([192.0, 138.0, 74.0], dtype=np.float32) * stitch_w[..., None]

    top_rgb = vest_rgb * (1.0 - tee_w[..., None]) + tee_rgb * tee_w[..., None]
    lapel_seam = (
        np.maximum(
            soft_band(np.abs(x), 0.046, 0.0035, 0.0035) * soft_step(z, 0.02, 0.01),
            soft_band(np.abs(x), 0.145, 0.004, 0.004),
        )
        * 0.34
    )
    top_rgb = top_rgb * (1.0 - lapel_seam[..., None]) + WARM_SEAM_RGB * lapel_seam[..., None]
    out[is_top] = top_rgb[is_top]

    # 4. Sunlit Khaki Shorts & Saddle-Tan Trumpo Belt Pouch (morphologically closed to erase star border!)
    is_lower = (y >= 0.43) & (y < 0.82) & (~skin) & (~is_top)
    is_belt = is_lower & (r < 52.0) & (g < 52.0) & (b < 60.0)
    pouch_seed = is_lower & (~is_belt) & (
        ((b > g + 18.0) & (r > 68.0) & (z > 0.04))
        | ((r > 165.0) & (g > 140.0) & (b < 185.0) & (z > 0.05))
        | ((z > 0.084) & (y > 0.555) & (y < 0.715) & (x > -0.102) & (x < 0.045))
    )
    is_pouch = is_lower & (~is_belt) & binary_erosion(binary_dilation(pouch_seed, iterations=4), iterations=4)
    is_shorts = is_lower & (~is_belt) & (~is_pouch)
    s_shade = shade * woven_grain(pos, 185.0, 0.022)

    cuff_w = 1.0 - soft_step(y, 0.565, 0.008)
    shorts_col = (
        np.array([164.0, 140.0, 102.0], dtype=np.float32)[None, None, :] * (1.0 - cuff_w[..., None])
        + np.array([182.0, 162.0, 124.0], dtype=np.float32)[None, None, :] * cuff_w[..., None]
    ) * s_shade[..., None]
    cuff_seam = soft_band(y, 0.565, 0.0035, 0.0035) * 0.32
    shorts_col = shorts_col * (1.0 - cuff_seam[..., None]) + WARM_SEAM_RGB * cuff_seam[..., None]
    out[is_shorts] = shorts_col[is_shorts]

    if is_pouch.any():
        pouch_rgb = np.array([144.0, 90.0, 54.0], dtype=np.float32)[None, None, :] * s_shade[..., None]
        buckle_w = (
            (1.0 - soft_step(np.abs(x + 0.026), 0.015, 0.004))
            * (1.0 - soft_step(np.abs(y - 0.645), 0.013, 0.004))
            * soft_step(z, 0.065, 0.010)
        )
        pouch_rgb = pouch_rgb * (1.0 - buckle_w[..., None]) + np.array([194.0, 156.0, 72.0], dtype=np.float32) * buckle_w[..., None]
        out[is_pouch] = pouch_rgb[is_pouch]
    out[is_belt] = np.array([66.0, 48.0, 44.0], dtype=np.float32)

    # 5. Footwear -> warm tan leather tsinelas
    is_shoe = (y < 0.12) & (~skin)
    shoe_col = np.array([124.0, 84.0, 58.0], dtype=np.float32)[None, None, :] * shade[..., None]
    sole_w = 1.0 - soft_step(y, 0.024, 0.004)
    shoe_col = shoe_col * (1.0 - sole_w[..., None]) + np.array([182.0, 144.0, 74.0], dtype=np.float32) * sole_w[..., None]
    out[is_shoe] = shoe_col[is_shoe]
    return out


def paint_nene(rgb: np.ndarray, pos: np.ndarray, nrm: np.ndarray) -> np.ndarray:
    """Nene (from ligaya.glb): mango-gold flower scrunchie, sunlit sky-blue chambray pinafore
    jumper over warm cream blouse, soft pleated skirt, striped cream knee socks, plum flats."""
    out = rgb.copy()
    x, y, z = pos[..., 0], pos[..., 1], pos[..., 2]
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    skin = detect_skin(rgb, pos)
    face_oval = front_face_oval(pos)
    face_feat = protect_facial_features(rgb, pos)
    shade = sunlit_cloth_shade(rgb, nrm)

    apply_child_skin_gradients(out, rgb, pos, skin, face_feat, (0.91, 0.80, 0.73), blush_strength=0.18)

    bare_arm = (np.abs(x) > 0.142) & (y > 0.80) & (y < 1.16) & (g > 124.0) & skin
    bare_neck = (y > 1.04) & (np.abs(x) < 0.070) & skin
    bare_cheek_temple = (np.abs(x) < 0.112) & (y >= 1.13) & (y < 1.38) & (z > 0.068) & (g > 122.0) & skin
    bare_ear = (np.abs(x) > 0.118) & (y > 1.135) & (y < 1.255) & (z > -0.025) & (z < 0.048) & (g > 128.0) & skin
    bare_leg = (y < 0.415) & skin
    protected_skin = face_feat | bare_cheek_temple | bare_arm | bare_neck | bare_ear | bare_leg

    # 1. Mango-Gold Hair Flower Scrunchie
    is_clip = (y > 1.45) & (z > 0.0) & (~protected_skin) & (~skin) & (r > 180.0) & ((b > 120.0) | (g > 155.0))
    is_clip_center = is_clip & (g > 165.0) & (b < 155.0)
    out[is_clip] = np.array([196.0, 152.0, 56.0], dtype=np.float32) * shade[is_clip, None]
    out[is_clip_center] = np.array([188.0, 84.0, 68.0], dtype=np.float32)

    # 2. Wavy Hair -> warm sunlit dark-chestnut Filipino waves (including front side-locks!)
    is_hair = (
        (y > 0.78)
        & (~protected_skin)
        & (~is_clip)
        & (g < 132.0)
        & (b < 98.0)
        & ((r - b) > 14.0)
        & ((y > 1.02) | (z < -0.01) | (np.abs(x) > 0.134))
    )
    h_lum = np.clip(luminance(rgb) / 76.0, 0.58, 1.48)
    out[is_hair, 0] = np.clip(66.0 * h_lum[is_hair], 44.0, 108.0)
    out[is_hair, 1] = np.clip(48.0 * h_lum[is_hair], 33.0, 80.0)
    out[is_hair, 2] = np.clip(50.0 * h_lum[is_hair], 36.0, 82.0)

    # 3. Sky-Blue Chambray Pinafore Jumper over Cream Blouse (smooth SDF seams & soft pleats!)
    is_dress = (
        (y >= 0.395)
        & (y <= 1.175)
        & (~protected_skin)
        & (~is_hair)
        & (~is_clip)
        & ((np.abs(x) < 0.31) | (y < 0.79))
    )
    d_shade = shade * woven_grain(pos, 205.0, 0.022)

    blouse_w = soft_step(y, 0.945, 0.008) * (1.0 - soft_step(np.abs(x), 0.066, 0.007))
    blouse_rgb = np.array([196.0, 189.0, 172.0], dtype=np.float32)[None, None, :] * d_shade[..., None]
    jumper_rgb = np.array([112.0, 156.0, 196.0], dtype=np.float32)[None, None, :] * d_shade[..., None]

    waist_w = soft_step(y, 0.745, 0.006) * (1.0 - soft_step(y, 0.790, 0.006))
    jumper_rgb = jumper_rgb * (1.0 - 0.14 * waist_w[..., None])
    hem_w = soft_step(y, 0.415, 0.005) * (1.0 - soft_step(y, 0.442, 0.005))
    jumper_rgb = jumper_rgb * (1.0 - hem_w[..., None]) + np.array([196.0, 158.0, 64.0], dtype=np.float32)[None, None, :] * d_shade[..., None] * hem_w[..., None]

    # Softchambray pleat shading instead of harsh black lines
    angle = np.arctan2(x, z)
    pleat_soft = (1.0 - soft_step(np.abs(np.sin(angle * 8.0)), 0.08, 0.06)) * (1.0 - soft_step(y, 0.745, 0.01)) * soft_step(y, 0.445, 0.01)
    jumper_rgb = jumper_rgb * (1.0 - 0.16 * pleat_soft[..., None]) + WARM_SEAM_RGB * (0.16 * pleat_soft[..., None])

    dress_rgb = jumper_rgb * (1.0 - blouse_w[..., None]) + blouse_rgb * blouse_w[..., None]
    bib_seam = (
        np.maximum(
            soft_band(y, 0.945, 0.0035, 0.0035) * (1.0 - soft_step(np.abs(x), 0.070, 0.005)),
            soft_step(y, 0.945, 0.005) * soft_band(np.abs(x), 0.066, 0.0035, 0.0035),
        )
        * 0.30
    )
    dress_rgb = dress_rgb * (1.0 - bib_seam[..., None]) + WARM_SEAM_RGB * bib_seam[..., None]
    out[is_dress] = dress_rgb[is_dress]

    # 4. Ribbed Cream Knee Socks with Smooth Varsity Stripes
    sock_mask = (y >= 0.055) & (y < 0.312) & skin
    sock_blend = 1.0 - soft_step(y, 0.302, 0.007)
    sock_rib = 1.0 + 0.022 * np.sin(np.arctan2(x, z) * 26.0)
    sk_shade = shade * sock_rib
    sock_rgb = np.array([194.0, 188.0, 176.0], dtype=np.float32)[None, None, :] * sk_shade[..., None]
    navy_w = soft_band(y, 0.285, 0.007, 0.0035)
    gold_w = soft_band(y, 0.266, 0.007, 0.0035)
    sock_rgb = sock_rgb * (1.0 - navy_w[..., None]) + np.array([68.0, 98.0, 144.0], dtype=np.float32)[None, None, :] * sk_shade[..., None] * navy_w[..., None]
    sock_rgb = sock_rgb * (1.0 - gold_w[..., None]) + np.array([196.0, 152.0, 58.0], dtype=np.float32)[None, None, :] * sk_shade[..., None] * gold_w[..., None]
    out[sock_mask] = out[sock_mask] * (1.0 - sock_blend[sock_mask, None]) + sock_rgb[sock_mask] * sock_blend[sock_mask, None]

    # 5. Footwear -> warm plum-slate Mary Jane flats
    is_shoe = (y < 0.118) & (~skin)
    shoe_col = np.array([72.0, 58.0, 82.0], dtype=np.float32)[None, None, :] * shade[..., None]
    sole_w = 1.0 - soft_step(y, 0.022, 0.004)
    shoe_col = shoe_col * (1.0 - sole_w[..., None]) + np.array([182.0, 174.0, 162.0], dtype=np.float32) * sole_w[..., None]
    out[is_shoe] = shoe_col[is_shoe]
    return out


def paint_buknoy(rgb: np.ndarray, pos: np.ndarray, nrm: np.ndarray) -> np.ndarray:
    """Buknoy (from junjun.glb): warm sunlit kayumanggi skin, open provincial checkered camisa
    over cream tee, sun-faded river-indigo shorts with gagamba matchbox, leaf-green tsinelas."""
    out = rgb.copy()
    x, y, z = pos[..., 0], pos[..., 1], pos[..., 2]
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    skin = detect_skin(rgb, pos)
    face_oval = front_face_oval(pos)
    face_feat = protect_facial_features(rgb, pos)
    shade = sunlit_cloth_shade(rgb, nrm)

    apply_child_skin_gradients(out, rgb, pos, skin, face_feat, (0.78, 0.73, 0.66), blush_strength=0.14)

    # 2. Spiky Hair -> warm sunlit espresso-slate spikes
    is_hair = (y > 1.18) & (~skin) & (~face_oval) & (r < 128.0) & (b < 72.0)
    h_lum = np.clip(luminance(rgb) / 46.0, 0.60, 1.60)
    out[is_hair, 0] = np.clip(52.0 * h_lum[is_hair], 40.0, 88.0)
    out[is_hair, 1] = np.clip(46.0 * h_lum[is_hair], 34.0, 78.0)
    out[is_hair, 2] = np.clip(54.0 * h_lum[is_hair], 40.0, 92.0)

    # 3. Open Provincial Checkered Camisa Vest over Warm Cream Tee (clean modeled garment boundary!)
    is_sando = (y >= 0.755) & (y <= 1.185) & (~skin) & (~face_oval) & (~is_hair) & (np.abs(x) < 0.19)
    c_shade = shade * woven_grain(pos, 195.0, 0.025)

    inner_w = (1.0 - soft_step(np.abs(x), 0.039, 0.006)) * soft_step(z, 0.01, 0.012)
    tee_rgb = np.array([194.0, 185.0, 162.0], dtype=np.float32)[None, None, :] * c_shade[..., None]

    # Smooth cosine-blended woven plaid (eliminates crisp pixel aliasing)
    u_p = np.mod((x + 0.55 * z) * 24.0, 1.0)
    v_p = np.mod(y * 24.0, 1.0)
    bu = 1.0 - soft_step(np.abs(u_p - 0.25), 0.21, 0.045)
    bv = 1.0 - soft_step(np.abs(v_p - 0.25), 0.21, 0.045)
    thin = np.maximum(soft_band(u_p, 0.75, 0.038, 0.025), soft_band(v_p, 0.75, 0.038, 0.025))

    ochre = np.array([192.0, 148.0, 76.0], dtype=np.float32)
    terra = np.array([182.0, 88.0, 62.0], dtype=np.float32)
    jade = np.array([64.0, 118.0, 108.0], dtype=np.float32)
    cream = np.array([196.0, 184.0, 152.0], dtype=np.float32)

    xor_w = bu * (1.0 - bv) + bv * (1.0 - bu)
    and_w = bu * bv
    plaid = ochre[None, None, :] * (1.0 - xor_w - and_w)[..., None] + terra[None, None, :] * xor_w[..., None] + jade[None, None, :] * and_w[..., None]
    plaid = plaid * (1.0 - 0.75 * thin[..., None]) + cream[None, None, :] * (0.75 * thin[..., None])
    camisa_rgb = plaid * c_shade[..., None]

    top_rgb = camisa_rgb * (1.0 - inner_w[..., None]) + tee_rgb * inner_w[..., None]
    placket_seam = soft_band(np.abs(x), 0.039, 0.0035, 0.0035) * soft_step(z, 0.01, 0.01) * 0.34
    top_rgb = top_rgb * (1.0 - placket_seam[..., None]) + WARM_SEAM_RGB * placket_seam[..., None]
    out[is_sando] = top_rgb[is_sando]

    # 4. Sun-Faded River-Indigo Shorts + Gagamba Matchbox (100% covers donor #23 & red/gold patch!)
    is_shorts = (y >= 0.41) & (y < 0.755) & (np.abs(x) < 0.22) & (~is_sando) & ((y >= 0.46) | (~skin))
    s_shade = shade * woven_grain(pos, 210.0, 0.026)
    hem_line = 0.465 + 0.006 * np.sin(x * 80.0)
    fray_w = 1.0 - soft_step(y - hem_line, 0.0, 0.007)
    shorts_rgb = (
        np.array([78.0, 108.0, 142.0], dtype=np.float32)[None, None, :] * (1.0 - fray_w[..., None])
        + np.array([128.0, 152.0, 178.0], dtype=np.float32)[None, None, :] * fray_w[..., None]
    ) * s_shade[..., None]

    mb_w = (
        soft_step(x, -0.112, 0.004)
        * (1.0 - soft_step(x, -0.056, 0.004))
        * soft_step(y, 0.602, 0.004)
        * (1.0 - soft_step(y, 0.680, 0.004))
        * soft_step(z, 0.045, 0.010)
    )
    striker_w = mb_w * soft_step(y, 0.660, 0.003)
    mb_col = np.array([192.0, 146.0, 64.0], dtype=np.float32)[None, None, :] * s_shade[..., None]
    shorts_rgb = shorts_rgb * (1.0 - mb_w[..., None]) + mb_col * mb_w[..., None]
    shorts_rgb = shorts_rgb * (1.0 - striker_w[..., None]) + np.array([168.0, 64.0, 56.0], dtype=np.float32) * striker_w[..., None]
    out[is_shorts] = shorts_rgb[is_shorts]

    # 5. Footwear -> sunlit leaf-green & ochre rubber tsinelas
    is_shoe = (y < 0.11) & (~skin)
    shoe_col = np.array([68.0, 132.0, 86.0], dtype=np.float32)[None, None, :] * shade[..., None]
    sole_w = 1.0 - soft_step(y, 0.022, 0.004)
    shoe_col = shoe_col * (1.0 - sole_w[..., None]) + np.array([182.0, 142.0, 68.0], dtype=np.float32) * sole_w[..., None]
    out[is_shoe] = shoe_col[is_shoe]
    return out


def paint_tala(rgb: np.ndarray, pos: np.ndarray, nrm: np.ndarray) -> np.ndarray:
    """Tala (from toni.glb): warm sunlit morena skin, berry-maroon & woven habi backward cap,
    maroon #07 barangay track singlet, plum-slate sprint shorts, woven friendship wristbands."""
    out = rgb.copy()
    x, y, z = pos[..., 0], pos[..., 1], pos[..., 2]
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    skin = detect_skin(rgb, pos)
    face_oval = front_face_oval(pos)
    face_feat = protect_facial_features(rgb, pos)
    shade = sunlit_cloth_shade(rgb, nrm)

    apply_child_skin_gradients(out, rgb, pos, skin, face_feat, (0.82, 0.78, 0.72), blush_strength=0.16)

    # Smooth woven habi friendship wristbands on forearms
    skin_edit = skin & (~face_feat)
    wrist_zone = skin_edit & (np.abs(x) > 0.235) & (y >= 0.702) & (y <= 0.761)
    if wrist_zone.any():
        wb_env = soft_step(y, 0.706, 0.004) * (1.0 - soft_step(y, 0.757, 0.004))
        band_t = np.mod((y - 0.705) / 0.052 * 3.0, 1.0)
        c1 = np.array([184.0, 68.0, 72.0], dtype=np.float32)
        c2 = np.array([196.0, 152.0, 58.0], dtype=np.float32)
        c3 = np.array([62.0, 134.0, 124.0], dtype=np.float32)
        w2 = soft_step(band_t, 0.33, 0.06) * (1.0 - soft_step(band_t, 0.67, 0.06))
        w3 = soft_step(band_t, 0.67, 0.06)
        wb_col = (c1[None, None, :] * (1.0 - w2 - w3)[..., None] + c2[None, None, :] * w2[..., None] + c3[None, None, :] * w3[..., None]) * shade[..., None]
        out[wrist_zone] = out[wrist_zone] * (1.0 - wb_env[wrist_zone, None]) + wb_col[wrist_zone] * wb_env[wrist_zone, None]

    # 2. Backward Cap & Warm Sunlit Espresso Hair
    is_strap = (y > 1.36) & (y < 1.49) & (~skin) & (~face_oval) & (~face_feat) & (np.abs(r - g) < 24.0) & (r > 35.0) & (r < 120.0) & (z > 0.04)
    cap_body = (y > 1.36) & (~skin) & (~face_oval) & (~face_feat) & (~is_strap) & (r > 125.0) & ((r - g) > 42.0)
    is_cap = cap_body | is_strap

    cap_shade = shade * woven_grain(pos, 195.0, 0.022)
    cap_rgb = np.array([144.0, 54.0, 68.0], dtype=np.float32)[None, None, :] * cap_shade[..., None]
    habi_env = soft_step(y, 1.392, 0.005) * (1.0 - soft_step(y, 1.454, 0.005))
    angle = np.arctan2(x, z)
    dia = np.abs(np.sin(angle * 14.0)) + np.abs((y - 1.423) * 34.0)
    gold_w = (1.0 - soft_step(dia, 0.80, 0.08)) * habi_env
    teal_w = (1.0 - soft_step(dia, 0.40, 0.07)) * habi_env
    cap_rgb = cap_rgb * (1.0 - gold_w[..., None]) + np.array([196.0, 152.0, 58.0], dtype=np.float32)[None, None, :] * cap_shade[..., None] * gold_w[..., None]
    cap_rgb = cap_rgb * (1.0 - teal_w[..., None]) + np.array([58.0, 128.0, 118.0], dtype=np.float32)[None, None, :] * cap_shade[..., None] * teal_w[..., None]
    out[cap_body] = cap_rgb[cap_body]
    out[is_strap] = np.array([62.0, 52.0, 64.0], dtype=np.float32)

    is_hair = ((y > 1.165) | ((y > 1.10) & (z < -0.015))) & (~skin) & (~face_oval) & (~face_feat) & (~is_cap)
    h_lum = np.clip(luminance(rgb) / 56.0, 0.58, 1.50)
    out[is_hair, 0] = np.clip(56.0 * h_lum[is_hair], 40.0, 92.0)
    out[is_hair, 1] = np.clip(44.0 * h_lum[is_hair], 32.0, 74.0)
    out[is_hair, 2] = np.clip(52.0 * h_lum[is_hair], 38.0, 84.0)

    # 3. Sunlit Berry-Maroon Barangay Track Singlet (#07 in Barrio Gold — pure 3D SDF trim, zero donor confetti!)
    bare_neck_arm = skin & (
        (np.abs(x) > 0.195)
        | ((np.abs(x) > 0.142) & (y > 0.91))
        | ((np.abs(x) < 0.080) & (y > 1.045) & (z > 0.0))
    )
    is_jersey = (
        (y >= 0.665)
        & (y <= 1.170)
        & (~bare_neck_arm)
        & (~face_oval)
        & (~is_hair)
        & (~is_cap)
        & (np.abs(x) < 0.225)
    )
    j_shade = shade * woven_grain(pos, 215.0, 0.022)
    j_rgb = np.array([142.0, 50.0, 66.0], dtype=np.float32)[None, None, :] * j_shade[..., None]

    collar_d = np.hypot(x / 0.088, (y - 1.118) / 0.082)
    collar_trim = soft_band(collar_d, 1.0, 0.11, 0.05) * soft_step(y, 1.00, 0.02)
    arm_trim = soft_band(np.abs(x), 0.132, 0.011, 0.005) * soft_step(y, 0.90, 0.02)
    hem_gold = soft_band(y, 0.698, 0.009, 0.004)
    gold_trim_w = np.clip(np.maximum(collar_trim, np.maximum(arm_trim, hem_gold)), 0.0, 1.0)
    j_rgb = j_rgb * (1.0 - gold_trim_w[..., None]) + np.array([196.0, 152.0, 58.0], dtype=np.float32)[None, None, :] * j_shade[..., None] * gold_trim_w[..., None]

    hem_cream_w = 1.0 - soft_step(y, 0.684, 0.005)
    j_rgb = j_rgb * (1.0 - hem_cream_w[..., None]) + np.array([194.0, 186.0, 170.0], dtype=np.float32)[None, None, :] * j_shade[..., None] * hem_cream_w[..., None]

    # Smooth anti-aliased "07" chest number with warm-umber edge
    in_chest = is_jersey & (z > 0.05) & (y >= 0.795) & (y <= 0.945)
    ox = (x + 0.032) / 0.023
    oy = (y - 0.87) / 0.062
    rad0 = np.hypot(ox, oy)
    w_0 = (1.0 - soft_step(rad0, 1.02, 0.09)) * soft_step(rad0, 0.54, 0.09)
    w_7_top = soft_step(x, 0.006, 0.004) * (1.0 - soft_step(x, 0.056, 0.004)) * soft_band(y, 0.924, 0.011, 0.004)
    w_7_diag = soft_step(y, 0.805, 0.005) * (1.0 - soft_step(y, 0.932, 0.005)) * (1.0 - soft_step(np.abs(x - (0.016 + (y - 0.805) * 0.26)), 0.011, 0.004))
    num_w = np.clip(np.maximum(w_0, np.maximum(w_7_top, w_7_diag)), 0.0, 1.0) * in_chest.astype(np.float32)
    num_halo = np.clip(gaussian_filter(num_w, sigma=1.2) * 1.6, 0.0, 1.0) * (1.0 - num_w) * in_chest.astype(np.float32)
    j_rgb = j_rgb * (1.0 - 0.45 * num_halo[..., None]) + WARM_SEAM_RGB * (0.45 * num_halo[..., None])
    j_rgb = j_rgb * (1.0 - num_w[..., None]) + np.array([196.0, 156.0, 62.0], dtype=np.float32)[None, None, :] * j_shade[..., None] * num_w[..., None]
    out[is_jersey] = j_rgb[is_jersey]

    # 4. Warm Plum-Slate Athletic Runner Shorts
    is_shorts = (y >= 0.33) & (y < 0.665) & (~skin) & (~is_jersey) & (np.abs(x) < 0.245)
    s_shade = shade * woven_grain(pos, 200.0, 0.022)
    s_rgb = np.array([72.0, 62.0, 80.0], dtype=np.float32)[None, None, :] * s_shade[..., None]
    stripe_w = soft_band(np.abs(x), 0.158, 0.012, 0.005)
    hem_m_w = 1.0 - soft_step(y, 0.375, 0.006)
    s_rgb = s_rgb * (1.0 - stripe_w[..., None]) + np.array([196.0, 152.0, 58.0], dtype=np.float32)[None, None, :] * s_shade[..., None] * stripe_w[..., None]
    s_rgb = s_rgb * (1.0 - hem_m_w[..., None]) + np.array([138.0, 50.0, 66.0], dtype=np.float32)[None, None, :] * s_shade[..., None] * hem_m_w[..., None]
    out[is_shorts] = s_rgb[is_shorts]

    # 5. Track Shoes -> sunlit maroon & barrio-gold sprint shoes
    is_shoe = (y < 0.115) & (~skin)
    sh_rgb = np.array([112.0, 48.0, 60.0], dtype=np.float32)[None, None, :] * shade[..., None]
    gold_sw = soft_band(y, 0.034, 0.009, 0.004)
    sole_sw = 1.0 - soft_step(y, 0.020, 0.004)
    sh_rgb = sh_rgb * (1.0 - gold_sw[..., None]) + np.array([196.0, 152.0, 58.0], dtype=np.float32) * gold_sw[..., None]
    sh_rgb = sh_rgb * (1.0 - sole_sw[..., None]) + np.array([186.0, 180.0, 170.0], dtype=np.float32) * sole_sw[..., None]
    out[is_shoe] = sh_rgb[is_shoe]
    return out


PAINTERS = {
    "kiko": paint_kiko,
    "nene": paint_nene,
    "buknoy": paint_buknoy,
    "tala": paint_tala,
}


# ---------------------------------------------------------------------------
# Godot .import Sidecar Writers (Preserve Existing `[remap]` & `[deps]` UIDs!)
# ---------------------------------------------------------------------------


def extract_remap_and_deps(imp_path: Path) -> str | None:
    """Read existing Godot-generated `[remap]` and `[deps]` blocks while stripping any `md5`/`generator_parameters`."""
    if not imp_path.exists():
        return None
    text = imp_path.read_text(encoding="utf-8", errors="ignore")
    if "[params]" not in text or "uid=" not in text:
        return None
    head, _ = text.split("[params]", 1)
    clean_lines = [
        ln
        for ln in head.splitlines()
        if not ln.strip().startswith("md5=") and not ln.strip().startswith("generator_parameters=")
    ]
    return "\n".join(clean_lines).rstrip() + "\n\n"


def write_glb_import(glb_path: Path, kid_id: str) -> None:
    imp_path = glb_path.with_name(glb_path.name + ".import")
    preserved = extract_remap_and_deps(imp_path)
    if preserved is None:
        preserved = f"""[remap]

importer="scene"
importer_version=1
type="PackedScene"

[deps]

source_file="res://assets/models/characters/{kid_id}.glb"

"""
    params = """[params]

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
_subresources={}
gltf/naming_version=2
gltf/embedded_image_handling=1
"""
    imp_path.write_text(preserved + params, encoding="utf-8", newline="\n")


def write_model_tex_import(tex_path: Path, res_path: str, is_normal: bool) -> None:
    imp_path = tex_path.with_name(tex_path.name + ".import")
    preserved = extract_remap_and_deps(imp_path)
    if preserved is None:
        preserved = f"""[remap]

importer="texture"
type="CompressedTexture2D"
metadata={{
"vram_texture": true
}}

[deps]

source_file="{res_path}"

"""
    normal_flag = 1 if is_normal else 0
    params = f"""[params]

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
    imp_path.write_text(preserved + params, encoding="utf-8", newline="\n")


def write_ui_tex_import(tex_path: Path, res_path: str, compress_mode: int, mipmaps: bool) -> None:
    imp_path = tex_path.with_name(tex_path.name + ".import")
    preserved = extract_remap_and_deps(imp_path)
    if preserved is None:
        preserved = f"""[remap]

importer="texture"
type="CompressedTexture2D"

[deps]

source_file="{res_path}"

"""
    mip_str = "true" if mipmaps else "false"
    params = f"""[params]

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
    imp_path.write_text(preserved + params, encoding="utf-8", newline="\n")


# ---------------------------------------------------------------------------
# Optional Blender 5.2 Portrait/Card/Face Renderer (`--with-portraits`)
# ---------------------------------------------------------------------------

BLENDER_SCRIPT = r'''
import json
import math
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
    for o in bpy.data.objects:
        if o.type == "MESH" and not any(m.type == "ARMATURE" for m in o.modifiers):
            o.hide_render = True
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
    bg.inputs["Color"].default_value = (1.0, 0.78, 0.52, 1.0)
    bg.inputs["Strength"].default_value = 0.60

    cyaw_deg = float(job["card_yaw"])
    cpitch_deg = float(job["card_pitch"])
    key_side = float(job["key_side"])

    key_sun = add_sun(scene, "Key", 3.45, (0, 0, 0), (1.0, 0.84, 0.58), angle_deg=11.0)
    aim_sun_from_dir(key_sun, 26.0, cyaw_deg + key_side * 38.0)

    fill_sun = add_sun(scene, "Fill", 1.05, (0, 0, 0), (1.0, 0.80, 0.58), angle_deg=28.0)
    aim_sun_from_dir(fill_sun, 8.0, cyaw_deg - key_side * 52.0)

    mango_rim = add_sun(scene, "MangoRim", 4.40, (0, 0, 0), (1.0, 0.68, 0.22), angle_deg=8.0)
    aim_sun_from_dir(mango_rim, 20.0, cyaw_deg - key_side * 142.0)

    gold_kicker = add_sun(scene, "GoldKicker", 2.40, (0, 0, 0), (1.0, 0.75, 0.32), angle_deg=10.0)
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
    chin_world = rig.matrix_world @ head_pb.head if head_pb is not None else Vector((0.0, -0.04, 1.12))
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

    sclera = in_eye_band & (r > 152.0) & (g > 142.0) & (b > 132.0) & (np.abs(r - b) < 46.0)
    near_sclera = binary_dilation(sclera, iterations=10)
    dark_eye = in_eye_band & near_sclera & (lum < 112.0) & (b < 100.0)
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

        dx = (xx - icx) / irad
        dy = (yy - icy) / irad
        r_norm = np.hypot(dx, dy)
        crescent = dark_eye & (r_norm < 0.92) & (r_norm > 0.28) & (dy > 0.05)
        c_weight = np.clip((dy - 0.05) * (1.0 - np.abs(r_norm - 0.62) * 1.8), 0.0, 1.0)
        rgb[crescent, 0] = np.clip(rgb[crescent, 0] + 46.0 * c_weight[crescent], 0.0, 255.0)
        rgb[crescent, 1] = np.clip(rgb[crescent, 1] + 22.0 * c_weight[crescent], 0.0, 255.0)
        rgb[crescent, 2] = np.clip(rgb[crescent, 2] + 5.0 * c_weight[crescent], 0.0, 255.0)

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

    pil_rgb = Image.fromarray(np.clip(rgb, 0.0, 255.0).astype(np.uint8), mode="RGB")
    pil_rgb = ImageEnhance.Color(pil_rgb).enhance(1.16)
    pil_rgb = ImageEnhance.Contrast(pil_rgb).enhance(1.06)
    graded = np.asarray(pil_rgb, dtype=np.float32)

    warm_zone = (alpha > 0.2) & (~sclera) & (~catchlight_mask)
    graded[warm_zone, 0] = np.clip(graded[warm_zone, 0] * 1.05 + 3.0, 0.0, 255.0)
    graded[warm_zone, 1] = np.clip(graded[warm_zone, 1] * 0.99 + 1.0, 0.0, 255.0)
    graded[warm_zone, 2] = np.clip(graded[warm_zone, 2] * 0.90, 0.0, 255.0)

    fg = alpha > 0.38
    inner_band = binary_erosion(fg, iterations=2) & (~binary_erosion(fg, iterations=6))
    side_x = (xx - 300.0) / 300.0
    rim_dir_w = np.clip((-kdir * side_x) * 0.65 + (1.0 - yy / 800.0) * 0.45, 0.0, 1.0)
    mango_col = np.array([255.0, 194.0, 68.0], dtype=np.float32)
    rw_mask = inner_band & (~sclera) & (~catchlight_mask)
    blend_m = 0.30 * rim_dir_w[rw_mask, None]
    graded[rw_mask] = graded[rw_mask] * (1.0 - blend_m) + mango_col * blend_m

    graded[sclera] = np.clip(rgb[sclera] * np.array([1.01, 0.99, 0.96], dtype=np.float32), 0.0, 252.0)
    graded[catchlight_mask] = rgb[catchlight_mask]
    return graded, alpha


def make_komiks_backdrop(width: int, height: int, pal: dict, center: tuple[float, float]) -> Image.Image:
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

    port_rgb = graded_rgb.copy()
    port_rgb[inner_ink] = port_rgb[inner_ink] * 0.42 + INK_RGB * 0.58
    port_rgb[outer_ink_p] = INK_RGB
    port_alpha = np.clip(alpha * 255.0, 0.0, 255.0)
    port_alpha[outer_ink_p] = np.maximum(port_alpha[outer_ink_p], 242.0)
    port_rgba = np.dstack([port_rgb, port_alpha]).astype(np.uint8)
    Image.fromarray(port_rgba, mode="RGBA").save(portrait_dst, format="PNG", compress_level=6)

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
# Main Pipeline Execution & Self-Verification
# ---------------------------------------------------------------------------


def snapshot_authored_hashes() -> dict[Path, str]:
    """Pin all 6 authored `.glb`s, `_0..2.png` textures, and `.import` files."""
    hashes: dict[Path, str] = {}
    for aid in AUTHORED_IDS:
        for suffix in (f"{aid}.glb", f"{aid}.glb.import"):
            p = CHAR_DIR / suffix
            if p.exists():
                hashes[p] = sha256_file(p)
        for ti in range(3):
            for suffix in (f"{aid}_{ti}.png", f"{aid}_{ti}.png.import"):
                p = CHAR_DIR / suffix
                if p.exists():
                    hashes[p] = sha256_file(p)
    return hashes


def verify_kid_outputs(kid_id: str) -> dict[str, float | int]:
    glb_path = CHAR_DIR / f"{kid_id}.glb"
    gltf, _ = parse_glb(glb_path)
    prim = gltf["meshes"][0]["primitives"][0]
    pos_acc = gltf["accessors"][prim["attributes"]["POSITION"]]
    min_y = float(pos_acc["min"][1])
    max_y = float(pos_acc["max"][1])
    height = max_y - min_y
    if abs(min_y) > 0.002 or not (1.69 <= height <= 1.71):
        raise AssertionError(f"{kid_id}.glb height/origin out of bounds: min_y={min_y}, height={height}")
    if len(gltf.get("animations", [])) < 7:
        raise AssertionError(f"{kid_id}.glb missing animations: {len(gltf.get('animations', []))}")

    tex1 = np.asarray(Image.open(CHAR_DIR / f"{kid_id}_1.png").convert("RGB"), dtype=np.float32) / 255.0
    mean_srgb = float(tex1.mean())
    max_srgb = float(tex1.max())
    min_srgb = float(tex1.min())
    if not (0.42 <= mean_srgb <= 0.50):
        raise AssertionError(f"{kid_id}_1.png mean_sRGB={mean_srgb:.4f} outside [0.42, 0.50]")
    if max_srgb > 0.781:
        raise AssertionError(f"{kid_id}_1.png max_sRGB={max_srgb:.4f} exceeds 0.78")

    for imp_name in (f"{kid_id}.glb.import", f"{kid_id}_0.png.import", f"{kid_id}_1.png.import", f"{kid_id}_2.png.import"):
        txt = (CHAR_DIR / imp_name).read_text(encoding="utf-8")
        if "md5=" in txt or "generator_parameters=" in txt:
            raise AssertionError(f"{imp_name} contains forbidden md5/generator_parameters line")
        if imp_name.endswith(".png.import") and "compress/mode=2" not in txt:
            raise AssertionError(f"{imp_name} missing compress/mode=2")
    if "compress/normal_map=1" not in (CHAR_DIR / f"{kid_id}_2.png.import").read_text(encoding="utf-8"):
        raise AssertionError(f"{kid_id}_2.png.import missing compress/normal_map=1")

    return {
        "glb_mib": glb_path.stat().st_size / (1024.0 * 1024.0),
        "height": height,
        "min_srgb": min_srgb,
        "mean_srgb": mean_srgb,
        "max_srgb": max_srgb,
    }


def main() -> None:
    parser = argparse.ArgumentParser(description="Build 4 expansion Filipino kids (Overhaul v2).")
    parser.add_argument("--with-portraits", action="store_true", help="Also re-render 2D portraits/cards/faces in Blender.")
    args = parser.parse_args()

    pinned_hashes = snapshot_authored_hashes()

    print("[1/2] Sculpting organic child proportions & painting soft sunlit 90s province textures...")
    for spec in KIDS_SPEC:
        kid_id = spec["id"]
        base_id = spec["base"]
        src_glb = CHAR_DIR / f"{base_id}.glb"
        gltf, bin_chunk = parse_glb(src_glb)

        prim = gltf["meshes"][0]["primitives"][0]
        raw_pos = read_accessor(gltf, bin_chunk, prim["attributes"]["POSITION"]).astype(np.float32)
        raw_nrm = read_accessor(gltf, bin_chunk, prim["attributes"]["NORMAL"]).astype(np.float32)
        raw_uv = read_accessor(gltf, bin_chunk, prim["attributes"]["TEXCOORD_0"]).astype(np.float32)
        raw_idx = read_accessor(gltf, bin_chunk, prim["indices"]).astype(np.int64).reshape(-1, 3)

        sculpted_pos, sculpted_nrm = sculpt_organic_child_mesh(raw_pos, raw_nrm, raw_idx, kid_id)
        pos_grid, nrm_grid, valid_uv = build_uv_3d_maps(
            sculpted_pos, sculpted_nrm, raw_uv, raw_idx, size=BASE_COLOR_SIZE
        )

        img_base_src = extract_embedded_image(gltf, bin_chunk, 1).convert("RGB").resize(
            (BASE_COLOR_SIZE, BASE_COLOR_SIZE), Image.Resampling.LANCZOS
        )
        base_arr = np.asarray(img_base_src, dtype=np.float32)
        face_feat = protect_facial_features(base_arr, pos_grid, valid_uv)

        painted_raw = PAINTERS[kid_id](base_arr, pos_grid, nrm_grid)
        painted_sunlit = finalize_sunlit_albedo(
            painted_raw,
            base_arr,
            valid_uv,
            face_feat,
            target_mean=float(spec["target_mean_srgb"]),
        )
        img_painted = Image.fromarray(np.round(painted_sunlit).astype(np.uint8), mode="RGB")

        img_mr, img_norm = polish_aux_maps(
            extract_embedded_image(gltf, bin_chunk, 0),
            extract_embedded_image(gltf, bin_chunk, 2),
            pos_grid,
            kid_id,
        )

        png_0 = encode_png(img_mr)
        png_1 = encode_png(img_painted)
        png_2 = encode_png(img_norm)

        new_glb_bytes = rebuild_glb_with_sculpt_and_images(
            gltf, bin_chunk, sculpted_pos, sculpted_nrm, [png_0, png_1, png_2]
        )
        dst_glb = CHAR_DIR / f"{kid_id}.glb"
        dst_glb.write_bytes(new_glb_bytes)
        write_glb_import(dst_glb, kid_id)

        for idx_t, png_bytes in enumerate([png_0, png_1, png_2]):
            tex_path = CHAR_DIR / f"{kid_id}_{idx_t}.png"
            tex_path.write_bytes(png_bytes)
            write_model_tex_import(
                tex_path,
                f"res://assets/models/characters/{kid_id}_{idx_t}.png",
                is_normal=(idx_t == 2),
            )

        stats = verify_kid_outputs(kid_id)
        print(
            f"  -> {kid_id}.glb ({stats['glb_mib']:.2f} MiB, h={stats['height']:.4f}m) | "
            f"{kid_id}_1.png sRGB min={stats['min_srgb']:.3f} mean={stats['mean_srgb']:.3f} max={stats['max_srgb']:.3f}"
        )

    if args.with_portraits:
        CARDS_DIR.mkdir(parents=True, exist_ok=True)
        FACES_DIR.mkdir(parents=True, exist_ok=True)
        blender_bin = find_blender()
        print(f"[Portraits] Rendering close-up 3/4 golden-hour portraits in {blender_bin.name}...")
        with tempfile.TemporaryDirectory(prefix="kalyeah_10kids_") as tmp:
            tmp_dir = Path(tmp)
            jobs = [
                {
                    "id": s["id"],
                    "glb": str(CHAR_DIR / f"{s['id']}.glb"),
                    "card_clip": s["card_clip"],
                    "card_frame": s["card_frame"],
                    "card_yaw": s["card_yaw"],
                    "card_pitch": s["card_pitch"],
                    "card_dist": s["card_dist"],
                    "head_rot": list(s["head_rot"]),
                    "cam_roll": s["cam_roll"],
                    "key_side": s["key_side"],
                    "card_raw": str(tmp_dir / f"{s['id']}_card_raw.png"),
                }
                for s in KIDS_SPEC
            ]
            manifest_path = tmp_dir / "jobs.json"
            results_path = tmp_dir / "results.json"
            script_path = tmp_dir / "render_jobs.py"
            manifest_path.write_text(json.dumps(jobs, indent=2), encoding="utf-8")
            script_path.write_text(BLENDER_SCRIPT, encoding="utf-8")
            proc = subprocess.run(
                [str(blender_bin), "--background", "--python", str(script_path), "--", str(manifest_path), str(results_path)],
                capture_output=True,
                text=True,
                check=False,
            )
            if proc.returncode != 0 or not results_path.exists():
                sys.stderr.write(proc.stdout[-3000:] + "\n" + proc.stderr[-3000:] + "\n")
                raise RuntimeError(f"Blender render failed with exit code {proc.returncode}")
            render_meta = json.loads(results_path.read_text(encoding="utf-8"))
            for spec in KIDS_SPEC:
                kid_id = spec["id"]
                portrait_dst = PORTRAIT_DIR / f"{kid_id}.png"
                card_dst = CARDS_DIR / f"{kid_id}.webp"
                face_dst = FACES_DIR / f"{kid_id}.png"
                build_portrait_card_and_face(
                    tmp_dir / f"{kid_id}_card_raw.png",
                    portrait_dst,
                    card_dst,
                    face_dst,
                    spec,
                    render_meta[kid_id],
                )
                write_ui_tex_import(portrait_dst, f"res://assets/portraits/{kid_id}.png", compress_mode=1, mipmaps=False)
                write_ui_tex_import(card_dst, f"res://assets/portraits/cards/{kid_id}.webp", compress_mode=0, mipmaps=False)
                write_ui_tex_import(face_dst, f"res://assets/portraits/faces/{kid_id}.png", compress_mode=0, mipmaps=True)

    print("[2/2] Verifying SHA-256 immutability of all 6 authored kids...")
    for path, before_hash in pinned_hashes.items():
        if sha256_file(path) != before_hash:
            raise RuntimeError(f"Integrity violation: {path} was modified!")
    print("Done! Overhaul v2 4 expansion kids (.glb + sunlit _0/_1/_2.png + .import) built and verified.")


if __name__ == "__main__":
    main()
