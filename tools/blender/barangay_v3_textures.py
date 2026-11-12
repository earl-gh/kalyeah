"""Deadlock-grade painterly 2048x2048 Albedo + Tangent-Space Normal Map generator
for Kalyeah's 1990s Philippine Provincial Barangay architectural & prop kits.

Uses real TrueType brand fonts (`KalyeahDisplay-Regular.ttf` & `KalyeahSans-Variable.ttf`)
via Blender's native `FONT` curve -> mesh evaluator + 4x4 supersampled NumPy rasterizer
for crisp weathered wall-painted murals and hand-painted signboards, plus physical
per-texel heightfields converted into OpenGL Y+ tangent-space normal maps.
"""

from __future__ import annotations

import hashlib
import math
from pathlib import Path
from typing import Dict, Tuple

import bpy
import numpy as np

ATLAS_RES = 2048
GRID = 8
CELL = ATLAS_RES // GRID  # 256 px
PAD_PX = 14  # safe interior UV margin inside each cell so mipmaps never bleed

PROJECT_ROOT = Path(__file__).resolve().parents[2]
OUT_DIR = PROJECT_ROOT / "assets" / "models" / "barangay"
DISPLAY_FONT_PATH = PROJECT_ROOT / "assets" / "fonts" / "brand" / "KalyeahDisplay-Regular.ttf"
SANS_FONT_PATH = PROJECT_ROOT / "assets" / "fonts" / "brand" / "KalyeahSans-Variable.ttf"


# ---------------------------------------------------------------------------
# Fast Tileable Painterly Noise & Heightfield Helpers
# ---------------------------------------------------------------------------

def value_noise(u: np.ndarray, v: np.ndarray, fu: int, fv: int, seed: int) -> np.ndarray:
    """Periodic bilinear smoothstep value noise on [0, 1] UV arrays."""
    rng = np.random.default_rng(seed)
    base = rng.random((fv, fu), dtype=np.float32)
    grid = np.pad(base, ((0, 2), (0, 2)), mode="wrap")
    su = np.mod(u, 1.0) * fu
    sv = np.mod(v, 1.0) * fv
    iu = np.floor(su).astype(np.int32) % fu
    iv = np.floor(sv).astype(np.int32) % fv
    du = su - np.floor(su)
    dv = sv - np.floor(sv)
    du = du * du * (3.0 - 2.0 * du)
    dv = dv * dv * (3.0 - 2.0 * dv)
    n00 = grid[iv, iu]
    n10 = grid[iv, iu + 1]
    n01 = grid[iv + 1, iu]
    n11 = grid[iv + 1, iu + 1]
    return (n00 * (1.0 - du) + n10 * du) * (1.0 - dv) + (n01 * (1.0 - du) + n11 * du) * dv


def painterly_fbm(u: np.ndarray, v: np.ndarray, seed: int, fu: int = 6, fv: int = 6) -> np.ndarray:
    """Deadlock-style layered gouache/plaster FBM centered around 0.0 ([-0.5, +0.5])."""
    n1 = value_noise(u, v, fu, fv, seed)
    n2 = value_noise(u, v, fu * 2, fv * 2, seed + 19)
    n3 = value_noise(u, v, fu * 4, fv * 4, seed + 47)
    raw = 0.56 * n1 + 0.29 * n2 + 0.15 * n3
    # Subtle painterly brush quantization blended with smooth gradient (Deadlock look)
    quant = np.round(raw * 8.0) / 8.0
    return (0.72 * raw + 0.28 * quant) - 0.5


def mix_rgb(a: np.ndarray, b, t: np.ndarray) -> np.ndarray:
    b_arr = np.asarray(b, dtype=np.float32)
    t_clamped = np.clip(t, 0.0, 1.0)[..., None]
    return a * (1.0 - t_clamped) + b_arr * t_clamped


# ---------------------------------------------------------------------------
# TrueType Vector-to-NumPy 4x4 Supersampled Anti-Aliased Text Rasterizer
# ---------------------------------------------------------------------------

_FONT_CACHE: Dict[str, bpy.types.VectorFont] = {}


def _get_font(bold_display: bool = True) -> bpy.types.VectorFont:
    key = "display" if bold_display else "sans"
    if key in _FONT_CACHE:
        try:
            _ = _FONT_CACHE[key].name
            return _FONT_CACHE[key]
        except ReferenceError:
            pass
    fpath = DISPLAY_FONT_PATH if bold_display else SANS_FONT_PATH
    if fpath.exists():
        fnt = bpy.data.fonts.load(str(fpath), check_existing=True)
    else:
        fnt = bpy.data.fonts[0]
    _FONT_CACHE[key] = fnt
    return fnt


def render_ttf_mask(
    width_px: int,
    height_px: int,
    text: str,
    x0_frac: float,
    x1_frac: float,
    y0_frac: float,
    y1_frac: float,
    bold_display: bool = True,
    ss: int = 3,
) -> np.ndarray:
    """Rasterizes a UTF-8 string into a `(height_px, width_px)` float32 [0..1] alpha mask
    where row 0 is bottom (`lv=0`) and row `height_px-1` is top (`lv=1`)."""
    mask_hi = np.zeros((height_px * ss, width_px * ss), dtype=np.float32)
    text_clean = text.strip()
    if not text_clean:
        return np.zeros((height_px, width_px), dtype=np.float32)

    cu = bpy.data.curves.new(name="_tmp_txt_cu", type="FONT")
    cu.body = text_clean
    cu.font = _get_font(bold_display=bold_display)
    cu.fill_mode = "FRONT"
    cu.resolution_u = 3
    cu.space_character = 1.03

    ob = bpy.data.objects.new(name="_tmp_txt_ob", object_data=cu)
    bpy.context.scene.collection.objects.link(ob)
    dg = bpy.context.evaluated_depsgraph_get()
    eval_ob = ob.evaluated_get(dg)
    me = bpy.data.meshes.new_from_object(eval_ob)
    me.calc_loop_triangles()

    n_verts = len(me.vertices)
    n_tris = len(me.loop_triangles)
    if n_verts >= 3 and n_tris >= 1:
        co = np.empty(n_verts * 3, dtype=np.float32)
        me.vertices.foreach_get("co", co)
        pts = co.reshape((-1, 3))[:, :2]

        tri_idx = np.empty(n_tris * 3, dtype=np.int32)
        me.loop_triangles.foreach_get("vertices", tri_idx)
        tris = tri_idx.reshape((-1, 3))

        xmin, ymin = pts.min(axis=0)
        xmax, ymax = pts.max(axis=0)
        span_x = max(float(xmax - xmin), 1e-4)
        span_y = max(float(ymax - ymin), 1e-4)

        W = width_px * ss
        H = height_px * ss
        box_w = max((x1_frac - x0_frac) * W, 2.0)
        box_h = max((y1_frac - y0_frac) * H, 2.0)
        # Uniform fit preserving natural glyph proportions (slightly condensed allowed up to 0.72x)
        sx = box_w / span_x
        sy = box_h / span_y
        if sx < sy * 0.68:
            sy = sx / 0.68
        elif sx > sy * 1.18:
            sx = sy * 1.18

        cx_target = 0.5 * (x0_frac + x1_frac) * W
        cy_target = 0.5 * (y0_frac + y1_frac) * H
        px = (pts[:, 0] - 0.5 * (xmin + xmax)) * sx + cx_target
        py = (pts[:, 1] - 0.5 * (ymin + ymax)) * sy + cy_target

        for a_i, b_i, c_i in tris:
            ax, ay = float(px[a_i]), float(py[a_i])
            bx, by = float(px[b_i]), float(py[b_i])
            cx, cy = float(px[c_i]), float(py[c_i])
            ix0 = max(int(math.floor(min(ax, bx, cx))), 0)
            ix1 = min(int(math.ceil(max(ax, bx, cx))), W - 1)
            iy0 = max(int(math.floor(min(ay, by, cy))), 0)
            iy1 = min(int(math.ceil(max(ay, by, cy))), H - 1)
            if ix0 > ix1 or iy0 > iy1:
                continue
            denom = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy)
            if abs(denom) < 1e-6:
                continue
            inv_d = 1.0 / denom
            gx = np.arange(ix0, ix1 + 1, dtype=np.float32)[None, :] + 0.5
            gy = np.arange(iy0, iy1 + 1, dtype=np.float32)[:, None] + 0.5
            w1 = ((by - cy) * (gx - cx) + (cx - bx) * (gy - cy)) * inv_d
            w2 = ((cy - ay) * (gx - cx) + (ax - cx) * (gy - cy)) * inv_d
            w3 = 1.0 - w1 - w2
            inside = (w1 >= -1e-4) & (w2 >= -1e-4) & (w3 >= -1e-4)
            sub = mask_hi[iy0 : iy1 + 1, ix0 : ix1 + 1]
            sub[inside] = 1.0

    bpy.data.meshes.remove(me)
    bpy.context.scene.collection.objects.unlink(ob)
    bpy.data.objects.remove(ob)
    bpy.data.curves.remove(cu)

    return mask_hi.reshape((height_px, ss, width_px, ss)).mean(axis=(1, 3))


# ---------------------------------------------------------------------------
# Atlas Cell Registry Helpers
# ---------------------------------------------------------------------------

_SWATCH_PBR_TABLE: Dict[str, Tuple[float, float]] = {
    # Glass, glossy plastic, polished metal & chrome (`roughness`, `metallic`)
    "jalousie_window": (0.25, 0.06),
    "capiz_window": (0.36, 0.00),
    "windshield_glass": (0.22, 0.08),
    "headlamp_amber": (0.22, 0.08),
    "meter_box": (0.28, 0.14),
    "jeep_chrome": (0.22, 0.78),
    "radiator_grille": (0.30, 0.62),
    "wheel_rim": (0.32, 0.55),
    "zinc_brushed": (0.36, 0.52),
    "trike_galv": (0.38, 0.48),
    "engine_metal": (0.40, 0.56),
    "water_drum_blue": (0.30, 0.00),
    "merienda_jug_org": (0.28, 0.00),
    "tile_teal_chipped": (0.32, 0.00),
    "tile_terracotta": (0.54, 0.00),
    "barber_pole_stripe": (0.30, 0.04),
    # Glossy 90s jeepney/tricycle pressed-steel enamels
    "jeep_yellow": (0.32, 0.18),
    "jeep_red": (0.32, 0.18),
    "jeep_blue": (0.32, 0.18),
    "jeep_green": (0.32, 0.18),
    "trike_blue": (0.34, 0.16),
    "team_blue": (0.44, 0.06),
    "team_red": (0.44, 0.06),
    "hoop_rim_orange": (0.42, 0.35),
    # Corrugated GI (*yero*) & structural steel (matte oxidized tropical metal — zero OpenGL zebra glare)
    "yero_zinc_weathered": (0.64, 0.22),
    "yero_green_faded": (0.70, 0.12),
    "yero_green": (0.68, 0.14),
    "yero_red_oxide": (0.72, 0.12),
    "yero_patchwork": (0.76, 0.10),
    "yero_rust_heavy": (0.82, 0.08),
    "yero_rusted": (0.80, 0.08),
    "rollup_shutter": (0.56, 0.28),
    "steel_dark": (0.52, 0.42),
    "steel_rusted": (0.72, 0.20),
    "chain_net": (0.46, 0.48),
    # Varnished hardwoods, vinyl & matte painterly tropical foliage
    "narra_warm": (0.52, 0.00),
    "door_provincial": (0.54, 0.00),
    "bamboo_pole": (0.50, 0.00),
    "bamboo_gold": (0.50, 0.00),
    "vinyl_seat": (0.44, 0.00),
    "leaf_banana": (0.70, 0.00),
    "leaf_banana_rib": (0.68, 0.00),
    "leaf_palm_frond": (0.72, 0.00),
    "leaf_mango_sun": (0.80, 0.00),
    "leaf_mango_mid": (0.84, 0.00),
    "leaf_mango_deep": (0.88, 0.00),
    "pseudostem_ban": (0.48, 0.00),
    "fruit_mango_gold": (0.42, 0.00),
    "fruit_buko_green": (0.46, 0.00),
    "puso_crimson": (0.52, 0.00),
    "bougainvillea": (0.62, 0.00),
}


