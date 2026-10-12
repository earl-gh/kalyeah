"""Adds a 'Throw' animation (and a ball) to the Meshy Mixamo-rigged boy, renders key frames.

Run: python throw_boy.py -- in.glb out_dir [export]
Armature space: +Z up, the boy faces -Y, his right is -X. Poses are given as world directions
for each limb bone, so no bone-roll guessing is needed.
"""
import math
import sys

import bpy
from mathutils import Matrix, Vector

P = "mixamorig:"
V = lambda x, y, z: Vector((x, y, z)).normalized()

# bone -> world direction (head to tail) it should point at, per key frame
KEYS = {
    1: {  # ready: ball in the right hand at the hip, knees soft
        "RightArm": V(-0.25, -0.15, -1), "RightForeArm": V(-0.1, -0.9, -0.35),
        "LeftArm": V(0.3, -0.15, -1), "LeftForeArm": V(0.1, -0.7, -0.6),
        "LeftUpLeg": V(0.1, -0.1, -1), "LeftLeg": V(0.1, 0.0, -1),
        "RightUpLeg": V(-0.1, -0.1, -1), "RightLeg": V(-0.1, 0.0, -1),
        "hips": (0.0, 0.0, 0.0, 0.0), "twist": 0.0, "lean": 0.0,
    },
    9: {  # wind-up: lean back, ball behind the head, lead leg lifted, left arm points ahead
        "RightArm": V(-0.55, 0.6, 0.55), "RightForeArm": V(-0.1, 0.65, 0.75),
        "LeftArm": V(0.45, -0.85, 0.25), "LeftForeArm": V(0.4, -0.9, 0.2),
        "LeftUpLeg": V(0.15, -0.75, -0.65), "LeftLeg": V(0.15, -0.35, -0.95),
        "RightUpLeg": V(-0.1, 0.15, -1), "RightLeg": V(-0.1, 0.35, -0.95),
        "hips": (0.0, 0.05, -0.06, 0.0), "twist": -38.0, "lean": -12.0,
    },
    15: {  # release: planted front foot, hips open, arm whips forward and up
        "RightArm": V(-0.3, -0.8, 0.55), "RightForeArm": V(-0.15, -0.95, 0.25),
        "LeftArm": V(0.55, 0.35, -0.55), "LeftForeArm": V(0.35, 0.3, -0.8),
        "LeftUpLeg": V(0.12, -0.55, -0.85), "LeftLeg": V(0.1, -0.1, -1),
        "RightUpLeg": V(-0.1, 0.5, -0.85), "RightLeg": V(-0.1, 0.75, -0.65),
        "hips": (0.0, -0.18, -0.1, 0.0), "twist": 32.0, "lean": 22.0,
    },
    21: {  # follow-through: arm sweeps down across the body
        "RightArm": V(0.15, -0.6, -0.75), "RightForeArm": V(0.4, -0.7, -0.6),
        "LeftArm": V(0.5, 0.45, -0.55), "LeftForeArm": V(0.4, 0.2, -0.85),
        "LeftUpLeg": V(0.12, -0.45, -0.9), "LeftLeg": V(0.1, -0.05, -1),
        "RightUpLeg": V(-0.1, 0.35, -0.92), "RightLeg": V(-0.1, 0.55, -0.8),
        "hips": (0.0, -0.2, -0.12, 0.0), "twist": 38.0, "lean": 30.0,
    },
    30: None,  # back to frame 1
}
KEYS[30] = KEYS[1]
LIMBS = ["LeftArm", "LeftForeArm", "RightArm", "RightForeArm", "LeftUpLeg", "LeftLeg", "RightUpLeg", "RightLeg"]
ORDER = ["Hips", "Spine", "Spine1", "Spine2", "RightShoulder", "LeftShoulder", "Neck", "Head",
         "RightArm", "RightForeArm", "LeftArm", "LeftForeArm", "LeftUpLeg", "LeftLeg", "RightUpLeg", "RightLeg"]


