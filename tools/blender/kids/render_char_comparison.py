#!/usr/bin/env python3
"""Valorant-bar studio turnaround & 10m/30m/50m competitive readability renderer for Kalyeah kids.

Renders two standardized inspection sheets per kid using consistent neutral studio lighting
(warm key + cool sky fill + crisp rim) against a muted warm Barangay stucco wall:
  1. <prefix>_<kid>.png              (1600x900) Orthographic Front / Side / Back + 85mm Head/Torso Close-Up
  2. <prefix>_readability_<kid>.png  (1600x600) Exact 10m / 30m / 50m competitive readability vs Barangay stucco

Usage:
  blender --background --python tools/blender/kids/render_char_comparison.py -- \
      --kids karding,narda,jumong,tere --prefix before --out-dir C:/path/to/char_polish
"""
from __future__ import annotations

import argparse
import math
import os
import sys
from pathlib import Path

import bpy
import numpy as np
from mathutils import Vector

# Minimal crisp 5x7 uppercase/digit bitmap font for self-contained panel labels inside Blender.
FONT_5X7: dict[str, tuple[str, ...]] = {
    "A": ("01110", "10001", "10001", "11111", "10001", "10001", "10001"),
    "B": ("11110", "10001", "10001", "11110", "10001", "10001", "11110"),
    "C": ("01111", "10000", "10000", "10000", "10000", "10000", "01111"),
    "D": ("11110", "10001", "10001", "10001", "10001", "10001", "11110"),
    "E": ("11111", "10000", "10000", "11110", "10000", "10000", "11111"),
    "F": ("11111", "10000", "10000", "11110", "10000", "10000", "10000"),
    "G": ("01111", "10000", "10000", "10111", "10001", "10001", "01110"),
    "H": ("10001", "10001", "10001", "11111", "10001", "10001", "10001"),
    "I": ("11111", "00100", "00100", "00100", "00100", "00100", "11111"),
    "J": ("00111", "00010", "00010", "00010", "10010", "10010", "01100"),
    "K": ("10001", "10010", "10100", "11000", "10100", "10010", "10001"),
    "L": ("10000", "10000", "10000", "10000", "10000", "10000", "11111"),
    "M": ("10001", "11011", "10101", "10101", "10001", "10001", "10001"),
    "N": ("10001", "11001", "10101", "10011", "10001", "10001", "10001"),
    "O": ("01110", "10001", "10001", "10001", "10001", "10001", "01110"),
    "P": ("11110", "10001", "10001", "11110", "10000", "10000", "10000"),
    "Q": ("01110", "10001", "10001", "10001", "10101", "10010", "01101"),
    "R": ("11110", "10001", "10001", "11110", "10100", "10010", "10001"),
    "S": ("01111", "10000", "10000", "01110", "00001", "00001", "11110"),
    "T": ("11111", "00100", "00100", "00100", "00100", "00100", "00100"),
    "U": ("10001", "10001", "10001", "10001", "10001", "10001", "01110"),
    "V": ("10001", "10001", "10001", "10001", "10001", "01010", "00100"),
    "W": ("10001", "10001", "10001", "10101", "10101", "11011", "10001"),
    "X": ("10001", "10001", "01010", "00100", "01010", "10001", "10001"),
    "Y": ("10001", "10001", "01010", "00100", "00100", "00100", "00100"),
    "Z": ("11111", "00001", "00010", "00100", "01000", "10000", "11111"),
    "0": ("01110", "10011", "10101", "10101", "11001", "10001", "01110"),
    "1": ("00100", "01100", "00100", "00100", "00100", "00100", "01110"),
    "2": ("01110", "10001", "00001", "00110", "01000", "10000", "11111"),
    "3": ("11110", "00001", "00001", "01110", "00001", "00001", "11110"),
    "4": ("00010", "00110", "01010", "10010", "11111", "00010", "00010"),
    "5": ("11111", "10000", "11110", "00001", "00001", "10001", "01110"),
    "6": ("01110", "10000", "11110", "10001", "10001", "10001", "01110"),
    "7": ("11111", "00001", "00010", "00100", "01000", "01000", "01000"),
    "8": ("01110", "10001", "10001", "01110", "10001", "10001", "01110"),
    "9": ("01110", "10001", "10001", "01111", "00001", "00010", "01100"),
    " ": ("00000", "00000", "00000", "00000", "00000", "00000", "00000"),
    "-": ("00000", "00000", "00000", "11111", "00000", "00000", "00000"),
    "/": ("00001", "00010", "00010", "00100", "01000", "01000", "10000"),
    ":": ("00000", "00100", "00100", "00000", "00100", "00100", "00000"),
    ".": ("00000", "00000", "00000", "00000", "00000", "01100", "01100"),
    "(": ("00010", "00100", "01000", "01000", "01000", "00100", "00010"),
    ")": ("01000", "00100", "00010", "00010", "00010", "00100", "01000"),
    "|": ("00100", "00100", "00100", "00100", "00100", "00100", "00100"),
}


