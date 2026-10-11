"""Rigs the Meshy Junjun model (headless Blender 4.5): armature, automatic
weights, and three looping actions (idle, run, cast); exports a rigged .glb.

    python tools/blender/rig_junjun.py -- <in.glb> <out_dir> [export|test]

Bone positions come from measuring the mesh (feet at z = -0.951, 1.9 tall, faces -Y).
"""
import math
import sys

import numpy as np

import bpy
import bmesh
from mathutils import Euler, Vector

V = Vector
FLOOR = -0.951


def build_armature():
    arm_data = bpy.data.armatures.new("JunjunRig")
    rig = bpy.data.objects.new("JunjunRig", arm_data)
    bpy.context.scene.collection.objects.link(rig)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode="EDIT")
    eb = arm_data.edit_bones

    def bone(name, head, tail, parent=None, connect=False):
        b = eb.new(name)
        b.head, b.tail = V(head), V(tail)
        if parent:
            b.parent = eb[parent]
            b.use_connect = connect
        return b

    bone("root", (0, 0, FLOOR), (0, 0, FLOOR + 0.12))
    bone("hips", (0, 0, -0.46), (0, 0, -0.30), "root")
    bone("spine", (0, 0, -0.30), (0, 0, -0.02), "hips", True)
    bone("chest", (0, 0, -0.02), (0, 0, 0.26), "spine", True)
    bone("neck", (0, 0, 0.26), (0, 0, 0.38), "chest", True)
    bone("head", (0, 0, 0.38), (0, 0, 0.95), "neck", True)
    for s, side in ((1, "L"), (-1, "R")):
        bone(f"clavicle.{side}", (s * 0.05, 0, 0.19), (s * 0.20, 0, 0.19), "chest")
        bone(f"upper_arm.{side}", (s * 0.20, 0, 0.19), (s * 0.30, -0.02, -0.08), f"clavicle.{side}", True)
        bone(f"forearm.{side}", (s * 0.30, -0.02, -0.08), (s * 0.335, -0.08, -0.26), f"upper_arm.{side}", True)
        bone(f"hand.{side}", (s * 0.335, -0.08, -0.26), (s * 0.34, -0.10, -0.42), f"forearm.{side}", True)
        bone(f"thigh.{side}", (s * 0.125, 0, -0.46), (s * 0.15, -0.01, -0.71), "hips")
        bone(f"shin.{side}", (s * 0.15, -0.01, -0.71), (s * 0.17, 0.0, -0.88), f"thigh.{side}", True)
        bone(f"foot.{side}", (s * 0.17, 0.0, -0.88), (s * 0.19, -0.14, -0.935), f"shin.{side}", True)
    bpy.ops.object.mode_set(mode="OBJECT")
    return rig


# influence radius (m) of every bone: a vertex follows a bone only inside this
# distance, so the arms at his sides do not drag the torso
RADIUS = {"root": 0.0, "hips": 0.26, "spine": 0.26, "chest": 0.30, "neck": 0.16, "head": 0.62,
          "clavicle": 0.11, "upper_arm": 0.105, "forearm": 0.095, "hand": 0.125,
          "thigh": 0.17, "shin": 0.125, "foot": 0.14}


def _seg_dist(points, a, b):
    ab = b - a
    t = np.clip(((points - a) @ ab) / (ab @ ab), 0.0, 1.0)
    return np.linalg.norm(points - (a + t[:, None] * ab), axis=1)


# bones each clothing part may follow, so a hand resting on the shorts never drags them
ALLOWED = {
    "Red_Shorts": {"hips", "spine", "thigh", "shin"},
    "White_Sando": {"hips", "spine", "chest", "neck", "clavicle", "upper_arm", "head"},
    "Blue_Slippers": {"shin", "foot", "thigh"},
    "Dark_Accents": {"hips", "spine", "chest", "thigh", "shin", "foot"},
    "Hair": {"head", "neck"},
    "Face": {"head", "neck"},
}


