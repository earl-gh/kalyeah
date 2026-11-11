#!/usr/bin/env python3
"""Render full-body 3D action character cards for the 4 new kids (kiko, nene, buknoy, tala).

Generates `assets/ui/character_cards/{kiko,nene,buknoy,tala}.webp` at exact `480x737` RGBA WebP,
matching the worn kraft-cardstock plaque, 2-ply fractal-torn paper corners, 90s Filipino Komiks
radial speed-burst + Ben-Day halftone window, wide-angle foreshortened action poses, golden-hour
rim lighting, dual eye catchlights, and `#17131A` charcoal comic ink contour of `junjun..inday.webp`.

Usage:
    python tools/blender/render_character_cards_new4.py
"""

from __future__ import annotations

import hashlib
import json
import math
import os
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

import numpy as np
from PIL import Image, ImageEnhance
from scipy.ndimage import (
    binary_dilation,
    binary_erosion,
    binary_fill_holes,
    distance_transform_edt,
    gaussian_filter,
)
from scipy.ndimage import label as nd_label

REPO_ROOT = Path(__file__).resolve().parents[2]
CHAR_DIR = REPO_ROOT / "assets" / "models" / "characters"
OUT_DIR = REPO_ROOT / "assets" / "ui" / "character_cards"

CARD_W = 480
CARD_H = 737
EXPECTED_CARD_ASPECT = 325.0 / 499.0  # CharactersPage.CARD.x / CharactersPage.CARD.y
INK_RGB = np.array([23.0, 19.0, 26.0], dtype=np.float32)  # #17131A

CARDS_SPEC = [
    {
        "id": "kiko",
        "seed": 101,
        "clip": "Run",
        "frame": 4,
        "bone_tweaks": [
            ["mixamorig:LeftArm", [0.0, 0.0, 1.0], 32.0],
            ["mixamorig:LeftArm", [1.0, 0.0, 0.0], -22.0],
            ["mixamorig:LeftForeArm", [1.0, 0.0, 0.0], 20.0],
            ["mixamorig:RightArm", [0.0, 0.0, 1.0], -38.0],
            ["mixamorig:RightArm", [1.0, 0.0, 0.0], 18.0],
        ],
        "cam_yaw": 20.0,
        "cam_pitch": -6.0,
        "cam_roll": -8.5,
        "head_tilt_deg": 4.0,
        "gaze_blend": 0.84,
        "key_side": -1,
        "burst_inner": (228, 142, 20),   # Sunlit Mango-Amber Gold (complements blue maong vest)
        "burst_outer": (152, 72, 8),
        "ray_color": (255, 245, 200),
        "halftone_color": (255, 236, 174),
    },
    {
        "id": "nene",
        "seed": 102,
        "clip": "Airborne",
        "frame": 8,
        "bone_tweaks": [
            ["mixamorig:RightArm", [0.0, 1.0, 0.0], 26.0],
            ["mixamorig:RightArm", [0.0, 0.0, 1.0], -22.0],
            ["mixamorig:LeftArm", [0.0, 1.0, 0.0], 10.0],
        ],
        "cam_yaw": 16.0,
        "cam_pitch": -4.0,
        "cam_roll": -8.0,
        "head_tilt_deg": 4.0,
        "gaze_blend": 0.84,
        "key_side": 1,
        "burst_inner": (176, 34, 88),    # Rich Berry-Mulberry Wine (complements sky-blue pinafore & mango clip)
        "burst_outer": (104, 14, 50),
        "ray_color": (255, 240, 206),
        "halftone_color": (255, 218, 228),
    },
    {
        "id": "buknoy",
        "seed": 103,
        "clip": "Run",
        "frame": 3,
        "bone_tweaks": [
            ["mixamorig:LeftArm", [0.0, 0.0, 1.0], 38.0],
            ["mixamorig:LeftArm", [1.0, 0.0, 0.0], -24.0],
            ["mixamorig:LeftForeArm", [1.0, 0.0, 0.0], 24.0],
            ["mixamorig:RightArm", [0.0, 0.0, 1.0], -36.0],
            ["mixamorig:RightArm", [0.0, 1.0, 0.0], 22.0],
        ],
        "cam_yaw": 22.0,
        "cam_pitch": -7.0,
        "cam_roll": -9.0,
        "head_tilt_deg": 3.5,
        "gaze_blend": 0.85,
        "key_side": 1,
        "burst_inner": (18, 134, 142),   # Barrio Teal-Turquoise (complements terracotta/ochre plaid camisa)
        "burst_outer": (9, 74, 82),
        "ray_color": (252, 242, 196),
        "halftone_color": (214, 248, 244),
    },
    {
        "id": "tala",
        "seed": 104,
        "clip": "Run",
        "frame": 10,
        "bone_tweaks": [
            ["mixamorig:RightArm", [0.0, 0.0, 1.0], -40.0],
            ["mixamorig:RightArm", [1.0, 0.0, 0.0], -24.0],
            ["mixamorig:RightForeArm", [1.0, 0.0, 0.0], 24.0],
            ["mixamorig:LeftArm", [0.0, 0.0, 1.0], 34.0],
            ["mixamorig:LeftArm", [0.0, 1.0, 0.0], -20.0],
        ],
        "cam_yaw": -22.0,
        "cam_pitch": -7.0,
        "cam_roll": 9.0,
        "head_tilt_deg": -4.0,
        "gaze_blend": 0.85,
        "key_side": -1,
        "burst_inner": (46, 68, 154),    # Heroic Midnight Indigo (complements maroon #07 singlet & gold habi)
        "burst_outer": (22, 34, 88),
        "ray_color": (254, 230, 162),
        "halftone_color": (252, 234, 186),
    },
]


