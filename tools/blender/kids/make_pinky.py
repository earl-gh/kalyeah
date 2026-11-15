# Grow a pinky on hands with a thumb + 3 fingers: a clean closed tube that follows a copy of the outermost
# finger's chain, textured by borrowing the source finger's UVs at the matching spot.
# usage: python make_pinky.py in.glb out.glb HandBoneA,HandBoneB
import sys, bpy, bmesh
import numpy as np
from mathutils import Vector, Matrix, kdtree

SRC, DST, HANDS = sys.argv[1], sys.argv[2], sys.argv[3].split(',')
LEN_SCALE = 0.80      # pinky length vs the source finger
RAD_SCALE = 0.86      # pinky thickness vs the source finger
SHIFT = 1.55          # pinky centre offset from the source finger's chain, in pinky radii
DROP = 0.06           # pinky knuckle lower than the source's, share of the source length
SPLAY_DEG = 6.0
RINGS, SEGS = 20, 20
BASE_SINK = 0.40      # tube starts this share of its length inside the palm, curving in toward the palm centre

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=SRC)
arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
mesh = max((o for o in bpy.data.objects if o.type == 'MESH' and o.vertex_groups), key=lambda o: len(o.data.vertices))
AM, MW = arm.matrix_world.copy(), mesh.matrix_world.copy()
AMi, MWi = AM.inverted(), MW.inverted()
V = lambda x: Vector(tuple(float(c) for c in x))
A = lambda x: np.array(tuple(x), dtype=float)
gname = {g.index: g.name for g in mesh.vertex_groups}


def poly_param(x, poly):
    """distance to a polyline, arc-length position along it, and the segment's frame index"""
    best, bs, bi, acc = 9.0, 0.0, 0, 0.0
    for i, (a, b) in enumerate(zip(poly[:-1], poly[1:])):
        ab = b - a; L = np.linalg.norm(ab); u = np.clip(((x - a) @ ab) / max(L * L, 1e-12), 0, 1)
        d = np.linalg.norm(x - (a + u * ab))
        if d < best:
            best, bs, bi = d, acc + u * L, i
        acc += L
    return best, bs, bi


plans = []
for hand in HANDS:
    hb = arm.data.bones[hand]
    chains = list(hb.children)
    hhead = A(AM @ hb.head_local)
    base_of = {c.name: A(AM @ c.head_local) for c in chains}
    tip_of = {c.name: A(AM @ (c.children_recursive[-1] if c.children_recursive else c).tail_local) for c in chains}
    down = np.mean(list(tip_of.values()), axis=0) - hhead; down /= np.linalg.norm(down)
    thumb = min(chains, key=lambda c: float((base_of[c.name] - hhead) @ down))
    fingers = [c for c in chains if c != thumb]
    knuck = np.mean([base_of[c.name] for c in fingers], axis=0)
    axis = np.mean([tip_of[c.name] for c in fingers], axis=0) - knuck; axis /= np.linalg.norm(axis)
    t = base_of[thumb.name] - knuck; lat = t - (t @ axis) * axis; lat /= np.linalg.norm(lat)
    normal = np.cross(axis, lat)
    src = min(fingers, key=lambda c: float((base_of[c.name] - knuck) @ lat))
    names = [src.name] + [x.name for x in src.children_recursive]
    poly = [A(AM @ arm.data.bones[n].head_local) for n in names] + [tip_of[src.name]]
    seg_len = [np.linalg.norm(b - a) for a, b in zip(poly[:-1], poly[1:])]
    total = float(sum(seg_len))
    # source finger surface samples: vertices the chain drives, in the finger's own frame
    samples = []
    for v in mesh.data.vertices:
        ws = [(gname[g.group], g.weight) for g in v.groups]
        if not ws:
            continue
        n_, w_ = max(ws, key=lambda q: q[1])
        if n_ in names and w_ > 0.55:
            x = A(MW @ v.co)
            d, s, i = poly_param(x, poly)
            seg = poly[i + 1] - poly[i]; seg /= np.linalg.norm(seg)
            r = x - poly[i] - ((x - poly[i]) @ seg) * seg
            ang = np.arctan2(r @ normal, r @ lat)
            samples.append((v.index, s / total, ang, d))
    radius = float(np.quantile([q[3] for q in samples], 0.75))
    plans.append(dict(hand=hand, names=names, poly=poly, total=total, samples=samples, radius=radius,
                      lat=lat, axis=axis, normal=normal, side="Left" if hhead[0] > 0 else "Right"))
    print(f"{hand}: thumb {thumb.name}, copying outer finger {src.name} {names}, length {total:.3f} m, radius {radius:.4f} m, {len(samples)} samples")