def draw_text(canvas: np.ndarray, text: str, x: int, y: int, scale: int = 2,
              color=(0.97, 0.96, 0.94, 1.0)) -> None:
    """Draw crisp bitmap text onto an RGBA float32 array in top-down (row 0 = top) coordinates."""
    h, w, _ = canvas.shape
    cx = x
    for ch in text.upper():
        glyph = FONT_5X7.get(ch, FONT_5X7[" "])
        for ry, row in enumerate(glyph):
            for rx, bit in enumerate(row):
                if bit == "1":
                    y0 = y + ry * scale
                    y1 = min(h, y0 + scale)
                    x0 = cx + rx * scale
                    x1 = min(w, x0 + scale)
                    if 0 <= y0 < h and 0 <= x0 < w:
                        canvas[y0:y1, x0:x1] = color
        cx += 6 * scale


def fill_rect(canvas: np.ndarray, x0: int, y0: int, x1: int, y1: int, color, alpha: float = 1.0) -> None:
    h, w, _ = canvas.shape
    xa, xb = max(0, x0), min(w, x1)
    ya, yb = max(0, y0), min(h, y1)
    if xa >= xb or ya >= yb:
        return
    c = np.array(color, dtype=np.float32)
    if alpha >= 0.999:
        canvas[ya:yb, xa:xb] = c
    else:
        canvas[ya:yb, xa:xb, :3] = canvas[ya:yb, xa:xb, :3] * (1.0 - alpha) + c[:3] * alpha
        canvas[ya:yb, xa:xb, 3] = 1.0


def save_rgba_topdown(canvas: np.ndarray, out_path: Path) -> None:
    """Save top-down RGBA float32 array (sRGB display pixels) as PNG via Blender."""
    out_path.parent.mkdir(parents=True, exist_ok=True)
    h, w, _ = canvas.shape
    # Blender images are bottom-up; flip vertically and keep exact display values via raw write
    flipped = np.ascontiguousarray(canvas[::-1, :, :], dtype=np.float32)
    img = bpy.data.images.new("__cmp_out__", width=w, height=h, alpha=True, float_buffer=False)
    img.colorspace_settings.name = "sRGB"
    img.pixels.foreach_set(flipped.ravel())
    img.filepath_raw = str(out_path)
    img.file_format = "PNG"
    img.save()
    bpy.data.images.remove(img)


def render_panel_to_array(scn, w: int, h: int, tmp_path: Path) -> np.ndarray:
    """Render current scene camera to PNG and reload as top-down sRGB RGBA float32 array."""
    scn.render.resolution_x = w
    scn.render.resolution_y = h
    scn.render.filepath = str(tmp_path)
    bpy.ops.render.render(write_still=True)
    img = bpy.data.images.load(str(tmp_path), check_existing=False)
    img.colorspace_settings.name = "sRGB"
    px = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)
    bpy.data.images.remove(img)
    return px[::-1, :, :].copy()