class AtlasCanvas:
    """2048x2048 painterly Albedo + Height/Normal + PBR Roughness/Metallic builder."""

    def __init__(self) -> None:
        self.albedo = np.full((ATLAS_RES, ATLAS_RES, 3), 0.50, dtype=np.float32)
        self.height = np.zeros((ATLAS_RES, ATLAS_RES), dtype=np.float32)
        self.roughness = np.full((ATLAS_RES, ATLAS_RES), 0.88, dtype=np.float32)
        self.metallic = np.zeros((ATLAS_RES, ATLAS_RES), dtype=np.float32)
        self.uv_rects: Dict[str, Tuple[float, float, float, float]] = {}
        self._active_name: str = ""

    def register(
        self,
        name: str,
        row: int,
        col: int,
        span_cols: int = 1,
        pad_px: int = PAD_PX,
    ) -> Tuple[int, int, int, int]:
        """Registers a swatch at `(row, col)` (row 0 = top, row 7 = bottom) and returns pixel slice `(x0, x1, y0, y1)`."""
        self._active_name = name
        x0 = col * CELL
        x1 = (col + span_cols) * CELL
        y0 = (GRID - 1 - row) * CELL
        y1 = y0 + CELL
        u0 = (x0 + pad_px) / ATLAS_RES
        u1 = (x1 - pad_px) / ATLAS_RES
        v0 = (y0 + pad_px) / ATLAS_RES
        v1 = (y1 - pad_px) / ATLAS_RES
        self.uv_rects[name] = (u0, v0, u1, v1)
        return x0, x1, y0, y1

    def cell_grids(self, span_cols: int = 1) -> Tuple[np.ndarray, np.ndarray]:
        w = CELL * span_cols
        h = CELL
        lu = (np.arange(w, dtype=np.float32) + 0.5) / float(w)
        lv = (np.arange(h, dtype=np.float32) + 0.5) / float(h)
        return np.meshgrid(lu, lv)

    def commit_patch(
        self,
        x0: int,
        x1: int,
        y0: int,
        y1: int,
        rgb: np.ndarray,
        hgt: np.ndarray,
        roughness: float | None = None,
        metallic: float | None = None,
    ) -> None:
        # Clamp strictly into sunlit Deadlock daytime sRGB budget (zero harsh blacks, zero white blowout)
        rgb_safe = np.clip(rgb, 0.13, 0.76).astype(np.float32)
        hgt_safe = np.clip(hgt, -1.5, 1.5).astype(np.float32)
        self.albedo[y0:y1, x0:x1, :] = rgb_safe
        self.height[y0:y1, x0:x1] = hgt_safe

        default_r, default_m = _SWATCH_PBR_TABLE.get(self._active_name, (0.88, 0.00))
        if self._active_name.startswith("sign_") or self._active_name.startswith("poster_"):
            default_r, default_m = (0.64, 0.02)
        base_r = default_r if roughness is None else roughness
        base_m = default_m if metallic is None else metallic
        # Micro-surface roughness modulation from physical height crevices & paint grain
        r_patch = np.clip(base_r - hgt_safe * 0.055, 0.18, 0.96).astype(np.float32)
        m_patch = np.full_like(hgt_safe, np.clip(base_m, 0.0, 0.90), dtype=np.float32)
        self.roughness[y0:y1, x0:x1] = r_patch
        self.metallic[y0:y1, x0:x1] = m_patch

    def build_normal_rgb(self, strength: float = 2.6) -> np.ndarray:
        """Converts `self.height` into an OpenGL Y+ tangent-space normal map in [0..1] RGB."""
        h = self.height
        # Central differences inside each 256px cell so cell boundaries never create false ridges
        dx = np.zeros_like(h)
        dy = np.zeros_like(h)
        dx[:, 1:-1] = (h[:, 2:] - h[:, :-2]) * 0.5
        dy[1:-1, :] = (h[2:, :] - h[:-2, :]) * 0.5
        # Zero gradients right on 256px cell seams
        for k in range(1, GRID):
            s = k * CELL
            dx[:, max(s - 1, 0) : min(s + 2, ATLAS_RES)] *= 0.15
            dy[max(s - 1, 0) : min(s + 2, ATLAS_RES), :] *= 0.15

        nx = -dx * strength
        ny = -dy * strength
        nz = np.ones_like(h)
        inv_len = 1.0 / np.sqrt(nx * nx + ny * ny + nz * nz)
        n_rgb = np.stack([
            (nx * inv_len) * 0.5 + 0.5,
            (ny * inv_len) * 0.5 + 0.5,
            (nz * inv_len) * 0.5 + 0.5,
        ], axis=-1)
        return np.clip(n_rgb, 0.0, 1.0).astype(np.float32)

    def build_orm_rgb(self) -> np.ndarray:
        """Builds glTF 2.0 channel-packed ORM texture: `R = Occlusion (1.0)`, `G = Roughness`, `B = Metallic`."""
        ao = np.clip(0.94 + self.height * 0.04, 0.82, 1.00).astype(np.float32)
        return np.stack([ao, self.roughness, self.metallic], axis=-1)

    def save_pair(self, albedo_jpg: Path, normal_png: Path, orm_png: Path | None = None) -> None:
        OUT_DIR.mkdir(parents=True, exist_ok=True)
        self._save_blender_image(self.albedo, albedo_jpg, fmt="JPEG", quality=93, non_color=False)
        write_texture_import_sidecar(albedo_jpg, is_normal=False)
        n_rgb = self.build_normal_rgb(strength=2.8)
        self._save_blender_image(n_rgb, normal_png, fmt="PNG", quality=90, non_color=True)
        write_texture_import_sidecar(normal_png, is_normal=True)
        if orm_png is not None:
            orm_rgb = self.build_orm_rgb()
            self._save_blender_image(orm_rgb, orm_png, fmt="PNG", quality=90, non_color=True)
            write_texture_import_sidecar(orm_png, is_normal=False)

    @staticmethod
    def _save_blender_image(
        rgb: np.ndarray,
        out_path: Path,
        fmt: str,
        quality: int,
        non_color: bool,
    ) -> None:
        h, w, _ = rgb.shape
        rgba = np.ones((h, w, 4), dtype=np.float32)
        rgba[:, :, :3] = rgb
        img = bpy.data.images.new("_tmp_atlas_save", width=w, height=h, alpha=False)
        img.colorspace_settings.name = "Non-Color" if non_color else "sRGB"
        img.pixels.foreach_set(rgba.ravel())
        img.filepath_raw = str(out_path)
        img.file_format = fmt
        scene = bpy.context.scene
        prev_fmt = scene.render.image_settings.file_format
        prev_qual = scene.render.image_settings.quality
        prev_cm = scene.view_settings.view_transform
        scene.render.image_settings.file_format = fmt
        scene.render.image_settings.quality = quality
        img.save()
        scene.render.image_settings.file_format = prev_fmt
        scene.render.image_settings.quality = prev_qual
        scene.view_settings.view_transform = prev_cm
        bpy.data.images.remove(img)


def write_texture_import_sidecar(tex_path: Path, is_normal: bool = True) -> None:
    """Writes a clean VRAM-compressed Godot 4.6.2 `.import` sidecar (`compress/mode=2`, zero `md5=`)."""
    res_path = f"res://assets/models/barangay/{tex_path.name}"
    h_md5 = hashlib.md5(res_path.encode("utf-8")).hexdigest()
    uid_seed = hashlib.sha1(res_path.encode("utf-8")).hexdigest()[:13]
    alphabet = "0123456789abcdefghijklmnopqrstuvwxyz"
    val = int(uid_seed, 16)
    uid_chars = []
    for _ in range(13):
        uid_chars.append(alphabet[val % 36])
        val //= 36
    uid_str = "uid://" + "".join(uid_chars)

    norm_flag = 1 if is_normal else 0
    sidecar = tex_path.with_name(tex_path.name + ".import")
    if sidecar.exists():
        raw = sidecar.read_text(encoding="utf-8")
        if "[params]" in raw:
            header_part = raw.split("[params]")[0]
            params_part = f"""[params]

compress/mode=2
compress/high_quality=false
compress/lossy_quality=0.7
compress/uastc_level=0
compress/rdo_quality_loss=0.0
compress/hdr_compression=1
compress/normal_map={norm_flag}
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
            sidecar.write_text(header_part + params_part, encoding="utf-8", newline="\n")
            return
    content = f"""[remap]

importer="texture"
type="CompressedTexture2D"
uid="{uid_str}"
path.s3tc="res://.godot/imported/{tex_path.name}-{h_md5}.s3tc.ctex"
path.etc2="res://.godot/imported/{tex_path.name}-{h_md5}.etc2.ctex"
metadata={{
"imported_formats": ["s3tc_bptc", "etc2_astc"],
"vram_texture": true
}}

[deps]

source_file="{res_path}"
dest_files=["res://.godot/imported/{tex_path.name}-{h_md5}.s3tc.ctex", "res://.godot/imported/{tex_path.name}-{h_md5}.etc2.ctex"]

[params]

compress/mode=2
compress/high_quality=false
compress/lossy_quality=0.7
compress/uastc_level=0
compress/rdo_quality_loss=0.0
compress/hdr_compression=1
compress/normal_map={norm_flag}
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
    sidecar.write_text(content, encoding="utf-8", newline="\n")


# ---------------------------------------------------------------------------
# Reusable Painterly Surface Material Generators (Albedo + Physical Height)
# ---------------------------------------------------------------------------