# ---- the pinky's transform: shifted out by a finger width + gap, lowered, splayed, scaled about the knuckle
for p in plans:
    base = p["poly"][0]
    shift = -p["lat"] * (SHIFT * p["radius"] * RAD_SCALE) - p["axis"] * DROP * p["total"]
    rot = Matrix.Rotation(np.radians(SPLAY_DEG), 4, V(p["normal"]))
    if (A(rot.to_3x3() @ V(p["axis"]))) @ p["lat"] > 0:
        rot = Matrix.Rotation(-np.radians(SPLAY_DEG), 4, V(p["normal"]))
    p["T"] = Matrix.Translation(V(base + shift)) @ rot @ Matrix.Scale(LEN_SCALE, 4) @ Matrix.Translation(V(-base))
    p["rot3"] = rot.to_3x3()

# ---- bones
bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode='EDIT')
eb = arm.data.edit_bones
for p in plans:
    parent, p["new"] = eb[p["hand"]], []
    for j, n in enumerate(p["names"]):
        s = eb[n]
        b = eb.new(f"{p['side']}HandPinky{j + 1}")
        b.head = AMi @ (p["T"] @ (AM @ s.head)); b.tail = AMi @ (p["T"] @ (AM @ s.tail))
        b.roll = s.roll; b.parent = parent; b.use_connect = j > 0
        parent = b; p["new"].append(b.name)
bpy.ops.object.mode_set(mode='OBJECT')
for p in plans:
    for n in p["new"]:
        if mesh.vertex_groups.get(n) is None:
            mesh.vertex_groups.new(name=n)

# ---- mesh
me = mesh.data
uvl = me.uv_layers.active.data
vert_uv = {}
for poly_ in me.polygons:
    for li in poly_.loop_indices:
        vert_uv.setdefault(me.loops[li].vertex_index, tuple(uvl[li].uv))
src_mat = {}
for poly_ in me.polygons:
    for vi in poly_.vertices:
        src_mat.setdefault(vi, poly_.material_index)