def wire_honest_pbr_ao(mesh_obj) -> None:
    """Ensure ORM Red (AO) visibly modulates Base Color in Cycles (matching KalyeahKid.shader _OcclusionStrength=0.55)."""
    if not mesh_obj.data.materials:
        return
    mat = mesh_obj.data.materials[0]
    if not mat or not mat.node_tree:
        return
    nt = mat.node_tree
    bsdf = next((n for n in nt.nodes if n.type == "BSDF_PRINCIPLED"), None)
    if bsdf is None:
        return
    base_inp = bsdf.inputs.get("Base Color")
    if not base_inp or not base_inp.is_linked:
        return
    base_sock = base_inp.links[0].from_socket

    # Locate ORM SeparateColor node (or image named *_orm)
    orm_red = None
    for n in nt.nodes:
        if n.type == "SEPARATE_COLOR" and n.inputs["Color"].is_linked:
            src_n = n.inputs["Color"].links[0].from_node
            if src_n.type == "TEX_IMAGE" and src_n.image and "orm" in src_n.image.name.lower():
                orm_red = n.outputs["Red"]
                break
    if orm_red is None:
        return

    # Mix AO between 1.0 and ORM.R with factor 0.55, then multiply onto Base Color
    ao_mix = nt.nodes.new("ShaderNodeMix")
    ao_mix.data_type = "FLOAT"
    ao_mix.inputs["Factor"].default_value = 0.55
    ao_mix.inputs["A"].default_value = 1.0
    nt.links.new(orm_red, ao_mix.inputs["B"])

    col_mul = nt.nodes.new("ShaderNodeMix")
    col_mul.data_type = "RGBA"
    col_mul.blend_type = "MULTIPLY"
    col_mul.inputs["Factor"].default_value = 1.0
    nt.links.new(base_sock, col_mul.inputs["A"])
    nt.links.new(ao_mix.outputs["Result"], col_mul.inputs["B"])

    for lnk in list(base_inp.links):
        nt.links.remove(lnk)
    nt.links.new(col_mul.outputs["Result"], base_inp)


def make_stucco_material(name: str, base_rgb=(0.44, 0.38, 0.32), roughness=0.95):
    """Muted warm Barangay stucco wall material with subtle procedural plaster variation (low visual noise)."""
    mat = bpy.data.materials.new(name)
    if mat.node_tree is None:
        mat.use_nodes = True
    nt = mat.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Specular IOR Level"].default_value = 0.04

    tc = nt.nodes.new("ShaderNodeTexCoord")
    noise = nt.nodes.new("ShaderNodeTexNoise")
    noise.inputs["Scale"].default_value = 2.8
    noise.inputs["Detail"].default_value = 3.0
    noise.inputs["Roughness"].default_value = 0.55

    ramp = nt.nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.elements[0].position = 0.25
    ramp.color_ramp.elements[0].color = (base_rgb[0] * 0.90, base_rgb[1] * 0.89, base_rgb[2] * 0.88, 1.0)
    ramp.color_ramp.elements[1].position = 0.78
    ramp.color_ramp.elements[1].color = (min(1.0, base_rgb[0] * 1.07), min(1.0, base_rgb[1] * 1.06), min(1.0, base_rgb[2] * 1.05), 1.0)

    nt.links.new(tc.outputs["Object"], noise.inputs["Vector"])
    nt.links.new(noise.outputs["Fac"], ramp.inputs["Fac"])
    nt.links.new(ramp.outputs["Color"], bsdf.inputs["Base Color"])
    return mat


def add_studio_lights(scn, H: float) -> None:
    """Consistent neutral Valorant-style 3-point daylight rig (warm sun key + cool sky fill + crisp rim).

    Uses directional SUN lights (zero harsh proximity hotspots or floating glossy reflection dots on walls)
    so turnaround and 10m/30m/50m Barangay readability sheets have identical, even illumination.
    """
    world = bpy.data.worlds.new("StudioWorld")
    if world.node_tree is None:
        world.use_nodes = True
    bg = world.node_tree.nodes["Background"]
    bg.inputs["Color"].default_value = (0.64, 0.72, 0.84, 1.0)
    bg.inputs["Strength"].default_value = 0.48
    scn.world = world

    rig = (
        ("KeySun",  (-1.8, -2.4, 2.8), (1.00, 0.96, 0.89), 2.55, math.radians(6.0)),
        ("FillSun", ( 2.5, -1.6, 1.5), (0.74, 0.86, 1.00), 0.95, math.radians(18.0)),
        ("RimSun",  ( 0.7,  2.6, 2.3), (0.92, 0.96, 1.00), 1.45, math.radians(4.0)),
    )
    origin = Vector((0.0, 0.0, 0.0))
    for name, dir_from, col, energy, angle in rig:
        ld = bpy.data.lights.new(name, "SUN")
        ld.energy = energy
        ld.color = col
        ld.angle = angle
        lo = bpy.data.objects.new(name, ld)
        lo.location = Vector(dir_from) * 5.0
        lo.rotation_euler = (origin - Vector(dir_from)).to_track_quat("-Z", "Y").to_euler()
        scn.collection.objects.link(lo)


def add_plane(scn, name: str, loc, rot, scale_xy, mat):
    bpy.ops.mesh.primitive_plane_add(size=1.0, location=loc, rotation=rot)
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = (scale_xy[0], scale_xy[1], 1.0)
    obj.data.materials.append(mat)
    return obj