def mat_stucco_wall(
    lu: np.ndarray,
    lv: np.ndarray,
    base_rgb: Tuple[float, float, float],
    seed: int,
    moss: float = 0.45,
    sill_streaks: float = 0.50,
    patch_chb: float = 0.18,
) -> Tuple[np.ndarray, np.ndarray]:
    """Deadlock-style weathered tropical painted stucco with trowel relief, rain streaks & damp moss base."""
    b = painterly_fbm(lu, lv, seed, 6, 6)
    fine = value_noise(lu, lv, 24, 24, seed + 101) - 0.5
    col = np.empty(lu.shape + (3,), dtype=np.float32)
    col[:] = base_rgb
    col += b[..., None] * 0.09 + fine[..., None] * 0.035

    # Warm top sunlight vs cool lower bounce gradient
    col += ((lv - 0.45) * 0.075)[..., None] * np.array([1.0, 0.92, 0.76], dtype=np.float32)

    # Top-down rain-streak stains below sills/cornices
    if sill_streaks > 0.01:
        streak_n = value_noise(lu, lv * 0.25, 22, 4, seed + 303)
        streak = np.clip((1.0 - lv) * 0.45 + (lv > 0.62) * (lv - 0.62) * 1.9, 0.0, 1.0) * (streak_n > 0.54)
        drip = np.clip((streak_n - 0.50) * 2.4, 0.0, 1.0) * np.exp(-((1.0 - lv) * 3.2)) * sill_streaks
        col = mix_rgb(col, (base_rgb[0] * 0.72, base_rgb[1] * 0.71, base_rgb[2] * 0.68), drip * 0.65 + streak * 0.14)

    hgt = b * 0.32 + fine * 0.18

    # Subtle patch-exposed CHB / plaster peel pocket
    if patch_chb > 0.01:
        peel_field = painterly_fbm(lu, lv, seed + 509, 4, 4) + (0.32 - lv) * 0.35
        peel = np.clip((peel_field - (0.34 - patch_chb * 0.35)) * 9.0, 0.0, 1.0)
        gy = (lv * 5.0) % 1.0
        stag = 0.5 * (np.floor(lv * 5.0) % 2.0)
        gx = (lu * 3.0 + stag) % 1.0
        mortar = ((gx < 0.07) | (gy < 0.10)).astype(np.float32)
        chb_col = np.array([0.46, 0.45, 0.43], dtype=np.float32) * (1.0 - mortar[..., None] * 0.18)
        col = mix_rgb(col, chb_col, peel * 0.85)
        hgt -= peel * (0.38 + mortar * 0.32)

    # Damp green-olive tropical moss along bottom plinth contact
    if moss > 0.01:
        mn = value_noise(lu, lv, 12, 10, seed + 709)
        moss_mask = np.clip((0.26 + 0.16 * mn - lv) * 5.5, 0.0, 1.0) * moss
        col = mix_rgb(col, (0.29, 0.39, 0.23), moss_mask * 0.78)
        hgt += moss_mask * 0.14

    return col, hgt


def mat_chb_firewall(
    lu: np.ndarray,
    lv: np.ndarray,
    base_rgb: Tuple[float, float, float],
    seed: int,
    plaster_patch: float = 0.42,
    moss: float = 0.55,
) -> Tuple[np.ndarray, np.ndarray]:
    """Unpainted & trowel-patched Concrete Hollow Block (CHB) side firewall with deep mortar courses."""
    courses_v = 6.0
    courses_u = 3.0
    row_id = np.floor(lv * courses_v)
    gy = (lv * courses_v) % 1.0
    gx = (lu * courses_u + 0.5 * (row_id % 2.0)) % 1.0
    blk_id = row_id * 7.0 + np.floor(lu * courses_u + 0.5 * (row_id % 2.0))
    blk_var = (np.sin(blk_id * 12.9898 + seed) * 43758.5453) % 1.0 - 0.5

    mortar_h = np.exp(-((gy - 0.05) / 0.038) ** 2) + np.exp(-((gy - 0.98) / 0.038) ** 2)
    mortar_v = np.exp(-((gx - 0.04) / 0.032) ** 2)
    mortar = np.clip(np.maximum(mortar_h, mortar_v), 0.0, 1.0)

    grain = value_noise(lu, lv, 32, 32, seed + 31) - 0.5
    b = painterly_fbm(lu, lv, seed, 6, 6)
    col = np.empty(lu.shape + (3,), dtype=np.float32)
    col[:] = base_rgb
    col += (blk_var * 0.055 + b * 0.07 + grain * 0.045)[..., None]
    # Mortar squeeze-out & shadow line
    col = mix_rgb(col, (base_rgb[0] * 0.74, base_rgb[1] * 0.74, base_rgb[2] * 0.72), mortar * 0.72)
    hgt = -mortar * 0.68 + grain * 0.24 + blk_var * 0.10

    if plaster_patch > 0.01:
        p_noise = painterly_fbm(lu, lv, seed + 177, 4, 4)
        patch = np.clip((p_noise + plaster_patch - 0.32) * 7.5, 0.0, 1.0)
        plaster_col = np.array([0.57, 0.55, 0.50], dtype=np.float32) + b[..., None] * 0.05
        col = mix_rgb(col, plaster_col, patch * 0.88)
        hgt = hgt * (1.0 - patch * 0.85) + patch * (0.28 + b * 0.14)

    if moss > 0.01:
        mn = value_noise(lu, lv, 10, 10, seed + 401)
        mm = np.clip((0.28 + 0.15 * mn - lv) * 5.0, 0.0, 1.0) * moss
        col = mix_rgb(col, (0.28, 0.38, 0.22), mm * 0.76)

    return col, hgt


def mat_corrugated_yero(
    lu: np.ndarray,
    lv: np.ndarray,
    base_rgb: Tuple[float, float, float],
    seed: int,
    rust_amt: float = 0.45,
    ribs: int = 28,
    patchwork: bool = False,
) -> Tuple[np.ndarray, np.ndarray]:
    """Fine-pitch corrugated GI (*yero*) roofing with subtle matte normal ribs, sheet seams & oxidized rust."""
    phase = lu * ribs * (2.0 * math.pi)
    wave = np.sin(phase)
    b = painterly_fbm(lu, lv, seed, 8, 6)
    sheet_col_id = np.floor(lu * 3.0) + np.floor(lv * 2.0) * 3.0
    sheet_var = (((np.sin(sheet_col_id * 17.13 + seed) * 31741.9) % 1.0) - 0.5) * 0.055
    col = np.empty(lu.shape + (3,), dtype=np.float32)
    col[:] = base_rgb
    if patchwork:
        q1 = (lu > 0.48) & (lv > 0.46)
        q2 = (lu < 0.52) & (lv < 0.38)
        col[q1] = (0.53, 0.54, 0.55)
        col[q2] = (0.48, 0.30, 0.21)
    # Fine ridge/valley shading + painterly sheet variation (avoids wide zebra stripes in gl_compatibility)
    col += (wave * 0.030 + b * 0.065 + sheet_var)[..., None]
    hgt = wave * 0.26 + b * 0.14

    # Horizontal & vertical GI sheet overlap seams
    seam_h = np.exp(-((lv - 0.50) / 0.016) ** 2)
    seam_v = np.exp(-((lu - 0.333) / 0.012) ** 2) + np.exp(-((lu - 0.667) / 0.012) ** 2)
    seam = np.clip(seam_h + seam_v * 0.65, 0.0, 1.0)
    col = mix_rgb(col, (0.26, 0.21, 0.19), seam * 0.52)
    hgt += (lv > 0.50).astype(np.float32) * 0.12

    # Oxidized sienna/umber rust streaks collecting along eaves, valleys, and lap seams
    rn = painterly_fbm(lu, lv, seed + 89, 10, 8) + 0.5
    eave_damp = np.clip((0.28 - lv) * 1.4, 0.0, 0.35)
    valley_bias = np.clip(-wave * 0.20 + seam * 0.48 + eave_damp, -0.15, 0.8)
    rust_mask = np.clip((rn + valley_bias - (1.02 - rust_amt * 0.82)) * 4.0, 0.0, 1.0)
    col = mix_rgb(col, (0.46, 0.26, 0.17), rust_mask * 0.80)
    hgt -= rust_mask * 0.10
    return col, hgt


def mat_hardwood(
    lu: np.ndarray,
    lv: np.ndarray,
    base_rgb: Tuple[float, float, float],
    seed: int,
    planks: int = 4,
    vertical: bool = False,
) -> Tuple[np.ndarray, np.ndarray]:
    """Warm Philippine *narra* / *mahogany* / *coco-lumber* planks with grain & beveled joints."""
    across = lu if vertical else lv
    along = lv if vertical else lu
    pid = np.floor(across * planks)
    pf = (across * planks) % 1.0
    p_var = ((np.sin(pid * 19.31 + seed) * 14731.7) % 1.0) - 0.5
    grain = painterly_fbm(
        along * 0.5 if not vertical else lu * 3.0,
        across * 3.0 if not vertical else lv * 0.5,
        seed + 23,
        4 if not vertical else 18,
        18 if not vertical else 4,
    )
    groove = np.exp(-((pf - 0.03) / 0.028) ** 2) + np.exp(-((pf - 0.98) / 0.028) ** 2)
    col = np.empty(lu.shape + (3,), dtype=np.float32)
    col[:] = base_rgb
    col += (p_var * 0.055 + grain * 0.085)[..., None]
    col = mix_rgb(col, (base_rgb[0] * 0.58, base_rgb[1] * 0.54, base_rgb[2] * 0.50), np.clip(groove, 0.0, 1.0) * 0.78)
    hgt = -np.clip(groove, 0.0, 1.0) * 0.65 + grain * 0.25 + p_var * 0.08
    return col, hgt


def mat_capiz_window(lu: np.ndarray, lv: np.ndarray, seed: int) -> Tuple[np.ndarray, np.ndarray]:
    """Traditional Filipino sliding *capiz* mother-of-pearl lattice sash in dark *narra* frame."""
    border = (lu < 0.09) | (lu > 0.91) | (lv < 0.09) | (lv > 0.91) | (np.abs(lu - 0.50) < 0.038) | (np.abs(lv - 0.50) < 0.035)
    gx = (lu * 7.0) % 1.0
    gy = (lv * 9.0) % 1.0
    muntin = (gx < 0.13) | (gy < 0.13)
    shimmer = painterly_fbm(lu, lv, seed, 14, 14)
    pearl = np.empty(lu.shape + (3,), dtype=np.float32)
    pearl[:] = (0.64, 0.61, 0.52)
    pearl += shimmer[..., None] * np.array([0.08, 0.07, 0.04], dtype=np.float32)
    # Warm interior amber glow hint in lower panes
    pearl += ((1.0 - lv) * 0.05)[..., None] * np.array([0.10, 0.05, -0.02], dtype=np.float32)
    wood = np.array([0.33, 0.22, 0.15], dtype=np.float32)
    col = mix_rgb(pearl, (0.38, 0.26, 0.18), muntin.astype(np.float32) * 0.88)
    col = mix_rgb(col, wood, border.astype(np.float32) * 0.95)
    hgt = border.astype(np.float32) * 0.70 + muntin.astype(np.float32) * 0.35 + shimmer * 0.10
    return col, hgt


def mat_jalousie_window(lu: np.ndarray, lv: np.ndarray, seed: int) -> Tuple[np.ndarray, np.ndarray]:
    """90s glass *jalousie* louver blades with aluminum side clips & dark interior depth."""
    frame = (lu < 0.08) | (lu > 0.92) | (lv < 0.07) | (lv > 0.93) | (np.abs(lu - 0.50) < 0.03)
    slat_f = (lv * 13.0) % 1.0
    b = painterly_fbm(lu, lv, seed, 8, 8)
    glass = np.empty(lu.shape + (3,), dtype=np.float32)
    glass[:] = (0.31, 0.43, 0.48)
    # Sky reflection gradient across each angled glass louver blade
    glass += ((slat_f - 0.35) * 0.22)[..., None] * np.array([0.85, 0.95, 1.0], dtype=np.float32) + b[..., None] * 0.04
    edge_shadow = (slat_f < 0.14).astype(np.float32)
    glass = mix_rgb(glass, (0.18, 0.23, 0.26), edge_shadow * 0.80)
    alu_clip = ((lu > 0.08) & (lu < 0.12)) | ((lu > 0.88) & (lu < 0.92))
    col = mix_rgb(glass, (0.58, 0.60, 0.61), alu_clip.astype(np.float32) * 0.85)
    col = mix_rgb(col, (0.36, 0.25, 0.17), frame.astype(np.float32) * 0.94)
    hgt = (slat_f - 0.5) * 0.55 + frame.astype(np.float32) * 0.65
    return col, hgt