def sha256_file(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def find_blender() -> Path:
    candidates = [
        os.environ.get("BLENDER_BIN", ""),
        r"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe",
        shutil.which("blender") or "",
    ]
    for c in candidates:
        if c and Path(c).exists():
            return Path(c)
    raise FileNotFoundError("Blender 5.2 executable not found.")


# ---------------------------------------------------------------------------
# Blender 5.2 Full-Body Wide-Angle Action Renderer
# ---------------------------------------------------------------------------

BLENDER_ACTION_SCRIPT = r'''
import json
import math
import sys
import bpy
from bpy_extras.object_utils import world_to_camera_view
from mathutils import Euler, Matrix, Quaternion, Vector

argv = sys.argv[sys.argv.index("--") + 1:]
manifest_path, results_path = argv[0], argv[1]
with open(manifest_path, "r", encoding="utf-8") as f:
    jobs = json.load(f)

def tame_materials():
    for mat in bpy.data.materials:
        if not mat.node_tree:
            continue
        for node in mat.node_tree.nodes:
            if node.type == "NORMAL_MAP" and "Strength" in node.inputs:
                node.inputs["Strength"].default_value = 0.18
            if node.type != "BSDF_PRINCIPLED":
                continue
            for name in ("Metallic", "Roughness"):
                for link in list(node.inputs[name].links):
                    mat.node_tree.links.remove(link)
            node.inputs["Metallic"].default_value = 0.0
            node.inputs["Roughness"].default_value = 0.84
            if "Specular IOR Level" in node.inputs:
                node.inputs["Specular IOR Level"].default_value = 0.14
            if "Coat Weight" in node.inputs:
                node.inputs["Coat Weight"].default_value = 0.0

def add_sun(scene, name, energy, color, angle_deg, pitch_deg, yaw_deg):
    light = bpy.data.lights.new(name, "SUN")
    light.energy = energy
    light.color = color
    light.angle = math.radians(angle_deg)
    obj = bpy.data.objects.new(name, light)
    p = math.radians(pitch_deg)
    y = math.radians(yaw_deg)
    obj.rotation_euler = Euler((math.radians(90.0) - p, 0.0, y))
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
    if chosen is None:
        raise RuntimeError(f"Action {clip_name} not found")
    slot = next((sl for sl in chosen.slots if sl.target_id_type == "OBJECT"), None)
    rig.animation_data_create()
    rig.animation_data.action = chosen
    if slot is not None:
        rig.animation_data.action_slot = slot
    bpy.context.scene.frame_set(frame)
    bpy.context.view_layer.update()

def set_pose_bone_world_rot(rig, pb, new_world_rot_3x3):
    cur_world = rig.matrix_world @ pb.matrix
    loc = cur_world.to_translation()
    sca = cur_world.to_scale()
    m4 = new_world_rot_3x3.to_4x4()
    m4.translation = loc
    for c in range(3):
        m4.col[c].xyz *= sca[c]
    pb.matrix = rig.matrix_world.inverted() @ m4
    bpy.context.view_layer.update()

def rotate_bone_world_axis(rig, bone_name, axis_world, deg):
    pb = rig.pose.bones.get(bone_name)
    if pb is None:
        return
    cur_w = (rig.matrix_world @ pb.matrix).to_3x3().normalized()
    q = Quaternion(Vector(axis_world).normalized(), math.radians(float(deg)))
    set_pose_bone_world_rot(rig, pb, q.to_matrix() @ cur_w)

def aim_head_toward_camera(rig, cam_dir_from_target, gaze_blend, tilt_deg):
    """Rotate mixamorig:Neck (38%) + mixamorig:Head (62%) in world deform space so eyes engage the camera."""
    neck = rig.pose.bones.get("mixamorig:Neck") or rig.pose.bones.get("Neck")
    head = rig.pose.bones.get("mixamorig:Head") or rig.pose.bones.get("Head")
    if head is None:
        return

    t_fwd = Vector((0.0, -1.0, 0.0))
    goal_fwd = t_fwd.lerp(cam_dir_from_target.normalized(), float(gaze_blend)).normalized()
    y_new = (-goal_fwd).normalized()

    tilt_rad = math.radians(float(tilt_deg))
    up_ref = Quaternion(goal_fwd, tilt_rad) @ Vector((0.0, 0.0, 1.0))
    x_new = y_new.cross(up_ref).normalized()
    z_new = x_new.cross(y_new).normalized()
    r_goal = Matrix((x_new, y_new, z_new)).transposed()

    for pb, frac in ((neck, 0.38), (head, 1.0)):
        if pb is None:
            continue
        b_rest = (rig.matrix_world @ pb.bone.matrix_local).to_3x3().normalized()
        b_cur = (rig.matrix_world @ pb.matrix).to_3x3().normalized()
        r_deform = b_cur @ b_rest.inverted()
        q_diff = r_deform.to_quaternion().rotation_difference(r_goal.to_quaternion())
        axis, angle = q_diff.to_axis_angle()
        max_ang = math.radians(46.0)
        clamped_ang = min(angle, max_ang) * frac
        q_step = Quaternion(axis, clamped_ang)
        new_b = q_step.to_matrix() @ b_cur
        set_pose_bone_world_rot(rig, pb, new_b)

def evaluated_skinned_points(meshes):
    deps = bpy.context.evaluated_depsgraph_get()
    pts = []
    for m in meshes:
        em = m.evaluated_get(deps)
        me = em.to_mesh()
        mw = em.matrix_world
        pts.extend([mw @ v.co for v in me.vertices])
        em.to_mesh_clear()
    return pts

results = {}
RW, RH = 768, 1152

for job in jobs:
    kid_id = job["id"]
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=job["glb"])
    rig = next(o for o in bpy.data.objects if o.type == "ARMATURE")

    # Keep native 110k-vert watertight glTF mesh (NO SUBSURF on split UV seams!) & hide bone helper Icosphere
    skinned_meshes = []
    for o in bpy.data.objects:
        if o.type == "MESH":
            if any(m.type == "ARMATURE" for m in o.modifiers):
                skinned_meshes.append(o)
            else:
                o.hide_render = True
                o.hide_viewport = True

    tame_materials()
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 40
    scene.cycles.use_denoising = True
    scene.render.film_transparent = True
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.resolution_x, scene.render.resolution_y = RW, RH

    world = bpy.data.worlds.new("W")
    scene.world = world
    bg = world.node_tree.nodes["Background"]
    bg.inputs["Color"].default_value = (1.0, 0.78, 0.50, 1.0)
    bg.inputs["Strength"].default_value = 0.58

    cyaw_deg = float(job["cam_yaw"])
    cpitch_deg = float(job["cam_pitch"])
    croll_deg = float(job["cam_roll"])
    key_side = float(job["key_side"])

    add_sun(scene, "Key", 3.55, (1.0, 0.84, 0.58), 11.0, 26.0, cyaw_deg + key_side * 38.0)
    add_sun(scene, "Fill", 1.08, (1.0, 0.80, 0.58), 28.0, 10.0, cyaw_deg - key_side * 52.0)
    add_sun(scene, "MangoRim", 4.55, (1.0, 0.67, 0.20), 8.0, 20.0, cyaw_deg - key_side * 142.0)
    add_sun(scene, "GoldKicker", 2.45, (1.0, 0.75, 0.32), 10.5, 22.0, cyaw_deg + key_side * 142.0)

    activate_clip(rig, job["clip"], int(job["frame"]))
    for bname, ax, dg in job.get("bone_tweaks", []):
        rotate_bone_world_axis(rig, bname, ax, dg)

    cpitch = math.radians(cpitch_deg)
    cyaw = math.radians(cyaw_deg)
    croll = math.radians(croll_deg)
    cam_unit_offset = Euler((0.0, 0.0, cyaw)).to_matrix() @ Vector((0.0, -math.cos(cpitch), math.sin(cpitch)))
    cam_unit_offset.normalize()

    aim_head_toward_camera(rig, cam_unit_offset, job["gaze_blend"], job["head_tilt_deg"])

    # Punchy 23mm wide-angle perspective lens for heroic comic foreshortening matching junjun..inday
    cam_data = bpy.data.cameras.new("Cam")
    cam_data.type = "PERSP"
    cam_data.lens = 23.0
    cam_data.sensor_fit = "VERTICAL"
    cam_data.sensor_height = 36.0
    cam = bpy.data.objects.new("Cam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    cam.rotation_euler = Euler((math.radians(90.0) - cpitch, croll, cyaw))
    bpy.context.view_layer.update()

    pts = evaluated_skinned_points(skinned_meshes)
    min_v = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    max_v = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    look_at = 0.5 * (min_v + max_v)
    cdist = 1.95

    tan_y = (cam_data.sensor_height * 0.5) / cam_data.lens
    tan_x = tan_y * (float(RW) / float(RH))
    cam_rot_mat = cam.rotation_euler.to_matrix()
    cam_right = cam_rot_mat.col[0].normalized()
    cam_up = cam_rot_mat.col[1].normalized()
    cam_back = cam_rot_mat.col[2].normalized()

    for _ in range(6):
        cam.location = look_at + cam_back * cdist
        bpy.context.view_layer.update()
        inv_m = cam.matrix_world.inverted()
        ux_min, ux_max = 1e9, -1e9
        uy_min, uy_max = 1e9, -1e9
        for p in pts[::4]:
            pc = inv_m @ p
            depth = max(-pc.z, 0.12)
            ux = pc.x / (depth * tan_x)
            uy = pc.y / (depth * tan_y)
            if ux < ux_min: ux_min = ux
            if ux > ux_max: ux_max = ux
            if uy < uy_min: uy_min = uy
            if uy > uy_max: uy_max = uy
        mid_ux = 0.5 * (ux_min + ux_max)
        mid_uy = 0.5 * (uy_min + uy_max)
        look_at += cam_right * (mid_ux * cdist * tan_x * 0.85) + cam_up * (mid_uy * cdist * tan_y * 0.85)
        span = max((ux_max - ux_min) * 0.5, (uy_max - uy_min) * 0.5)
        cdist *= (span / 0.88)

    cam.location = look_at + cam_back * cdist
    bpy.context.view_layer.update()

    head_pb = rig.pose.bones.get("mixamorig:Head") or rig.pose.bones.get("Head")
    b_rest = (rig.matrix_world @ head_pb.bone.matrix_local).to_3x3().normalized()
    b_cur = (rig.matrix_world @ head_pb.matrix).to_3x3().normalized()
    r_def = b_cur @ b_rest.inverted()
    chin_world = rig.matrix_world @ head_pb.head
    eye_world = chin_world + r_def @ Vector((0.0, -0.115, 0.145))
    eye_ndc = world_to_camera_view(scene, cam, eye_world)

    results[kid_id] = {
        "eye_px": [float(eye_ndc.x * RW), float((1.0 - eye_ndc.y) * RH)],
        "raw_size": [RW, RH],
    }
    scene.render.filepath = job["raw_png"]
    bpy.ops.render.render(write_still=True)

with open(results_path, "w", encoding="utf-8") as f:
    json.dump(results, f, indent=2)
'''


# ---------------------------------------------------------------------------
# Procedural Worn Kraft Cardstock + 90s Komiks Burst + 2-Ply Torn Frame
# ---------------------------------------------------------------------------


def rounded_rect_sdf(xx: np.ndarray, yy: np.ndarray, x0: float, y0: float, x1: float, y1: float, r: float) -> np.ndarray:
    """Signed distance to rounded rectangle [x0, x1] x [y0, y1] with corner radius r (<0 inside)."""
    cx = 0.5 * (x0 + x1)
    cy = 0.5 * (y0 + y1)
    hx = 0.5 * (x1 - x0) - r
    hy = 0.5 * (y1 - y0) - r
    qx = np.abs(xx - cx) - hx
    qy = np.abs(yy - cy) - hy
    out_d = np.hypot(np.maximum(qx, 0.0), np.maximum(qy, 0.0))
    in_d = np.minimum(np.maximum(qx, qy), 0.0)
    return out_d + in_d - r


def build_kraft_and_burst_card(spec: dict) -> tuple[np.ndarray, np.ndarray]:
    """Construct the 480x737 aged kraft cardstock plaque, radial komiks burst, and 2-ply torn paper frame."""
    rng = np.random.default_rng(int(spec["seed"]))
    yy, xx = np.mgrid[0:CARD_H, 0:CARD_W].astype(np.float32)

    # 1. Outer rounded cardstock plaque: x in [9, 461], y in [22, 722], corner_radius = 52
    sdf_card = rounded_rect_sdf(xx, yy, 9.0, 22.0, 461.0, 722.0, 52.0)
    card_alpha = np.clip(0.5 - sdf_card / 1.25, 0.0, 1.0)

    # 2. Fibrous aged kraft paper texture + macro tear-contour noise (smooth, zero detached crumbs!)
    n_coarse = gaussian_filter(rng.standard_normal((CARD_H, CARD_W)).astype(np.float32), sigma=28.0)
    n_coarse /= float(n_coarse.std()) + 1e-6
    n_tear_macro = gaussian_filter(rng.standard_normal((CARD_H, CARD_W)).astype(np.float32), sigma=15.0)
    n_tear_macro /= float(n_tear_macro.std()) + 1e-6
    n_tear_mid = gaussian_filter(rng.standard_normal((CARD_H, CARD_W)).astype(np.float32), sigma=5.2)
    n_tear_mid /= float(n_tear_mid.std()) + 1e-6
    n_med = gaussian_filter(rng.standard_normal((CARD_H, CARD_W)).astype(np.float32), sigma=3.8)
    n_med /= float(n_med.std()) + 1e-6
    n_fine = gaussian_filter(rng.standard_normal((CARD_H, CARD_W)).astype(np.float32), sigma=0.95)
    n_fine /= float(n_fine.std()) + 1e-6

    grad = 0.55 * (xx / CARD_W) + 0.45 * (yy / CARD_H)
    kraft_base = (
        np.array([208.0, 170.0, 124.0], dtype=np.float32)[None, None, :] * (1.0 - grad[..., None] * 0.16)
        + n_coarse[..., None] * np.array([9.0, 7.5, 6.0], dtype=np.float32)
        + n_med[..., None] * np.array([6.5, 5.2, 4.0], dtype=np.float32)
        + n_fine[..., None] * np.array([5.0, 4.0, 3.2], dtype=np.float32)
    )

    flecks = rng.random((CARD_H, CARD_W)) < 0.0085
    flecks_soft = gaussian_filter(flecks.astype(np.float32), sigma=0.65)
    kraft_base -= flecks_soft[..., None] * np.array([75.0, 62.0, 48.0], dtype=np.float32)

    spot_centers = [
        (56.0 + rng.uniform(-10, 12), 682.0 + rng.uniform(-12, 10), 13.5),
        (82.0 + rng.uniform(-8, 12), 698.0 + rng.uniform(-8, 8), 8.0),
        (418.0 + rng.uniform(-12, 8), 692.0 + rng.uniform(-10, 8), 11.5),
        (412.0 + rng.uniform(-10, 8), 58.0 + rng.uniform(-8, 10), 9.5),
    ]
    for sx, sy, sr in spot_centers:
        d_sp = np.hypot(xx - sx, yy - sy) + n_med * 1.8
        spot_w = np.clip((sr - d_sp) / 2.2, 0.0, 1.0) * (0.55 + 0.45 * np.clip((d_sp - sr * 0.45) / (sr * 0.55), 0.0, 1.0))
        kraft_base -= spot_w[..., None] * np.array([46.0, 38.0, 28.0], dtype=np.float32)

    edge_dark = np.clip((sdf_card + 4.2) / 2.4, 0.0, 1.0)
    edge_bevel = np.clip(1.0 - np.abs(sdf_card + 5.6) / 1.8, 0.0, 1.0)
    border_col = np.array([44.0, 22.0, 9.0], dtype=np.float32)
    bevel_col = np.array([226.0, 190.0, 144.0], dtype=np.float32)
    kraft = kraft_base * (1.0 - 0.16 * np.clip((sdf_card + 22.0) / 22.0, 0.0, 1.0)[..., None])
    kraft = kraft * (1.0 - 0.35 * edge_bevel[..., None]) + bevel_col[None, None, :] * (0.35 * edge_bevel[..., None])
    kraft = kraft * (1.0 - 0.88 * edge_dark[..., None]) + border_col[None, None, :] * (0.88 * edge_dark[..., None])

    # 3. Inner 90s Filipino Komiks Radial Burst + Ben-Day Halftone Window
    cx, cy = 236.0, 332.0
    dx = xx - cx
    dy = yy - cy
    dist = np.hypot(dx, dy)
    norm_d = np.clip(dist / 410.0, 0.0, 1.0)
    ang = np.arctan2(dy, dx)

    c_in = np.array(spec["burst_inner"], dtype=np.float32)
    c_out = np.array(spec["burst_outer"], dtype=np.float32)
    burst = c_in[None, None, :] * (1.0 - norm_d[..., None] * 0.88) + c_out[None, None, :] * (norm_d[..., None] * 0.88)

    n_rays = 40
    ray_phases = rng.uniform(0.0, 2.0 * math.pi, size=n_rays)
    ray_reach = rng.uniform(0.55, 1.22, size=n_rays)
    sector = ((ang + math.pi) / (2.0 * math.pi)) * n_rays
    s_idx = np.floor(sector).astype(int) % n_rays
    s_frac = sector - np.floor(sector)
    tri_wave = 1.0 - 2.0 * np.abs(s_frac - 0.5)
    reach = ray_reach[s_idx] + 0.08 * np.sin(ang * 19.0 + ray_phases[s_idx])

    spike_outer = (
        np.clip((tri_wave - (0.54 - 0.25 * norm_d)) * 7.5, 0.0, 1.0)
        * np.clip((norm_d - 0.16) * 3.8, 0.0, 1.0)
        * np.clip((reach - norm_d * 0.70) * 3.2, 0.0, 1.0)
    )
    needle_wave = np.clip((np.sin(ang * 82.0 + 1.3) - 0.72) * 5.0, 0.0, 1.0) * np.clip((norm_d - 0.22) * 2.8, 0.0, 1.0)
    ray_mask = np.maximum(spike_outer * 0.90, needle_wave * 0.60)

    ray_distress = np.clip(0.86 + 0.24 * n_fine + 0.16 * n_med, 0.35, 1.0)
    ray_w = ray_mask * ray_distress
    ray_col = np.array(spec["ray_color"], dtype=np.float32)
    burst = burst * (1.0 - ray_w[..., None]) + ray_col[None, None, :] * ray_w[..., None]

    rot_x = xx * 0.8660254 - yy * 0.5
    rot_y = xx * 0.5 + yy * 0.8660254
    pitch = 7.6
    gx = (np.mod(rot_x, pitch) - 0.5 * pitch) / (0.5 * pitch)
    gy = (np.mod(rot_y, pitch) - 0.5 * pitch) / (0.5 * pitch)
    dot_r = np.hypot(gx, gy)
    dot_thresh = np.clip(0.18 + 0.62 * (norm_d ** 1.15), 0.0, 0.78)
    dots = np.clip((dot_thresh - dot_r) * 3.6, 0.0, 1.0) * 0.38
    ht_col = np.array(spec["halftone_color"], dtype=np.float32)
    burst = burst * (1.0 - dots[..., None]) + ht_col[None, None, :] * dots[..., None]

    speck_field = gaussian_filter(rng.standard_normal((CARD_H, CARD_W)).astype(np.float32), sigma=1.15)
    specks = np.clip((speck_field - 2.30) * 4.2, 0.0, 1.0) * np.clip(norm_d * 1.25, 0.25, 1.0)
    burst = burst * (1.0 - 0.78 * specks[..., None]) + ray_col[None, None, :] * (0.78 * specks[..., None])

    sdf_win_clean = rounded_rect_sdf(xx, yy, 24.0, 38.0, 446.0, 704.0, 36.0)
    win_vig = np.clip((sdf_win_clean + 26.0) / 26.0, 0.0, 1.0) * 0.22
    burst *= (1.0 - win_vig[..., None])

    # 4. Crisp Connected 2-Ply Torn Paper Corner & Edge Flaps (zero floating crumbs!)
    tear_jitter = 13.0 * n_tear_macro + 4.8 * n_tear_mid + 0.55 * n_fine
    tear_intrusion = np.full((CARD_H, CARD_W), -1e6, dtype=np.float32)
    corners = [
        (24.0, 38.0, 1.0, 1.0, rng.uniform(66.0, 86.0)),     # Top-left flap
        (446.0, 38.0, -1.0, 1.0, rng.uniform(72.0, 94.0)),   # Top-right flap
        (24.0, 704.0, 1.0, -1.0, rng.uniform(70.0, 92.0)),   # Bottom-left flap
        (446.0, 704.0, -1.0, -1.0, rng.uniform(64.0, 84.0)), # Bottom-right flap
    ]
    for cx0, cy0, sx_d, sy_d, reach_c in corners:
        u = (xx - cx0) * sx_d
        v = (yy - cy0) * sy_d
        diag = 0.70 * u + 0.71 * v
        flap = reach_c - diag + tear_jitter
        tear_intrusion = np.maximum(tear_intrusion, flap)

    side_tabs = [
        (24.0, rng.uniform(250.0, 440.0), 1.0, 0.0, 16.0, 72.0),
        (446.0, rng.uniform(260.0, 470.0), -1.0, 0.0, 15.0, 70.0),
        (rng.uniform(170.0, 300.0), 38.0, 0.0, 1.0, 12.0, 64.0),
        (rng.uniform(165.0, 305.0), 704.0, 0.0, -1.0, 14.0, 68.0),
    ]
    for tx, ty, nx, ny, depth_t, span_t in side_tabs:
        along = (xx - tx) * (-ny) + (yy - ty) * nx
        into = (xx - tx) * nx + (yy - ty) * ny
        envelope = np.clip(1.0 - (along / span_t) ** 2, 0.0, 1.0)
        tab_sdf = depth_t * envelope - into + 0.55 * tear_jitter
        tear_intrusion = np.maximum(tear_intrusion, tab_sdf)

    raw_open_sdf = (-sdf_win_clean) - 4.2 * np.abs(n_tear_mid) - 0.5 * n_fine
    burst_open_sdf = np.minimum(raw_open_sdf, -tear_intrusion)

    # Enforce 100% single connected open burst window (eliminates any detached paper islands)
    open_core = binary_fill_holes(burst_open_sdf > 0.0)
    open_sdf_clean = distance_transform_edt(open_core) - distance_transform_edt(~open_core)

    pulp_width = np.clip(7.5 + 3.8 * n_tear_mid + 0.8 * n_fine, 3.2, 13.5)
    pulp_open = binary_fill_holes(open_sdf_clean > pulp_width)
    pulp_sdf_clean = distance_transform_edt(pulp_open) - distance_transform_edt(~pulp_open)

    top_ply_w = np.clip(0.5 - open_sdf_clean / 1.15, 0.0, 1.0)
    pulp_ply_w = np.clip(0.5 - pulp_sdf_clean / 1.15, 0.0, 1.0)

    shadow_src = ((~pulp_open) & (sdf_card < -2.0)).astype(np.float32)
    tear_shadow = gaussian_filter(shadow_src, sigma=3.8) * (1.0 - pulp_ply_w)
    comp = burst * (1.0 - 0.54 * tear_shadow[..., None])

    pulp_rgb = (
        np.array([237.0, 215.0, 183.0], dtype=np.float32)[None, None, :]
        + n_fine[..., None] * np.array([7.0, 6.0, 5.0], dtype=np.float32)
        + n_med[..., None] * np.array([5.0, 4.0, 3.0], dtype=np.float32)
    )
    pulp_edge_ink = np.clip(1.0 - np.abs(pulp_sdf_clean) / 1.15, 0.0, 1.0)
    pulp_rgb = pulp_rgb * (1.0 - 0.52 * pulp_edge_ink[..., None]) + np.array([72.0, 44.0, 22.0], dtype=np.float32) * (0.52 * pulp_edge_ink[..., None])
    comp = comp * (1.0 - pulp_ply_w[..., None]) + pulp_rgb * pulp_ply_w[..., None]

    top_edge_ink = np.clip(1.0 - np.abs(open_sdf_clean) / 1.15, 0.0, 1.0)
    kraft_creased = kraft.copy()
    for cx0, cy0, sx_d, sy_d, _ in corners:
        c_line = np.abs((xx - cx0) * sx_d * 0.52 + (yy - cy0) * sy_d * 0.85 - 48.0 + 4.5 * n_tear_mid)
        crease_dark = np.clip(1.0 - c_line / 1.1, 0.0, 1.0) * (sdf_card < -6.0)
        crease_hi = np.clip(1.0 - np.abs(c_line - 1.6) / 1.1, 0.0, 1.0) * (sdf_card < -6.0)
        kraft_creased = kraft_creased * (1.0 - 0.36 * crease_dark[..., None]) + np.array([82.0, 52.0, 28.0], dtype=np.float32) * (0.36 * crease_dark[..., None])
        kraft_creased = np.clip(kraft_creased + crease_hi[..., None] * 18.0, 0.0, 255.0)

    kraft_creased = kraft_creased * (1.0 - 0.42 * top_edge_ink[..., None]) + np.array([88.0, 56.0, 28.0], dtype=np.float32) * (0.42 * top_edge_ink[..., None])
    comp = comp * (1.0 - top_ply_w[..., None]) + kraft_creased * top_ply_w[..., None]

    return np.clip(comp, 0.0, 255.0), card_alpha


# ---------------------------------------------------------------------------
# Supersampled Eye Catchlights, Golden-Hour Grading & Ink Composite
# ---------------------------------------------------------------------------


def grade_supersampled_kid(
    raw_png_path: Path,
    eye_px: list[float],
    key_side: int,
) -> tuple[Image.Image, int]:
    """Enhance warm golden-hour lighting, mango rim, honey iris crescent & dual catchlights at 768x1152."""
    img = Image.open(raw_png_path).convert("RGBA")
    arr = np.asarray(img, dtype=np.float32)
    rgb = arr[..., :3].copy()
    alpha = arr[..., 3] / 255.0
    h, w = alpha.shape
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)

    ex, ey = float(eye_px[0]), float(eye_px[1])
    in_eye_band = (
        (yy > max(20.0, ey - 105.0))
        & (yy < min(float(h - 20), ey + 105.0))
        & (xx > max(20.0, ex - 135.0))
        & (xx < min(float(w - 20), ex + 135.0))
        & (alpha > 0.90)
    )
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    lum = 0.299 * r + 0.587 * g + 0.114 * b

    sclera = in_eye_band & (r > 158.0) & (g > 148.0) & (b > 136.0) & (np.abs(r - b) < 46.0)
    near_sclera = binary_dilation(sclera, iterations=6)
    dark_eye = in_eye_band & near_sclera & (lum < 112.0) & (b < 98.0)
    iris_core = gaussian_filter(dark_eye.astype(np.float32), sigma=1.7) > 0.44
    labeled, n_comp = nd_label(iris_core)

    comps: list[tuple[int, int]] = []
    for cid in range(1, n_comp + 1):
        area = int((labeled == cid).sum())
        if area >= 26:
            comps.append((area, cid))
    comps.sort(reverse=True)

    catchlight_mask = np.zeros((h, w), dtype=bool)
    kdir = -1.0 if key_side < 0 else 1.0
    eyes_lit = 0

    for area, cid in comps[:2]:
        cmask = labeled == cid
        cys, cxs = np.where(cmask)
        icx = float(cxs.mean())
        icy = float(cys.mean())
        irad = max(math.sqrt(area / math.pi) * 1.20, 5.2)

        dx_i = (xx - icx) / irad
        dy_i = (yy - icy) / irad
        r_norm = np.hypot(dx_i, dy_i)
        crescent = dark_eye & (r_norm < 0.95) & (r_norm > 0.25) & (dy_i > 0.04)
        c_weight = np.clip((dy_i - 0.04) * (1.0 - np.abs(r_norm - 0.62) * 1.7), 0.0, 1.0)
        rgb[crescent, 0] = np.clip(rgb[crescent, 0] + 48.0 * c_weight[crescent], 0.0, 255.0)
        rgb[crescent, 1] = np.clip(rgb[crescent, 1] + 24.0 * c_weight[crescent], 0.0, 255.0)
        rgb[crescent, 2] = np.clip(rgb[crescent, 2] + 6.0 * c_weight[crescent], 0.0, 255.0)

        p1x = icx + kdir * irad * 0.28
        p1y = icy - irad * 0.28
        rad1 = float(np.clip(irad * 0.29, 2.3, 4.4))
        w1 = np.clip((rad1 + 0.65 - np.hypot(xx - p1x, yy - p1y)) / 1.15, 0.0, 1.0)

        p2x = icx - kdir * irad * 0.26
        p2y = icy + irad * 0.26
        rad2 = float(np.clip(irad * 0.14, 1.2, 2.2))
        w2 = np.clip((rad2 + 0.55 - np.hypot(xx - p2x, yy - p2y)) / 1.05, 0.0, 1.0)

        glint = np.maximum(w1, w2 * 0.90)
        g_active = glint > 0.02
        catchlight_mask |= glint > 0.35
        glint_col = np.array([255.0, 252.0, 240.0], dtype=np.float32)
        rgb[g_active] = rgb[g_active] * (1.0 - glint[g_active, None]) + glint_col * glint[g_active, None]
        eyes_lit += 1

    pil_rgb = Image.fromarray(np.clip(rgb, 0.0, 255.0).astype(np.uint8), mode="RGB")
    pil_rgb = ImageEnhance.Color(pil_rgb).enhance(1.16)
    pil_rgb = ImageEnhance.Contrast(pil_rgb).enhance(1.07)
    graded = np.asarray(pil_rgb, dtype=np.float32)

    warm_zone = (alpha > 0.15) & (~sclera) & (~catchlight_mask)
    graded[warm_zone, 0] = np.clip(graded[warm_zone, 0] * 1.05 + 3.0, 0.0, 255.0)
    graded[warm_zone, 1] = np.clip(graded[warm_zone, 1] * 0.99 + 1.0, 0.0, 255.0)
    graded[warm_zone, 2] = np.clip(graded[warm_zone, 2] * 0.91, 0.0, 255.0)

    fg = binary_fill_holes(alpha > 0.38)
    inner_rim = binary_erosion(fg, iterations=2) & (~binary_erosion(fg, iterations=5))
    side_x = (xx - 0.5 * w) / (0.5 * w)
    rim_w = np.clip((-kdir * side_x) * 0.60 + (1.0 - yy / float(h)) * 0.40, 0.0, 1.0)
    rw_mask = inner_rim & (~sclera) & (~catchlight_mask)
    mango_col = np.array([255.0, 198.0, 74.0], dtype=np.float32)
    blend_m = 0.28 * rim_w[rw_mask, None]
    graded[rw_mask] = graded[rw_mask] * (1.0 - blend_m) + mango_col * blend_m

    graded[sclera] = np.clip(rgb[sclera] * np.array([1.01, 0.99, 0.96], dtype=np.float32), 0.0, 252.0)
    graded[catchlight_mask] = rgb[catchlight_mask]

    out_rgba = np.dstack([np.clip(graded, 0.0, 255.0), np.clip(alpha * 255.0, 0.0, 255.0)]).astype(np.uint8)
    return Image.fromarray(out_rgba, mode="RGBA"), eyes_lit