def pose_frame(rig, rest, key, frame):
    pb = rig.pose.bones
    for b in pb:
        b.rotation_mode = "QUATERNION"
        b.rotation_quaternion = (1, 0, 0, 0)
        b.location = (0, 0, 0)
    bpy.context.view_layer.update()
    dx, dy, dz, _ = key["hips"]
    for name in ORDER:
        bone = pb[P + name]
        r3 = rest[name]["rot"]
        head = bone.matrix.translation.copy()
        if name == "Hips":
            head = rest[name]["head"] + Vector((dx, dy, dz))
            rot = Matrix.Rotation(math.radians(key["twist"] * 0.5), 3, "Z") @ r3
        elif name in ("Spine", "Spine1", "Spine2"):
            rot = Matrix.Rotation(math.radians(key["twist"] * 0.25), 3, "Z") @ \
                Matrix.Rotation(math.radians(key["lean"] / 3.0), 3, "X") @ bone.matrix.to_3x3()
            head = bone.matrix.translation.copy()
        elif name in LIMBS:
            d = key[name]
            rot = rest[name]["dir"].rotation_difference(d).to_matrix() @ r3
        else:
            continue
        bone.matrix = Matrix.Translation(head) @ rot.to_4x4()
        bpy.context.view_layer.update()
    for b in pb:
        b.keyframe_insert("location", frame=frame)
        b.keyframe_insert("rotation_quaternion", frame=frame)


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
    bpy.data.actions.remove(bpy.data.actions["Running"]) if "Running" in bpy.data.actions else None
    rig.animation_data_create()
    rest = {}
    for b in rig.data.bones:
        n = b.name.replace(P, "")
        rest[n] = {"rot": b.matrix_local.to_3x3(), "head": b.head_local.copy(),
                   "dir": (b.tail_local - b.head_local).normalized()}
    act = bpy.data.actions.new("Throw")
    rig.animation_data.action = act
    for frame, key in KEYS.items():
        pose_frame(rig, rest, key, frame)
    bpy.context.scene.frame_start, bpy.context.scene.frame_end = 1, 30

    # the thing being thrown: a small ball that rides the hand, then flies off at the release
    ball = bpy.data.objects.new("Ball", bpy.data.meshes.new("Ball"))
    import bmesh
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=20, v_segments=12, radius=0.07)
    bm.to_mesh(ball.data)
    bm.free()
    for p in ball.data.polygons:
        p.use_smooth = True
    mat = bpy.data.materials.new("BallMat")
    mat.diffuse_color = (0.9, 0.75, 0.15, 1)
    mat.use_nodes = True
    mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.95, 0.3, 0.02, 1)
    mat.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.8
    ball.data.materials.append(mat)
    bpy.context.scene.collection.objects.link(ball)
    hand = rig.pose.bones[P + "RightHand"]
    for f in range(1, 16):
        bpy.context.scene.frame_set(f)
        ball.location = (rig.matrix_world @ (hand.head + (hand.tail - hand.head) * 0.5))
        ball.keyframe_insert("location", frame=f)
    release = ball.location.copy()
    for f, (dy, dz) in {18: (-1.6, 0.35), 22: (-3.6, 0.55), 26: (-5.6, 0.35), 30: (-7.4, -0.3)}.items():
        ball.location = release + Vector((0.0, dy, dz))
        ball.keyframe_insert("location", frame=f)

    if mode == "export":
        bpy.ops.object.select_all(action="SELECT")
        bpy.ops.export_scene.gltf(filepath=f"{out_dir}/boy_throw.glb", export_format="GLB",
                                  export_animations=True, export_animation_mode="ACTIONS")
        return

    scene = bpy.context.scene
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
    for i, f in enumerate([1, 7, 10, 13, 15, 18, 21, 25]):
        scene.frame_set(f)
        cam.location = Vector((-7.0, -1.6, 1.1))
        cam.rotation_euler = (Vector((0, -1.6, 0.95)) - cam.location).to_track_quat("-Z", "Y").to_euler()
        scene.render.filepath = f"{out_dir}/throw_{i}.png"
        bpy.ops.render.render(write_still=True)


if __name__ == "__main__":
    main()