def render_kid(glb_path: Path, kid: str, prefix: str, out_dir: Path, samples: int, pose_mode: str) -> None:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(glb_path))

    arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
    mesh = max(
        (o for o in bpy.data.objects if o.type == "MESH" and o.vertex_groups),
        key=lambda o: len(o.data.vertices),
    )
    # Remove any stray helper mesh objects (e.g. glTF bone display Icosphere)
    for o in list(bpy.data.objects):
        if o.type == "MESH" and o is not mesh:
            bpy.data.objects.remove(o, do_unlink=True)

    scn = bpy.context.scene
    scn.render.engine = "CYCLES"
    scn.cycles.device = "CPU"
    scn.cycles.samples = samples
    scn.cycles.use_denoising = True
    scn.render.image_settings.file_format = "PNG"
    scn.render.image_settings.color_mode = "RGBA"
    scn.view_settings.view_transform = "Standard"
    scn.view_settings.exposure = 0.0

    # Pose character in Idle frame 0 (default) or Rest A-pose
    if pose_mode == "idle" and "Idle" in bpy.data.actions:
        idle = bpy.data.actions["Idle"]
        if arm.animation_data is None:
            arm.animation_data_create()
        arm.animation_data.action = idle
        try:
            arm.animation_data.action_slot = idle.slots[0]
        except Exception:
            pass
        arm.data.pose_position = "POSE"
        scn.frame_set(int(idle.frame_range[0]))
    else:
        if arm.animation_data:
            arm.animation_data.action = None
        arm.data.pose_position = "REST"
    bpy.context.view_layer.update()

    # Measure posed world bounding box
    dg = bpy.context.evaluated_depsgraph_get()
    ev = mesh.evaluated_get(dg)
    ev_m = ev.to_mesh()
    pts = np.array([tuple(mesh.matrix_world @ v.co) for v in ev_m.vertices], dtype=np.float64)
    ev.to_mesh_clear()
    zmin, zmax = float(pts[:, 2].min()), float(pts[:, 2].max())
    cx = float(0.5 * (pts[:, 0].min() + pts[:, 0].max()))
    cy = float(0.5 * (pts[:, 1].min() + pts[:, 1].max()))
    H = max(zmax - zmin, 1.0)
    mid_z = zmin + H * 0.50

    head_b = arm.pose.bones.get("mixamorig:Head")
    head_pos = (arm.matrix_world @ head_b.head) if head_b else Vector((cx, cy, zmin + H * 0.82))
    bust_target = Vector((head_pos.x, head_pos.y, head_pos.z - H * 0.04))

    wire_honest_pbr_ao(mesh)
    add_studio_lights(scn, H)

    # Neutral ground plane + muted Barangay stucco backdrop
    floor_mat = make_stucco_material("BarangayFloor", base_rgb=(0.28, 0.27, 0.25), roughness=0.95)
    wall_mat = make_stucco_material("BarangayWall", base_rgb=(0.42, 0.36, 0.30), roughness=0.95)
    curb_mat = make_stucco_material("BarangayCurb", base_rgb=(0.33, 0.31, 0.28), roughness=0.95)

    floor = add_plane(scn, "StudioFloor", (0.0, 0.0, zmin - 0.002), (0.0, 0.0, 0.0), (140.0, 140.0), floor_mat)
    # Four directional backdrop walls far enough behind each orthographic camera angle
    wall_back = add_plane(scn, "WallBack", (0.0, 3.2, 6.0), (math.pi * 0.5, 0.0, 0.0), (90.0, 18.0), wall_mat)
    wall_front = add_plane(scn, "WallFront", (0.0, -3.2, 6.0), (math.pi * 0.5, 0.0, math.pi), (90.0, 18.0), wall_mat)
    wall_side = add_plane(scn, "WallSide", (-3.2, 0.0, 6.0), (math.pi * 0.5, 0.0, -math.pi * 0.5), (90.0, 18.0), wall_mat)
    curb = add_plane(scn, "CurbTrim", (0.0, 3.05, 0.14), (math.pi * 0.5, 0.0, 0.0), (90.0, 0.28), curb_mat)

    cam_d = bpy.data.cameras.new("CmpCam")
    cam = bpy.data.objects.new("CmpCam", cam_d)
    scn.collection.objects.link(cam)
    scn.camera = cam

    tmp_png = out_dir / f"__tmp_{prefix}_{kid}.png"
    out_dir.mkdir(parents=True, exist_ok=True)

    # ---- Sheet A: 1600x900 Turnaround (Front / Side / Back Ortho + 85mm Bust Close-Up) ----
    # Each panel is 400 x 850 (plus a 50px top header bar)
    pw, ph = 400, 850
    ortho_scale = H * 1.18  # spans height with clean headroom/footroom; width = ortho_scale * (400/850) = ~0.55*H? Wait:
    # In Blender, ortho_scale controls the LARGER sensor dimension (vertical when h > w)!
    # When w=400, h=850, vertical visible span = ortho_scale, horizontal visible span = ortho_scale * (400/850) = 0.47 * ortho_scale.
    # Make sure wide arms/cape fit comfortably inside horizontal span (>= 1.05m): ortho_scale = max(H * 1.24, 2.25)
    ortho_scale = max(H * 1.28, 2.28)
    cam_d.sensor_fit = "VERTICAL"
    cam_d.sensor_height = 24.0

    # 1) Front Ortho (hide WallFront)
    wall_front.hide_render = True
    wall_back.hide_render = False
    wall_side.hide_render = True
    curb.hide_render = False
    cam_d.type = "ORTHO"
    cam_d.ortho_scale = ortho_scale
    cam.location = Vector((cx, cy - 2.6, mid_z))
    cam.rotation_euler = (Vector((cx, cy, mid_z)) - cam.location).to_track_quat("-Z", "Y").to_euler()
    p_front = render_panel_to_array(scn, pw, ph, tmp_png)

    # 2) Side Ortho (from character's right side -X looking +X, or +X looking -X; use -X looking +X? Wait:
    #    In Blender +X = character Left, -X = character Right. Looking from -X shows right arm & cape;
    #    Wait: wall_side is at -3.2, so camera at +2.6 looking -X looks at character's Left side with WallSide behind!
    #    Let's check Karding's cape: prompt mentions right bicep & left forearm; let's render slight 3/4-right or clean right profile with wall at +3.2!)
    wall_front.hide_render = True
    wall_back.hide_render = True
    wall_side.hide_render = False
    curb.hide_render = True
    wall_side.location = (3.2, 0.0, 6.0)
    wall_side.rotation_euler = (math.pi * 0.5, 0.0, math.pi * 0.5)
    cam.location = Vector((cx - 2.6, cy, mid_z))
    cam.rotation_euler = (Vector((cx, cy, mid_z)) - cam.location).to_track_quat("-Z", "Y").to_euler()
    p_side = render_panel_to_array(scn, pw, ph, tmp_png)

    # 3) Back Ortho (camera at +Y looking -Y; WallFront at -3.2 is behind)
    wall_front.hide_render = False
    wall_back.hide_render = True
    wall_side.hide_render = True
    cam.location = Vector((cx, cy + 2.6, mid_z))
    cam.rotation_euler = (Vector((cx, cy, mid_z)) - cam.location).to_track_quat("-Z", "Y").to_euler()
    p_back = render_panel_to_array(scn, pw, ph, tmp_png)

    # 4) Close-Up Head / Torso Bust (58mm portrait perspective 3/4 front — full head + shoulders + upper torso)
    wall_front.hide_render = True
    wall_back.hide_render = False
    wall_side.hide_render = True
    curb.hide_render = False
    cam_d.type = "PERSP"
    cam_d.lens = 58.0
    bust_dir = Vector((-0.24, -1.0, 0.06)).normalized()
    cam.location = bust_target + bust_dir * (H * 1.48)
    cam.rotation_euler = (bust_target - cam.location).to_track_quat("-Z", "Y").to_euler()
    p_bust = render_panel_to_array(scn, pw, ph, tmp_png)

    # Assemble 1600x900 turnaround sheet
    sheet_a = np.zeros((900, 1600, 4), dtype=np.float32)
    sheet_a[:, :] = (0.11, 0.12, 0.14, 1.0)
    panels = (
        (p_front, "FRONT ORTHO"),
        (p_side,  "RIGHT SIDE ORTHO"),
        (p_back,  "BACK ORTHO"),
        (p_bust,  "HEAD / TORSO CLOSE-UP"),
    )
    for idx, (arr, label) in enumerate(panels):
        x0 = idx * pw
        sheet_a[50:900, x0:x0 + pw] = arr
        if idx > 0:
            fill_rect(sheet_a, x0 - 1, 50, x0 + 1, 900, (0.16, 0.17, 0.20, 1.0))
        fill_rect(sheet_a, x0 + 10, 862, x0 + pw - 10, 892, (0.08, 0.09, 0.11, 1.0), alpha=0.78)
        draw_text(sheet_a, label, x0 + 20, 870, scale=2)

    tri_cnt = sum(len(p.vertices) - 2 for p in mesh.data.polygons)
    sk_cnt = (len(mesh.data.shape_keys.key_blocks) - 1) if mesh.data.shape_keys else 0
    header = f"{prefix.upper()} | {kid.upper()} | {tri_cnt} TRIS | {sk_cnt} SHAPES | H:{H:.2f}M"
    draw_text(sheet_a, header, 18, 14, scale=3, color=(0.98, 0.90, 0.62, 1.0))
    save_rgba_topdown(sheet_a, out_dir / f"{prefix}_{kid}.png")

    # ---- Sheet B: 1600x600 Competitive Readability (10m / 30m / 50m vs Barangay Stucco Wall) ----
    # Exact 55 deg vertical FOV (matching 3P competitive shooter camera)
    wall_front.hide_render = True
    wall_back.hide_render = False
    wall_side.hide_render = True
    curb.hide_render = False
    wall_back.location = (0.0, 2.2, 6.0)
    curb.location = (0.0, 2.05, 0.14)

    cam_d.type = "PERSP"
    cam_d.sensor_fit = "VERTICAL"
    cam_d.sensor_height = 24.0
    vfov_rad = math.radians(55.0)
    cam_d.lens = 24.0 / (2.0 * math.tan(vfov_rad * 0.5))

    specs = (
        (10.0, 534, "10M CLOSE COMBAT"),
        (30.0, 533, "30M MID-LANE"),
        (50.0, 533, "50M LONG-LANE"),
    )
    sheet_b = np.zeros((600, 1600, 4), dtype=np.float32)
    sheet_b[:, :] = (0.11, 0.12, 0.14, 1.0)
    cur_x = 0
    for dist_m, panel_w, label in specs:
        target = Vector((cx, cy, zmin + H * 0.55))
        # Slight over-the-shoulder elevation (~1.55m eye height) at exact dist_m distance
        eye_z = zmin + 1.55
        cam.location = Vector((cx, cy - dist_m, eye_z))
        cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
        arr = render_panel_to_array(scn, panel_w, 550, tmp_png)
        sheet_b[50:600, cur_x:cur_x + panel_w] = arr
        if cur_x > 0:
            fill_rect(sheet_b, cur_x - 1, 50, cur_x + 1, 600, (0.16, 0.17, 0.20, 1.0))
        fill_rect(sheet_b, cur_x + 12, 562, cur_x + panel_w - 12, 592, (0.08, 0.09, 0.11, 1.0), alpha=0.80)
        draw_text(sheet_b, label, cur_x + 22, 570, scale=2)
        cur_x += panel_w

    draw_text(sheet_b, f"{prefix.upper()} READABILITY | {kid.upper()} | 55 DEG VFOV | BARANGAY STUCCO",
              18, 14, scale=3, color=(0.98, 0.90, 0.62, 1.0))
    save_rgba_topdown(sheet_b, out_dir / f"{prefix}_readability_{kid}.png")

    if tmp_png.exists():
        tmp_png.unlink()
    print(f"[cmp] saved {prefix}_{kid}.png and {prefix}_readability_{kid}.png", flush=True)


def main() -> int:
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    ap = argparse.ArgumentParser()
    ap.add_argument("--kids", default="karding,narda,jumong,tere")
    ap.add_argument("--models-dir", default="assets/models/characters")
    ap.add_argument("--prefix", default="before")
    ap.add_argument("--out-dir", required=True)
    ap.add_argument("--samples", type=int, default=24)
    ap.add_argument("--pose", choices=("idle", "rest"), default="idle")
    args = ap.parse_args(argv)

    models_dir = Path(args.models_dir)
    out_dir = Path(args.out_dir)
    for kid in [k.strip().lower() for k in args.kids.split(",") if k.strip()]:
        glb = models_dir / f"{kid}.glb"
        if not glb.is_file():
            raise FileNotFoundError(f"Missing character GLB: {glb}")
        render_kid(glb, kid, args.prefix, out_dir, args.samples, args.pose)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
