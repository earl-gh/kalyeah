# Pose-space corrective shape keys for elbows, knees and shoulders.
# For each joint: bend it, let Corrective Smooth restore the volume the skinning loses (relative to the rest
# shape), turn that fix back into rest space through the skinning matrices, and store it as a shape key
# "corr_<bone>" (full strength at the baked angle; the game drives it from the joint's bend).
# usage: python bake_correctives.py norm.glb out.glb
import sys, bpy, math, json
import numpy as np
from mathutils import Vector, Matrix

SRC, DST = sys.argv[1], sys.argv[2]
P = "mixamorig:"
# bone, direction its child part swings toward (world), angle, zone radius (m)
JOINTS = [("LeftForeArm", (0, -1, 0), 110, 0.06), ("RightForeArm", (0, -1, 0), 110, 0.06),
          ("LeftLeg", (0, 1, 0), 110, 0.09), ("RightLeg", (0, 1, 0), 110, 0.09),
          ("LeftArm", (0, 0, 1), 80, 0.10), ("RightArm", (0, 0, 1), 80, 0.10)]

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=SRC)
for a in list(bpy.data.actions):
    bpy.data.actions.remove(a)
arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
if arm.animation_data:
    arm.animation_data_clear()
mesh = max((o for o in bpy.data.objects if o.type == 'MESH' and o.vertex_groups), key=lambda o: len(o.data.vertices))
for o in [o for o in bpy.data.objects if o.type == 'MESH' and o != mesh]:
    bpy.data.objects.remove(o, do_unlink=True)
AM, MW = arm.matrix_world.copy(), mesh.matrix_world.copy()
AMi, MWi = AM.inverted(), MW.inverted()
pb = arm.pose.bones
me = mesh.data
nv = len(me.vertices)
rest = np.array([tuple(v.co) for v in me.vertices])
restw = np.array([tuple(MW @ v.co) for v in me.vertices])
vg_name = {g.index: g.name for g in mesh.vertex_groups}
weights = [[(vg_name[g.group], g.weight) for g in v.groups if g.weight > 0] for v in me.vertices]
twins = {}
for i, p_ in enumerate(np.round(rest / 1e-5).astype(np.int64)):
    twins.setdefault(tuple(p_), []).append(i)
twin_of = {}
for ids in twins.values():
    if len(ids) > 1:
        for i in ids:
            twin_of[i] = ids
if me.shape_keys is None:
    mesh.shape_key_add(name="Basis", from_mix=False)

# zone vertex group used by Corrective Smooth
zone = mesh.vertex_groups.new(name="corr_zone")
cs = mesh.modifiers.new("corr", 'CORRECTIVE_SMOOTH')
cs.rest_source = 'ORCO'
cs.smooth_type = 'LENGTH_WEIGHTED'
cs.iterations = 12
cs.factor = 0.6
cs.vertex_group = zone.name
cs.use_only_smooth = False
cs.show_viewport = False


def swing(n, target, deg):
    bpy.context.view_layer.update()
    b = pb[n]
    d = (AM @ b.tail - AM @ b.head).normalized()
    pivot = AM @ b.head
    axis = d.cross(Vector(target).normalized())
    if axis.length < 1e-6:
        return
    R = Matrix.Rotation(math.radians(deg), 4, axis.normalized())
    b.matrix = AMi @ (Matrix.Translation(pivot) @ R @ Matrix.Translation(-pivot) @ AM @ b.matrix)
    bpy.context.view_layer.update()


def evaluated():
    dg = bpy.context.evaluated_depsgraph_get()
    ev = mesh.evaluated_get(dg)
    m = ev.to_mesh()
    out = np.array([tuple(v.co) for v in m.vertices])
    ev.to_mesh_clear()
    return out


def reset_pose():
    for b in pb:
        b.matrix_basis = Matrix.Identity(4)
    bpy.context.view_layer.update()


meta = []
GENTLE = {"LeftForeArm", "RightForeArm"}      # tight folds: less smoothing, or the inner crease folds through itself
for name, target, deg, radius in JOINTS:
    bn = P + name
    cs.iterations, cs.factor = (5, 0.35) if name in GENTLE else (12, 0.6)
    if bn not in pb:
        continue
    reset_pose()
    centre = np.array(AM @ arm.data.bones[bn].head_local)
    d = np.linalg.norm(restw - centre, axis=1)
    idx = np.where(d < radius)[0]
    zone.remove(list(range(nv)))
    for i in idx:
        zone.add([int(i)], float(1 - (d[i] / radius) ** 2), 'REPLACE')
    swing(bn, target, deg)
    cs.show_viewport = False
    posed = evaluated()
    cs.show_viewport = True
    fixed = evaluated()
    cs.show_viewport = False
    delta_posed = fixed - posed
    # skinning matrices (mesh space, 3x3) at this pose
    S = {}
    for b in arm.data.bones:
        S[b.name] = (MWi @ AM @ pb[b.name].matrix @ b.matrix_local.inverted() @ AMi @ MW).to_3x3()
    key = mesh.shape_key_add(name=f"corr_{name}", from_mix=False)
    moved = 0
    dr_all = {}
    for i in idx:
        dp = Vector(tuple(delta_posed[i]))
        if dp.length < 1e-6:
            continue
        M = Matrix(((0, 0, 0), (0, 0, 0), (0, 0, 0)))
        for gname, w in weights[i]:
            if gname in S:
                M = M + S[gname] * w
        try:
            dr = M.inverted() @ dp
        except ValueError:
            continue
        dr_all[int(i)] = np.array(tuple(dr))
    # seam twins share one correction (different ones crack the skin open along texture seams)
    for i in list(dr_all):
        ids = twin_of.get(i)
        if ids:
            avg = np.mean([dr_all.get(j, np.zeros(3)) for j in ids], axis=0)
            for j in ids:
                dr_all[j] = avg
    for i, dr in dr_all.items():
        key.data[i].co = Vector(tuple(rest[i] + dr))
        moved += 1
    # bend angle at the baked pose, for the game's driver: angle between the bone and its parent, minus rest
    bpy.context.view_layer.update()
    par = pb[bn].parent
    def ang(p_, c_):
        a = (p_.tail - p_.head).normalized(); b2 = (c_.tail - c_.head).normalized()
        return math.degrees(a.angle(b2))
    rest_ang = math.degrees((arm.data.bones[bn].parent.tail_local - arm.data.bones[bn].parent.head_local).normalized()
                            .angle((arm.data.bones[bn].tail_local - arm.data.bones[bn].head_local).normalized())) if par else 0.0
    posed_ang = ang(par, pb[bn]) if par else deg
    meta.append(dict(shape=f"corr_{name}", bone=bn, parent=par.name if par else None,
                     rest_angle=round(rest_ang, 2), full_angle=round(posed_ang, 2)))
    print(f"corr_{name}: {moved} vertices, rest {rest_ang:.1f} deg -> baked at {posed_ang:.1f} deg")
reset_pose()
mesh.modifiers.remove(cs)
mesh.vertex_groups.remove(zone)
me["kalyeah_correctives"] = json.dumps(meta)      # exported as mesh extras for the game's driver
bpy.ops.export_scene.gltf(filepath=DST, export_format='GLB', export_animations=True, export_skins=True,
                          export_morph=True, export_extras=True, export_image_format='AUTO')
print("exported", DST)
