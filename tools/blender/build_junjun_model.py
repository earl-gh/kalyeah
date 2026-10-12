"""Builds the in-game Junjun: the Meshy boy (Mixamo skeleton) made light for phones and
given the game's animations (idle, run, cast, stun, ko), exported as one GLB.

usage: python build_junjun_model.py -- model.glb out.glb [target_triangles]
 * mesh decimated to ~target triangles (weights kept), one 1024 px base-colour texture,
   no normal / metal-roughness maps (the game lights him matte);
 * run is the model's own Meshy "Running" loop; the others are made with throw_boy.pose_frame (spine only twists about the vertical,
   feet IK-planted, arms follow the torso), 30 fps, loops are seamless.
The source GLB is the owner's Meshy export and is not stored in the repo.
"""
import copy
import math
import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import throw_boy as T  # noqa: E402

FPS = 30
BASE = dict(hips=(0, 0, 0), hyaw=0, cyaw=0, lean=0, hy=0, hp=0, sr=(0, 0), sl=(0, 0))
V = lambda x, y, z: (x, y, z)


def key(**kw):
    d = dict(BASE)
    d.update(kw)
    return d


def torso_arm(sign, swing, bend, out=0.2):
    """Arm and forearm directions (torso frame): `swing` degrees forward of straight down,
    the elbow bent `bend` degrees further forward."""
    a, b = math.radians(swing), math.radians(swing * 0.6 + bend)
    return (V(sign * out, -math.sin(a), -math.cos(a)), V(sign * 0.12, -math.sin(b), -math.cos(b)))


def idle(i, n):
    th = math.tau * i / n
    s, c = math.sin(th), math.cos(th)
    sw = math.sin(th - 0.9)
    arm_r, fore_r = torso_arm(-1, 4 + 3 * sw, 22 + 4 * s)
    arm_l, fore_l = torso_arm(1, 4 - 3 * sw, 22 - 4 * s)
    return key(hips=(0.012 * s, 0, -0.012 - 0.008 * (1 - c) / 2), lean=2 + 1.4 * s, hy=5 * s, hp=1.5 * c,
               sr=(0, 1.4 * s), sl=(0, 1.4 * s), arm_r=arm_r, fore_r=fore_r, hand_r=fore_r,
               arm_l=arm_l, fore_l=fore_l, hand_l=fore_l,
               foot_l=(0.13, 0.0, 0.0), foot_r=(-0.13, 0.0, 0.0))


def stun(i, n):
    th = math.tau * i / n
    s, c = math.sin(th), math.cos(th)
    return key(hips=(0.04 * s, 0, -0.1), lean=6 + 3 * s, hyaw=5 * s, cyaw=-8 * s, hy=-10 * math.sin(th + 0.8),
               hp=10 + 3 * c, sr=(0, -3), sl=(0, -3),
               arm_r=V(-0.6, -0.1 + 0.08 * s, -0.78), fore_r=V(-0.55, -0.25 + 0.1 * s, -0.8), hand_r=V(-0.5, -0.3, -0.8),
               arm_l=V(0.6, -0.1 - 0.08 * s, -0.78), fore_l=V(0.55, -0.25 - 0.1 * s, -0.8), hand_l=V(0.5, -0.3, -0.8),
               foot_l=(0.2 + 0.03 * s, 0.0, 0.0), foot_r=(-0.2 + 0.03 * s, 0.05, 0.0))


def ko(i, n):
    u = i / (n - 1)
    e = u * u * (3 - 2 * u)
    ra, rf = torso_arm(-1, 4, 22)
    return key(hips=(0, 0.1 * e, -0.05 * e), lean=-6 - 14 * e, hp=-10 * e, sr=(0, 8 * e), sl=(0, 8 * e),
               arm_r=V(-0.2 - 0.7 * e, -0.05 + 0.1 * e, -1 + 1.0 * e), fore_r=V(-0.1 - 0.8 * e, 0.1 * e, -0.9 + 1.0 * e),
               hand_r=V(-0.1 - 0.8 * e, 0.1 * e, -0.9 + 1.0 * e),
               arm_l=V(0.2 + 0.7 * e, -0.05 + 0.1 * e, -1 + 1.0 * e), fore_l=V(0.1 + 0.8 * e, 0.1 * e, -0.9 + 1.0 * e),
               hand_l=V(0.1 + 0.8 * e, 0.1 * e, -0.9 + 1.0 * e),
               foot_l=(0.13 + 0.05 * e, -0.1 * e, 0.0), foot_r=(-0.13 - 0.05 * e, 0.1 * e, 0.28 * e))


