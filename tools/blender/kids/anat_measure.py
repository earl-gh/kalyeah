# anatomical mismatch vs the reference pose: palm roll, elbow bend plane, foot yaw (degrees, left side)
import sys, bpy, json, math
from mathutils import Vector
P = "mixamorig:"
ref = json.load(open(sys.argv[3]))
def load(f):
    bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.gltf(filepath=f)
    arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE'); AM = arm.matrix_world
    return {b.name[len(P):]: (AM @ b.head_local, AM @ b.tail_local) for b in arm.data.bones if b.name.startswith(P)}
def signed(a, b, axis):
    a = (a - a.dot(axis) * axis).normalized(); b = (b - b.dot(axis) * axis).normalized()
    return math.degrees(math.atan2(a.cross(b).dot(axis), a.dot(b)))
rh = {k: Vector(v["head"]) for k, v in ref.items()}
out = {}
norm = load(sys.argv[1]); con = load(sys.argv[2])
for s in ("Left", "Right"):
    # palm: across-palm vector (pinky knuckle -> index knuckle) about the hand direction
    hd_k = (con[s+"HandMiddle1"][0] - con[s+"Hand"][0]).normalized(); hd_r = (rh[s+"HandMiddle1"] - rh[s+"Hand"]).normalized()
    ak = con[s+"HandIndex1"][0] - con[s+"HandPinky1"][0]; ar = rh[s+"HandIndex1"] - rh[s+"HandPinky1"]
    q = hd_k.rotation_difference(hd_r)
    out[s+"_palm_roll"] = round(signed(q @ ak, ar, hd_r), 1)
    # elbow: bend-plane normal from the kid's authored pre-bend, carried by the arm's re-pose rotation
    a0 = (norm[s+"ForeArm"][0] - norm[s+"Arm"][0]).normalized(); f0 = (norm[s+"Hand"][0] - norm[s+"ForeArm"][0]).normalized()
    bend = math.degrees(a0.angle(f0)); n0 = a0.cross(f0)
    a1 = (con[s+"ForeArm"][0] - con[s+"Arm"][0]).normalized()
    nk = a0.rotation_difference(a1) @ n0
    ra = (rh[s+"ForeArm"] - rh[s+"Arm"]).normalized()
    fwd = Vector((0, -1, 0)); nr = ra.cross(fwd)                 # reference elbow folds the forearm forward
    out[s+"_elbow_prebend"] = round(bend, 1)
    out[s+"_elbow_plane"] = round(signed(a1.rotation_difference(ra) @ nk, nr, ra), 1) if bend > 4 else None
    # foot yaw
    tk = con[s+"ToeBase"][0] - con[s+"Foot"][0]; tr = rh[s+"ToeBase"] - rh[s+"Foot"]
    out[s+"_foot_yaw"] = round(math.degrees(math.atan2(tk.x, -tk.y) - math.atan2(tr.x, -tr.y)), 1)
print("ANAT", json.dumps(out))
