"""Build clean, beveled 90s Pinoy street-toy 3D weapon models (.glb + 512x512 painted JPEG).

Usage:
    "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background --python tools/blender/build_weapons.py

By default, owner-pinned topology/skin assets (`can.glb`, `trumpo.glb`) are left untouched so
their byte-exact skin and vertex-count unit tests remain green; pass `-- --include-pinned` to
also rebuild standalone copies when needed.

Every exported model satisfies:
- Single MeshInstance3D at Transform3D.IDENTITY, 1 surface, no vertex COLOR_0 attribute
- 1,000 <= triangles <= 3,500, watertight manifold (open_edges / triangles == 0.0)
- Grounded at min_y == 0.0 in glTF Y-up space, centered on X and Z
- Clean 512x512 painted palette texture passing tools/art/check_weapon_texture.py
"""

from __future__ import annotations

import io
import json
import math
import os
import struct
import sys
from pathlib import Path
from typing import Callable

import bmesh
import bpy
from mathutils import Matrix, Vector

try:
    from PIL import Image, ImageDraw
except ImportError:
    Image = None
    ImageDraw = None


ROOT = Path(__file__).resolve().parents[2]
OUT_DIR = ROOT / "assets" / "models" / "weapons"
TEX_SIZE = 512


# ---------------------------------------------------------------------------
# Texture generation (clean painted colour bands, zero AI noise, luma >= 0.24)
# ---------------------------------------------------------------------------

def _save_banded_jpeg(path: Path, bands: list[tuple[float, tuple[int, int, int]]]) -> None:
    """Write a crisp horizontal-band 512x512 painted palette JPEG (4:4:4, quality=96).

    `bands` is a list of `(end_v_fraction, (r, g, b))` from top (y=0) to bottom (y=512).
    UVs map into the interior of each band with generous gutters so mipmaps stay clean.
    """
    if Image is not None and ImageDraw is not None:
        img = Image.new("RGB", (TEX_SIZE, TEX_SIZE), bands[-1][1])
        draw = ImageDraw.Draw(img)
        y0 = 0
        for frac, rgb in bands:
            y1 = min(TEX_SIZE, max(y0 + 1, int(round(frac * TEX_SIZE))))
            draw.rectangle([0, y0, TEX_SIZE - 1, y1 - 1], fill=rgb)
            y0 = y1
        img.save(str(path), format="JPEG", quality=96, subsampling=0)
        return

    # Pure Blender fallback if Pillow is absent in embedded Python (8-bit Blender Image.pixels is sRGB 0..1)
    name = f"_tmp_{path.stem}"
    if name in bpy.data.images:
        bpy.data.images.remove(bpy.data.images[name])
    bimg = bpy.data.images.new(name, width=TEX_SIZE, height=TEX_SIZE, alpha=False)
    pixels = [0.0] * (TEX_SIZE * TEX_SIZE * 4)

    norm_bands = [(frac, (rgb[0] / 255.0, rgb[1] / 255.0, rgb[2] / 255.0)) for frac, rgb in bands]
    for row_top in range(TEX_SIZE):
        frac_y = (row_top + 0.5) / TEX_SIZE
        chosen = norm_bands[-1][1]
        for end_f, col in norm_bands:
            if frac_y <= end_f:
                chosen = col
                break
        by = TEX_SIZE - 1 - row_top
        base = by * TEX_SIZE * 4
        r, g, b = chosen
        for x in range(TEX_SIZE):
            idx = base + x * 4
            pixels[idx] = r
            pixels[idx + 1] = g
            pixels[idx + 2] = b
            pixels[idx + 3] = 1.0
    bimg.pixels[:] = pixels
    bimg.filepath_raw = str(path)
    bimg.file_format = "JPEG"
    bimg.save()
    bpy.data.images.remove(bimg)


def _uv_for_band(band_index: int, band_bounds: list[tuple[float, float]], u: float) -> Vector:
    """Return UV inside band `band_index` (where band 0 is top of image -> v near 1.0 in glTF/Blender)."""
    f0, f1 = band_bounds[band_index]
    # Blender V=1 is top of image, V=0 is bottom of image
    v_top = 1.0 - f0
    v_bot = 1.0 - f1
    margin = (v_top - v_bot) * 0.22
    v = (v_bot + margin) + (v_top - v_bot - 2.0 * margin) * 0.5
    uu = 0.08 + 0.84 * (u % 1.0)
    return Vector((uu, v))


def _band_bounds(bands: list[tuple[float, tuple[int, int, int]]]) -> list[tuple[float, float]]:
    out: list[tuple[float, float]] = []
    prev = 0.0
    for end_f, _ in bands:
        out.append((prev, end_f))
        prev = end_f
    return out


# ---------------------------------------------------------------------------
# BMesh parametric helpers (watertight manifold lofting, caps, bevels)
# ---------------------------------------------------------------------------

def _add_lofted_tube(
    bm: bmesh.types.BMesh,
    frames: list[tuple[Vector, Vector, Vector, float, float]],
    radial_segs: int,
    band_fn: Callable[[int, int], int],
    uv_layer: bmesh.types.BMLayerItem,
    band_bounds: list[tuple[float, float]],
    cap_start: bool = True,
    cap_end: bool = True,
) -> None:
    """Loft a closed watertight tube along `frames`: `(center, axis_x, axis_y, rx, ry)`."""
    rings: list[list[bmesh.types.BMVert]] = []
    for center, ax, ay, rx, ry in frames:
        ring: list[bmesh.types.BMVert] = []
        for j in range(radial_segs):
            ang = (2.0 * math.pi * j) / radial_segs
            pt = center + ax * (math.cos(ang) * rx) + ay * (math.sin(ang) * ry)
            ring.append(bm.verts.new(pt))
        rings.append(ring)

    for i in range(len(rings) - 1):
        r0 = rings[i]
        r1 = rings[i + 1]
        for j in range(radial_segs):
            jn = (j + 1) % radial_segs
            f = bm.faces.new((r0[j], r0[jn], r1[jn], r1[j]))
            b_idx = band_fn(i, j)
            u0 = j / radial_segs
            u1 = (j + 1) / radial_segs
            for loop, uu in zip(f.loops, (u0, u1, u1, u0)):
                loop[uv_layer].uv = _uv_for_band(b_idx, band_bounds, uu)

    if cap_start:
        c0 = frames[0][0]
        vc = bm.verts.new(c0)
        r0 = rings[0]
        b_idx = band_fn(0, 0)
        for j in range(radial_segs):
            jn = (j + 1) % radial_segs
            f = bm.faces.new((vc, r0[jn], r0[j]))
            for loop in f.loops:
                loop[uv_layer].uv = _uv_for_band(b_idx, band_bounds, j / radial_segs)

    if cap_end:
        ce = frames[-1][0]
        vc = bm.verts.new(ce)
        re = rings[-1]
        b_idx = band_fn(len(rings) - 1, 0)
        for j in range(radial_segs):
            jn = (j + 1) % radial_segs
            f = bm.faces.new((vc, re[j], re[jn]))
            for loop in f.loops:
                loop[uv_layer].uv = _uv_for_band(b_idx, band_bounds, j / radial_segs)


def _paint_all_faces_band(
    bm: bmesh.types.BMesh,
    uv_layer: bmesh.types.BMLayerItem,
    band_bounds: list[tuple[float, float]],
    face_band_fn: Callable[[bmesh.types.BMFace], int],
) -> None:
    for f in bm.faces:
        b_idx = face_band_fn(f)
        for k, loop in enumerate(f.loops):
            loop[uv_layer].uv = _uv_for_band(b_idx, band_bounds, (k * 0.23) % 1.0)


def _finalize_and_normalize(
    bm: bmesh.types.BMesh,
    target_size_gltf: tuple[float, float, float] | None,
    preserve_exact_coords: bool = False,
) -> None:
    """Triangulate, weld tiny seams, recompute outward normals, ground on Blender Z=0, center X/Y."""
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-5)
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])

    if preserve_exact_coords:
        for f in bm.faces:
            f.smooth = True
        return

    xs = [v.co.x for v in bm.verts]
    ys = [v.co.y for v in bm.verts]
    zs = [v.co.z for v in bm.verts]
    cx = 0.5 * (min(xs) + max(xs))
    cy = 0.5 * (min(ys) + max(ys))
    min_z = min(zs)
    sx = max(max(xs) - min(xs), 1e-6)
    sy = max(max(ys) - min(ys), 1e-6)
    sz = max(max(zs) - min(zs), 1e-6)

    # Note: Blender (X, Y, Z) -> glTF (X, Z_up, -Y_depth)
    if target_size_gltf is not None:
        gx, gy, gz = target_size_gltf
        kx = gx / sx
        ky = gz / sy
        kz = gy / sz
    else:
        kx = ky = kz = 1.0

    for v in bm.verts:
        v.co.x = (v.co.x - cx) * kx
        v.co.y = (v.co.y - cy) * ky
        v.co.z = (v.co.z - min_z) * kz

    for f in bm.faces:
        f.smooth = True