def composite_kid_onto_card(
    card_rgb: np.ndarray,
    card_alpha: np.ndarray,
    kid_rgba_hi: Image.Image,
) -> Image.Image:
    """Fit action-posed kid onto 480x737 card so limbs pop boldly over torn frame with zero edge clipping."""
    hi_arr = np.asarray(kid_rgba_hi, dtype=np.float32)
    ys, xs = np.where(hi_arr[..., 3] > 16.0)
    if len(ys) == 0:
        raise RuntimeError("Empty character alpha in supersampled render")

    pad = 4
    x0 = max(int(xs.min()) - pad, 0)
    y0 = max(int(ys.min()) - pad, 0)
    x1 = min(int(xs.max()) + pad + 1, kid_rgba_hi.width)
    y1 = min(int(ys.max()) + pad + 1, kid_rgba_hi.height)
    trimmed = kid_rgba_hi.crop((x0, y0, x1, y1))

    max_w, max_h = 462.0, 712.0
    scale = min(max_w / float(trimmed.width), max_h / float(trimmed.height))
    nw = int(round(trimmed.width * scale))
    nh = int(round(trimmed.height * scale))
    scaled = trimmed.resize((nw, nh), Image.Resampling.LANCZOS)

    px = int(round(237.0 - 0.5 * nw))
    px = max(6, min(CARD_W - nw - 6, px))
    py = int(round(10.0 + (max_h - nh) * 0.42))
    py = max(6, min(CARD_H - nh - 8, py))

    fg_canvas = Image.new("RGBA", (CARD_W, CARD_H), (0, 0, 0, 0))
    fg_canvas.paste(scaled, (px, py))
    fg_arr = np.asarray(fg_canvas, dtype=np.float32)
    fg_rgb = fg_arr[..., :3].copy()
    fg_alpha = fg_arr[..., 3] / 255.0

    # Smooth anti-aliased #17131A charcoal comic ink contour strictly on true outer silhouette
    fg_mask = fg_alpha > 0.42
    dist_out = distance_transform_edt(~fg_mask)
    dist_in = distance_transform_edt(fg_mask)
    outer_ink_w = np.clip(2.05 - dist_out, 0.0, 1.0) * (1.0 - fg_alpha)
    inner_ink_w = np.clip(1.45 - dist_in, 0.0, 1.0) * fg_alpha * 0.55

    fg_rgb = fg_rgb * (1.0 - inner_ink_w[..., None]) + INK_RGB[None, None, :] * inner_ink_w[..., None]

    kid_comb_alpha = np.clip(fg_alpha + outer_ink_w, 0.0, 1.0)
    safe_div = np.maximum(kid_comb_alpha, 1e-5)
    kid_comb_rgb = (fg_rgb * fg_alpha[..., None] + INK_RGB[None, None, :] * outer_ink_w[..., None]) / safe_div[..., None]

    sh_shifted = np.zeros_like(fg_alpha)
    sh_shifted[6:, 4:] = fg_alpha[:-6, :-4]
    drop_shadow = gaussian_filter(sh_shifted, sigma=6.5) * card_alpha * 0.36
    lit_card_rgb = card_rgb * (1.0 - drop_shadow[..., None])

    out_alpha = kid_comb_alpha + card_alpha * (1.0 - kid_comb_alpha)
    denom = np.maximum(out_alpha, 1e-5)
    out_rgb = (
        kid_comb_rgb * kid_comb_alpha[..., None]
        + lit_card_rgb * (card_alpha * (1.0 - kid_comb_alpha))[..., None]
    ) / denom[..., None]

    zero_m = out_alpha < (4.0 / 255.0)
    out_alpha[zero_m] = 0.0
    out_rgb[zero_m] = 0.0

    rgba_u8 = np.dstack([
        np.clip(out_rgb, 0.0, 255.0).astype(np.uint8),
        np.clip(np.round(out_alpha * 255.0), 0.0, 255.0).astype(np.uint8),
    ])
    return Image.fromarray(rgba_u8, mode="RGBA")


