# Roster reference skeleton: junjun's mixamorig bones with the upper arms lowered to 40 degrees below
# horizontal (apex-rig-forge R1 A-pose) and the legs straight. Writes ref_rest.json: per contract bone
# its world rest orientation (3x3, Blender coords), head, length and parent.
# usage: python build_reference.py junjun.glb ref_rest.json
import sys, bpy, math, json
from mathutils import Vector, Matrix

SRC, OUT = sys.argv[1], sys.argv[2]
P = "mixamorig:"
ARM_DROP = 40.0

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=SRC)
for a in list(bpy.data.actions):
    bpy.data.actions.remove(a)
arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
if arm.animation_data:
    arm.animation_data_clear()
AM = arm.matrix_world.copy(); AMi = AM.inverted()
pb = arm.pose.bones
for b in pb:
    b.matrix_basis = Matrix.Identity(4)
bpy.context.view_layer.update()


def rotate_world(name, axis, deg):
    b = pb[name]
    pivot = AM @ b.head
    R = Matrix.Rotation(math.radians(deg), 4, axis)
    b.matrix = AMi @ (Matrix.Translation(pivot) @ R @ Matrix.Translation(-pivot) @ AM @ b.matrix)
    bpy.context.view_layer.update()


for side, sgn in (("Left", 1), ("Right", -1)):
    n = P + side + "Arm"
    d = (AM @ pb[n].tail - AM @ pb[n].head).normalized()
    cur = math.degrees(math.asin(-d.z))
    # rotate about the forward axis so the upper arm points ARM_DROP below horizontal
    rotate_world(n, Vector((0, 1, 0)) * sgn, -(ARM_DROP - cur) * 1.0)
    d2 = (AM @ pb[n].tail - AM @ pb[n].head).normalized()
    if abs(math.degrees(math.asin(-d2.z)) - ARM_DROP) > 1.0:          # wrong sign: turn the other way
        rotate_world(n, Vector((0, 1, 0)) * sgn, 2 * (ARM_DROP - cur))
    # forearm and hand follow in line with the upper arm (straight arm, like the source)
    for leg in ("UpLeg",):
        pass
    print(side, "arm now", round(math.degrees(math.asin(-(AM @ pb[n].tail - AM @ pb[n].head).normalized().z)), 1), "deg below")

out = {}
for b in arm.data.bones:
    if not b.name.startswith(P):
        continue
    m = AM @ pb[b.name].matrix
    out[b.name[len(P):]] = dict(rot=[list(r) for r in m.to_3x3().normalized()], head=list(m.translation),
                                length=(AM @ pb[b.name].tail - AM @ pb[b.name].head).length,
                                parent=(b.parent.name[len(P):] if b.parent and b.parent.name.startswith(P) else None))
json.dump(out, open(OUT, "w"), indent=1)
print("reference bones:", len(out))