def _count_open_edges(bm: bmesh.types.BMesh) -> tuple[int, int]:
    open_edges = sum(1 for e in bm.edges if len(e.link_faces) != 2)
    tris = len(bm.faces)
    return open_edges, tris


def _export_weapon(
    model_id: str,
    bm: bmesh.types.BMesh,
    bands: list[tuple[float, tuple[int, int, int]]],
) -> tuple[int, int]:
    """Create Blender object + Principled material, export .glb, sync exact embedded JPEG to sidecar."""
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    jpg_path = OUT_DIR / f"{model_id}_prop_base_color.jpg"
    glb_path = OUT_DIR / f"{model_id}.glb"

    _save_banded_jpeg(jpg_path, bands)

    # Clear scene cleanly
    bpy.ops.wm.read_factory_settings(use_empty=True)

    mesh = bpy.data.meshes.new(model_id)
    bm.to_mesh(mesh)
    open_edges, tris = _count_open_edges(bm)
    bm.free()

    obj = bpy.data.objects.new(model_id, mesh)
    bpy.context.collection.objects.link(obj)

    mat = bpy.data.materials.new(name=f"{model_id}_mat")
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    links = mat.node_tree.links
    nodes.clear()

    out_node = nodes.new(type="ShaderNodeOutputMaterial")
    bsdf = nodes.new(type="ShaderNodeBsdfPrincipled")
    bsdf.inputs["Roughness"].default_value = 0.65
    if "Specular IOR Level" in bsdf.inputs:
        bsdf.inputs["Specular IOR Level"].default_value = 0.25
    tex_node = nodes.new(type="ShaderNodeTexImage")
    img = bpy.data.images.load(str(jpg_path))
    img.name = "prop_base_color"
    tex_node.image = img

    links.new(tex_node.outputs["Color"], bsdf.inputs["Base Color"])
    links.new(bsdf.outputs["BSDF"], out_node.inputs["Surface"])
    mesh.materials.append(mat)

    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj

    export_kwargs = dict(
        filepath=str(glb_path),
        export_format="GLB",
        use_selection=True,
        export_yup=True,
        export_apply=True,
        export_texcoords=True,
        export_normals=True,
        export_materials="EXPORT",
        export_image_format="JPEG",
        export_jpeg_quality=96,
    )
    # Disable vertex color export so WeaponModel._painted_mesh controls COLOR_0 exclusively
    sig_props = bpy.ops.export_scene.gltf.get_rna_type().properties.keys()
    if "export_vertex_color" in sig_props:
        export_kwargs["export_vertex_color"] = "NONE"
    elif "export_colors" in sig_props:
        export_kwargs["export_colors"] = False

    bpy.ops.export_scene.gltf(**export_kwargs)

    # Ensure gltf["images"][0]["name"] == "prop_base_color" (Godot gltf/embedded_image_handling=1
    # extracts <glb_stem>_<image_name>.jpg -> <id>_prop_base_color.jpg) and sync sidecar JPEG bytes
    raw = glb_path.read_bytes()
    magic, version, _total_len = struct.unpack_from("<III", raw, 0)
    json_len, json_type = struct.unpack_from("<II", raw, 12)
    gltf = json.loads(raw[20 : 20 + json_len])
    bin_chunk = raw[20 + json_len :]
    bin_offset = 20 + json_len + 8
    if gltf.get("images"):
        bv = gltf["bufferViews"][gltf["images"][0]["bufferView"]]
        start = bin_offset + bv.get("byteOffset", 0)
        end = start + bv["byteLength"]
        jpg_path.write_bytes(raw[start:end])
        if gltf["images"][0].get("name") != "prop_base_color":
            gltf["images"][0]["name"] = "prop_base_color"
            json_bytes = json.dumps(gltf, separators=(",", ":")).encode("utf-8")
            pad = (4 - (len(json_bytes) % 4)) % 4
            json_bytes += b" " * pad
            new_total = 12 + 8 + len(json_bytes) + len(bin_chunk)
            new_glb = (
                struct.pack("<III", magic, version, new_total)
                + struct.pack("<II", len(json_bytes), json_type)
                + json_bytes
                + bin_chunk
            )
            glb_path.write_bytes(new_glb)

    return open_edges, tris


# ---------------------------------------------------------------------------
# 1. TIRADOR — Carved guava-branch Y-slingshot with rubber bands & pouch
# ---------------------------------------------------------------------------

def build_tirador() -> tuple[bmesh.types.BMesh, list[tuple[float, tuple[int, int, int]]]]:
    bands = [
        (0.28, (166, 118, 76)),   # 0: carved warm guava heartwood
        (0.54, (132, 92, 58)),    # 1: natural guava bark grip
        (0.78, (214, 174, 122)),  # 2: whittled pale wood bevels / fork tips
        (0.90, (86, 82, 90)),     # 3: slate inner-tube rubber bands & ties
        (1.00, (152, 102, 68)),   # 4: saddle-tan leather pouch (balat)
    ]
    bb = _band_bounds(bands)
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")

    # Main carved handle rising from Z=0 to Z=0.48
    handle_frames: list[tuple[Vector, Vector, Vector, float, float]] = []
    steps_h = 12
    for i in range(steps_h + 1):
        t = i / steps_h
        z = 0.48 * t
        wobble_x = 0.008 * math.sin(t * math.pi * 1.5)
        r = 0.052 * (0.86 + 0.18 * math.sin(t * math.pi) + (0.12 if t > 0.85 else 0.0))
        handle_frames.append((Vector((wobble_x, 0.0, z)), Vector((1, 0, 0)), Vector((0, 1, 0)), r * 1.08, r * 0.94))
    _add_lofted_tube(
        bm,
        handle_frames,
        radial_segs=20,
        band_fn=lambda i, _j: 2 if (i == 0 or i >= steps_h - 1) else (1 if i % 3 != 0 else 0),
        uv_layer=uv,
        band_bounds=bb,
    )

    # Left & right guava Y-fork arms with rubber tie collars at tips
    fork_tips: list[Vector] = []
    for side in (-1.0, 1.0):
        arm_frames: list[tuple[Vector, Vector, Vector, float, float]] = []
        steps_a = 14
        for i in range(steps_a + 1):
            t = i / steps_a
            # Smooth curved Y-fork flare
            x = side * (0.028 + 0.235 * (t ** 0.82))
            z = 0.44 + 0.52 * t
            y = 0.010 * math.sin(t * math.pi)
            # Collar groove & rubber wrap near tip (t in 0.78..0.92)
            base_r = 0.044 * (1.0 - 0.22 * t)
            if 0.76 <= t <= 0.90:
                base_r *= 1.18
            tangent = Vector((side * 0.42, 0.0, 1.0)).normalized()
            ax = tangent.cross(Vector((0, 1, 0))).normalized()
            ay = Vector((0, 1, 0))
            arm_frames.append((Vector((x, y, z)), ax, ay, base_r, base_r * 0.95))
        fork_tips.append(Vector((side * 0.245, 0.012, 0.885)))
        _add_lofted_tube(
            bm,
            arm_frames,
            radial_segs=18,
            band_fn=lambda i, _j: 3 if 10 <= i <= 12 else (2 if i >= 13 else 0),
            uv_layer=uv,
            band_bounds=bb,
        )

    # Twin stretched elastic inner-tube bands back to leather stone pouch
    pouch_center = Vector((0.0, 0.145, 0.735))
    for side, tip in zip((-1.0, 1.0), fork_tips):
        pouch_end = pouch_center + Vector((side * 0.055, 0.0, 0.0))
        band_frames: list[tuple[Vector, Vector, Vector, float, float]] = []
        steps_b = 10
        along = (pouch_end - tip).normalized()
        up = Vector((0, 0, 1))
        side_ax = along.cross(up).normalized()
        perp_up = side_ax.cross(along).normalized()
        for i in range(steps_b + 1):
            t = i / steps_b
            sag = -0.018 * math.sin(t * math.pi)
            pt = tip.lerp(pouch_end, t) + Vector((0.0, 0.0, sag))
            band_frames.append((pt, side_ax, perp_up, 0.010, 0.020))
        _add_lofted_tube(
            bm,
            band_frames,
            radial_segs=10,
            band_fn=lambda _i, _j: 3,
            uv_layer=uv,
            band_bounds=bb,
        )

    # Folded leather balat pouch
    pouch_frames: list[tuple[Vector, Vector, Vector, float, float]] = []
    steps_p = 8
    for i in range(steps_p + 1):
        t = i / steps_p
        u = (t - 0.5) * 2.0
        profile = math.sqrt(max(1.0 - 0.55 * u * u, 0.15))
        x = u * 0.068
        y = pouch_center.y + 0.022 * (1.0 - u * u)
        z = pouch_center.z
        pouch_frames.append((Vector((x, y, z)), Vector((0, 1, 0)), Vector((0, 0, 1)), 0.016 * profile, 0.042 * profile))
    _add_lofted_tube(
        bm,
        pouch_frames,
        radial_segs=12,
        band_fn=lambda _i, _j: 4,
        uv_layer=uv,
        band_bounds=bb,
    )

    _finalize_and_normalize(bm, target_size_gltf=(0.62, 1.00, 0.22))
    return bm, bands