def verify_card_webp(path: Path) -> dict[str, float | int]:
    with Image.open(path) as im:
        if im.mode != "RGBA":
            raise AssertionError(f"{path.name}: expected RGBA, got {im.mode}")
        w, h = im.size
        if (w, h) != (CARD_W, CARD_H):
            raise AssertionError(f"{path.name}: expected {(CARD_W, CARD_H)}, got {(w, h)}")
        aspect = float(w) / float(h)
        if abs(aspect - EXPECTED_CARD_ASPECT) >= 0.005:
            raise AssertionError(f"{path.name}: aspect {aspect:.6f} != {EXPECTED_CARD_ASPECT:.6f}")
        arr = np.asarray(im)
        alpha = arr[..., 3]
        transparent_px = int((alpha == 0).sum())
        if transparent_px < 15000:
            raise AssertionError(f"{path.name}: too few transparent outer pixels ({transparent_px})")
        for corner in (alpha[:6, :6], alpha[:6, -6:], alpha[-6:, :6], alpha[-6:, -6:]):
            if int(corner.max()) != 0:
                raise AssertionError(f"{path.name}: outer corner is not transparent")
    return {
        "bytes": path.stat().st_size,
        "transparent_px": transparent_px,
    }


def main() -> None:
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    blender_bin = find_blender()

    glb_hashes: dict[Path, str] = {}
    for spec in CARDS_SPEC:
        gp = CHAR_DIR / f"{spec['id']}.glb"
        if not gp.exists():
            raise FileNotFoundError(f"Missing character GLB: {gp}")
        glb_hashes[gp] = sha256_file(gp)

    print(f"[1/3] Rendering full-body 23mm wide-angle action poses in {blender_bin.name}...")
    with tempfile.TemporaryDirectory(prefix="kalyeah_charcards_new4_") as tmp:
        tmp_dir = Path(tmp)
        jobs = []
        for spec in CARDS_SPEC:
            kid_id = spec["id"]
            jobs.append(
                {
                    "id": kid_id,
                    "glb": str(CHAR_DIR / f"{kid_id}.glb"),
                    "clip": spec["clip"],
                    "frame": spec["frame"],
                    "bone_tweaks": spec["bone_tweaks"],
                    "cam_yaw": spec["cam_yaw"],
                    "cam_pitch": spec["cam_pitch"],
                    "cam_roll": spec["cam_roll"],
                    "head_tilt_deg": spec["head_tilt_deg"],
                    "gaze_blend": spec["gaze_blend"],
                    "key_side": spec["key_side"],
                    "raw_png": str(tmp_dir / f"{kid_id}_action_raw.png"),
                }
            )

        manifest_path = tmp_dir / "jobs.json"
        results_path = tmp_dir / "results.json"
        script_path = tmp_dir / "blender_action.py"
        manifest_path.write_text(json.dumps(jobs, indent=2), encoding="utf-8")
        script_path.write_text(BLENDER_ACTION_SCRIPT, encoding="utf-8")

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
            raise RuntimeError(f"Blender action render failed with exit code {proc.returncode}")

        meta_map = json.loads(results_path.read_text(encoding="utf-8"))

        print("[2/3] Compositing worn kraft frames, 90s komiks bursts, 2-ply tears & inked figures...")
        for spec in CARDS_SPEC:
            kid_id = spec["id"]
            meta = meta_map[kid_id]
            card_rgb, card_alpha = build_kraft_and_burst_card(spec)
            graded_kid, eyes_lit = grade_supersampled_kid(
                tmp_dir / f"{kid_id}_action_raw.png",
                meta["eye_px"],
                key_side=int(spec["key_side"]),
            )
            final_card = composite_kid_onto_card(card_rgb, card_alpha, graded_kid)
            dst_webp = OUT_DIR / f"{kid_id}.webp"
            final_card.save(dst_webp, "WEBP", quality=88, method=6, alpha_quality=100, exact=True)
            stats = verify_card_webp(dst_webp)
            print(
                f"  -> {dst_webp.relative_to(REPO_ROOT)}: {CARD_W}x{CARD_H} RGBA, "
                f"{stats['bytes'] / 1024:.1f} KiB, alpha0={stats['transparent_px']} px, eyes_lit={eyes_lit}"
            )

    print("[3/3] Verifying source GLB immutability...")
    for gp, h_before in glb_hashes.items():
        if sha256_file(gp) != h_before:
            raise RuntimeError(f"Integrity violation: {gp} modified!")
    print("Done! All 4 new character cards rendered and verified cleanly.")


if __name__ == "__main__":
    main()
