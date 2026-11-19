# Render a game-ready kid (.glb from retopo_kid.py) into the pictures the UI uses, all transparent PNGs:
#   <out>/<id>_portrait.png  600x800 head and shoulders
#   <out>/<id>_face.png      512x512 face (scaled down for the HUD faces and nametag avatars)
#   <out>/<id>_body.png      480x737 full body, standing in the Idle clip
# usage: python render_kid_art.py kid.glb out_dir id
import os
import sys

import bpy
from mathutils import Vector

SRC, OUT, KID = sys.argv[1:4]
os.makedirs(OUT, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=SRC)
arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
mesh = next(o for o in bpy.data.objects if o.type == "MESH")
idle = bpy.data.actions.get("Idle")
if idle:
    if arm.animation_data is None:
        arm.animation_data_create()
    arm.animation_data.action = idle
    try:
        arm.animation_data.action_slot = idle.slots[0]
    except Exception:
        pass
scn = bpy.context.scene
scn.frame_set(int(idle.frame_range[0]) if idle else 0)
bpy.context.view_layer.update()

# frame from the skeleton (the skinned mesh's bounds are not posed here): soles to the top of the head
body = [pb for pb in arm.pose.bones if pb.name.startswith("mixamorig:") and
        any(k in pb.name for k in ("HeadTop_End", "Head", "Foot", "ToeBase", "Toe_End"))]
joints = [arm.matrix_world @ pb.head for pb in body] + [arm.matrix_world @ pb.tail for pb in body]
zmin = min(p.z for p in joints)
zmax = max(p.z for p in joints) + 0.04 * (max(p.z for p in joints) - min(p.z for p in joints))   # hair above the head bone
H = zmax - zmin
head_bone = arm.pose.bones.get("mixamorig:Head")
head = arm.matrix_world @ head_bone.head if head_bone else Vector((0, 0, zmax - H * 0.12))

scn.render.engine = "CYCLES"
scn.cycles.device = "CPU"
scn.cycles.samples = 48
scn.render.film_transparent = True
scn.render.image_settings.file_format = "PNG"
scn.render.image_settings.color_mode = "RGBA"
scn.view_settings.view_transform = "Standard"
world = bpy.data.worlds.new("w")
world.use_nodes = True
world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.55, 0.57, 0.62, 1)
world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.6
scn.world = world
for name, loc, energy, size in (("key", (-1.2, -2.0, 2.4), 3.2, 1.6), ("fill", (2.2, -1.4, 1.2), 1.2, 2.0),
                                ("rim", (0.6, 2.4, 2.2), 4.0, 0.8)):
    light = bpy.data.objects.new(name, bpy.data.lights.new(name, "AREA"))
    light.data.energy = energy * 120
    light.data.size = size
    light.location = Vector(loc) * (H / 1.4)
    light.rotation_euler = (Vector((0, 0, H * 0.6)) - light.location).to_track_quat("-Z", "Y").to_euler()
    scn.collection.objects.link(light)

cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
scn.collection.objects.link(cam)
scn.camera = cam
cam.data.lens = 85


def shoot(name, w, h, target, distance, lift=0.0):
    scn.render.resolution_x, scn.render.resolution_y = w, h
    cam.location = target + Vector((0.28, -1.0, 0.08 + lift)).normalized() * distance
    cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
    scn.render.filepath = os.path.join(OUT, f"{KID}_{name}.png")
    bpy.ops.render.render(write_still=True)


shoot("portrait", 600, 800, head + Vector((0, 0, -H * 0.06)), H * 1.05)
shoot("face", 512, 512, head + Vector((0, 0, H * 0.01)), H * 0.62)
shoot("body", 480, 737, Vector((0, 0, zmin + H * 0.5)), H * 3.0)
print("rendered", KID)