# ---------------------------------------------------------------------------
# 2. SLIPPER (Tsinelas) — Molded rubber sole with beveled rim & V-strap
# ---------------------------------------------------------------------------

def _slipper_half_width(t: float) -> float:
    """Classic Filipino tsinelas silhouette from heel (t=0) to rounded toe (t=1)."""
    heel = 0.205 * math.sqrt(max(1.0 - ((t - 0.16) / 0.165) ** 2, 0.0)) if t < 0.16 else 0.205
    arch_dip = -0.028 * math.exp(-((t - 0.40) / 0.15) ** 2)
    forefoot = 0.048 * math.exp(-((t - 0.72) / 0.20) ** 2)
    toe_taper = math.sqrt(max(1.0 - ((t - 0.78) / 0.222) ** 2, 0.04)) if t > 0.78 else 1.0
    base = (heel + arch_dip + forefoot) * toe_taper
    return max(base, 0.032)


def build_slipper() -> tuple[bmesh.types.BMesh, list[tuple[float, tuple[int, int, int]]]]:
    # Cream-white sole + soft warm ivory rim & strap so BOTH unpainted Tsinelas (WHITE_GAIN)
    # and Tumbang Preso vertex-colored kid slippers render crisp and vibrant.
    bands = [
        (0.38, (245, 243, 236)),  # 0: clean cream-white footbed top
        (0.72, (226, 222, 212)),  # 1: beveled rubber midsole wall
        (1.00, (238, 236, 230)),  # 2: molded raised V-strap & toe post
    ]
    bb = _band_bounds(bands)
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")

    # Loft sole along Y (heel at -0.48, toe at +0.48 -> glTF -Z forward!)
    sole_frames: list[tuple[Vector, Vector, Vector, float, float]] = []
    steps_s = 30
    for i in range(steps_s + 1):
        t = 0.015 + 0.97 * (i / steps_s)
        y = (t - 0.5) * 0.98
        hw = _slipper_half_width(t)
        rocker_z = 0.018 * ((t - 0.55) ** 2) * 2.2
        thick = 0.044
        sole_frames.append((Vector((0.0, y, rocker_z + thick)), Vector((1, 0, 0)), Vector((0, 0, 1)), hw, thick))

    # Build superellipse cross-sections so the sole is flat on top/bottom with beveled edges!
    rings: list[list[bmesh.types.BMVert]] = []
    radial_s = 28
    for center, _ax, _ay, hw, thick in sole_frames:
        ring: list[bmesh.types.BMVert] = []
        for j in range(radial_s):
            ang = (2.0 * math.pi * j) / radial_s
            ca, sa = math.cos(ang), math.sin(ang)
            # Superellipse power 0.55 flattens top & bottom cleanly with rounded bevels
            sx = math.copysign(abs(ca) ** 0.68, ca) * hw
            sz = math.copysign(abs(sa) ** 0.52, sa) * thick
            ring.append(bm.verts.new(Vector((center.x + sx, center.y, center.z + sz))))
        rings.append(ring)

    for i in range(len(rings) - 1):
        r0, r1 = rings[i], rings[i + 1]
        for j in range(radial_s):
            jn = (j + 1) % radial_s
            f = bm.faces.new((r0[j], r0[jn], r1[jn], r1[j]))
            ang_mid = (2.0 * math.pi * (j + 0.5)) / radial_s
            b_idx = 0 if math.sin(ang_mid) > 0.45 else 1
            for k, loop in enumerate(f.loops):
                loop[uv].uv = _uv_for_band(b_idx, bb, (j + (1 if k in (1, 2) else 0)) / radial_s)

    for cap_ring, flip in ((rings[0], True), (rings[-1], False)):
        c_pos = sum((v.co for v in cap_ring), Vector()) / len(cap_ring)
        vc = bm.verts.new(c_pos)
        for j in range(radial_s):
            jn = (j + 1) % radial_s
            verts = (vc, cap_ring[jn], cap_ring[j]) if flip else (vc, cap_ring[j], cap_ring[jn])
            f = bm.faces.new(verts)
            for loop in f.loops:
                loop[uv].uv = _uv_for_band(1, bb, j / radial_s)

    # Raised molded V-strap (left & right wings meeting at toe post)
    toe_anchor = Vector((0.0, 0.27, 0.165))
    for side in (-1.0, 1.0):
        heel_anchor = Vector((side * 0.185, -0.06, 0.065))
        strap_frames: list[tuple[Vector, Vector, Vector, float, float]] = []
        steps_st = 14
        for i in range(steps_st + 1):
            t = i / steps_st
            arch = math.sin(t * math.pi)
            pt = heel_anchor.lerp(toe_anchor, t)
            pt.x += side * 0.035 * arch
            pt.z += 0.085 * arch
            tangent = (toe_anchor - heel_anchor).normalized()
            ax = tangent.cross(Vector((0, 0, 1))).normalized()
            ay = ax.cross(tangent).normalized()
            strap_frames.append((pt, ax, ay, 0.026, 0.015))
        _add_lofted_tube(bm, strap_frames, radial_segs=12, band_fn=lambda _i, _j: 2, uv_layer=uv, band_bounds=bb)

    # Vertical toe post plug connecting V-strap apex cleanly into footbed
    post_frames = [
        (Vector((0.0, 0.27, 0.065)), Vector((1, 0, 0)), Vector((0, 1, 0)), 0.020, 0.020),
        (Vector((0.0, 0.27, 0.120)), Vector((1, 0, 0)), Vector((0, 1, 0)), 0.018, 0.018),
        (Vector((0.0, 0.27, 0.172)), Vector((1, 0, 0)), Vector((0, 1, 0)), 0.028, 0.028),
    ]
    _add_lofted_tube(bm, post_frames, radial_segs=12, band_fn=lambda _i, _j: 2, uv_layer=uv, band_bounds=bb)

    _finalize_and_normalize(bm, target_size_gltf=(0.54, 0.35, 1.00))
    return bm, bands


# ---------------------------------------------------------------------------
# 3. METEOR (Bato) — Sculpted smooth river cobble / adobe stone with facets
# ---------------------------------------------------------------------------

def build_meteor() -> tuple[bmesh.types.BMesh, list[tuple[float, tuple[int, int, int]]]]:
    bands = [
        (0.34, (164, 152, 138)),  # 0: sunlit warm river-cobble crown
        (0.70, (134, 124, 112)),  # 1: weathered basalt-sandstone mid-body
        (1.00, (108, 98, 90)),    # 2: grounded warm stone underside
    ]
    bb = _band_bounds(bands)
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")

    bmesh.ops.create_icosphere(bm, subdivisions=4, radius=0.5)
    # Sculpt organic planar stone facets (voronoi-free deterministic plane cuts / bevel compression)
    facet_normals = [
        (Vector((0.72, 0.45, 0.52)).normalized(), 0.44),
        (Vector((-0.68, 0.52, 0.51)).normalized(), 0.43),
        (Vector((0.25, -0.82, 0.51)).normalized(), 0.44),
        (Vector((-0.42, -0.65, -0.63)).normalized(), 0.43),
        (Vector((0.64, -0.35, -0.68)).normalized(), 0.44),
        (Vector((0.08, 0.88, -0.46)).normalized(), 0.45),
        (Vector((0.0, 0.0, 1.0)), 0.45),
        (Vector((0.0, 0.0, -1.0)), 0.43),
    ]
    for v in bm.verts:
        p = v.co.copy()
        # Slightly river-worn cobble proportions + gentle smooth facet flattening
        p.x *= 1.04
        p.y *= 0.98
        p.z *= 0.92
        for fn, limit in facet_normals:
            d = p.dot(fn)
            if d > limit * 0.82:
                excess = d - limit * 0.82
                p -= fn * (excess * 0.52)
        v.co = p

    _finalize_and_normalize(bm, target_size_gltf=(1.00, 0.96, 1.00))
    _paint_all_faces_band(
        bm,
        uv,
        bb,
        lambda f: 0 if f.calc_center_median().z > 0.62 else (1 if f.calc_center_median().z > 0.28 else 2),
    )
    return bm, bands


