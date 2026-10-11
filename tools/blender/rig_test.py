"""Renders the rigged model in a few poses so deformation can be checked by eye."""
import math

import bpy
from mathutils import Vector


def render_poses(out_dir, rig):
    scene = bpy.context.scene
    w = bpy.data.worlds.new("W")
    scene.world = w
    w.use_nodes = True
    w.node_tree.nodes["Background"].inputs["Color"].default_value = (0.75, 0.62, 0.45, 1)
    sun = bpy.data.objects.new("S", bpy.data.lights.new("S", "SUN"))
    sun.data.energy = 3.0
    sun.rotation_euler = (math.radians(50), 0, math.radians(-30))
    scene.collection.objects.link(sun)
    bpy.ops.mesh.primitive_plane_add(size=30, location=(0, 0, -0.952))
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 12
    scene.cycles.device = "CPU"
    scene.cycles.use_denoising = True
    scene.render.resolution_x, scene.render.resolution_y = 500, 700
    cam = bpy.data.objects.new("C", bpy.data.cameras.new("C"))
    scene.collection.objects.link(cam)
    cam.data.lens = 50
    scene.camera = cam
    shots = [("idle", 13, 0), ("run", 1, 0), ("run", 1, 90), ("run", 13, 90), ("cast", 14, 0), ("cast", 14, 90)]
    for i, (action, frame, ang) in enumerate(shots):
        rig.animation_data.action = bpy.data.actions[action]
        scene.frame_set(frame)
        a = math.radians(ang)
        cam.location = (math.sin(a) * 4.6, -math.cos(a) * 4.6, 0.1)
        cam.rotation_euler = (Vector((0, 0, 0)) - cam.location).to_track_quat("-Z", "Y").to_euler()
        scene.render.filepath = f"{out_dir}/pose_{i}.png"
        bpy.ops.render.render(write_still=True)
