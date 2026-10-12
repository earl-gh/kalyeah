"""'Throw' animation for the Meshy Mixamo-rigged boy, built the way an animator would:

  * a few key poses (ready, anticipation, wind-up, stride, release, follow-through, settle)
    smoothed with a spline, so nothing moves in straight lines or stops dead at a key;
  * a kinetic chain: hips lead, the spine, shoulder, upper arm, forearm and hand each follow
    a few frames later (drag / overlap), which is what makes a throw look like a whip;
  * feet are planted with two-bone IK (knees always bend forward, no sliding or floating);
  * the head stays level and tracks the target while the torso twists;
  * slightly bent elbows, wrists that snap at the release.

Run: python throw_boy.py -- in.glb out_dir [test|export]
Armature space: +Z up, the boy faces -Y, his right (throwing) side is -X.
"""
import math
import sys

import bpy
import bmesh
from mathutils import Matrix, Vector

P = "mixamorig:"
V = lambda x, y, z: (x, y, z)  # a direction, normalised when used

FRAMES = 42
RELEASE = 28
# key time -> pose. dirs are world directions head->tail of each bone.
KEYS = [
    (1, dict(  # ready, ball held low in the right hand
        hips=(0, 0, 0), hyaw=0, cyaw=0, lean=0,
        sh_r=V(-1, -0.05, 0.1), sh_l=V(1, -0.05, 0.1),
        arm_r=V(-0.3, -0.1, -1), fore_r=V(-0.12, -0.85, -0.5), hand_r=V(-0.1, -0.8, -0.6),
        arm_l=V(0.3, -0.05, -1), fore_l=V(0.15, -0.45, -0.88), hand_l=V(0.1, -0.5, -0.85),
        foot_l=(0.12, 0.0, 0.0), foot_r=(-0.12, 0.0, 0.0), head=V(0, -0.1, 1))),
    (8, dict(  # anticipation: sink a little, turn away, arm swings down and back
        hips=(0, 0.04, -0.05), hyaw=-8, cyaw=-16, lean=-3,
        sh_r=V(-1, 0.1, 0.05), sh_l=V(1, -0.1, 0.1),
        arm_r=V(-0.35, 0.25, -0.92), fore_r=V(-0.2, -0.2, -0.95), hand_r=V(-0.1, -0.2, -0.95),
        arm_l=V(0.4, -0.35, -0.85), fore_l=V(0.3, -0.7, -0.65), hand_l=V(0.2, -0.7, -0.6),
        foot_l=(0.13, -0.1, 0.1), foot_r=(-0.13, 0.02, 0.0), head=V(0, -0.15, 1))),
    (15, dict(  # wind-up: lean back, elbow high, ball behind the ear, lead foot reaching
        hips=(0.02, 0.1, -0.09), hyaw=-26, cyaw=-44, lean=-14,
        sh_r=V(-1, 0.35, 0.3), sh_l=V(1, -0.3, 0.05),
        arm_r=V(-0.62, 0.42, 0.5), fore_r=V(-0.1, 0.45, 0.88), hand_r=V(-0.05, 0.4, 0.9),
        arm_l=V(0.45, -0.85, 0.2), fore_l=V(0.4, -0.9, 0.1), hand_l=V(0.4, -0.9, 0.0),
        foot_l=(0.17, -0.42, 0.28), foot_r=(-0.14, 0.08, 0.0), head=V(0.02, -0.2, 1))),
    (19, dict(  # stride lands: hips begin to open, the forearm lags behind (lay-back)
        hips=(0.02, -0.02, -0.12), hyaw=-6, cyaw=-34, lean=-8,
        sh_r=V(-1, 0.3, 0.3), sh_l=V(1, -0.2, 0.0),
        arm_r=V(-0.65, 0.3, 0.38), fore_r=V(-0.1, 0.78, 0.6), hand_r=V(-0.1, 0.75, 0.55),
        arm_l=V(0.5, -0.55, 0.25), fore_l=V(0.4, -0.7, 0.2), hand_l=V(0.4, -0.7, 0.1),
        foot_l=(0.17, -0.56, 0.0), foot_r=(-0.14, 0.08, 0.0), head=V(0.0, -0.18, 1))),
    (24, dict(  # release: hips and chest snap forward, arm whips through, wrist snaps
        hips=(0.0, -0.2, -0.13), hyaw=28, cyaw=40, lean=24,
        sh_r=V(-1, -0.3, 0.2), sh_l=V(1, 0.2, -0.05),
        arm_r=V(-0.3, -0.82, 0.48), fore_r=V(-0.1, -0.97, 0.22), hand_r=V(-0.05, -0.9, 0.45),
        arm_l=V(0.55, 0.3, -0.5), fore_l=V(0.35, 0.5, -0.75), hand_l=V(0.3, 0.5, -0.8),
        foot_l=(0.17, -0.56, 0.0), foot_r=(-0.14, 0.06, 0.1), head=V(0.02, -0.3, 1))),
    (30, dict(  # follow-through: arm sweeps across, back foot drags forward
        hips=(0.0, -0.27, -0.15), hyaw=40, cyaw=52, lean=34,
        sh_r=V(-1, -0.45, 0.1), sh_l=V(1, 0.3, -0.1),
        arm_r=V(0.1, -0.6, -0.8), fore_r=V(0.35, -0.75, -0.55), hand_r=V(0.4, -0.7, -0.6),
        arm_l=V(0.5, 0.45, -0.6), fore_l=V(0.35, 0.3, -0.88), hand_l=V(0.3, 0.3, -0.9),
        foot_l=(0.17, -0.56, 0.0), foot_r=(-0.14, -0.05, 0.14), head=V(0.04, -0.4, 1))),
    (42, dict(  # settle: stand up, arms hang loose
        hips=(0.0, -0.2, -0.04), hyaw=10, cyaw=14, lean=6,
        sh_r=V(-1, -0.05, 0.05), sh_l=V(1, -0.05, 0.05),
        arm_r=V(-0.2, -0.1, -1), fore_r=V(-0.1, -0.4, -0.9), hand_r=V(-0.1, -0.4, -0.9),
        arm_l=V(0.25, -0.05, -1), fore_l=V(0.12, -0.4, -0.9), hand_l=V(0.1, -0.4, -0.9),
        foot_l=(0.17, -0.56, 0.0), foot_r=(-0.14, -0.02, 0.0), head=V(0.0, -0.1, 1))),
]
# how many frames each part trails the hips (the whip)
LAG = {"spine": 1, "spine1": 2, "spine2": 3, "sh": 2.5, "arm": 3, "fore": 4.5, "hand": 6, "head": 4,
       "legs": 0}