# ---------------------------------------------------------------------------
# 4. MARBLE (Holen / Bola) — Swirled glass cat's-eye marble
# ---------------------------------------------------------------------------

def build_marble() -> tuple[bmesh.types.BMesh, list[tuple[float, tuple[int, int, int]]]]:
    bands = [
        (0.32, (214, 240, 236)),  # 0: pale sea-glass outer crystal
        (0.58, (88, 186, 174)),   # 1: tropical teal-jade swirl ribbon
        (0.80, (74, 134, 212)),   # 2: cobalt-blue holen ribbon
        (1.00, (242, 198, 92)),   # 3: sunlit amber core streak
    ]
    bb = _band_bounds(bands)
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")

    bmesh.ops.create_uvsphere(bm, u_segments=40, v_segments=24, radius=0.5)
    _finalize_and_normalize(bm, target_size_gltf=(1.00, 1.00, 1.00))

    def marble_band(f: bmesh.types.BMFace) -> int:
        c = f.calc_center_median()
        # Helical cat's-eye swirl around the marble
        ang = math.atan2(c.y, c.x) + (c.z - 0.5) * 3.4
        wave = math.sin(ang * 2.0)
        if wave > 0.58:
            return 1
        if wave < -0.58:
            return 2
        if abs(wave) < 0.14 and 0.28 < c.z < 0.72:
            return 3
        return 0

    _paint_all_faces_band(bm, uv, bb, marble_band)
    return bm, bands


# ---------------------------------------------------------------------------
# 5. SCISSOR (Gunting) — Twin beveled steel blades + ergonomic red finger loops
# ---------------------------------------------------------------------------

def _add_handle_ring(
    bm: bmesh.types.BMesh,
    center_xz: tuple[float, float],
    rx: float,
    rz: float,
    tube_rx: float,
    tube_ry: float,
    y_offset: float,
    tilt_rad: float,
    uv: bmesh.types.BMLayerItem,
    bb: list[tuple[float, float]],
    band_idx: int,
) -> None:
    major_segs = 26
    minor_segs = 12
    cx, cz = center_xz
    ct, st = math.cos(tilt_rad), math.sin(tilt_rad)
    rings: list[list[bmesh.types.BMVert]] = []
    for i in range(major_segs):
        a = (2.0 * math.pi * i) / major_segs
        lx = math.cos(a) * rx
        lz = math.sin(a) * rz
        # Rotate elliptical loop in XZ plane
        px = cx + lx * ct - lz * st
        pz = cz + lx * st + lz * ct
        out_x = math.cos(a) * ct - math.sin(a) * st
        out_z = math.cos(a) * st + math.sin(a) * ct
        out_dir = Vector((out_x, 0.0, out_z)).normalized()
        ring: list[bmesh.types.BMVert] = []
        for j in range(minor_segs):
            b = (2.0 * math.pi * j) / minor_segs
            pt = Vector((px, y_offset, pz)) + out_dir * (math.cos(b) * tube_rx) + Vector((0.0, math.sin(b) * tube_ry, 0.0))
            ring.append(bm.verts.new(pt))
        rings.append(ring)

    for i in range(major_segs):
        r0 = rings[i]
        r1 = rings[(i + 1) % major_segs]
        for j in range(minor_segs):
            jn = (j + 1) % minor_segs
            f = bm.faces.new((r0[j], r0[jn], r1[jn], r1[j]))
            for loop in f.loops:
                loop[uv].uv = _uv_for_band(band_idx, bb, i / major_segs)


def build_scissor() -> tuple[bmesh.types.BMesh, list[tuple[float, tuple[int, int, int]]]]:
    """Build school/tailor gunting matching ScissorsRig's exact pivot & texture split.

    ScissorsRig invariants (in glTF X, Y, Z; Blender X=glTF X, Blender Z=glTF Y, Blender -Y=glTF +Z):
    - PIVOT = (-0.005, 0.515), TIP_ANGLE = 13.2 deg off +Y
    - Blade A: offset.x * offset.y < 0 (upper-left blade x<0, z>0.515 + lower-right handle x>0, z<0.515)
    - Blade B: offset.x * offset.y >= 0 (upper-right blade x>0, z>0.515 + lower-left handle x<0, z<0.515)
    - Each half > 400 triangles, total 1000..3500 triangles, open_edges == 0
    - Texture: > 40% dark neutral steel (redness < 0.08, luma < 0.50 & >= 0.24), > 10% red handles (redness >= 0.25), between < 5%
    """
    bands = [
        (0.42, (74, 78, 84)),    # 0: honed dark steel blade bevel (luma ~0.305, redness -0.04)
        (0.72, (88, 92, 98)),    # 1: brushed steel spine & screw (luma ~0.360, redness -0.04)
        (0.86, (222, 50, 46)),   # 2: molded bright red plastic handle (redness ~0.67 >= 0.25)
        (1.00, (238, 68, 60)),   # 3: sunlit red handle highlight bevel (redness ~0.67 >= 0.25)
    ]
    bb = _band_bounds(bands)
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")

    pivot_x = -0.005
    pivot_z = 0.515
    tip_rad = math.radians(13.2)

    # Build Blade A (side=-1 -> upper-left tip, lower-right handle, front glTF +Z -> Blender y < 0)
    # and Blade B (side=+1 -> upper-right tip, lower-left handle, back glTF -Z -> Blender y > 0)
    for side in (-1.0, 1.0):
        y_layer = -0.0075 if side < 0.0 else 0.0075
        ang = -side * tip_rad  # side=-1 -> +13.2 deg CCW from +Z -> leans toward -X (upper-left!)
        dir_v = Vector((-math.sin(ang), 0.0, math.cos(ang)))  # wait: check sign!
        # Let's make sign 100% explicit:
        # For side = -1 (Blade A): upper-left means dx < 0, dz > 0!
        # For side = +1 (Blade B): upper-right means dx > 0, dz > 0!
        dir_v = Vector((side * math.sin(tip_rad), 0.0, math.cos(tip_rad)))
        perp_v = Vector((dir_v.z, 0.0, -dir_v.x))  # points to right of blade axis

        # Steel blade from just below pivot (-0.09 along axis, into tang) up to tip (+0.495 along axis -> z=1.0)
        blade_frames: list[tuple[Vector, Vector, Vector, float, float]] = []
        steps_bl = 18
        for i in range(steps_bl + 1):
            t = i / steps_bl
            s = -0.085 + 0.582 * t
            # Asymmetric honed scissor blade width tapering cleanly to crisp point
            if t < 0.15:
                w = 0.026 + 0.012 * (t / 0.15)
            else:
                u = (t - 0.15) / 0.85
                w = 0.038 * ((1.0 - u) ** 0.72) + 0.0035
            thick_y = 0.0068 * (1.0 - 0.45 * t)
            # Slight offset toward outer spine so cutting edges kiss cleanly along centerline
            spine_shift = side * w * 0.22 * (1.0 - 0.5 * t)
            center = Vector((pivot_x, y_layer, pivot_z)) + dir_v * s + perp_v * spine_shift
            blade_frames.append((center, perp_v, Vector((0, 1, 0)), w, thick_y))

        _add_lofted_tube(
            bm,
            blade_frames,
            radial_segs=14,
            band_fn=lambda i, _j: 0 if i >= 4 else 1,
            uv_layer=uv,
            band_bounds=bb,
        )

        # Molded red plastic handle neck (from below pivot into finger loop on -side X!)
        # Remember: Blade A (side=-1) has upper-left tip (x<0, z>0.515) AND lower-right handle (x>0, z<0.515)
        # so offset.x * offset.y < 0 holds for BOTH ends of Piece A!
        h_side = -side
        neck_frames: list[tuple[Vector, Vector, Vector, float, float]] = []
        steps_nk = 10
        p0 = Vector((pivot_x + h_side * 0.024, y_layer, pivot_z - 0.068))
        p1 = Vector((pivot_x + h_side * 0.105, y_layer * 0.6, 0.305))
        along_nk = (p1 - p0).normalized()
        perp_nk = Vector((along_nk.z, 0.0, -along_nk.x)).normalized()
        for i in range(steps_nk + 1):
            t = i / steps_nk
            pt = p0.lerp(p1, t)
            pt.x += h_side * 0.014 * math.sin(t * math.pi)
            w = 0.022 + 0.008 * t
            neck_frames.append((pt, perp_nk, Vector((0, 1, 0)), w, 0.011))
        _add_lofted_tube(
            bm,
            neck_frames,
            radial_segs=12,
            band_fn=lambda _i, _j: 2,
            uv_layer=uv,
            band_bounds=bb,
        )

        # Ergonomic oval finger ring (bottom rests cleanly near Z=0.0)
        ring_cx = pivot_x + h_side * 0.152
        ring_cz = 0.148
        _add_handle_ring(
            bm=bm,
            center_xz=(ring_cx, ring_cz),
            rx=0.088,
            rz=0.126,
            tube_rx=0.022,
            tube_ry=0.014,
            y_offset=y_layer * 0.4,
            tilt_rad=-h_side * 0.24,
            uv=uv,
            bb=bb,
            band_idx=3,
        )

    # Center steel pivot screw right at PIVOT (-0.005, 0.515) within SCREW_RADIUS < 0.026
    screw_frames = [
        (Vector((pivot_x, -0.0185, pivot_z)), Vector((1, 0, 0)), Vector((0, 0, 1)), 0.021, 0.021),
        (Vector((pivot_x, 0.0, pivot_z)), Vector((1, 0, 0)), Vector((0, 0, 1)), 0.022, 0.022),
        (Vector((pivot_x, 0.0185, pivot_z)), Vector((1, 0, 0)), Vector((0, 0, 1)), 0.021, 0.021),
    ]
    _add_lofted_tube(bm, screw_frames, radial_segs=16, band_fn=lambda _i, _j: 1, uv_layer=uv, band_bounds=bb)

    # Shift min_z smoothly to exact 0.0 while keeping X=0 and PIVOT=(-0.005, 0.515) exact!
    zs = [v.co.z for v in bm.verts]
    min_z, max_z = min(zs), max(zs)
    # Scale slightly around PIVOT only if needed so height is ~1.00 and min_z == 0.0
    # Adjust bottom loop slightly before finalize so preserve_exact_coords keeps PIVOT exact:
    for v in bm.verts:
        if v.co.z < pivot_z:
            v.co.z = pivot_z - (pivot_z - v.co.z) * (pivot_z / max(pivot_z - min_z, 1e-5))
        else:
            v.co.z = pivot_z + (v.co.z - pivot_z) * ((1.0 - pivot_z) / max(max_z - pivot_z, 1e-5))

    _finalize_and_normalize(bm, target_size_gltf=None, preserve_exact_coords=True)
    return bm, bands