def skin(mesh, rig):
    """Weights by position, not by shell: every vertex follows the bones within
    their radius, so the body, shorts and shirt layers at the same place always
    agree and never tear apart. Up to 4 bones per vertex."""
    pts = np.array([list(mesh.matrix_world @ v.co) for v in mesh.data.vertices])
    names = [b.name for b in rig.data.bones]
    dist = np.zeros((len(pts), len(names)))
    for j, bone in enumerate(rig.data.bones):
        dist[:, j] = _seg_dist(pts, np.array(bone.head_local), np.array(bone.tail_local))
    w = np.zeros_like(dist)
    for j, n in enumerate(names):
        r = RADIUS[n.split(".")[0]]
        if r > 0:
            w[:, j] = np.clip(1.0 - dist[:, j] / r, 0.0, 1.0) ** 2
    for prefix, bones in ALLOWED.items():
        if mesh.name.startswith(prefix):
            for j, n in enumerate(names):
                if n.split(".")[0] not in bones:
                    w[:, j] = 0.0
            dist = dist + np.array([0.0 if n.split(".")[0] in bones else 99.0 for n in names])
    # the head is rigid: everything above the neck follows the head bone only, so hair,
    # eyes and skin never slide against each other
    head = names.index("head")
    top = pts[:, 2] > 0.40
    w[top] = 0.0
    w[top, head] = 1.0
    # nobody in range (the hair tips, the toes): follow the nearest bone
    none = w.sum(axis=1) < 1e-6
    nearest = np.argmin(dist + np.where(np.array([n == "root" for n in names]), 9.0, 0.0), axis=1)
    w[none, nearest[none]] = 1.0
    if mesh.name.startswith("Body_Skin"):
        # this part is loose chips (knuckles, toes, wrist patches): every chip moves as one
        # rigid piece, or a chip that straddles two bones is stretched into a long spike
        bm = bmesh.new()
        bm.from_mesh(mesh.data)
        bm.verts.ensure_lookup_table()
        seen = set()
        for v in bm.verts:
            if v.index in seen:
                continue
            seen.add(v.index)
            comp, stack = [], [v]
            while stack:
                a = stack.pop()
                comp.append(a.index)
                for e in a.link_edges:
                    b = e.other_vert(a)
                    if b.index not in seen:
                        seen.add(b.index)
                        stack.append(b)
            centre = pts[comp].mean(axis=0)
            w[comp] = w[comp[int(np.argmin(np.linalg.norm(pts[comp] - centre, axis=1)))]]
        bm.free()
    # keep the 4 strongest and normalise
    order = np.argsort(-w, axis=1)
    keep = np.zeros_like(w)
    rows = np.arange(len(w))[:, None]
    keep[rows, order[:, :4]] = w[rows, order[:, :4]]
    keep /= keep.sum(axis=1, keepdims=True)
    mesh.parent = rig
    mesh.matrix_parent_inverse = rig.matrix_world.inverted()
    mod = mesh.modifiers.new("Armature", "ARMATURE")
    mod.object = rig
    groups = [mesh.vertex_groups.new(name=n) for n in names]
    for j, g in enumerate(groups):
        idx = np.nonzero(keep[:, j] > 0.001)[0]
        for i in idx:
            g.add([int(i)], float(keep[i, j]), "REPLACE")


def pose(rig, name, values):
    """values: {bone: (x, y, z) euler degrees} applied as the pose."""
    for b in rig.pose.bones:
        b.rotation_mode = "XYZ"
        b.rotation_euler = Euler((0, 0, 0))
        b.location = V((0, 0, 0))
    for bone, rot in values.items():
        rig.pose.bones[bone].rotation_euler = Euler(tuple(math.radians(a) for a in rot))


def key(rig, frame):
    for b in rig.pose.bones:
        b.keyframe_insert("rotation_euler", frame=frame)
        b.keyframe_insert("location", frame=frame)


def make_action(rig, name, frames, length):
    action = bpy.data.actions.new(name)
    rig.animation_data_create()
    rig.animation_data.action = action
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode="POSE")
    for f, values, loc in frames:
        pose(rig, name, values)
        for bn, l in (loc or {}).items():
            rig.pose.bones[bn].location = V(l)
        key(rig, f)
    bpy.ops.object.mode_set(mode="OBJECT")
    action.use_fake_user = True
    action.frame_range = (frames[0][0], length)
    return action