def _vec(x):
    return Vector(x) if not isinstance(x, (int, float)) else x


def sample(name, t):
    """Cardinal spline through the key values of `name` at time t."""
    ts = [k[0] for k in KEYS]
    vals = [_vec(k[1][name]) for k in KEYS]
    t = min(max(t, ts[0]), ts[-1])
    i = max(j for j in range(len(ts) - 1) if ts[j] <= t) if t < ts[-1] else len(ts) - 2
    t0, t1 = ts[i], ts[i + 1]
    u = (t - t0) / (t1 - t0)

    def tangent(j):
        a, b = max(j - 1, 0), min(j + 1, len(ts) - 1)
        return (vals[b] - vals[a]) * ((t1 - t0) / max(ts[b] - ts[a], 1e-6)) * 0.85

    p0, p1, m0, m1 = vals[i], vals[i + 1], tangent(i), tangent(i + 1)
    h00, h10, h01, h11 = 2*u**3 - 3*u**2 + 1, u**3 - 2*u**2 + u, -2*u**3 + 3*u**2, u**3 - u**2
    return p0 * h00 + m0 * h10 + p1 * h01 + m1 * h11


def direction(name, t):
    return Vector(sample(name, t)).normalized()


def set_bone(pb, head, rot3):
    pb.matrix = Matrix.Translation(head) @ rot3.to_4x4()
    bpy.context.view_layer.update()