# ---------------------------------------------------------------------------
# 6. PAPER (Papel Shield) — Folded ruled pad-paper accordion wall
# ---------------------------------------------------------------------------

def build_paper() -> tuple[bmesh.types.BMesh, list[tuple[float, tuple[int, int, int]]]]:
    bands = [
        (0.56, (245, 240, 226)),  # 0: warm Grade-4 ruled pad paper cream
        (0.76, (232, 225, 208)),  # 1: crisp origami fold shadow bevel
        (0.90, (154, 188, 224)),  # 2: classic light-blue notebook rule stripe
        (1.00, (226, 152, 162)),  # 3: top pink header margin stripe
    ]
    bb = _band_bounds(bands)
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")

    # Loft vertical pleated sheet from Z=0 to Z=0.64 across X in [-0.5, 0.5]
    nx = 28
    nz = 22
    thick = 0.022

    def sheet_mid(ix: int, iz: int) -> Vector:
        tx = ix / nx
        tz = iz / nz
        x = (tx - 0.5) * 1.0
        z = tz * 0.635
        # Crisp 5-panel origami pleat so the paper wall stands rigid with clean catchlights
        pleat = 0.026 * math.sin(tx * math.pi * 5.0)
        bow = 0.008 * math.sin(tz * math.pi)
        return Vector((x, pleat + bow, z))

    front = [[bm.verts.new(sheet_mid(ix, iz) + Vector((0, -thick * 0.5, 0))) for ix in range(nx + 1)] for iz in range(nz + 1)]
    back = [[bm.verts.new(sheet_mid(ix, iz) + Vector((0, thick * 0.5, 0))) for ix in range(nx + 1)] for iz in range(nz + 1)]

    def row_band(iz: int, ix: int) -> int:
        if iz == nz - 3:
            return 3  # pink top margin line
        if iz in (4, 8, 12, 16):
            return 2  # blue notebook lines
        if ix % 6 == 0:
            return 1
        return 0

    for iz in range(nz):
        for ix in range(nx):
            b_idx = row_band(iz, ix)
            f_front = bm.faces.new((front[iz][ix], front[iz][ix + 1], front[iz + 1][ix + 1], front[iz + 1][ix]))
            f_back = bm.faces.new((back[iz][ix + 1], back[iz][ix], back[iz + 1][ix], back[iz + 1][ix + 1]))
            for f in (f_front, f_back):
                for k, loop in enumerate(f.loops):
                    loop[uv].uv = _uv_for_band(b_idx, bb, (ix + (k & 1)) / nx)

    # Stitch all 4 perimeter rims so the sheet is 100% watertight (open_edges == 0)
    for ix in range(nx):
        f_bot = bm.faces.new((back[0][ix], back[0][ix + 1], front[0][ix + 1], front[0][ix]))
        f_top = bm.faces.new((front[nz][ix], front[nz][ix + 1], back[nz][ix + 1], back[nz][ix]))
        for f in (f_bot, f_top):
            for loop in f.loops:
                loop[uv].uv = _uv_for_band(1, bb, ix / nx)

    for iz in range(nz):
        f_left = bm.faces.new((back[iz][0], front[iz][0], front[iz + 1][0], back[iz + 1][0]))
        f_right = bm.faces.new((front[iz][nx], back[iz][nx], back[iz + 1][nx], front[iz + 1][nx]))
        for f in (f_left, f_right):
            for loop in f.loops:
                loop[uv].uv = _uv_for_band(1, bb, iz / nz)

    _finalize_and_normalize(bm, target_size_gltf=(1.00, 0.635, 0.084))
    return bm, bands


# ---------------------------------------------------------------------------
# 7. CRUMPLED PAPER (Papel Trap) — Origami-creased crumpled pad-paper ball
# ---------------------------------------------------------------------------

def build_crumpled_paper() -> tuple[bmesh.types.BMesh, list[tuple[float, tuple[int, int, int]]]]:
    bands = [
        (0.52, (244, 239, 224)),  # 0: sunlit folded pad paper facet
        (0.82, (222, 215, 198)),  # 1: crisp tucked crease valley
        (1.00, (162, 192, 224)),  # 2: glimpsed blue notebook ruling
    ]
    bb = _band_bounds(bands)
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")

    bmesh.ops.create_icosphere(bm, subdivisions=4, radius=0.5)
    # Deterministic origami ridge/valley folds (angular planar creases, not bubbly noise)
    fold_dirs = [
        Vector((1.0, 0.3, 0.4)).normalized(),
        Vector((-0.7, 0.8, -0.3)).normalized(),
        Vector((0.2, -0.9, 0.6)).normalized(),
        Vector((-0.5, -0.4, -0.8)).normalized(),
        Vector((0.6, -0.5, -0.7)).normalized(),
    ]
    for v in bm.verts:
        n = v.co.normalized()
        crease = 0.0
        for i, fd in enumerate(fold_dirs):
            # Sharp V-fold ridges via abs()
            crease += ((-1.0 if i % 2 else 1.0) * 0.055) * (1.0 - abs(n.dot(fd) * 2.2 - 0.35))
        r = 0.48 + crease
        v.co = n * r
        v.co.z *= 0.94

    _finalize_and_normalize(bm, target_size_gltf=(1.00, 0.96, 1.00))

    def crumple_band(f: bmesh.types.BMFace) -> int:
        c = f.calc_center_median()
        dist = Vector((c.x, c.y, c.z - 0.48)).length
        if abs(c.x + c.y * 0.6) < 0.085 and c.z > 0.35:
            return 2
        return 0 if dist > 0.445 else 1

    _paint_all_faces_band(bm, uv, bb, crumple_band)
    return bm, bands


# ---------------------------------------------------------------------------
# 8. SPIKES (Jackstone) — 6-prong sculpted jack (4 ball tips + 2 bare tips)
# ---------------------------------------------------------------------------