bm = bmesh.new(); bm.from_mesh(me)
dl = bm.verts.layers.deform.verify()
uv = bm.loops.layers.uv.active
gi = {g.name: g.index for g in mesh.vertex_groups}
for p in plans:
    npoly = [A(p["T"] @ V(x)) for x in p["poly"]]
    seg = [np.linalg.norm(b - a) for a, b in zip(npoly[:-1], npoly[1:])]
    tot = float(sum(seg))
    lat, nrm = A(p["rot3"] @ V(p["lat"])), A(p["rot3"] @ V(p["normal"]))
    kd = kdtree.KDTree(len(p["samples"]))
    for k, (vi, s, ang, _) in enumerate(p["samples"]):
        kd.insert((s * 3.0, np.cos(ang), np.sin(ang)), k)
    kd.balance()

    def at(s):
        """point and tangent along the new chain at arc-length share s (may be < 0: inside the palm)"""
        d = s * tot
        if d <= 0:
            t0 = (npoly[1] - npoly[0]) / seg[0]
            return npoly[0] + t0 * d, t0
        acc = 0.0
        for i, L in enumerate(seg):
            if d <= acc + L or i == len(seg) - 1:
                t0 = (npoly[i + 1] - npoly[i]) / L
                return npoly[i] + t0 * (d - acc), t0
            acc += L

    R0 = p["radius"] * RAD_SCALE
    rings = []
    ss = np.concatenate([np.linspace(-BASE_SINK, 0.80, RINGS - 5), 0.80 + 0.19 * np.sin(np.linspace(0.3, 1.0, 5) * np.pi / 2)])
    for s in ss:
        c, tg = at(min(s, 0.985))
        if s < 0:   # inside the palm: curve back toward the palm centre so no base edge shows
            c = c + lat * (-s / BASE_SINK) * R0 * 1.4
        u_ = lat - (lat @ tg) * tg; u_ /= np.linalg.norm(u_); w_ = np.cross(tg, u_)
        taper = 1.0 - 0.18 * max(s, 0)
        tipround = np.sqrt(max(0.04, 1 - ((s - 0.80) / 0.20) ** 2)) if s > 0.80 else 1.0
        r = R0 * taper * max(tipround, 0.0)
        ring = []
        for k in range(SEGS):
            a = 2 * np.pi * k / SEGS
            x = c + r * (np.cos(a) * u_ + np.sin(a) * w_)
            ring.append((bm.verts.new(MWi @ V(x)), s, a))
        rings.append(ring)
    cap_tip = bm.verts.new(MWi @ V(at(0.985)[0] + at(0.985)[1] * R0 * 0.12))
    cap_base = bm.verts.new(MWi @ V(at(-BASE_SINK)[0]))
    faces = []
    for i in range(RINGS - 1):
        for k in range(SEGS):
            a, b = rings[i][k][0], rings[i][(k + 1) % SEGS][0]
            c, d = rings[i + 1][(k + 1) % SEGS][0], rings[i + 1][k][0]
            faces.append(bm.faces.new((a, b, c, d)))
    for k in range(SEGS):
        faces.append(bm.faces.new((rings[-1][k][0], rings[-1][(k + 1) % SEGS][0], cap_tip)))
        faces.append(bm.faces.new((rings[0][(k + 1) % SEGS][0], rings[0][k][0], cap_base)))
    info = {cap_tip: (1.0, 0.0), cap_base: (-BASE_SINK, 0.0)}
    for ring in rings:
        for v, s, a in ring:
            info[v] = (s, a)
    mat_votes = [src_mat.get(q[0], 0) for q in p["samples"]]
    mi = max(set(mat_votes), key=mat_votes.count)
    for f in faces:
        f.material_index = mi
        f.smooth = True
        # smooth per-point texture lookup; a face whose points land on different UV islands gets one spot instead
        luv = []
        for loop in f.loops:
            s_, a_ = info[loop.vert]
            _, k, _ = kd.find((min(max(s_, 0.02), 1.0) * 3.0, np.cos(a_), np.sin(a_)))
            luv.append(vert_uv.get(p["samples"][k][0], (0.5, 0.5)))
        spread = max(np.hypot(u0[0] - u1[0], u0[1] - u1[1]) for u0 in luv for u1 in luv)
        if spread > 0.02:
            ss_ = np.mean([info[l.vert][0] for l in f.loops]); aa = np.angle(np.mean([np.exp(1j * info[l.vert][1]) for l in f.loops]))
            _, k, _ = kd.find((min(max(ss_, 0.02), 1.0) * 3.0, np.cos(aa), np.sin(aa)))
            luv = [vert_uv.get(p["samples"][k][0], (0.5, 0.5))] * len(luv)
        for loop, u_ in zip(f.loops, luv):
            loop[uv].uv = u_
    bmesh.ops.recalc_face_normals(bm, faces=faces)
    # weights along the new chain (3 phalanges + end), smooth blends; the base blends into the hand
    cuts = np.cumsum([0] + seg) / tot
    hand_g = gi[p["hand"]]
    for v, (s, _) in info.items():
        w = v[dl]; w.clear()
        if s <= 0.06:
            a = np.clip((s + BASE_SINK) / (0.06 + BASE_SINK), 0, 1)
            w[hand_g] = 1 - 0.6 * a; w[gi[p["new"][0]]] = 0.6 * a
            continue
        j = int(np.clip(np.searchsorted(cuts, s) - 1, 0, len(p["new"]) - 2))
        lo, hi = cuts[j], cuts[j + 1]
        f = (s - lo) / max(hi - lo, 1e-6)
        if f > 0.75 and j + 1 < len(p["new"]) - 1:
            a = (f - 0.75) / 0.5
            w[gi[p["new"][j]]] = 1 - a; w[gi[p["new"][j + 1]]] = a
        elif f < 0.25 and j > 0:
            a = 0.5 + f / 0.5
            w[gi[p["new"][j]]] = a; w[gi[p["new"][j - 1]]] = 1 - a
        else:
            w[gi[p["new"][j]]] = 1.0
    # nudge the outer palm edge toward the new knuckle so the tube's base reads as part of the hand
    kn = npoly[0]
    for v in bm.verts:
        if v in info:
            continue
        w = v[dl]
        if hand_g not in w or w[hand_g] < 0.35:
            continue
        x = A(MW @ v.co)
        if (x - p["poly"][0]) @ p["lat"] > 0:
            continue
        d = np.linalg.norm(x - kn)
        fall = max(0.0, 1 - d / (4.5 * R0))
        if fall > 0:
            push = (kn - p["poly"][0]); push = push - (push @ p["axis"]) * p["axis"]
            v.co = MWi @ V(x + push * 0.5 * (3 * fall ** 2 - 2 * fall ** 3))
    print(f"  {p['hand']}: pinky tube {len(info)} verts, material {mi}, bones {p['new']}")
bm.to_mesh(me); bm.free(); me.update()
bpy.ops.export_scene.gltf(filepath=DST, export_format='GLB', export_animations=True, export_skins=True,
                          export_image_format='AUTO')
print("exported", DST)
