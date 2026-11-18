# Play one of junjun's real clips on a kid (world-space rotation transfer) and render a frame strip.
# usage: python clip_preview.py junjun.glb kid.glb ClipName out_prefix [nframes]
import sys, bpy, math
from mathutils import Vector, Matrix, Quaternion
sys.path.insert(0, __import__('os').path.dirname(__import__('os').path.abspath(__file__)))
from rig_drivers import add_drivers

SRCF, KIDF, CLIP, OUT = sys.argv[1:5]
NF = int(sys.argv[5]) if len(sys.argv) > 5 else 6
P = "mixamorig:"
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=SRCF)
src = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
src_objs = set(bpy.data.objects)
bpy.ops.import_scene.gltf(filepath=KIDF)
kid = next(o for o in bpy.data.objects if o.type == 'ARMATURE' and o not in src_objs)
for o in list(bpy.data.objects):
    if o.type == 'MESH' and (o in src_objs or not o.vertex_groups):
        bpy.data.objects.remove(o, do_unlink=True)
if kid.animation_data:
    kid.animation_data_clear()
add_drivers(kid)
act = bpy.data.actions[CLIP]
if src.animation_data is None:
    src.animation_data_create()
src.animation_data.action = act
try:
    src.animation_data.action_slot = act.slots[0]
except Exception:
    pass
f0, f1 = act.frame_range
SW, KW = src.matrix_world.to_3x3(), kid.matrix_world.to_3x3()
srest = {b.name: (SW @ b.matrix_local.to_3x3()) for b in src.data.bones}
# the kids rest in the roster reference pose (arms lowered), not in the source's rest: measure every
# rotation from that reference pose, or the arms end up lower than the clip by the difference
import json, os
_ref = json.load(open(os.environ.get("REF", os.path.join(os.path.dirname(os.path.abspath(__file__)), "ref_rest.json"))))
for _n, _v in _ref.items():
    if P + _n in srest:
        srest[P + _n] = Matrix(_v["rot"])
krest = {b.name: (KW @ b.matrix_local.to_3x3()) for b in kid.data.bones}
order = sorted(kid.data.bones, key=lambda b: len(b.parent_recursive))
sleg = (src.data.bones[P + "LeftUpLeg"].head_local - src.data.bones[P + "LeftFoot"].head_local).length
kleg = (kid.data.bones[P + "LeftUpLeg"].head_local - kid.data.bones[P + "LeftFoot"].head_local).length
scn = bpy.context.scene
for loc in [(2, -3, 3), (-2, -3, 2), (0, 3, 2)]:
    l = bpy.data.objects.new("l", bpy.data.lights.new("l", 'SUN')); l.rotation_euler = (Vector((0, 0, 0)) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler(); l.data.energy = 2.5; scn.collection.objects.link(l)
scn.render.engine = 'CYCLES'; scn.cycles.samples = 10; scn.render.resolution_x = 260; scn.render.resolution_y = 400
scn.world = bpy.data.worlds.new("w"); scn.world.color = (0.5, 0.5, 0.5)
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); scn.collection.objects.link(cam); scn.camera = cam
for k in range(NF):
    f = f0 + (f1 - f0) * k / NF
    scn.frame_set(int(f), subframe=f - int(f))
    D = {}
    for b in src.data.bones:
        D[b.name] = (SW @ src.pose.bones[b.name].matrix.to_3x3()) @ srest[b.name].inverted()
    A = {}
    for b in order:
        d = D.get(b.name)
        if d is None:
            d = (A[b.parent.name] @ krest[b.parent.name].inverted()) if b.parent else Matrix.Identity(3)
        A[b.name] = d @ krest[b.name]
        pb = kid.pose.bones[b.name]
        if any(c.type == 'COPY_ROTATION' for c in pb.constraints):
            continue
        if b.parent:
            L = krest[b.parent.name].inverted() @ krest[b.name]
            basis = L.inverted() @ A[b.parent.name].inverted() @ A[b.name]
        else:
            basis = krest[b.name].inverted() @ A[b.name]
        pb.rotation_mode = 'QUATERNION'
        pb.rotation_quaternion = basis.to_quaternion()
    hs = src.pose.bones[P + "Hips"]
    delta = (src.matrix_world @ hs.head) - (src.matrix_world @ src.data.bones[P + "Hips"].head_local)
    delta = Vector((0, 0, delta.z)) * (kleg / sleg)          # in place: height only
    kid.pose.bones[P + "Hips"].location = krest[P + "Hips"].inverted() @ delta
    bpy.context.view_layer.update()
    _c = os.environ.get("CLOSE")
    if _c:
        tgt = kid.matrix_world @ kid.pose.bones[P + _c].head
        for v, off in enumerate([(0.0, -1.0, 0.15), (1.0 if tgt.x > 0 else -1.0, -0.2, 0.1)]):
            cam.location = tgt + Vector(off).normalized() * 0.95
            cam.rotation_euler = (tgt - cam.location).to_track_quat('-Z', 'Y').to_euler()
            scn.render.filepath = f"{OUT}_{v}_{k}.png"; bpy.ops.render.render(write_still=True)
        continue
    for v, loc in enumerate([(0.0, -4.0, 1.0), (4.0, 0.0, 0.9)] if __import__("os").environ.get("FRONT") else [(2.6, -3.2, 1.0), (4.0, 0.0, 0.9)]):
        cam.location = loc; cam.rotation_euler = (Vector((0, 0, 0.85)) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
        scn.render.filepath = f"{OUT}_{v}_{k}.png"; bpy.ops.render.render(write_still=True)
print("done", CLIP, f0, f1)
