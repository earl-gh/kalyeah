# Separate webbed / fused fingers: cut the skin that joins neighbouring fingers beyond the base of the finger,
# close each finger's open side, and fan the fingers slightly apart (mesh + bones, rest pose).
# usage: python separate_fingers.py in.glb out.glb HandA,HandB
import sys, bpy, bmesh
import numpy as np
from mathutils import Vector, Matrix

SRC, DST, HANDS = sys.argv[1], sys.argv[2], sys.argv[3].split(',')
CUT_S = 0.30          # cut only beyond this share of the finger's length (the base stays joined, like real webbing)
MIN_FUSED_FACES = 6   # fewer joined faces than this: the pair is already apart, leave it
SPREAD_DEG = (5.0, 1.5, -2.0, -5.5)   # index, middle, ring, pinky fan (degrees, toward the thumb side +)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=SRC)
arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
arm.data.pose_position = 'REST'
mesh = max((o for o in bpy.data.objects if o.type == 'MESH' and o.vertex_groups), key=lambda o: len(o.data.vertices))
AM, MW = arm.matrix_world.copy(), mesh.matrix_world.copy()
AMi, MWi = AM.inverted(), MW.inverted()
A = lambda x: np.array(tuple(x), dtype=float)
V = lambda x: Vector(tuple(float(c) for c in x))

hand_info = []
for hand in HANDS:
    hb = arm.data.bones[hand]
    chains = [c for c in hb.children]
    # follow single-child links (metacarpal-style bones) to the chains that start the digits
    digits = []
    for c in chains:
        x = c
        while len(x.children) >= 2:
            break
        digits.append(c)
    hhead = A(AM @ hb.head_local)
    base = {c.name: A(AM @ c.head_local) for c in digits}
    tip = {c.name: A(AM @ (c.children_recursive[-1] if c.children_recursive else c).tail_local) for c in digits}
    named = [c for c in digits if "Thumb" in c.name]
    if named:
        thumb = named[0]
    else:
        down = np.mean(list(tip.values()), axis=0) - hhead; down /= np.linalg.norm(down)
        thumb = min(digits, key=lambda c: float((base[c.name] - hhead) @ down))
    fingers = [c for c in digits if c != thumb]
    if len(fingers) != 4:
        print(f"{hand}: {len(fingers)} fingers (need 4), skipped"); continue
    knuck = np.mean([base[c.name] for c in fingers], axis=0)
    axis = np.mean([tip[c.name] for c in fingers], axis=0) - knuck; axis /= np.linalg.norm(axis)
    t = base[thumb.name] - knuck; lat = t - (t @ axis) * axis; lat /= np.linalg.norm(lat)
    normal = np.cross(axis, lat)
    fingers.sort(key=lambda c: -float((base[c.name] - knuck) @ lat))     # index .. pinky
    groups = []
    for c in fingers:
        names = [c.name] + [x.name for x in c.children_recursive]
        poly = [A(AM @ arm.data.bones[n].head_local) for n in names] + [tip[c.name]]
        groups.append(dict(chain=c.name, names=names, poly=poly))
    hand_info.append(dict(hand=hand, fingers=groups, lat=lat, axis=axis, normal=normal, knuck=knuck))

gname = {g.index: g.name for g in mesh.vertex_groups}
bm = bmesh.new(); bm.from_mesh(mesh.data)
bm.verts.ensure_lookup_table(); bm.faces.ensure_lookup_table()
dl = bm.verts.layers.deform.active
uvl = bm.loops.layers.uv.active


def finger_of(v, hi):
    w = v[dl]
    if not w:
        return -1, 0.0
    g = gname.get(max(w.keys(), key=lambda k: w[k]))
    for k, f in enumerate(hi["fingers"]):
        if g in f["names"]:
            x = A(MW @ v.co); poly = f["poly"]
            tot = sum(np.linalg.norm(b - a) for a, b in zip(poly[:-1], poly[1:]))
            best, bs, acc = 9.0, 0.0, 0.0
            for a, b in zip(poly[:-1], poly[1:]):
                ab = b - a; L = np.linalg.norm(ab); u = np.clip(((x - a) @ ab) / max(L * L, 1e-12), 0, 1)
                d = np.linalg.norm(x - (a + u * ab))
                if d < best:
                    best, bs = d, (acc + u * L) / tot
                acc += L
            return k, bs
    return -1, 0.0