def aim(pb, rest, d, name):
    """Rotate the bone (keeping its roll) so head->tail points along d."""
    r = rest[name]
    set_bone(pb, pb.matrix.translation.copy(), r["dir"].rotation_difference(d).to_matrix() @ r["rot"])


def two_bone_ik(rig, rest, thigh, shin, foot, ankle, pole):
    pb = rig.pose.bones
    th, sh_, ft = pb[P + thigh], pb[P + shin], pb[P + foot]
    H = th.matrix.translation.copy()
    L1, L2 = rest[thigh]["len"], rest[shin]["len"]
    axis = ankle - H
    dist = min(max(axis.length, 0.05), (L1 + L2) * 0.995)
    axis.normalize()
    a = (L1 * L1 - L2 * L2 + dist * dist) / (2 * dist)
    h = math.sqrt(max(L1 * L1 - a * a, 0.0))
    side = (pole - axis * pole.dot(axis)).normalized()
    knee = H + axis * a + side * h
    aim(th, rest, (knee - H).normalized(), thigh)
    knee = th.tail.copy() if False else knee
    shin_head = pb[P + shin].matrix.translation
    aim(sh_, rest, ((H + axis * dist) - knee).normalized(), shin)


def pose_frame(rig, rest, f):
    pb = rig.pose.bones
    for b in pb:
        b.rotation_mode = "QUATERNION"
        b.rotation_quaternion = (1, 0, 0, 0)
        b.location = (0, 0, 0)
    bpy.context.view_layer.update()
    hips_off = sample("hips", f)
    hyaw = sample("hyaw", f)
    hb = pb[P + "Hips"]
    set_bone(hb, rest["Hips"]["head"] + hips_off,
             Matrix.Rotation(math.radians(hyaw), 3, "Z") @ rest["Hips"]["rot"])
    shares = {"Spine": (0.3, 0.3, LAG["spine"]), "Spine1": (0.35, 0.4, LAG["spine1"]), "Spine2": (0.35, 0.3, LAG["spine2"])}
    for name, (yaw_share, lean_share, lag) in shares.items():
        t = f - lag
        extra_yaw = sample("cyaw", t) - sample("hyaw", t)
        pbn = pb[P + name]
        rot = Matrix.Rotation(math.radians(extra_yaw * yaw_share), 3, "Z") @ \
            Matrix.Rotation(math.radians(sample("lean", t) * lean_share), 3, "X") @ pbn.matrix.to_3x3()
        set_bone(pbn, pbn.matrix.translation.copy(), rot)
    t = f - LAG["head"]
    aim(pb[P + "Head"], rest, direction("head", t), "Head")
    t = f - LAG["sh"]
    aim(pb[P + "RightShoulder"], rest, direction("sh_r", t), "RightShoulder")
    aim(pb[P + "LeftShoulder"], rest, direction("sh_l", t), "LeftShoulder")
    for side in ("r", "l"):
        S = "Right" if side == "r" else "Left"
        aim(pb[P + S + "Arm"], rest, direction("arm_" + side, f - LAG["arm"]), S + "Arm")
        aim(pb[P + S + "ForeArm"], rest, direction("fore_" + side, f - LAG["fore"]), S + "ForeArm")
        aim(pb[P + S + "Hand"], rest, direction("hand_" + side, f - LAG["hand"]), S + "Hand")
    for side, S, sx in (("l", "Left", 1.0), ("r", "Right", -1.0)):
        o = sample("foot_" + side, f)
        ankle = Vector((o.x, o.y, rest[S + "Foot"]["head"].z + o.z))
        two_bone_ik(rig, rest, S + "UpLeg", S + "Leg", S + "Foot", ankle, Vector((sx * 0.12, -1.0, 0.0)).normalized())
        flat = Vector((0.04 * sx, -0.9, -0.25)).normalized()
        lifted = Vector((0.04 * sx, -0.7, -0.7)).normalized()
        aim(pb[P + S + "Foot"], rest, flat.lerp(lifted, min(o.z / 0.25, 1.0)).normalized(), S + "Foot")
    for b in pb:
        b.keyframe_insert("location", frame=f)
        b.keyframe_insert("rotation_quaternion", frame=f)