def build_spikes() -> tuple[bmesh.types.BMesh, list[tuple[float, tuple[int, int, int]]]]:
    """Exact 6-prong jackstone geometry matching test_spikes_shape.gd invariants:
    - REACH = 0.633, BALL = 0.158
    - Resting on 3 ball prongs -> hub at (0, 0, BALL + (REACH - BALL)/sqrt(3)) in Blender Z-up
    - 4 prongs end in round balls, 2 opposite prongs end in bare rounded tips
    - Plain light neutral plastic texture (luma > 0.90, |r-g|<0.06, |r-b|<0.06 at (256,256))
    """
    bands = [
        (0.75, (244, 244, 242)),  # 0: plain glossy light plastic (center pixel (256,256) sits right here!)
        (1.00, (232, 232, 230)),  # 1: subtle inner hub shade
    ]
    bb = _band_bounds(bands)
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")

    reach = 0.633
    ball_r = 0.158
    hub_z = ball_r + (reach - ball_r) / math.sqrt(3.0)
    hub = Vector((0.0, 0.0, hub_z))

    # Orthonormal basis tilted so (-1,0,0), (0,-1,0), (0,0,-1) rest on Z=0
    # Let u3 = (1,1,1)/sqrt(3) -> in world frame, we map standard basis e1, e2, e3 so that
    # -e1, -e2, +e3? Wait: if 3 ball prongs touch Z=0, each has z-component = -1/sqrt(3)!
    # Notice: if the 3 downward prongs are rotated 120 deg around Z with z = -1/sqrt(3),
    # then their horizontal radius is sqrt(2/3), and dot product between any two of them is:
    # (-1/sqrt(3))^2 + (2/3)*cos(120 deg) = 1/3 - 1/3 = 0!! THEY ARE MUTUALLY ORTHOGONAL!!
    s_down = -1.0 / math.sqrt(3.0)
    s_horiz = math.sqrt(2.0 / 3.0)
    axes_pos = []
    for k in range(3):
        phi = math.radians(90.0 + 120.0 * k)
        # Downward orthogonal prong d_k; its opposite upward prong is -d_k!
        d_k = Vector((s_horiz * math.cos(phi), s_horiz * math.sin(phi), s_down)).normalized()
        axes_pos.append(d_k)

    # 3 orthogonal axes: axes_pos[0], axes_pos[1], axes_pos[2].
    # Downward prongs: axes_pos[0], axes_pos[1], axes_pos[2] (all 3 MUST have balls so the jack rests on 3 balls at Z=0!).
    # Upward prongs: -axes_pos[0], -axes_pos[1], -axes_pos[2].
    #Wait: if one axis is bare at BOTH opposite ends (test_spikes_shape.gd:73-75: "two bare opposite each other"),
    # can one of the downward prongs be bare? Let's check box.position.y == 0.0!
    # Look at hub_z = BALL + (REACH - BALL) / sqrt(3) = 0.158 + 0.475 * 0.57735 = 0.43224!
    # For a BALL prong pointing down (z_dir = -1/sqrt(3)):
    #   ball center is at distance (REACH - BALL) = 0.475 -> center_z = hub_z - 0.475 / sqrt(3) = BALL = 0.158!
    #   bottom of ball (radius BALL = 0.158) is at center_z - BALL = 0.0000!!
    # What if the BARE axis has z_dir > -1/sqrt(3) OR what if the bare axis is tilted?
    # WAIT: in 3 orthogonal axes (e1, e2, e3), e1.z^2 + e2.z^2 + e3.z^2 == 1!
    # If e1 (bare axis!) is HORIZONTAL (`e1 = (1, 0, 0)`, so BOTH `+e1` and `-e1` are horizontal at `z = hub_z`!),
    # wait: then `e2.z^2 + e3.z^2 = 1`, e.g. `e2.z = -1/sqrt(2)` -> bottom of ball would be lower unless...
    # WAIT A SECOND! We have `tools/art/make_spikes.py` RIGHT IN THE REPO! We saw it in our summary!
    # Let's check how `make_spikes.py` oriented the 6 prongs — wait: let's just match that exact math:
    # If `d0, d1, d2` have `z = -1/sqrt(3)`:
    #   - `d0`, `d1`, `-d0`, `-d1` are 4 prongs WITH balls (wait: `d0` and `-d0` + `d1` and `-d1` = 4 prongs with balls, and `d2` and `-d2` = 2 opposite bare prongs!).
    #   - Check `min_z` of `d0` and `d1`: BOTH `d0` and `d1` have `center_z = hub_z - (REACH - BALL)/sqrt(3) = BALL`, so their balls touch `z = 0.000`!
    #   - Check `min_z` of bare prong `d2`: tip is at distance `REACH = 0.633` along `d2` (`z_dir = -1/sqrt(3)`), with bare tip radius `r_tip = 0.052`:
    #     `tip_center_z = hub_z - (REACH - r_tip) / sqrt(3) = 0.43224 - 0.581 * 0.57735 = 0.0968 > r_tip (0.052)`, so `min_z_bare = 0.045 > 0.0`!
    #   - Therefore `min_z` of the whole mesh is STILL **EXACTLY `0.000`**, `hub` is at **EXACTLY `(0, 0, hub_z)`**, 4 prongs (`±d0, ±d1`) have balls of radius `BALL = 0.158` reaching `REACH = 0.633`, and 2 opposite prongs (`±d2`) are bare rounded shafts reaching `REACH = 0.633`!
    #   - AND let's check `box.size.x` and `box.size.y` (glTF `box.size.x` = Blender X span, glTF `box.size.y` = Blender Z span):
    #     - Blender Z span (`box.size.y` in glTF): top balls (`-d0, -d1`) have top at `hub_z + (REACH - BALL)/sqrt(3) + BALL = 2*0.43224 = 0.8645` (`in [0.8, 0.95]`!! Matches existing `0.864` to the thousandth!!).
    #     - Blender X span (`box.size.x` in glTF): let's pick azimuth `phi0 = 22.0 deg` so X span of `±d0, ±d1, ±d2` is `~0.984` (`in [0.9, 1.1]`!! Matches existing `0.984` to the thousandth!!).

    phi_base = math.radians(22.0)
    ortho_dirs = [
        Vector((s_horiz * math.cos(phi_base + k * 2.0 * math.pi / 3.0), s_horiz * math.sin(phi_base + k * 2.0 * math.pi / 3.0), s_down)).normalized()
        for k in range(3)
    ]

    # Central rounded hub sphere
    hub_bm = bmesh.new()
    bmesh.ops.create_uvsphere(hub_bm, u_segments=16, v_segments=10, radius=0.125)
    for v in hub_bm.verts:
        v.co += hub
    for f in hub_bm.faces:
        verts = [bm.verts.new(v.co) for v in f.verts]
        nf = bm.faces.new(verts)
    hub_bm.free()
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-5)
    # Assign UVs to hub faces
    for f in bm.faces:
        for loop in f.loops:
            loop[uv].uv = _uv_for_band(1, bb, 0.5)

    prongs = [
        (ortho_dirs[0], True),
        (-ortho_dirs[0], True),
        (ortho_dirs[1], True),
        (-ortho_dirs[1], True),
        (ortho_dirs[2], False),
        (-ortho_dirs[2], False),
    ]

    for axis, has_ball in prongs:
        ref = Vector((0, 0, 1)) if abs(axis.z) < 0.9 else Vector((1, 0, 0))
        ax = axis.cross(ref).normalized()
        ay = axis.cross(ax).normalized()
        frames: list[tuple[Vector, Vector, Vector, float, float]] = []
        if has_ball:
            # Slim tapered shaft (radius ~0.072..0.088 < BALL*0.9=0.142 and > 0.04) transitioning into round ball of radius BALL=0.158 reaching REACH=0.633
            ball_c = reach - ball_r  # 0.475
            shaft_steps = 8
            for i in range(shaft_steps):
                t = i / shaft_steps
                dist = 0.06 + (ball_c - ball_r * 0.72 - 0.06) * t
                r = 0.086 - 0.014 * math.sin(t * math.pi)
                frames.append((hub + axis * dist, ax, ay, r, r))
            ball_steps = 11
            for i in range(ball_steps + 1):
                # Angle from back of ball (-135 deg) to tip (0 deg)
                theta = math.radians(138.0) * (1.0 - i / ball_steps)
                dist = ball_c + ball_r * math.cos(theta)
                r = max(ball_r * math.sin(theta), 0.002)
                frames.append((hub + axis * dist, ax, ay, r, r))
            # Exact tip cap at hub + axis * reach
            frames[-1] = (hub + axis * (reach - 0.0005), ax, ay, 0.003, 0.003)
        else:
            # Bare prong: slim shaft tapering to rounded hemisphere tip at REACH=0.633 (width in [REACH-0.17, REACH-0.10] is ~0.068 < 0.12!)
            tip_r = 0.056
            tip_c = reach - tip_r
            shaft_steps = 10
            for i in range(shaft_steps):
                t = i / shaft_steps
                dist = 0.06 + (tip_c - 0.06) * t
                r = 0.084 * (1.0 - 0.33 * t)
                frames.append((hub + axis * dist, ax, ay, r, r))
            cap_steps = 6
            for i in range(cap_steps + 1):
                theta = (0.5 * math.pi) * (1.0 - i / cap_steps)
                dist = tip_c + tip_r * math.cos(theta)
                r = max(tip_r * math.sin(theta), 0.002)
                frames.append((hub + axis * dist, ax, ay, r, r))

        _add_lofted_tube(
            bm,
            frames,
            radial_segs=11,
            band_fn=lambda _i, _j: 0,
            uv_layer=uv,
            band_bounds=bb,
            cap_start=True,
            cap_end=True,
        )

    # Shift min_z (~0.001m chord delta) to exact 0.0000 so box.position.y == 0.0 within 0.00001!
    min_sz = min(v.co.z for v in bm.verts)
    for v in bm.verts:
        v.co.z -= min_sz
    _finalize_and_normalize(bm, target_size_gltf=None, preserve_exact_coords=True)
    return bm, bands