report = []
moves = []   # (hand info, finger index, set of verts) for the fan
for hi in hand_info:
    lab = {}
    for v in bm.verts:
        k, s = finger_of(v, hi)
        if k >= 0:
            lab[v] = (k, s)
    def flab(f):
        ks = [lab[v][0] for v in f.verts if v in lab]
        if len(ks) < len(f.verts):
            return -1
        return max(set(ks), key=ks.count)
    face_lab = {f: flab(f) for f in bm.faces}
    cut_edges = []
    fused = {}
    for e in bm.edges:
        if len(e.link_faces) != 2:
            continue
        a, b = (face_lab[f] for f in e.link_faces)
        if a < 0 or b < 0 or a == b or abs(a - b) != 1:
            continue
        if min(lab[v][1] for v in e.verts if v in lab) < CUT_S:
            continue
        cut_edges.append(e)
        key = (min(a, b), max(a, b)); fused[key] = fused.get(key, 0) + 1
    keep = [e for e in cut_edges if fused[(min(face_lab[e.link_faces[0]], face_lab[e.link_faces[1]]), max(face_lab[e.link_faces[0]], face_lab[e.link_faces[1]]))] >= MIN_FUSED_FACES]
    if keep:
        bmesh.ops.split_edges(bm, edges=keep)
        bnd = [e for e in bm.edges if e.is_boundary and all(v in lab or True for v in e.verts)]
        # fill only the new holes: boundary edges whose verts belong to this hand's fingers
        new_bnd = [e for e in bnd if all((v in lab) for v in e.verts)]
        res = bmesh.ops.holes_fill(bm, edges=new_bnd, sides=0)
        fills = res["faces"]
        bmesh.ops.triangulate(bm, faces=fills)
        fills = [f for f in bm.faces if f not in face_lab]
        for f in fills:
            f.smooth = True
            nb = [lf for v in f.verts for lf in v.link_faces if lf in face_lab]
            if nb:
                f.material_index = nb[0].material_index
            for loop in f.loops:
                other = [l for l in loop.vert.link_loops if l.face in face_lab]
                if other:
                    loop[uvl].uv = other[0][uvl].uv
    report.append(f"{hi['hand']}: joined pairs {dict((f'{a}-{b}', n) for (a, b), n in fused.items())}, cut {len(keep)} edges")
    # fan: rotate each finger's vertices about its knuckle around the palm normal, more toward the tip
    for k, f in enumerate(hi["fingers"]):
        verts = [v for v, (kk, s) in lab.items() if kk == k]
        moves.append((hi, k, verts))

for hi, k, verts in moves:
    ang = np.radians(SPREAD_DEG[k])
    pivot = hi["fingers"][k]["poly"][0]
    R = Matrix.Rotation(ang, 3, V(hi["normal"]))
    if (A(R @ V(hi["axis"])) - hi["axis"]) @ hi["lat"] * np.sign(ang) < 0:
        R = Matrix.Rotation(-ang, 3, V(hi["normal"]))
    for v in verts:
        x = A(MW @ v.co)
        s = np.clip(((x - pivot) @ hi["axis"]) / 0.03, 0, 1)      # ease in over the first 3 cm
        rx = A(R @ V(x - pivot)) + pivot
        v.co = MWi @ V(x + (rx - x) * s)
    hi["fingers"][k]["R"] = R
bm.normal_update()
bm.to_mesh(mesh.data); bm.free(); mesh.data.update()

# the same fan for the finger bones
bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode='EDIT')
eb = arm.data.edit_bones
for hi in hand_info:
    for f in hi["fingers"]:
        R = f.get("R")
        if R is None:
            continue
        pivot = f["poly"][0]
        for n in f["names"]:
            b = eb[n]
            for attr in ("head", "tail"):
                x = A(AM @ getattr(b, attr))
                s = np.clip(((x - pivot) @ hi["axis"]) / 0.03, 0, 1)
                rx = A(R @ V(x - pivot)) + pivot
                setattr(b, attr, AMi @ V(x + (rx - x) * s))
bpy.ops.object.mode_set(mode='OBJECT')
print("\n".join(report))
bpy.ops.export_scene.gltf(filepath=DST, export_format='GLB', export_animations=True, export_skins=True,
                          export_image_format='AUTO')
print("exported", DST)