def mat_mural_signboard(
    canvas: AtlasCanvas,
    x0: int,
    x1: int,
    y0: int,
    y1: int,
    bg_rgb: Tuple[float, float, float],
    line1: str,
    line2: str,
    fg1: Tuple[float, float, float],
    fg2: Tuple[float, float, float],
    header_rgb: Tuple[float, float, float] | None = None,
    mural_on_plaster: bool = False,
    seed: int = 42,
) -> None:
    """Paints a crisp wide (`512x256` or `256x256`) weathered 90s signboard or wall-painted mural."""
    w = x1 - x0
    h = y1 - y0
    span_cols = max(w // CELL, 1)
    lu, lv = canvas.cell_grids(span_cols=span_cols)

    if mural_on_plaster:
        col, hgt = mat_stucco_wall(lu, lv, bg_rgb, seed=seed, moss=0.14, sill_streaks=0.28, patch_chb=0.08)
        # Crisp painted mural panel & border band on plaster
        in_band = (lu > 0.045) & (lu < 0.955) & (lv > 0.10) & (lv < 0.90)
        border_line = in_band & ((lu < 0.070) | (lu > 0.930) | (lv < 0.14) | (lv > 0.86))
        wear = np.clip(painterly_fbm(lu, lv, seed + 61, 10, 8) + 0.80, 0.55, 0.96)
        inner_body = (lu >= 0.070) & (lu <= 0.930) & (lv >= 0.14) & (lv <= 0.50)
        col = mix_rgb(col, bg_rgb, inner_body.astype(np.float32) * wear * 0.78)
        if header_rgb is not None:
            hdr_zone = (lu >= 0.070) & (lu <= 0.930) & (lv > 0.50) & (lv <= 0.86)
            col = mix_rgb(col, header_rgb, hdr_zone.astype(np.float32) * wear * 0.92)
        col = mix_rgb(col, fg1, border_line.astype(np.float32) * wear * 0.82)
    else:
        col, hgt = mat_hardwood(lu, lv, bg_rgb, seed=seed, planks=2, vertical=False)
        wear = np.clip(painterly_fbm(lu, lv, seed + 73, 12, 8) + 0.84, 0.60, 0.98)
        if header_rgb is not None:
            hdr = (lv > 0.50) & (lv < 0.91) & (lu > 0.045) & (lu < 0.955)
            col = mix_rgb(col, header_rgb, hdr.astype(np.float32) * wear * 0.95)
        # Angle-iron / painted wood signboard molding frame
        frm = (lu < 0.045) | (lu > 0.955) | (lv < 0.07) | (lv > 0.93)
        col = mix_rgb(col, (0.30, 0.22, 0.17), frm.astype(np.float32) * 0.94)
        hgt = hgt * 0.42 + frm.astype(np.float32) * 0.65

    # Render crisp anti-aliased KalyeahDisplay typography for both primary & secondary lines!
    m1 = render_ttf_mask(w, h, line1, 0.09, 0.91, 0.55 if line2 else 0.25, 0.83 if line2 else 0.78, bold_display=True)
    # Crisp painted drop-shadow down-right
    m1_sh = np.zeros_like(m1)
    m1_sh[:-3, 3:] = m1[3:, :-3]
    paint_wear = np.clip(0.92 + painterly_fbm(lu, lv, seed + 131, 14, 10) * 0.22, 0.68, 1.0)
    if mural_on_plaster:
        paint_wear *= 0.95

    col = mix_rgb(col, (0.14, 0.14, 0.15), m1_sh * 0.62 * paint_wear)
    col = mix_rgb(col, fg1, m1 * paint_wear)
    hgt += m1 * 0.16 * paint_wear

    if line2:
        m2 = render_ttf_mask(w, h, line2, 0.10, 0.90, 0.17, 0.43, bold_display=True)
        m2_sh = np.zeros_like(m2)
        m2_sh[:-2, 2:] = m2[2:, :-2]
        col = mix_rgb(col, (0.14, 0.14, 0.15), m2_sh * 0.48 * paint_wear)
        col = mix_rgb(col, fg2, m2 * 0.98 * paint_wear)
        hgt += m2 * 0.12 * paint_wear

    canvas.commit_patch(x0, x1, y0, y1, col, hgt)


# ---------------------------------------------------------------------------
# 1. Barangay Architectural & Street Kit Atlas (`BarangayAtlas.jpg` + `_normal.png`)
# ---------------------------------------------------------------------------

def generate_barangay_atlas() -> Dict[str, Tuple[float, float, float, float]]:
    """Generates `BarangayAtlas.jpg` + `BarangayAtlas_normal.png` and returns `name -> (u0, v0, u1, v1)`."""
    cv = AtlasCanvas()
    lu1, lv1 = cv.cell_grids(1)

    # --- ROW 0: Sunlit Deadlock-Grade Tropical Painted Stucco & CHB Firewalls ---
    stucco_specs = [
        ("chb_raw_moss",       0, 0, "chb",    (0.50, 0.50, 0.47), 0.48, 0.58),
        ("chb_cream_moss",     0, 1, "stucco", (0.65, 0.60, 0.49), 0.42, 0.22),
        ("chb_salmon_moss",    0, 2, "stucco", (0.63, 0.44, 0.35), 0.44, 0.20),
        ("chb_mint_moss",      0, 3, "stucco", (0.41, 0.57, 0.52), 0.42, 0.18),
        ("chb_ochre_moss",     0, 4, "stucco", (0.65, 0.54, 0.34), 0.40, 0.20),
        ("chb_sky_moss",       0, 5, "stucco", (0.42, 0.54, 0.62), 0.40, 0.18),
        ("stucco_sandstone",   0, 6, "stucco", (0.62, 0.56, 0.46), 0.15, 0.08),
        ("chb_plaster_cracked",0, 7, "chb",    (0.53, 0.51, 0.47), 0.62, 0.35),
    ]
    for idx, (nm, r, c, kind, rgb, p1, p2) in enumerate(stucco_specs):
        x0, x1, y0, y1 = cv.register(nm, r, c)
        if kind == "chb":
            c_arr, h_arr = mat_chb_firewall(lu1, lv1, rgb, seed=100 + idx * 17, plaster_patch=p1, moss=p2)
        else:
            c_arr, h_arr = mat_stucco_wall(lu1, lv1, rgb, seed=100 + idx * 17, moss=p1, sill_streaks=0.52, patch_chb=p2)
        cv.commit_patch(x0, x1, y0, y1, c_arr, h_arr)

    # --- ROW 1: Molded Concrete Plinths/Cornices & Corrugated GI (*Yero*) Roofs ---
    # (1,0) concrete_moss plinth, (1,1) concrete_pillar/cornice, (1,2) stone_adobe_base
    x0, x1, y0, y1 = cv.register("concrete_moss", 1, 0)
    c_arr, h_arr = mat_stucco_wall(lu1, lv1, (0.49, 0.50, 0.47), seed=211, moss=0.68, sill_streaks=0.45, patch_chb=0.05)
    cv.commit_patch(x0, x1, y0, y1, c_arr, h_arr)

    x0, x1, y0, y1 = cv.register("concrete_pillar", 1, 1)
    c_arr, h_arr = mat_stucco_wall(lu1, lv1, (0.60, 0.58, 0.53), seed=223, moss=0.08, sill_streaks=0.28, patch_chb=0.0)
    # Subtle horizontal cornice molding flutes
    flute = np.exp(-((lv1 - 0.82) / 0.03) ** 2) + np.exp(-((lv1 - 0.18) / 0.03) ** 2)
    c_arr = mix_rgb(c_arr, (0.46, 0.44, 0.40), flute * 0.45)
    h_arr += flute * 0.45
    cv.commit_patch(x0, x1, y0, y1, c_arr, h_arr)

    x0, x1, y0, y1 = cv.register("adobe_stone", 1, 2)
    c_arr, h_arr = mat_chb_firewall(lu1, lv1, (0.48, 0.44, 0.38), seed=239, plaster_patch=0.08, moss=0.48)
    cv.commit_patch(x0, x1, y0, y1, c_arr, h_arr)

    yero_specs = [
        ("yero_rust_heavy",     1, 3, (0.52, 0.31, 0.21), 0.78, False),
        ("yero_patchwork",      1, 4, (0.54, 0.44, 0.36), 0.55, True),
        ("yero_red_oxide",      1, 5, (0.57, 0.29, 0.23), 0.40, False),
        ("yero_zinc_weathered", 1, 6, (0.58, 0.59, 0.60), 0.30, False),
        ("yero_green_faded",    1, 7, (0.34, 0.51, 0.37), 0.32, False),
    ]
    for idx, (nm, r, c, rgb, rust, pw) in enumerate(yero_specs):
        x0, x1, y0, y1 = cv.register(nm, r, c)
        c_arr, h_arr = mat_corrugated_yero(lu1, lv1, rgb, seed=260 + idx * 19, rust_amt=rust, ribs=28, patchwork=pw)
        cv.commit_patch(x0, x1, y0, y1, c_arr, h_arr)

    # --- ROW 2: Philippine Hardwoods (*Narra*, *Mahogany*, *Coco*), Sawali, Bamboo & Panaderya Sign ---
    x0, x1, y0, y1 = cv.register("sawali_honey", 2, 0)
    b = painterly_fbm(lu1, lv1, 301, 10, 10)
    herring = (((np.floor(lu1 * 12.0) + np.floor(lv1 * 12.0)) % 2.0) - 0.5)
    c_arr = np.full(lu1.shape + (3,), (0.60, 0.49, 0.31), dtype=np.float32) + (herring * 0.07 + b * 0.05)[..., None]
    h_arr = herring * 0.42 + b * 0.12
    cv.commit_patch(x0, x1, y0, y1, c_arr, h_arr)

    x0, x1, y0, y1 = cv.register("sign_panaderya", 2, 1, span_cols=2)
    mat_mural_signboard(
        cv, x0, x1, y0, y1,
        bg_rgb=(0.66, 0.57, 0.36),
        line1="PANADERYA • PANDESAL",
        line2="MAINIT PA • MONAY • ENSAYMADA",
        fg1=(0.68, 0.65, 0.54),
        fg2=(0.22, 0.18, 0.16),
        header_rgb=(0.61, 0.23, 0.19),
        mural_on_plaster=False,
        seed=311,
    )

    wood_specs = [
        ("narra_warm",        2, 3, (0.53, 0.34, 0.21), 4, False),
        ("wood_coco",         2, 4, (0.50, 0.36, 0.24), 4, False),
        ("wood_dark",         2, 5, (0.35, 0.24, 0.17), 3, False),
        ("planks_vertical",   2, 6, (0.47, 0.34, 0.23), 5, True),
        ("bamboo_pole",       2, 7, (0.56, 0.51, 0.30), 3, False),
    ]
    for idx, (nm, r, c, rgb, plk, vert) in enumerate(wood_specs):
        x0, x1, y0, y1 = cv.register(nm, r, c)
        c_arr, h_arr = mat_hardwood(lu1, lv1, rgb, seed=320 + idx * 13, planks=plk, vertical=vert)
        if nm == "bamboo_pole":
            node = np.exp(-(((lv1 * 3.0) % 1.0 - 0.5) / 0.05) ** 2)
            c_arr = mix_rgb(c_arr, (0.38, 0.33, 0.19), node * 0.65)
            h_arr += node * 0.55
        cv.commit_patch(x0, x1, y0, y1, c_arr, h_arr)
    # Alias legacy keys
    cv.uv_rects["sawali_aged"] = cv.uv_rects["sawali_honey"]
    cv.uv_rects["plywood_weathered"] = cv.uv_rects["wood_coco"]
    cv.uv_rects["plywood_teal_peel"] = cv.uv_rects["narra_warm"]
    cv.uv_rects["planks_shack"] = cv.uv_rects["planks_vertical"]

    # --- ROW 3: Capiz, Jalousie, Wrought-Iron Rehas, Doors, Barrio Tiles, Barber Pole, Roll-Up Shutter ---
    x0, x1, y0, y1 = cv.register("jalousie_window", 3, 0)
    cv.commit_patch(x0, x1, y0, y1, *mat_jalousie_window(lu1, lv1, 401))

    x0, x1, y0, y1 = cv.register("capiz_window", 3, 1)
    cv.commit_patch(x0, x1, y0, y1, *mat_capiz_window(lu1, lv1, 409))

    x0, x1, y0, y1 = cv.register("sari_grill_counter", 3, 2)
    # Dark interior storefront + colorful snack jars + iron security grille bars
    bg = np.full(lu1.shape + (3,), (0.24, 0.21, 0.20), dtype=np.float32) + painterly_fbm(lu1, lv1, 419, 8, 8)[..., None] * 0.05
    jars = (lv1 > 0.14) & (lv1 < 0.46)
    jar_hue = (np.floor(lu1 * 6.0) % 3.0)
    bg[jars & (jar_hue == 0)] = (0.62, 0.32, 0.26)
    bg[jars & (jar_hue == 1)] = (0.64, 0.54, 0.24)
    bg[jars & (jar_hue == 2)] = (0.30, 0.52, 0.56)
    bars = (((lu1 * 9.0) % 1.0) < 0.15) | (((lv1 * 4.0) % 1.0) < 0.10)
    c_arr = mix_rgb(bg, (0.42, 0.44, 0.46), bars.astype(np.float32) * 0.90)
    h_arr = bars.astype(np.float32) * 0.72
    cv.commit_patch(x0, x1, y0, y1, c_arr, h_arr)
    cv.uv_rects["rehas_grill"] = cv.uv_rects["sari_grill_counter"]

    x0, x1, y0, y1 = cv.register("door_provincial", 3, 3)
    c_arr, h_arr = mat_hardwood(lu1, lv1, (0.46, 0.31, 0.20), seed=431, planks=4, vertical=True)
    panel = ((lu1 > 0.18) & (lu1 < 0.82) & (lv1 > 0.54) & (lv1 < 0.86)) | ((lu1 > 0.18) & (lu1 < 0.82) & (lv1 > 0.14) & (lv1 < 0.44))
    panel_rim = panel & ((lu1 < 0.23) | (lu1 > 0.77) | (np.abs(lv1 - 0.54) < 0.03) | (np.abs(lv1 - 0.86) < 0.03) | (np.abs(lv1 - 0.14) < 0.03) | (np.abs(lv1 - 0.44) < 0.03))
    c_arr = mix_rgb(c_arr, (0.31, 0.20, 0.13), panel_rim.astype(np.float32) * 0.75)
    h_arr -= panel_rim.astype(np.float32) * 0.55
    cv.commit_patch(x0, x1, y0, y1, c_arr, h_arr)

    for idx, (nm, c, base_t, grd) in enumerate([
        ("tile_teal_chipped", 4, (0.34, 0.56, 0.54), 6),
        ("tile_terracotta",   5, (0.60, 0.38, 0.28), 5),
    ]):
        x0, x1, y0, y1 = cv.register(nm, 3, c)
        gx = (lu1 * grd) % 1.0
        gy = (lv1 * grd) % 1.0
        grout = ((gx < 0.09) | (gy < 0.09)).astype(np.float32)
        b = painterly_fbm(lu1, lv1, 440 + idx * 11, 10, 10)
        c_arr = np.full(lu1.shape + (3,), base_t, dtype=np.float32) + b[..., None] * 0.06
        c_arr = mix_rgb(c_arr, (0.42, 0.40, 0.37), grout * 0.78)
        h_arr = (1.0 - grout) * 0.45 + b * 0.08
        cv.commit_patch(x0, x1, y0, y1, c_arr, h_arr)

    x0, x1, y0, y1 = cv.register("barber_pole_stripe", 3, 6)
    diag = (lu1 * 2.0 + lv1 * 4.0) % 1.0
    c_arr = np.full(lu1.shape + (3,), (0.68, 0.66, 0.62), dtype=np.float32)
    c_arr[diag < 0.33] = (0.64, 0.22, 0.20)
    c_arr[(diag >= 0.50) & (diag < 0.82)] = (0.22, 0.38, 0.64)
    h_arr = np.sin(diag * 2.0 * math.pi) * 0.15
    cv.commit_patch(x0, x1, y0, y1, c_arr, h_arr)

    x0, x1, y0, y1 = cv.register("rollup_shutter", 3, 7)
    slat = np.sin(lv1 * 18.0 * 2.0 * math.pi)
    b = painterly_fbm(lu1, lv1, 467, 8, 8)
    c_arr = np.full(lu1.shape + (3,), (0.50, 0.52, 0.54), dtype=np.float32) + (slat * 0.065 + b * 0.05)[..., None]
    rust = np.clip((b + 0.38 - lv1 * 0.55) * 3.0, 0.0, 1.0)
    c_arr = mix_rgb(c_arr, (0.45, 0.28, 0.20), rust * 0.55)
    h_arr = slat * 0.62
    cv.commit_patch(x0, x1, y0, y1, c_arr, h_arr)
    cv.uv_rects["sandbag_burlap"] = cv.uv_rects["adobe_stone"]

    # --- ROW 4: Sorbetes Sign, Trapal Awnings, Chichirya Sachets, Hazard Curb, Foliage, Bougainvillea, Produce ---
    x0, x1, y0, y1 = cv.register("sign_sorbetes", 4, 0)
    mat_mural_signboard(
        cv, x0, x1, y0, y1,
        bg_rgb=(0.66, 0.56, 0.22),
        line1="SORBETES",
        line2="QUESO • UBE • MANGO",
        fg1=(0.68, 0.65, 0.56),
        fg2=(0.56, 0.22, 0.44),
        header_rgb=(0.22, 0.42, 0.64),
        mural_on_plaster=False,
        seed=499,
    )

    for idx, (nm, c, rgb, striped) in enumerate([
        ("trapal_orange",  1, (0.64, 0.40, 0.22), False),
        ("trapal_striped", 2, (0.28, 0.49, 0.62), True),
    ]):
        x0, x1, y0, y1 = cv.register(nm, 4, c)
        b = painterly_fbm(lu1, lv1, 501 + idx * 17, 8, 8)
        c_arr = np.full(lu1.shape + (3,), rgb, dtype=np.float32) + b[..., None] * 0.06
        if striped:
            st = (((lu1 * 6.0) % 1.0) < 0.50)
            c_arr[st] = np.array([0.66, 0.62, 0.52], dtype=np.float32) + b[st, None] * 0.05
        fold = np.sin(lu1 * 5.0 * 2.0 * math.pi) * 0.25
        h_arr = fold + b * 0.15
        cv.commit_patch(x0, x1, y0, y1, c_arr, h_arr)

    x0, x1, y0, y1 = cv.register("sachet_strips", 4, 3)
    sx = (lu1 * 5.0) % 1.0
    sy = (lv1 * 6.0) % 1.0
    pkt = (sx > 0.12) & (sx < 0.88) & (sy > 0.08) & (sy < 0.92)
    c_arr = np.full(lu1.shape + (3,), (0.25, 0.21, 0.19), dtype=np.float32)
    row_h = np.floor(lu1 * 5.0) % 3.0
    c_arr[pkt & (row_h == 0)] = (0.64, 0.26, 0.22)
    c_arr[pkt & (row_h == 1)] = (0.65, 0.54, 0.21)
    c_arr[pkt & (row_h == 2)] = (0.27, 0.52, 0.34)
    h_arr = pkt.astype(np.float32) * 0.55
    cv.commit_patch(x0, x1, y0, y1, c_arr, h_arr)

    x0, x1, y0, y1 = cv.register("hazard_curb", 4, 4)
    chv = ((lu1 * 4.0 + lv1 * 2.0) % 1.0) < 0.50
    b = painterly_fbm(lu1, lv1, 541, 10, 10)
    c_arr = np.where(chv[..., None], (0.65, 0.54, 0.21), (0.24, 0.23, 0.22)).astype(np.float32) + b[..., None] * 0.05
    cv.commit_patch(x0, x1, y0, y1, c_arr, b * 0.22)

    x0, x1, y0, y1 = cv.register("leaf_banana", 4, 5)
    b = painterly_fbm(lu1, lv1, 557, 10, 10)
    vein = np.sin((lv1 * 14.0 - np.abs(lu1 - 0.5) * 6.0) * 2.0 * math.pi)
    c_arr = np.full(lu1.shape + (3,), (0.34, 0.53, 0.25), dtype=np.float32) + (b * 0.08 + vein * 0.025)[..., None]
    cv.commit_patch(x0, x1, y0, y1, c_arr, vein * 0.25 + b * 0.15)

    x0, x1, y0, y1 = cv.register("bougainvillea", 4, 6)
    b = painterly_fbm(lu1, lv1, 569, 14, 14)
    blossom = b > 0.02
    c_arr = np.where(blossom[..., None], (0.63, 0.27, 0.45), (0.31, 0.47, 0.25)).astype(np.float32) + b[..., None] * 0.07
    cv.commit_patch(x0, x1, y0, y1, c_arr, b * 0.45)

    x0, x1, y0, y1 = cv.register("produce_mango", 4, 7)
    b = painterly_fbm(lu1, lv1, 577, 16, 16)
    c_arr = np.full(lu1.shape + (3,), (0.64, 0.52, 0.22), dtype=np.float32) + b[..., None] * 0.09
    cv.commit_patch(x0, x1, y0, y1, c_arr, b * 0.40)

    # --- ROW 5: Metals, Meralco Meter, Rubber Tire, Blue Water Drum, Team Banners & Palengke Mural ---
    for idx, (nm, c, rgb, rust) in enumerate([
        ("steel_dark",   0, (0.33, 0.35, 0.38), 0.22),
        ("steel_rusted", 1, (0.48, 0.30, 0.21), 0.68),
        ("zinc_brushed", 2, (0.58, 0.59, 0.61), 0.12),
    ]):
        x0, x1, y0, y1 = cv.register(nm, 5, c)
        b = painterly_fbm(lu1, lv1, 601 + idx * 13, 10, 10)
        c_arr = np.full(lu1.shape + (3,), rgb, dtype=np.float32) + b[..., None] * 0.06
        rn = np.clip((b + rust - 0.35) * 3.2, 0.0, 1.0)
        c_arr = mix_rgb(c_arr, (0.47, 0.27, 0.18), rn * 0.65)
        cv.commit_patch(x0, x1, y0, y1, c_arr, b * 0.22 - rn * 0.12)
    cv.uv_rects["steel_ochre"] = cv.uv_rects["hazard_curb"]
    cv.uv_rects["charcoal_ink"] = cv.uv_rects["steel_dark"]

    x0, x1, y0, y1 = cv.register("meter_box", 5, 3)
    c_arr = np.full(lu1.shape + (3,), (0.52, 0.53, 0.51), dtype=np.float32) + painterly_fbm(lu1, lv1, 641, 8, 8)[..., None] * 0.05
    r_glass = np.hypot(lu1 - 0.50, lv1 - 0.54)
    dial = r_glass < 0.30
    rim = (r_glass >= 0.27) & (r_glass < 0.33)
    c_arr[dial] = (0.64, 0.65, 0.60)
    c_arr[(r_glass < 0.22) & (lv1 > 0.50) & (lv1 < 0.58)] = (0.22, 0.23, 0.25)
    c_arr[rim] = (0.34, 0.36, 0.38)
    h_arr = dial.astype(np.float32) * 0.55 + rim.astype(np.float32) * 0.35
    cv.commit_patch(x0, x1, y0, y1, c_arr, h_arr)

    x0, x1, y0, y1 = cv.register("rubber_tire", 5, 4)
    tread = np.sin(lv1 * 16.0 * 2.0 * math.pi) * np.sign(np.sin(lu1 * 6.0 * 2.0 * math.pi))
    c_arr = np.full(lu1.shape + (3,), (0.23, 0.23, 0.25), dtype=np.float32) + (tread * 0.03)[..., None]
    cv.commit_patch(x0, x1, y0, y1, c_arr, tread * 0.48)

    x0, x1, y0, y1 = cv.register("water_drum_blue", 5, 5)
    rib = np.exp(-((lv1 - 0.32) / 0.035) ** 2) + np.exp(-((lv1 - 0.68) / 0.035) ** 2) + np.exp(-((lv1 - 0.93) / 0.035) ** 2)
    b = painterly_fbm(lu1, lv1, 659, 8, 8)
    c_arr = np.full(lu1.shape + (3,), (0.24, 0.46, 0.65), dtype=np.float32) + (b * 0.05 + rib * 0.06)[..., None]
    cv.commit_patch(x0, x1, y0, y1, c_arr, rib * 0.60 + b * 0.10)
    cv.uv_rects["lpg_teal"] = cv.uv_rects["tile_teal_chipped"]

    x0, x1, y0, y1 = cv.register("team_blue", 5, 6)
    b = painterly_fbm(lu1, lv1, 673, 8, 8)
    stripe = (lv1 > 0.42) & (lv1 < 0.58)
    c_arr = np.full(lu1.shape + (3,), (0.23, 0.47, 0.67), dtype=np.float32) + b[..., None] * 0.05
    c_arr[stripe] = (0.68, 0.70, 0.72)
    cv.commit_patch(x0, x1, y0, y1, c_arr, stripe.astype(np.float32) * 0.25 + b * 0.12)

    x0, x1, y0, y1 = cv.register("team_red", 5, 7)
    c_arr = np.full(lu1.shape + (3,), (0.65, 0.25, 0.23), dtype=np.float32) + b[..., None] * 0.05
    c_arr[stripe] = (0.69, 0.60, 0.30)
    cv.commit_patch(x0, x1, y0, y1, c_arr, stripe.astype(np.float32) * 0.25 + b * 0.12)
    cv.uv_rects["lamp_warm"] = cv.uv_rects["stucco_sandstone"]

    # --- ROW 6: 4 Wide `512x256` Deadlock-Grade Storefront Signboards & Painted Wall Murals ---
    x0, x1, y0, y1 = cv.register("sign_sari_sari", 6, 0, span_cols=2)
    mat_mural_signboard(
        cv, x0, x1, y0, y1,
        bg_rgb=(0.68, 0.63, 0.50),
        line1="TINDAHAN NI ALING NENA",
        line2="TINGI • YELO • MALAMIG NA POP",
        fg1=(0.74, 0.71, 0.61),
        fg2=(0.18, 0.28, 0.49),
        header_rgb=(0.56, 0.19, 0.17),
        mural_on_plaster=False,
        seed=701,
    )

    x0, x1, y0, y1 = cv.register("sign_karinderya", 6, 2, span_cols=2)
    mat_mural_signboard(
        cv, x0, x1, y0, y1,
        bg_rgb=(0.67, 0.57, 0.33),
        line1="KARINDERYA — LUTONG BAHAY",
        line2="SINIGANG • ADOBO • PANCIT",
        fg1=(0.74, 0.70, 0.59),
        fg2=(0.20, 0.16, 0.15),
        header_rgb=(0.54, 0.19, 0.16),
        mural_on_plaster=True,
        seed=719,
    )

    x0, x1, y0, y1 = cv.register("sign_barberya", 6, 4, span_cols=2)
    mat_mural_signboard(
        cv, x0, x1, y0, y1,
        bg_rgb=(0.69, 0.66, 0.59),
        line1="GUPITAN • BARBERSHOP",
        line2="GUPIT POGI • AHIT • MASAHE",
        fg1=(0.74, 0.72, 0.65),
        fg2=(0.56, 0.18, 0.17),
        header_rgb=(0.18, 0.28, 0.49),
        mural_on_plaster=True,
        seed=733,
    )

    x0, x1, y0, y1 = cv.register("sign_talyer", 6, 6, span_cols=2)
    mat_mural_signboard(
        cv, x0, x1, y0, y1,
        bg_rgb=(0.63, 0.52, 0.24),
        line1="VULCANIZING & HANGIN",
        line2="PASTA • TIRE • BATTERY CHARGE",
        fg1=(0.65, 0.57, 0.26),
        fg2=(0.20, 0.18, 0.18),
        header_rgb=(0.25, 0.28, 0.32),
        mural_on_plaster=False,
        seed=751,
    )

    # --- ROW 7: Civic Landmarks (`512x256`), Bawal Umihi Stencil (`512x256`) & Pole Posters (`256x256`) ---
    x0, x1, y0, y1 = cv.register("sign_brgy_hall", 7, 0, span_cols=2)
    mat_mural_signboard(
        cv, x0, x1, y0, y1,
        bg_rgb=(0.63, 0.59, 0.50),
        line1="BARANGAY HALL • SAN ROQUE",
        line2="TANGGAPAN NG PUNONG BARANGAY",
        fg1=(0.67, 0.64, 0.54),
        fg2=(0.21, 0.32, 0.50),
        header_rgb=(0.22, 0.36, 0.56),
        mural_on_plaster=False,
        seed=769,
    )

    x0, x1, y0, y1 = cv.register("sign_covered_court", 7, 2, span_cols=2)
    mat_mural_signboard(
        cv, x0, x1, y0, y1,
        bg_rgb=(0.62, 0.58, 0.49),
        line1="BRGY. SAN ROQUE COVERED COURT",
        line2="PALARONG PAMBARANGAY • SANGGUNIANG KABATAAN",
        fg1=(0.67, 0.63, 0.50),
        fg2=(0.58, 0.23, 0.20),
        header_rgb=(0.25, 0.43, 0.36),
        mural_on_plaster=False,
        seed=787,
    )

    x0, x1, y0, y1 = cv.register("chb_bawal_umihi", 7, 4, span_cols=2)
    mat_mural_signboard(
        cv, x0, x1, y0, y1,
        bg_rgb=(0.54, 0.52, 0.48),
        line1="BAWAL UMIHI DITO",
        line2="MULTA P500 • BRGY. ORDINANCE",
        fg1=(0.66, 0.64, 0.58),
        fg2=(0.58, 0.20, 0.18),
        header_rgb=(0.58, 0.21, 0.19),
        mural_on_plaster=True,
        seed=809,
    )

    x0, x1, y0, y1 = cv.register("poster_vote_halalan", 7, 6, span_cols=1)
    mat_mural_signboard(
        cv, x0, x1, y0, y1,
        bg_rgb=(0.65, 0.61, 0.50),
        line1="HALALAN '95",
        line2="VOTE! KAPITAN",
        fg1=(0.67, 0.63, 0.50),
        fg2=(0.21, 0.35, 0.58),
        header_rgb=(0.60, 0.22, 0.20),
        mural_on_plaster=True,
        seed=823,
    )

    x0, x1, y0, y1 = cv.register("poster_malabanan", 7, 7, span_cols=1)
    mat_mural_signboard(
        cv, x0, x1, y0, y1,
        bg_rgb=(0.64, 0.55, 0.23),
        line1="MALABANAN",
        line2="POSO NEGRO 812-44",
        fg1=(0.67, 0.64, 0.56),
        fg2=(0.20, 0.17, 0.17),
        header_rgb=(0.59, 0.21, 0.19),
        mural_on_plaster=False,
        seed=839,
    )
    cv.uv_rects["trapal_blue"] = cv.uv_rects["team_blue"]
    cv.uv_rects["backboard_worn"] = cv.uv_rects["sign_covered_court"]
    cv.uv_rects["sign_no_entry"] = cv.uv_rects["chb_bawal_umihi"]
    cv.uv_rects["laundry_warm"] = cv.uv_rects["trapal_orange"]
    cv.uv_rects["laundry_cool"] = cv.uv_rects["team_blue"]
    cv.uv_rects["jeep_ochre"] = cv.uv_rects["hazard_curb"]
    cv.uv_rects["jeep_crimson"] = cv.uv_rects["team_red"]

    cv.save_pair(
        OUT_DIR / "BarangayAtlas.jpg",
        OUT_DIR / "BarangayAtlas_normal.png",
        OUT_DIR / "BarangayAtlas_orm.png",
    )
    return cv.uv_rects


# ---------------------------------------------------------------------------
# 2. 90s Province Props Atlas (`ProvincePropsAtlas.jpg` + `_normal.png`)
# ---------------------------------------------------------------------------

def generate_province_props_atlas() -> Dict[str, Tuple[float, float, float, float]]:
    """Generates `ProvincePropsAtlas.jpg` + `ProvincePropsAtlas_normal.png` and returns `name -> (u0, v0, u1, v1)`."""
    cv = AtlasCanvas()
    lu1, lv1 = cv.cell_grids(1)

    # Row 0: Sarao Jeepney & Tricycle Pressed-Steel Enamels, Chrome, Grille, Wheel & Tire
    livery_specs = [
        ("jeep_chrome",    0, 0, (0.65, 0.66, 0.68), (0.48, 0.50, 0.53)),
        ("jeep_yellow",    0, 1, (0.67, 0.54, 0.19), (0.62, 0.23, 0.19)),
        ("jeep_red",       0, 2, (0.63, 0.22, 0.19), (0.67, 0.56, 0.22)),
        ("jeep_blue",      0, 3, (0.21, 0.42, 0.65), (0.66, 0.64, 0.56)),
        ("jeep_green",     0, 4, (0.24, 0.51, 0.34), (0.66, 0.56, 0.22)),
    ]
    for idx, (nm, r, c, main_c, stripe_c) in enumerate(livery_specs):
        x0, x1, y0, y1 = cv.register(nm, r, c)
        b = painterly_fbm(lu1, lv1, 901 + idx * 17, 10, 8)
        c_arr = np.full(lu1.shape + (3,), main_c, dtype=np.float32) + b[..., None] * 0.055
        band = (lv1 > 0.42) & (lv1 < 0.58)
        pinstripe = (np.abs(lv1 - 0.36) < 0.02) | (np.abs(lv1 - 0.64) < 0.02)
        c_arr[band] = np.asarray(stripe_c, dtype=np.float32) + b[band, None] * 0.04
        c_arr[pinstripe] = (0.67, 0.65, 0.58)
        bead = np.exp(-((lv1 - 0.50) / 0.04) ** 2)
        cv.commit_patch(x0, x1, y0, y1, c_arr, bead * 0.55 + b * 0.12)

    x0, x1, y0, y1 = cv.register("radiator_grille", 0, 5)
    slot = (((lu1 * 11.0) % 1.0) > 0.30) & (lv1 > 0.15) & (lv1 < 0.85)
    c_arr = np.where(slot[..., None], (0.20, 0.21, 0.23), (0.63, 0.64, 0.66)).astype(np.float32)
    cv.commit_patch(x0, x1, y0, y1, c_arr, (1.0 - slot.astype(np.float32)) * 0.72)

    x0, x1, y0, y1 = cv.register("tire_rubber", 0, 6)
    tread = np.sin(lv1 * 18.0 * 2.0 * math.pi)
    c_arr = np.full(lu1.shape + (3,), (0.22, 0.22, 0.24), dtype=np.float32) + (tread * 0.03)[..., None]
    cv.commit_patch(x0, x1, y0, y1, c_arr, tread * 0.50)

    x0, x1, y0, y1 = cv.register("wheel_rim", 0, 7)
    rad = np.hypot(lu1 - 0.5, lv1 - 0.5)
    ang = np.arctan2(lv1 - 0.5, lu1 - 0.5)
    spokes = (np.sin(ang * 8.0) > 0.25) & (rad > 0.14) & (rad < 0.38)
    c_arr = np.full(lu1.shape + (3,), (0.61, 0.56, 0.32), dtype=np.float32)
    c_arr[spokes] = (0.24, 0.24, 0.26)
    c_arr[rad < 0.14] = (0.52, 0.53, 0.55)
    cv.commit_patch(x0, x1, y0, y1, c_arr, (1.0 - spokes.astype(np.float32)) * 0.60)

    # Row 1: Windshield, Vinyl Seat, Headlamp, Tricycle Galvanized/Enamel, Engine Block
    x0, x1, y0, y1 = cv.register("windshield_glass", 1, 0)
    b = painterly_fbm(lu1, lv1, 953, 8, 8)
    glint = np.exp(-(((lu1 + lv1) - 1.05) / 0.14) ** 2)
    c_arr = np.full(lu1.shape + (3,), (0.29, 0.42, 0.49), dtype=np.float32) + (glint * 0.18 + b * 0.04)[..., None]
    cv.commit_patch(x0, x1, y0, y1, c_arr, b * 0.08)

    x0, x1, y0, y1 = cv.register("vinyl_seat", 1, 1)
    pleat = np.sin(lu1 * 10.0 * 2.0 * math.pi)
    c_arr = np.full(lu1.shape + (3,), (0.48, 0.23, 0.20), dtype=np.float32) + (pleat * 0.045)[..., None]
    cv.commit_patch(x0, x1, y0, y1, c_arr, pleat * 0.45)

    x0, x1, y0, y1 = cv.register("headlamp_amber", 1, 2)
    rad = np.hypot(lu1 - 0.5, lv1 - 0.5)
    c_arr = np.where((rad < 0.36)[..., None], (0.71, 0.66, 0.46), (0.55, 0.56, 0.58)).astype(np.float32)
    cv.commit_patch(x0, x1, y0, y1, c_arr, np.clip(0.42 - rad, 0.0, 0.42) * 1.4)

    x0, x1, y0, y1 = cv.register("trike_galv", 1, 3)
    cv.commit_patch(x0, x1, y0, y1, *mat_corrugated_yero(lu1, lv1, (0.57, 0.59, 0.61), seed=971, rust_amt=0.22, ribs=8))

    x0, x1, y0, y1 = cv.register("trike_blue", 1, 4)
    cv.commit_patch(x0, x1, y0, y1, *mat_stucco_wall(lu1, lv1, (0.22, 0.44, 0.65), seed=977, moss=0.0, sill_streaks=0.2, patch_chb=0.0))

    x0, x1, y0, y1 = cv.register("engine_metal", 1, 5)
    fins = np.sin(lv1 * 20.0 * 2.0 * math.pi)
    c_arr = np.full(lu1.shape + (3,), (0.39, 0.41, 0.43), dtype=np.float32) + (fins * 0.05)[..., None]
    cv.commit_patch(x0, x1, y0, y1, c_arr, fins * 0.65)

    x0, x1, y0, y1 = cv.register("yero_rusted", 1, 6)
    cv.commit_patch(x0, x1, y0, y1, *mat_corrugated_yero(lu1, lv1, (0.53, 0.31, 0.21), seed=983, rust_amt=0.68, ribs=26))

    x0, x1, y0, y1 = cv.register("yero_green", 1, 7)
    cv.commit_patch(x0, x1, y0, y1, *mat_corrugated_yero(lu1, lv1, (0.32, 0.50, 0.36), seed=991, rust_amt=0.28, ribs=26))

    # Row 2: Tropical Barks, Hardwoods, Bamboo & CHB Perimeter Wall Swatches
    wood_barks = [
        ("bark_mango",      2, 0, (0.37, 0.28, 0.20), 6, True),
        ("bark_palm_rings", 2, 1, (0.48, 0.40, 0.31), 9, False),
        ("pseudostem_ban",  2, 2, (0.40, 0.53, 0.27), 4, True),
        ("wood_coco",       2, 3, (0.51, 0.36, 0.24), 4, False),
        ("wood_dark",       2, 4, (0.35, 0.25, 0.18), 3, False),
        ("bamboo_gold",     2, 5, (0.58, 0.51, 0.30), 3, False),
    ]
    for idx, (nm, r, c, rgb, plk, vert) in enumerate(wood_barks):
        x0, x1, y0, y1 = cv.register(nm, r, c)
        c_arr, h_arr = mat_hardwood(lu1, lv1, rgb, seed=1010 + idx * 17, planks=plk, vertical=vert)
        cv.commit_patch(x0, x1, y0, y1, c_arr, h_arr)

    x0, x1, y0, y1 = cv.register("chb_wall_moss", 2, 6)
    cv.commit_patch(x0, x1, y0, y1, *mat_chb_firewall(lu1, lv1, (0.52, 0.51, 0.48), seed=1087, plaster_patch=0.36, moss=0.68))

    x0, x1, y0, y1 = cv.register("concrete_cap", 2, 7)
    cv.commit_patch(x0, x1, y0, y1, *mat_stucco_wall(lu1, lv1, (0.58, 0.56, 0.52), seed=1093, moss=0.22, sill_streaks=0.35, patch_chb=0.0))

    # Row 3: Multi-Tone Botanical Canopy Swatches (Deadlock painterly warm-sunlit olive -> warm sky-bounced olive)
    foliage_specs = [
        ("leaf_mango_sun",  3, 0, (0.34, 0.49, 0.20), (0.48, 0.59, 0.25)),
        ("leaf_mango_mid",  3, 1, (0.29, 0.44, 0.19), (0.41, 0.54, 0.23)),
        ("leaf_mango_deep", 3, 2, (0.25, 0.39, 0.18), (0.36, 0.49, 0.22)),
        ("leaf_palm_frond", 3, 3, (0.29, 0.49, 0.22), (0.41, 0.57, 0.26)),
        ("leaf_banana_rib", 3, 4, (0.34, 0.53, 0.23), (0.45, 0.60, 0.28)),
        ("fruit_mango_gold",3, 5, (0.68, 0.54, 0.20), (0.65, 0.36, 0.21)),
        ("fruit_buko_green",3, 6, (0.44, 0.55, 0.23), (0.36, 0.45, 0.19)),
        ("hoop_rim_orange", 3, 7, (0.68, 0.32, 0.18), (0.52, 0.24, 0.15)),
    ]
    for idx, (nm, r, c, c_lo, c_hi) in enumerate(foliage_specs):
        x0, x1, y0, y1 = cv.register(nm, r, c)
        b = painterly_fbm(lu1, lv1, 1120 + idx * 19, 10, 10)
        leaf_clump = value_noise(lu1, lv1, 14, 22, 1200 + idx * 23)
        leaf_fine = value_noise(lu1 * 1.4 + lv1 * 0.35, lv1 * 1.8 - lu1 * 0.25, 24, 28, 1260 + idx * 17)
        t = np.clip(leaf_clump * 0.55 + leaf_fine * 0.35 + b * 0.55 + 0.08, 0.0, 1.0)
        c_arr = mix_rgb(np.full(lu1.shape + (3,), c_lo, dtype=np.float32), c_hi, t)
        if nm.startswith("leaf_mango"):
            # Crisp Deadlock-style overlapping painterly lancet leaf-cluster brushstrokes + deep pocket clefts
            blade1 = value_noise(lu1 * 2.2 + lv1 * 0.7, lv1 * 3.4 - lu1 * 0.5, 26, 36, 1301 + idx * 13)
            blade2 = value_noise(lu1 * 2.4 - lv1 * 0.8, lv1 * 3.2 + lu1 * 0.6, 28, 34, 1347 + idx * 19)
            cleft = ((blade1 < 0.30) & (blade2 < 0.38)).astype(np.float32)
            tip_hi = ((blade1 > 0.68) | (blade2 > 0.72)).astype(np.float32)
            c_arr = mix_rgb(c_arr, (c_lo[0] * 0.74, c_lo[1] * 0.78, c_lo[2] * 0.76), cleft * 0.65)
            c_arr = mix_rgb(c_arr, (min(c_hi[0] * 1.08, 0.52), min(c_hi[1] * 1.06, 0.62), min(c_hi[2] * 1.05, 0.29)), tip_hi * 0.48)
            h_arr = (blade1 - 0.5) * 0.08 + (blade2 - 0.5) * 0.06 - cleft * 0.06
        else:
            h_arr = (leaf_clump - 0.5) * 0.55 + b * 0.22
        if nm == "leaf_palm_frond":
            pinnae = np.sin((lv1 * 18.0 - np.abs(lu1 - 0.5) * 8.0) * 2.0 * math.pi)
            h_arr += pinnae * 0.35
            c_arr += (pinnae * 0.03)[..., None]
        elif nm == "leaf_banana_rib":
            midrib = np.exp(-((lu1 - 0.50) / 0.045) ** 2)
            c_arr = mix_rgb(c_arr, (0.54, 0.62, 0.33), midrib * 0.85)
            h_arr += midrib * 0.55
        cv.commit_patch(x0, x1, y0, y1, c_arr, h_arr)

    # Row 4: Sculpted 3D *Sampayan* Laundry Fabrics (Striped Polo, Liga #23 Jersey, Floral Daster, Denim Shorts, Towel, Rope)
    fabrics = [
        ("cloth_polo_stripe", 4, 0, (0.64, 0.62, 0.56), (0.24, 0.44, 0.64), "stripe_h"),
        ("cloth_jersey_red",  4, 1, (0.64, 0.23, 0.21), (0.68, 0.58, 0.24), "trim"),
        ("cloth_daster_flor", 4, 2, (0.62, 0.37, 0.46), (0.68, 0.61, 0.32), "dots"),
        ("cloth_denim_blue",  4, 3, (0.26, 0.39, 0.55), (0.34, 0.48, 0.63), "weave"),
        ("cloth_towel_teal",  4, 4, (0.32, 0.56, 0.54), (0.66, 0.64, 0.58), "border"),
        ("cloth_sando_white", 4, 5, (0.66, 0.65, 0.61), (0.56, 0.55, 0.52), "weave"),
        ("merienda_jug_org",  4, 6, (0.68, 0.45, 0.19), (0.69, 0.58, 0.28), "dots"),
        ("trapal_canopy_str", 4, 7, (0.64, 0.25, 0.21), (0.67, 0.64, 0.56), "stripe_v"),
    ]
    for idx, (nm, r, c, c1, c2, pat) in enumerate(fabrics):
        x0, x1, y0, y1 = cv.register(nm, r, c)
        b = painterly_fbm(lu1, lv1, 1300 + idx * 17, 10, 10)
        drape = np.sin(lu1 * 5.0 * 2.0 * math.pi) * 0.35
        c_arr = np.full(lu1.shape + (3,), c1, dtype=np.float32) + (b * 0.05 + drape * 0.04)[..., None]
        if pat == "stripe_h":
            st = (((lv1 * 8.0) % 1.0) < 0.35)
            c_arr[st] = np.asarray(c2, dtype=np.float32)
        elif pat == "stripe_v":
            st = (((lu1 * 6.0) % 1.0) < 0.50)
            c_arr[st] = np.asarray(c2, dtype=np.float32)
        elif pat == "trim":
            tr = (lv1 < 0.14) | (lv1 > 0.86) | (lu1 < 0.10) | (lu1 > 0.90)
            c_arr[tr] = np.asarray(c2, dtype=np.float32)
        elif pat == "border":
            tr = (np.abs(lv1 - 0.16) < 0.04) | (np.abs(lv1 - 0.84) < 0.04)
            c_arr[tr] = np.asarray(c2, dtype=np.float32)
        elif pat == "dots":
            fl = value_noise(lu1, lv1, 14, 14, 1361 + idx) > 0.66
            c_arr[fl] = np.asarray(c2, dtype=np.float32)
        cv.commit_patch(x0, x1, y0, y1, c_arr, drape + b * 0.18)

    # Row 5 & 6: Additional Metals, Chain Net, Iron Grille, Poster Handbills & Backboard
    x0, x1, y0, y1 = cv.register("steel_dark", 5, 0)
    b = painterly_fbm(lu1, lv1, 1409, 10, 10)
    cv.commit_patch(x0, x1, y0, y1, np.full(lu1.shape + (3,), (0.32, 0.34, 0.37), dtype=np.float32) + b[..., None] * 0.05, b * 0.20)

    x0, x1, y0, y1 = cv.register("chain_net", 5, 1)
    d1 = np.abs(((lu1 * 6.0 + lv1 * 6.0) % 1.0) - 0.5) < 0.12
    d2 = np.abs(((lu1 * 6.0 - lv1 * 6.0) % 1.0) - 0.5) < 0.12
    net = d1 | d2
    c_arr = np.where(net[..., None], (0.64, 0.65, 0.66), (0.36, 0.38, 0.40)).astype(np.float32)
    cv.commit_patch(x0, x1, y0, y1, c_arr, net.astype(np.float32) * 0.55)

    x0, x1, y0, y1 = cv.register("backboard_liga", 5, 2, span_cols=2)
    w_bb, h_bb = x1 - x0, y1 - y0
    lu2, lv2 = cv.cell_grids(2)
    c_arr, h_arr = mat_hardwood(lu2, lv2, (0.65, 0.63, 0.58), seed=1429, planks=4, vertical=False)
    # Classic red shooter's target rectangle + outer border
    outer_b = (lu2 < 0.04) | (lu2 > 0.96) | (lv2 < 0.06) | (lv2 > 0.94)
    shoot_b = (lu2 > 0.33) & (lu2 < 0.67) & (lv2 > 0.16) & (lv2 < 0.54) & (
        (lu2 < 0.365) | (lu2 > 0.635) | (lv2 < 0.21) | (lv2 > 0.49)
    )
    red_p = (outer_b | shoot_b).astype(np.float32)
    c_arr = mix_rgb(c_arr, (0.62, 0.21, 0.19), red_p * 0.88)
    m_liga = render_ttf_mask(w_bb, h_bb, "BRGY. LIGA '95", 0.16, 0.84, 0.64, 0.86, bold_display=True)
    c_arr = mix_rgb(c_arr, (0.21, 0.33, 0.55), m_liga * 0.86)
    cv.commit_patch(x0, x1, y0, y1, c_arr, h_arr + (red_p + m_liga) * 0.15)

    x0, x1, y0, y1 = cv.register("sign_no_entry", 5, 4, span_cols=2)
    mat_mural_signboard(
        cv, x0, x1, y0, y1,
        bg_rgb=(0.61, 0.22, 0.20),
        line1="NO ENTRY • BAWAL DITO",
        line2="TODA ZONE 04 • SAN ROQUE",
        fg1=(0.67, 0.65, 0.59),
        fg2=(0.67, 0.59, 0.28),
        header_rgb=(0.23, 0.25, 0.28),
        mural_on_plaster=False,
        seed=1451,
    )

    x0, x1, y0, y1 = cv.register("poster_halalan", 5, 6)
    mat_mural_signboard(
        cv, x0, x1, y0, y1,
        bg_rgb=(0.65, 0.61, 0.50),
        line1="VOTE! '95",
        line2="KAGAWAD REYES",
        fg1=(0.67, 0.64, 0.52),
        fg2=(0.20, 0.34, 0.56),
        header_rgb=(0.60, 0.22, 0.20),
        mural_on_plaster=True,
        seed=1471,
    )

    x0, x1, y0, y1 = cv.register("poster_malabanan", 5, 7)
    mat_mural_signboard(
        cv, x0, x1, y0, y1,
        bg_rgb=(0.64, 0.55, 0.22),
        line1="MALABANAN",
        line2="SIPSIP POSO 812-44",
        fg1=(0.67, 0.64, 0.56),
        fg2=(0.20, 0.17, 0.17),
        header_rgb=(0.59, 0.21, 0.19),
        mural_on_plaster=False,
        seed=1483,
    )

    # Row 6 & Row 7: Wide `512x256` Route Boards, TODA Terminal, Merienda Stand & Bawal Umihi Wall Stencil
    x0, x1, y0, y1 = cv.register("sign_jeep_route", 6, 0, span_cols=2)
    mat_mural_signboard(
        cv, x0, x1, y0, y1,
        bg_rgb=(0.66, 0.56, 0.22),
        line1="BRGY. KALYEAH - BAYAN",
        line2="VIA TABING-ILOG • PLAZA",
        fg1=(0.68, 0.65, 0.56),
        fg2=(0.19, 0.17, 0.17),
        header_rgb=(0.60, 0.21, 0.19),
        mural_on_plaster=False,
        seed=1511,
    )

    x0, x1, y0, y1 = cv.register("sign_toda_terminal", 6, 2, span_cols=2)
    mat_mural_signboard(
        cv, x0, x1, y0, y1,
        bg_rgb=(0.64, 0.60, 0.50),
        line1="BRGY. TODA TERMINAL",
        line2="PILA NG TRICYCLE • ZONE 4",
        fg1=(0.67, 0.64, 0.55),
        fg2=(0.59, 0.22, 0.19),
        header_rgb=(0.21, 0.38, 0.60),
        mural_on_plaster=False,
        seed=1523,
    )

    x0, x1, y0, y1 = cv.register("sign_merienda", 6, 4, span_cols=2)
    mat_mural_signboard(
        cv, x0, x1, y0, y1,
        bg_rgb=(0.65, 0.57, 0.42),
        line1="MERIENDA • PALAMIG",
        line2="SAGO'T GULAMAN • BANANA Q",
        fg1=(0.67, 0.64, 0.53),
        fg2=(0.22, 0.36, 0.54),
        header_rgb=(0.61, 0.24, 0.19),
        mural_on_plaster=False,
        seed=1543,
    )

    x0, x1, y0, y1 = cv.register("chb_bawal_stencil", 6, 6, span_cols=2)
    mat_mural_signboard(
        cv, x0, x1, y0, y1,
        bg_rgb=(0.53, 0.52, 0.48),
        line1="BAWAL UMIHI DITO",
        line2="MULTA P500 • BRGY. TANOD",
        fg1=(0.67, 0.64, 0.58),
        fg2=(0.58, 0.20, 0.18),
        header_rgb=(0.58, 0.21, 0.19),
        mural_on_plaster=True,
        seed=1559,
    )

    # Row 7: Extra rich detail swatches (banana blossom, Panaderya signboard, Toda #04 plate, Palengke, Brick)
    x0, x1, y0, y1 = cv.register("puso_crimson", 7, 0)
    b = painterly_fbm(lu1, lv1, 1601, 10, 10)
    cv.commit_patch(x0, x1, y0, y1, np.full(lu1.shape + (3,), (0.54, 0.20, 0.25), dtype=np.float32) + b[..., None] * 0.06, b * 0.30)

    x0, x1, y0, y1 = cv.register("sign_panaderya", 7, 1, span_cols=2)
    mat_mural_signboard(
        cv, x0, x1, y0, y1,
        bg_rgb=(0.66, 0.57, 0.36),
        line1="PANADERYA • PANDESAL",
        line2="MAINIT PA • KABABAYAN",
        fg1=(0.68, 0.65, 0.54),
        fg2=(0.22, 0.18, 0.16),
        header_rgb=(0.61, 0.23, 0.19),
        mural_on_plaster=False,
        seed=1619,
    )

    x0, x1, y0, y1 = cv.register("plate_toda_04", 7, 3)
    mat_mural_signboard(
        cv, x0, x1, y0, y1,
        bg_rgb=(0.66, 0.56, 0.20),
        line1="TODA 04",
        line2="SAN ROQUE",
        fg1=(0.67, 0.65, 0.58),
        fg2=(0.19, 0.18, 0.18),
        header_rgb=(0.21, 0.38, 0.60),
        mural_on_plaster=False,
        seed=1637,
    )

    x0, x1, y0, y1 = cv.register("sign_palengke_fresh", 7, 4, span_cols=2)
    mat_mural_signboard(
        cv, x0, x1, y0, y1,
        bg_rgb=(0.64, 0.58, 0.44),
        line1="PRUTAS • GULAY • SARIWA",
        line2="PALENGKE NG SAN ROQUE",
        fg1=(0.67, 0.64, 0.52),
        fg2=(0.24, 0.44, 0.28),
        header_rgb=(0.59, 0.25, 0.19),
        mural_on_plaster=False,
        seed=1657,
    )

    x0, x1, y0, y1 = cv.register("broken_brick_patch", 7, 6, span_cols=2)
    lu2, lv2 = cv.cell_grids(2)
    cv.commit_patch(x0, x1, y0, y1, *mat_chb_firewall(lu2, lv2, (0.55, 0.42, 0.34), seed=1669, plaster_patch=0.52, moss=0.55))
    cv.uv_rects["terracotta_pot"] = cv.uv_rects["broken_brick_patch"]
    cv.uv_rects["iron_grille_wall"] = cv.uv_rects["steel_dark"]

    cv.save_pair(
        OUT_DIR / "ProvincePropsAtlas.jpg",
        OUT_DIR / "ProvincePropsAtlas_normal.png",
        OUT_DIR / "ProvincePropsAtlas_orm.png",
    )
    return cv.uv_rects