def make_ball(rig):
    ball = bpy.data.objects.new("Ball", bpy.data.meshes.new("Ball"))
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=20, v_segments=12, radius=0.07)
    bm.to_mesh(ball.data)
    bm.free()
    for p in ball.data.polygons:
        p.use_smooth = True
    mat = bpy.data.materials.new("BallMat")
    mat.use_nodes = True
    mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.95, 0.3, 0.02, 1)
    mat.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.8
    ball.data.materials.append(mat)
    bpy.context.scene.collection.objects.link(ball)
    hand = rig.pose.bones[P + "RightHand"]
    for f in range(1, RELEASE + 1):
        bpy.context.scene.frame_set(f)
        ball.location = rig.matrix_world @ (hand.head + (hand.tail - hand.head) * 0.5)
        ball.keyframe_insert("location", frame=f)
    release = ball.location.copy()
    for f in range(RELEASE + 1, FRAMES + 1):
        s = f - RELEASE
        ball.location = release + Vector((0.0, -0.4 * s, 0.13 * s - 0.009 * s * s))
        ball.keyframe_insert("location", frame=f)
    return ball


def main():
    argv = sys.argv[sys.argv.index("--") + 1:]
    src, out_dir = argv[0], argv[1]
    mode = argv[2] if len(argv) > 2 else "test"
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=src)
    ico = bpy.data.objects.get("Icosphere")
    if ico:
        bpy.data.objects.remove(ico)
    rig = bpy.data.objects["target_character"]
    if "Running" in bpy.data.actions:
        bpy.data.actions.remove(bpy.data.actions["Running"])
    rig.animation_data_create()
    rest = {}
    for b in rig.data.bones:
        n = b.name.replace(P, "")
        rest[n] = {"rot": b.matrix_local.to_3x3(), "head": b.head_local.copy(), "len": b.length,
                   "dir": (b.tail_local - b.head_local).normalized()}
    act = bpy.data.actions.new("Throw")
    rig.animation_data.action = act
    for f in range(1, FRAMES + 1):
        pose_frame(rig, rest, f)
    scene = bpy.context.scene
    scene.frame_start, scene.frame_end = 1, FRAMES
    make_ball(rig)

    if mode == "export":
        bpy.ops.object.select_all(action="SELECT")
        bpy.ops.export_scene.gltf(filepath=f"{out_dir}/boy_throw.glb", export_format="GLB",
                                  export_animations=True, export_animation_mode="ACTIONS")
        return

    w = bpy.data.worlds.new("W")
    scene.world = w
    w.use_nodes = True
    w.node_tree.nodes["Background"].inputs["Color"].default_value = (0.75, 0.62, 0.45, 1)
    sun = bpy.data.objects.new("S", bpy.data.lights.new("S", "SUN"))
    sun.data.energy = 3.0
    sun.rotation_euler = (math.radians(50), 0, math.radians(-30))
    scene.collection.objects.link(sun)
    bpy.ops.mesh.primitive_plane_add(size=30, location=(0, 0, 0))
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 14
    scene.cycles.device = "CPU"
    scene.cycles.use_denoising = True
    scene.render.resolution_x, scene.render.resolution_y = 760, 470
    cam = bpy.data.objects.new("C", bpy.data.cameras.new("C"))
    scene.collection.objects.link(cam)
    cam.data.lens = 34
    scene.camera = cam
    for i, f in enumerate([1, 10, 15, 20, 24, 27, 30, 38]):
        scene.frame_set(f)
        cam.location = Vector((-7.0, -1.6, 1.1))
        cam.rotation_euler = (Vector((0, -1.6, 0.95)) - cam.location).to_track_quat("-Z", "Y").to_euler()
        scene.render.filepath = f"{out_dir}/throw_{i}.png"
        bpy.ops.render.render(write_still=True)


if __name__ == "__main__":
    main()