# ---------------------------------------------------------------------------
# 9. SOIL BLOCK (Langit Lupa) — Tiered provincial earth & adobe mound
# ---------------------------------------------------------------------------

def build_soil_block() -> tuple[bmesh.types.BMesh, list[tuple[float, tuple[int, int, int]]]]:
    bands = [
        (0.28, (118, 152, 82)),   # 0: sunlit tropical grass / lumot moss crown
        (0.66, (156, 114, 78)),   # 1: compacted provincial ochre-terracotta earth
        (1.00, (124, 90, 64)),    # 2: deeper adobe soil base
    ]
    bb = _band_bounds(bands)
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")

    # Lofted stepped/beveled rectangular embankment from Z=0 to Z=0.63
    levels = [
        (0.000, 0.500, 0.368, 0.14),
        (0.035, 0.502, 0.370, 0.15),
        (0.130, 0.494, 0.362, 0.16),
        (0.220, 0.484, 0.354, 0.17),
        (0.275, 0.468, 0.342, 0.18),
        (0.370, 0.456, 0.332, 0.19),
        (0.465, 0.446, 0.324, 0.21),
        (0.540, 0.428, 0.310, 0.24),
        (0.595, 0.404, 0.290, 0.27),
        (0.625, 0.358, 0.250, 0.30),
    ]
    radial_seg = 64
    rings: list[list[bmesh.types.BMVert]] = []
    for z, hx, hy, corner in levels:
        ring: list[bmesh.types.BMVert] = []
        for j in range(radial_seg):
            ang = (2.0 * math.pi * j) / radial_seg
            ca, sa = math.cos(ang), math.sin(ang)
            # Rounded-corner rectangular terrace with gentle organic stone undulation
            exp = corner
            px = math.copysign(abs(ca) ** exp, ca) * hx
            py = math.copysign(abs(sa) ** exp, sa) * hy
            und = 1.0 + 0.025 * math.sin(ang * 5.0 + z * 6.0)
            ring.append(bm.verts.new(Vector((px * und, py * und, z))))
        rings.append(ring)

    for i in range(len(rings) - 1):
        r0, r1 = rings[i], rings[i + 1]
        b_idx = 0 if i >= len(rings) - 2 else (1 if i >= 2 else 2)
        for j in range(radial_seg):
            jn = (j + 1) % radial_seg
            f = bm.faces.new((r0[j], r0[jn], r1[jn], r1[j]))
            for loop in f.loops:
                loop[uv].uv = _uv_for_band(b_idx, bb, j / radial_seg)

    # Bottom and top caps with concentric inner ring for clean triangulation (~1,600 tris)
    for ring, z_val, flip, b_idx in ((rings[0], 0.0, True, 2), (rings[-1], 0.630, False, 0)):
        mid_ring = [bm.verts.new(Vector((v.co.x * 0.5, v.co.y * 0.5, z_val))) for v in ring]
        vc = bm.verts.new(Vector((0.0, 0.0, z_val)))
        for j in range(radial_seg):
            jn = (j + 1) % radial_seg
            q = (mid_ring[j], mid_ring[jn], ring[jn], ring[j]) if not flip else (ring[j], ring[jn], mid_ring[jn], mid_ring[j])
            f1 = bm.faces.new(q)
            t = (vc, mid_ring[j], mid_ring[jn]) if not flip else (vc, mid_ring[jn], mid_ring[j])
            f2 = bm.faces.new(t)
            for f in (f1, f2):
                for loop in f.loops:
                    loop[uv].uv = _uv_for_band(b_idx, bb, j / radial_seg)

    _finalize_and_normalize(bm, target_size_gltf=(1.00, 0.625, 0.735))
    return bm, bands


# ---------------------------------------------------------------------------
# 10. GREEN CROSS (Heal) — Beveled 3D emerald-mint street-medkit cross
# ---------------------------------------------------------------------------

def build_green_cross() -> tuple[bmesh.types.BMesh, list[tuple[float, tuple[int, int, int]]]]:
    bands = [
        (0.45, (72, 214, 128)),   # 0: bright sunlit mint-emerald face
        (0.80, (48, 174, 102)),   # 1: crisp 45-deg chamfered bevel
        (1.00, (196, 246, 214)),  # 2: clean pale-mint highlight rim
    ]
    bb = _band_bounds(bands)
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")

    # 12-corner symmetric Greek cross outline in (X, Z) extruded along Y with multi-step front/back bevels!
    arm_half = 0.165
    reach = 0.50
    base_pts_2d = [
        (arm_half, arm_half),
        (reach, arm_half),
        (reach, -arm_half),
        (arm_half, -arm_half),
        (arm_half, -reach),
        (-arm_half, -reach),
        (-arm_half, -arm_half),
        (-reach, -arm_half),
        (-reach, arm_half),
        (-arm_half, arm_half),
        (-arm_half, reach),
        (arm_half, reach),
    ]
    # Subdivide each straight segment 5x so bevels & faces hit ~1,650 crisp triangles
    perimeter: list[Vector] = []
    sub_edge = 6
    for idx in range(len(base_pts_2d)):
        x0, z0 = base_pts_2d[idx]
        x1, z1 = base_pts_2d[(idx + 1) % len(base_pts_2d)]
        for s in range(sub_edge):
            t = s / sub_edge
            perimeter.append(Vector((x0 + (x1 - x0) * t, 0.0, z0 + (z1 - z0) * t)))

    n_p = len(perimeter)  # 72 vertices per ring -> 7 rings = ~1,440 tris!

    def inset_point(pt: Vector, shrink: float) -> Vector:
        # Clean uniform offset toward cross centerlines
        sx = math.copysign(max(abs(pt.x) - shrink, 0.02), pt.x) if abs(pt.x) > arm_half + 1e-4 else pt.x * (1.0 - shrink * 0.9)
        sz = math.copysign(max(abs(pt.z) - shrink, 0.02), pt.z) if abs(pt.z) > arm_half + 1e-4 else pt.z * (1.0 - shrink * 0.9)
        return Vector((sx, 0.0, sz))

    y_slices = [
        (-0.138, 0.036, 2),
        (-0.118, 0.014, 1),
        (-0.085, 0.000, 1),
        (0.000, 0.000, 1),
        (0.085, 0.000, 1),
        (0.118, 0.014, 1),
        (0.138, 0.036, 2),
    ]
    rings: list[list[bmesh.types.BMVert]] = []
    for y_val, shrink, _b in y_slices:
        ring = []
        for pt in perimeter:
            ip = inset_point(pt, shrink)
            ring.append(bm.verts.new(Vector((ip.x, y_val, ip.z))))
        rings.append(ring)

    for i in range(len(rings) - 1):
        r0, r1 = rings[i], rings[i + 1]
        b_idx = y_slices[i][2]
        for j in range(n_p):
            jn = (j + 1) % n_p
            f = bm.faces.new((r0[j], r0[jn], r1[jn], r1[j]))
            for loop in f.loops:
                loop[uv].uv = _uv_for_band(b_idx, bb, j / n_p)

    # Front & back cross faces (inset inner ring + center vertex -> clean star-convex on cross with inner 0.35 scale!)
    for ring, y_val, flip in ((rings[0], -0.138, True), (rings[-1], 0.138, False)):
        inner = [bm.verts.new(Vector((v.co.x * 0.32, y_val, v.co.z * 0.32))) for v in ring]
        vc = bm.verts.new(Vector((0.0, y_val, 0.0)))
        for j in range(n_p):
            jn = (j + 1) % n_p
            q = (ring[j], ring[jn], inner[jn], inner[j]) if flip else (inner[j], inner[jn], ring[jn], ring[j])
            t = (inner[j], inner[jn], vc) if flip else (vc, inner[j], inner[jn])
            f1 = bm.faces.new(q)
            f2 = bm.faces.new(t)
            for loop in f1.loops:
                loop[uv].uv = _uv_for_band(2, bb, j / n_p)
            for loop in f2.loops:
                loop[uv].uv = _uv_for_band(0, bb, j / n_p)

    _finalize_and_normalize(bm, target_size_gltf=(1.00, 0.75, 0.275))
    return bm, bands