def make_action(rig, rest, name, frames, builder):
    action = bpy.data.actions.new(name)
    action.use_fake_user = True
    rig.animation_data.action = action
    for f in range(1, frames + 1):
        T.KEYS = [(1, builder(f - 1, frames)), (2, builder(f - 1, frames))]
        T.pose_frame(rig, rest, f)
    return action


def make_cast(rig, rest):
    """The throw from the stride landing to the follow-through, feet kept near the idle
    stance so it blends with idle and run without a hop."""
    keys = copy.deepcopy(T.THROW_KEYS)
    for _, k in keys:
        k["foot_l"], k["foot_r"] = (0.15, -0.28, 0.0), (-0.14, 0.12, 0.0)
        k["hips"] = (k["hips"][0] * 0.6, k["hips"][1] * 0.45, k["hips"][2] * 0.6)
    action = bpy.data.actions.new("cast")
    action.use_fake_user = True
    rig.animation_data.action = action
    T.KEYS = keys
    for f in range(1, 16):
        T.pose_frame(rig, rest, f, 19 + f)
    return action


def lighten_mesh(mesh, target):
    """Decimate with the weights kept, one small base-colour texture, matte material."""
    bpy.context.view_layer.objects.active = mesh
    tris = sum(len(p.vertices) - 2 for p in mesh.data.polygons)
    dec = mesh.modifiers.new("Decimate", "DECIMATE")
    dec.ratio = min(1.0, target / tris)
    bpy.ops.object.modifier_move_to_index(modifier=dec.name, index=0)
    bpy.ops.object.modifier_apply(modifier=dec.name)
    mat = mesh.data.materials[0]
    node = [n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED"][0]
    for name in ("Metallic", "Roughness", "Normal"):
        for link in list(node.inputs[name].links):
            mat.node_tree.links.remove(link)
    node.inputs["Metallic"].default_value = 0.0
    node.inputs["Roughness"].default_value = 1.0
    image = node.inputs["Base Color"].links[0].from_node.image
    image.scale(1024, 1024)
    return sum(len(p.vertices) - 2 for p in mesh.data.polygons)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:]
    src, out = argv[0], argv[1]
    target = int(argv[2]) if len(argv) > 2 else 5500
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=src)
    for name in ("Icosphere",):
        if name in bpy.data.objects:
            bpy.data.objects.remove(bpy.data.objects[name])
    rig = bpy.data.objects["target_character"]
    mesh = bpy.data.objects["Mesh0"]
    # the run is the model's own Meshy/Mixamo "Running" loop: frames 0..16 end where they start,
    # so the last frame is dropped to loop without a double frame
    run = bpy.data.actions["Running"]
    run.name = "run"
    run.use_fake_user = True
    run.use_frame_range = True
    run.frame_start, run.frame_end = 0, 15
    print("TRIS", lighten_mesh(mesh, target))
    rig.animation_data_create()
    rest = {}
    for b in rig.data.bones:
        n = b.name.replace(T.P, "")
        rest[n] = {"rot": b.matrix_local.to_3x3(), "head": b.head_local.copy(), "len": b.length,
                   "dir": (b.tail_local - b.head_local).normalized()}
    bpy.context.scene.render.fps = FPS
    T.THROW_KEYS = copy.deepcopy(T.KEYS)
    make_action(rig, rest, "idle", 72, idle)
    make_action(rig, rest, "stun", 36, stun)
    make_action(rig, rest, "ko", 14, ko)
    make_cast(rig, rest)
    rig.animation_data.action = bpy.data.actions["idle"]
    bpy.context.scene.frame_set(1)
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.gltf(filepath=out, export_format="GLB", export_animations=True,
                              export_animation_mode="ACTIONS", export_image_format="JPEG",
                              export_jpeg_quality=88, export_force_sampling=True)
    print("EXPORTED", out, os.path.getsize(out))


if __name__ == "__main__":
    main()