def build_actions(rig):
    """idle (breathing), run (8-frame-per-step loop, 24 frames), cast (throw)."""
    # Pose rotations are about each bone's local axes; with bones along Z, X bends
    # forward/back (swing) and Y twists. Values are tuned by looking at test renders.
    idle = [
        (1, {"chest": (0, 0, 0)}, {"hips": (0, 0, 0)}),
        (13, {"chest": (2.5, 0, 0), "head": (-1.5, 0, 0), "upper_arm.L": (0, 0, 3), "upper_arm.R": (0, 0, -3)}, {"hips": (0, 0, 0.008)}),
        (25, {"chest": (0, 0, 0)}, {"hips": (0, 0, 0)}),
    ]
    run_a = {"thigh.L": (-45, 0, 0), "shin.L": (55, 0, 0), "foot.L": (-10, 0, 0),
             "thigh.R": (40, 0, 0), "shin.R": (8, 0, 0), "foot.R": (5, 0, 0),
             "upper_arm.L": (45, 0, 0), "forearm.L": (-70, 0, 0), "upper_arm.R": (-45, 0, 0), "forearm.R": (-70, 0, 0),
             "spine": (-8, 0, 0), "chest": (-4, 0, -6), "head": (6, 0, 4)}
    run_b = {"thigh.R": (-45, 0, 0), "shin.R": (55, 0, 0), "foot.R": (-10, 0, 0),
             "thigh.L": (40, 0, 0), "shin.L": (8, 0, 0), "foot.L": (5, 0, 0),
             "upper_arm.R": (45, 0, 0), "forearm.R": (-70, 0, 0), "upper_arm.L": (-45, 0, 0), "forearm.L": (-70, 0, 0),
             "spine": (-8, 0, 0), "chest": (-4, 0, 6), "head": (6, 0, -4)}
    run_mid = {"thigh.L": (-5, 0, 0), "shin.L": (20, 0, 0), "thigh.R": (-5, 0, 0), "shin.R": (20, 0, 0),
               "spine": (-8, 0, 0), "head": (6, 0, 0), "upper_arm.L": (0, 0, 0), "upper_arm.R": (0, 0, 0),
               "forearm.L": (-70, 0, 0), "forearm.R": (-70, 0, 0)}
    run = [(1, run_a, {"hips": (0, 0, 0.0)}), (7, run_mid, {"hips": (0, 0, 0.04)}), (13, run_b, {"hips": (0, 0, 0.0)}),
           (19, run_mid, {"hips": (0, 0, 0.04)}), (25, run_a, {"hips": (0, 0, 0.0)})]
    cast = [
        (1, {"upper_arm.R": (0, 0, 4), "chest": (0, 0, 0)}, {}),
        (8, {"upper_arm.R": (40, 0, 70), "forearm.R": (-60, 0, 0), "chest": (-4, 0, -10), "spine": (3, 0, -4), "head": (0, 0, 4)}, {}),
        (14, {"upper_arm.R": (-95, 0, 55), "forearm.R": (-8, 0, 0), "chest": (6, 0, 12), "spine": (-4, 0, 5), "head": (-4, 0, -3)}, {}),
        (24, {"upper_arm.R": (0, 0, 4), "chest": (0, 0, 0)}, {}),
    ]
    make_action(rig, "idle", idle, 25)
    make_action(rig, "run", run, 25)
    make_action(rig, "cast", cast, 24)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:]
    src, out_dir = argv[0], argv[1]
    mode = argv[2] if len(argv) > 2 else "export"
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=src)
    meshes = [o for o in bpy.data.objects if o.type == "MESH"]
    rig = build_armature()
    for mesh in meshes:  # a file with separate body / clothes / hair parts keeps them separate
        skin(mesh, rig)
    build_actions(rig)
    if mode == "test":
        import rig_test
        rig_test.render_poses(out_dir, rig)
    if mode == "export":
        rig.animation_data.action = bpy.data.actions["idle"]
        bpy.ops.object.select_all(action="SELECT")
        bpy.ops.export_scene.gltf(filepath=f"{out_dir}/junjun_rigged.glb", export_format="GLB",
                                  export_animations=True, export_animation_mode="ACTIONS",
                                  export_nla_strips=False)


if __name__ == "__main__":
    main()