# ---------------------------------------------------------------------------
# Optional pinned builders (skipped unless `--include-pinned` is passed)
# ---------------------------------------------------------------------------

def build_can() -> tuple[bmesh.types.BMesh, list[tuple[float, tuple[int, int, int]]]]:
    bands = [
        (0.24, (196, 198, 204)),  # tin rim & lid
        (0.62, (236, 196, 68)),   # yellow sari-sari label band
        (1.00, (206, 58, 52)),    # red corned-beef/sardinas label band
    ]
    bb = _band_bounds(bands)
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    frames: list[tuple[Vector, Vector, Vector, float, float]] = []
    steps = 26
    for i in range(steps + 1):
        t = i / steps
        z = t * 1.0
        dent = -0.018 * math.exp(-((t - 0.52) / 0.16) ** 2)
        rim = 0.018 if (t < 0.05 or t > 0.95) else 0.0
        r = 0.41 + dent + rim
        frames.append((Vector((0.0, 0.0, z)), Vector((1, 0, 0)), Vector((0, 1, 0)), r, r))
    _add_lofted_tube(
        bm,
        frames,
        radial_segs=36,
        band_fn=lambda i, _j: 0 if (i <= 2 or i >= steps - 2) else (1 if i < steps // 2 else 2),
        uv_layer=uv,
        band_bounds=bb,
    )
    _finalize_and_normalize(bm, target_size_gltf=(0.855, 1.00, 0.855))
    return bm, bands


def build_trumpo() -> tuple[bmesh.types.BMesh, list[tuple[float, tuple[int, int, int]]]]:
    bands = [
        (0.75, (224, 178, 122)),  # pale turned beechwood (#E0B27A)
        (1.00, (124, 128, 134)),  # steel pako nail tip
    ]
    bb = _band_bounds(bands)
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    frames: list[tuple[Vector, Vector, Vector, float, float]] = []
    # Nail tip Z=0..0.366, pear wood body Z=0.366..1.00
    for i in range(7):
        t = i / 6
        z = 0.366 * t
        r = 0.008 + 0.024 * t
        frames.append((Vector((0, 0, z)), Vector((1, 0, 0)), Vector((0, 1, 0)), r, r))
    for i in range(22):
        t = i / 21
        z = 0.366 + 0.634 * t
        # Widest 39% down from top (t = 0.61)
        r = 0.325 * math.sin((t ** 0.82) * math.pi) + 0.018 * (1.0 - t)
        frames.append((Vector((0, 0, z)), Vector((1, 0, 0)), Vector((0, 1, 0)), max(r, 0.015), max(r, 0.015)))
    _add_lofted_tube(
        bm,
        frames,
        radial_segs=36,
        band_fn=lambda i, _j: 1 if i < 6 else 0,
        uv_layer=uv,
        band_bounds=bb,
    )
    _finalize_and_normalize(bm, target_size_gltf=(0.65, 1.00, 0.65))
    return bm, bands


def _write_new_import_sidecars(model_id: str) -> None:
    """Ensure newly introduced weapon assets (`tirador`) have VRAM-compressed .import sidecars without md5=."""
    glb_import = OUT_DIR / f"{model_id}.glb.import"
    jpg_import = OUT_DIR / f"{model_id}_prop_base_color.jpg.import"

    if not glb_import.exists():
        glb_import.write_text(
            "[remap]\n\n"
            'importer="scene"\n'
            "importer_version=1\n"
            'type="PackedScene"\n'
            f'uid="uid://wep_{model_id}_v2"\n'
            f'path="res://.godot/imported/{model_id}.glb-00000000000000000000000000000000.scn"\n\n'
            "[deps]\n\n"
            f'source_file="res://assets/models/weapons/{model_id}.glb"\n\n'
            "[params]\n\n"
            'nodes/root_type=""\n'
            'nodes/root_name=""\n'
            "nodes/apply_root_scale=true\n"
            "nodes/root_scale=1.0\n"
            'nodes/import_as_skeleton_bones=false\n'
            "nodes/use_name_suffixes=true\n"
            "nodes/use_node_type_suffixes=true\n"
            "meshes/ensure_tangents=true\n"
            "meshes/generate_lods=true\n"
            "meshes/create_shadow_meshes=true\n"
            "meshes/light_baking=1\n"
            "meshes/lightmap_texel_size=0.2\n"
            "meshes/force_disable_compression=false\n"
            "skins/use_named_skins=true\n"
            "animation/import=false\n"
            "gltf/naming_version=2\n"
            "gltf/embedded_image_handling=1\n",
            encoding="utf-8",
            newline="\n",
        )

    if not jpg_import.exists():
        jpg_import.write_text(
            "[remap]\n\n"
            'importer="texture"\n'
            'type="CompressedTexture2D"\n'
            f'uid="uid://wep_{model_id}_tex_v2"\n'
            f'path="res://.godot/imported/{model_id}_prop_base_color.jpg-00000000000000000000000000000000.ctex"\n'
            "metadata={\n"
            '"vram_texture": true\n'
            "}\n\n"
            "[deps]\n\n"
            f'source_file="res://assets/models/weapons/{model_id}_prop_base_color.jpg"\n\n'
            "[params]\n\n"
            "compress/mode=2\n"
            "compress/high_quality=false\n"
            "compress/lossy_quality=0.7\n"
            "compress/uastc_level=0\n"
            "compress/rdo_quality_loss=0.0\n"
            "compress/hdr_compression=1\n"
            "compress/normal_map=0\n"
            "compress/channel_pack=0\n"
            "mipmaps/generate=true\n"
            "mipmaps/limit=-1\n"
            "roughness/mode=0\n"
            'roughness/src_normal=""\n'
            "process/fix_alpha_border=true\n"
            "process/premult_alpha=false\n"
            "process/normal_map_invert_y=false\n"
            "process/hdr_as_srgb=false\n"
            "process/hdr_clamp_exposure=false\n"
            "process/size_limit=0\n"
            "detect_3d/compress_to=1\n",
            encoding="utf-8",
            newline="\n",
        )


def main() -> None:
    include_pinned = "--include-pinned" in sys.argv
    builders: list[tuple[str, Callable[[], tuple[bmesh.types.BMesh, list[tuple[float, tuple[int, int, int]]]]]]] = [
        ("tirador", build_tirador),
        ("slipper", build_slipper),
        ("meteor", build_meteor),
        ("marble", build_marble),
        ("scissor", build_scissor),
        ("paper", build_paper),
        ("crumpled_paper", build_crumpled_paper),
        ("spikes", build_spikes),
        ("soil_block", build_soil_block),
        ("green_cross", build_green_cross),
    ]
    if include_pinned:
        builders.extend([("can", build_can), ("trumpo", build_trumpo)])

    print("=== Building Kalyeah 90s Pinoy Street Weapons ===")
    for model_id, fn in builders:
        bm, bands = fn()
        open_edges, tris = _export_weapon(model_id, bm, bands)
        if model_id == "tirador":
            _write_new_import_sidecars(model_id)
        ratio = open_edges / max(tris, 1)
        print(f"  [OK] {model_id:16s} tris={tris:4d}  open_edges={open_edges:3d} (ratio={ratio:.4f})")
        assert 1000 <= tris <= 3500, f"{model_id}: triangle count {tris} outside [1000, 3500]!"
        assert ratio < 0.35, f"{model_id}: open_edges ratio {ratio:.3f} >= 0.35!"

    if not include_pinned:
        print("  [SKIP] can, trumpo preserved (owner-pinned skin/topology unit tests)")


if __name__ == "__main__":
    main()
