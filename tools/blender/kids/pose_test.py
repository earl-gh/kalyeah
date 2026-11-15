# Stress pose for skinning checks: left arm raised + elbow bent + wrist flexed + fist, right knee up and bent.
# usage: python pose_test.py model.glb out_prefix
import sys, bpy, math
from mathutils import Vector, Matrix

SRC, OUT = sys.argv[1], sys.argv[2]
P = "mixamorig:"
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=SRC)
for a in list(bpy.data.actions):
    bpy.data.actions.remove(a)
arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
if arm.animation_data:
    arm.animation_data_clear()
arm.data.pose_position = 'POSE'
mesh = max((o for o in bpy.data.objects if o.type == 'MESH'), key=lambda o: len(o.data.vertices))
for o in [o for o in bpy.data.objects if o.type == 'MESH' and o != mesh]:
    bpy.data.objects.remove(o, do_unlink=True)
AM = arm.matrix_world
AMi = AM.inverted()
pb = arm.pose.bones


def world_dir(n):
    b = pb[n]
    m = AM @ b.matrix
    return (AM @ b.tail - AM @ b.head).normalized(), m.translation


def swing(n, target, deg):
    """rotate bone n (and its children) about its head so it turns toward the world direction target"""
    if n not in pb:
        return
    bpy.context.view_layer.update()
    d, pivot = world_dir(n)
    axis = d.cross(Vector(target).normalized())
    if axis.length < 1e-6:
        return
    R = Matrix.Rotation(math.radians(deg), 4, axis.normalized())
    Mw = AM @ pb[n].matrix
    Mw = Matrix.Translation(pivot) @ R @ Matrix.Translation(-pivot) @ Mw
    pb[n].matrix = AMi @ Mw
    bpy.context.view_layer.update()


up, fwd, back, out_l, out_r = (0, 0, 1), (0, -1, 0), (0, 1, 0), (1, 0, 0), (-1, 0, 0)
swing(P + "LeftArm", up, 70)                 # shoulder: arm raised to the side
swing(P + "LeftForeArm", fwd, 110)           # elbow bent forward
swing(P + "LeftHand", fwd, 45)               # wrist flexed
for f in ("Index", "Middle", "Ring", "Pinky"):
    for j in (1, 2, 3):
        n = P + f"LeftHand{f}{j}"
        if n in pb:
            bpy.context.view_layer.update()
            d, _ = world_dir(n)
            hand_d, _ = world_dir(P + "LeftHand")
            palm = Vector(out_r)   # left palm faces the body (-X) in the rest pose
            swing(n, palm, 60)
swing(P + "RightUpLeg", fwd, 75)             # hip flexed
swing(P + "RightLeg", back, 110)             # knee bent
bpy.context.view_layer.update()

scn = bpy.context.scene
for loc in [(2, -3, 3), (-2, -3, 2), (0, 3, -2)]:
    l = bpy.data.objects.new("l", bpy.data.lights.new("l", 'SUN')); l.rotation_euler = (Vector((0, 0, 0)) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler(); l.data.energy = 2.5; scn.collection.objects.link(l)
scn.render.engine = 'CYCLES'; scn.cycles.samples = 12; scn.render.resolution_x = 300; scn.render.resolution_y = 300
scn.world = bpy.data.worlds.new("w"); scn.world.color = (0.5, 0.5, 0.5)
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); scn.collection.objects.link(cam); scn.camera = cam
cam.data.lens = 50
def at(n):
    return AM @ pb[P + n].head
shots = [("LeftArm", (0.3, -1, 0.5), 0.17), ("LeftForeArm", (0.6, -1, 0.3), 0.15), ("LeftHand", (0.2, -1, -0.3), 0.12),
         ("RightLeg", (-1, -0.4, 0.1), 0.17)]
for k, (n, dv, r) in enumerate(shots):
    c = at(n); dv = Vector(dv).normalized(); cam.location = c + dv * r * 5.0
    cam.rotation_euler = (c - cam.location).to_track_quat('-Z', 'Y').to_euler()
    scn.render.filepath = f"{OUT}_{k}.png"; bpy.ops.render.render(write_still=True)
