# bent joint with its corrective off / on. usage: corr_compare.py model.glb out_prefix
import sys, bpy, math
from mathutils import Vector, Matrix
SRC, OUT = sys.argv[1], sys.argv[2]
P = "mixamorig:"
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=SRC)
for a in list(bpy.data.actions): bpy.data.actions.remove(a)
arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
if arm.animation_data: arm.animation_data_clear()
mesh = max((o for o in bpy.data.objects if o.type == 'MESH'), key=lambda o: len(o.data.vertices))
for o in [o for o in bpy.data.objects if o.type == 'MESH' and o != mesh]: bpy.data.objects.remove(o, do_unlink=True)
AM = arm.matrix_world; AMi = AM.inverted(); pb = arm.pose.bones
def swing(n, target, deg):
    bpy.context.view_layer.update(); b = pb[n]
    d = (AM @ b.tail - AM @ b.head).normalized(); pivot = AM @ b.head
    axis = d.cross(Vector(target).normalized())
    R = Matrix.Rotation(math.radians(deg), 4, axis.normalized())
    b.matrix = AMi @ (Matrix.Translation(pivot) @ R @ Matrix.Translation(-pivot) @ AM @ b.matrix); bpy.context.view_layer.update()
swing(P + "LeftForeArm", (0, -1, 0), 110)
swing(P + "RightLeg", (0, 1, 0), 110)
keys = mesh.data.shape_keys.key_blocks
scn = bpy.context.scene
for loc in [(2, -3, 3), (-2, -3, 2), (0, 3, -2)]:
    l = bpy.data.objects.new("l", bpy.data.lights.new("l", 'SUN')); l.rotation_euler = (Vector((0,0,0)) - Vector(loc)).to_track_quat('-Z','Y').to_euler(); l.data.energy = 2.5; scn.collection.objects.link(l)
scn.render.engine = 'CYCLES'; scn.cycles.samples = 12; scn.render.resolution_x = scn.render.resolution_y = 300
scn.world = bpy.data.worlds.new("w"); scn.world.color = (0.5, 0.5, 0.5)
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); scn.collection.objects.link(cam); scn.camera = cam
def side_dir(bn, sgn):
    b = pb[P + bn]; par = b.parent
    a = (AM @ par.tail - AM @ par.head).normalized(); c_ = (AM @ b.tail - AM @ b.head).normalized()
    n = a.cross(c_).normalized() * sgn
    return tuple(n)
shots = [("LeftForeArm", "corr_LeftForeArm", side_dir("LeftForeArm", -1), 0.09),
         ("RightLeg", "corr_RightLeg", side_dir("RightLeg", -1), 0.11)]
import numpy as np
for sk in ("corr_LeftForeArm", "corr_RightLeg", "corr_LeftArm"):
    if sk in mesh.data.shape_keys.key_blocks:
        kb = mesh.data.shape_keys.key_blocks[sk]; base = mesh.data.shape_keys.key_blocks[0]
        dd = np.array([(kb.data[i].co - base.data[i].co).length for i in range(len(kb.data))]) * mesh.matrix_world.to_scale()[0]
        print("SHAPE", sk, "max move", round(float(dd.max()) * 1000, 1), "mm; verts >1mm", int((dd > 0.001).sum()))
for k, (bn, sk, dv, r) in enumerate(shots):
    c = AM @ pb[P + bn].head
    dv = Vector(dv).normalized(); cam.location = c + dv * r * 4.5
    cam.rotation_euler = (c - cam.location).to_track_quat('-Z', 'Y').to_euler()
    for val in (0.0, 1.0):
        for kb in keys[1:]: kb.value = 0.0
        if sk in keys: keys[sk].value = val
        scn.render.filepath = f"{OUT}_{k}_{int(val)}.png"; bpy.ops.render.render(write_still=True)
