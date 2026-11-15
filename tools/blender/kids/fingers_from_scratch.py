# Give a hand with no finger bones a thumb + 4 finger chains (3 phalanges each) found from the mesh.
# usage: python fingers_from_scratch.py in.glb out.glb LeftHand,RightHand
import sys, bpy
import numpy as np
from collections import deque
from mathutils import Vector

SRC, DST, HANDS = sys.argv[1], sys.argv[2], sys.argv[3].split(',')
NAMES = ("Thumb", "Index", "Middle", "Ring", "Pinky")
KNUCKLE = 0.50        # finger knuckle at this share of wrist->tip
THUMB_BASE = 0.22     # thumb chain starts this share of wrist->thumb tip
SPLITS = {"Thumb": (0.0, 0.42, 0.72, 1.0), "_": (0.0, 0.45, 0.75, 1.0)}
BLEND = 0.10
REACH = {"Thumb": 0.025, "_": 0.030}   # a vertex joins a digit only within this distance of its line (m)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=SRC)
arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
arm.data.pose_position = 'REST'
mesh = max((o for o in bpy.data.objects if o.type == 'MESH' and o.vertex_groups), key=lambda o: len(o.data.vertices))
AM, MW = arm.matrix_world.copy(), mesh.matrix_world.copy()
AMi = AM.inverted()
A = lambda x: np.array(tuple(x), dtype=float)
co = np.array([tuple(MW @ v.co) for v in mesh.data.vertices])
key = {}
weld = np.empty(len(co), dtype=np.int64)
for i, p in enumerate(np.round(co / 1e-5).astype(np.int64)):
    weld[i] = key.setdefault(tuple(p), i)
adj = {}
for e in mesh.data.edges:
    a, b = weld[e.vertices[0]], weld[e.vertices[1]]
    if a != b:
        adj.setdefault(a, set()).add(b); adj.setdefault(b, set()).add(a)


def seg_dist(P, a, b):
    ab = b - a; L2 = max(ab @ ab, 1e-12)
    u = np.clip(((P - a) @ ab) / L2, 0, 1)
    return np.linalg.norm(P - (a + np.outer(u, ab)), axis=1), u


plans = []
for hand in HANDS:
    W = A(AM @ arm.data.bones[hand].head_local)
    hg = mesh.vertex_groups[hand].index
    idx = []
    for v in mesh.data.vertices:
        ws = {g.group: g.weight for g in v.groups}
        if ws and max(ws, key=ws.get) == hg:
            idx.append(v.index)
    idx = np.array(idx)
    P = co[idx]
    d = np.linalg.norm(P - W, axis=1)
    dmax = d.max()
    wid = {weld[i]: k for k, i in enumerate(idx)}
    # tips: welded vertices with no 2-ring neighbour further from the wrist, out past 60% of the reach
    dist_w = {weld[i]: d[k] for k, i in enumerate(idx)}
    tips = []
    # distance over the skin from the wrist (a thumb pressed on the index still ends in its own peak)
    import heapq
    nodes = set(dist_w)
    near = min(dist_w.values())
    geo = {w: 0.0 for w, dw in dist_w.items() if dw < near + 0.012}
    pq = [(0.0, w) for w in geo]
    heapq.heapify(pq)
    while pq:
        g0, u = heapq.heappop(pq)
        if g0 > geo.get(u, 9e9):
            continue
        for n in adj.get(u, ()):
            if n not in nodes:
                continue
            g1 = g0 + float(np.linalg.norm(co[u] - co[n]))
            if g1 < geo.get(n, 9e9):
                geo[n] = g1; heapq.heappush(pq, (g1, n))
    gmax = max(geo.values())
    dist_w = {w: geo.get(w, 0.0) for w in dist_w}
    # digits line up front-to-back (palm faces the thigh, thumb in front): trace all 5 from tip to knuckle
    geo_v = np.array([dist_w[weld[i]] for i in idx])
    far = P[geo_v >= 0.85 * gmax].mean(axis=0)
    axis = (far - W) / np.linalg.norm(far - W)
    fwd = np.array([0.0, -1.0, 0.0]); lat = fwd - (fwd @ axis) * axis; lat /= np.linalg.norm(lat)
    normal = np.cross(axis, lat)
    sel = geo_v >= 0.30 * gmax
    Q = P[sel]; ax2 = (Q - W) @ axis; L2 = (Q - W) @ lat; N2 = (Q - W) @ normal
    bins = np.linspace(L2.min(), L2.max(), 31)
    tip_idx = []
    for b0, b1 in zip(bins[:-1], bins[1:]):
        m = np.where((L2 >= b0) & (L2 <= b1))[0]
        if len(m) < 3:
            continue
        top = ax2[m].max()
        tip_idx.extend(m[ax2[m] >= top - 0.10 * (ax2.max() - ax2.min())])
    tip_idx = np.array(sorted(set(tip_idx)))
    tl = L2[tip_idx]
    tcl = np.quantile(tl, [0.9, 0.7, 0.5, 0.3, 0.1])
    for _ in range(200):
        lt = np.argmin(np.abs(tl[:, None] - tcl[None]), axis=1)
        nc = np.array([tl[lt == k].mean() if np.any(lt == k) else tcl[k] for k in range(5)])
        if np.allclose(nc, tcl):
            break
        tcl = nc
    tc = np.stack([tcl, np.array([N2[tip_idx][lt == k].mean() if np.any(lt == k) else N2.mean() for k in range(5)])], axis=1)
    tip_ax = np.array([ax2[tip_idx][lt == k].max() if np.any(lt == k) else ax2.max() for k in range(5)])
    edges = np.linspace(ax2.max(), ax2.min(), 18)
    cur = tc.copy(); tracks = [[] for _ in range(5)]
    for i in range(len(edges) - 1):
        m = (ax2 <= edges[i]) & (ax2 > edges[i + 1])
        if m.sum() < 8:
            continue
        S = np.stack([L2[m], N2[m]], axis=1)
        mid = (edges[i] + edges[i + 1]) / 2
        dd = ((S[:, None, :] - cur[None]) ** 2).sum(-1); dd[:, tip_ax < mid] = np.inf
        if not np.isfinite(dd).any():
            continue
        ls = np.argmin(dd, axis=1)
        for k in range(5):
            if mid > tip_ax[k]:
                continue
            if np.any(ls == k):
                cur[k] = 0.5 * cur[k] + 0.5 * S[ls == k].mean(0)
            tracks[k].append(W + mid * axis + cur[k][0] * lat + cur[k][1] * normal)
    order = list(np.argsort(-tc[:, 0]))   # thumb (front) .. pinky (back)
    chains = []
    for name, k in zip(NAMES, order):
        tr = tracks[k]
        if len(tr) < 3:
            print(f"{hand}: digit {name} could not be traced"); break
        tip = W + tip_ax[k] * axis + tc[k][0] * lat + tc[k][1] * normal
        frac = THUMB_BASE if name == "Thumb" else KNUCKLE
        tr = [tip] + tr
        seg = np.cumsum([0] + [np.linalg.norm(b - a) for a, b in zip(tr[:-1], tr[1:])])
        want = frac_len = (1 - frac) * np.linalg.norm(tip - W)
        bi = int(np.clip(np.searchsorted(seg, frac_len), 1, len(tr) - 1))
        path = tr[:bi + 1][::-1]                   # knuckle .. tip along the traced finger
        cs = np.cumsum([0] + [np.linalg.norm(b - a) for a, b in zip(path[:-1], path[1:])]); cs /= cs[-1]
        sp = SPLITS["Thumb" if name == "Thumb" else "_"]
        joints = [np.array([np.interp(t, cs, [q[d] for q in path]) for d in range(3)]) for t in sp]
        chains.append((name, joints))
    if len(chains) < 5:
        continue
    print(f"  {hand}: digit tips along the width {np.round(tcl, 3)}, reach {gmax:.3f} m")
    side = "Left" if W[0] > 0 else "Right"
    plans.append(dict(hand=hand, idx=idx, chains=chains, side=side, axis=axis, normal=normal))
    print(f"{hand} ({side}): {len(idx)} hand verts")

bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode='EDIT')
eb = arm.data.edit_bones
for p in plans:
    hand_eb = eb[p["hand"]]
    for name, joints in p["chains"]:
        parent = hand_eb
        for j in range(3):
            b = eb.new(f"{p['side']}Hand{name}{j + 1}")
            b.head = AMi @ Vector(tuple(joints[j])); b.tail = AMi @ Vector(tuple(joints[j + 1]))
            b.parent = parent; b.use_connect = j > 0
            n_ = p["normal"] if p["normal"] @ np.array([-np.sign(joints[0][0]), 0.0, 0.0]) > 0 else -p["normal"]
            b.align_roll(AMi.to_3x3() @ Vector(tuple(n_)))      # bend axis faces the palm on both hands
            parent = b
bpy.ops.object.mode_set(mode='OBJECT')

for p in plans:
    P = co[p["idx"]]
    best = np.full(len(P), np.inf); lab = np.full(len(P), -1); spos = np.zeros(len(P))
    for k, (name, joints) in enumerate(p["chains"]):
        base, tip = joints[0], joints[-1]
        dd = np.full(len(P), np.inf); ss = np.zeros(len(P))
        L = np.linalg.norm(tip - base); acc = 0.0
        for a, b in zip(joints[:-1], joints[1:]):
            dk, u = seg_dist(P, a, b)
            m = dk < dd
            dd[m] = dk[m]; ss[m] = (acc + u[m] * np.linalg.norm(b - a)) / L
            acc += np.linalg.norm(b - a)
        beyond = (P - base) @ ((tip - base) / L) > -0.004
        take = beyond & (dd < best) & (dd < REACH["Thumb" if name == "Thumb" else "_"])
        best[take] = dd[take]; lab[take] = k; spos[take] = ss[take]
    gi = {}
    for name, _ in p["chains"]:
        for j in range(3):
            n = f"{p['side']}Hand{name}{j + 1}"
            gi[n] = mesh.vertex_groups.new(name=n)
    hand_vg = mesh.vertex_groups[p["hand"]]
    counts = [0] * 5
    for vi, k, s in zip(p["idx"], lab, spos):
        if k < 0:
            continue
        counts[k] += 1
        name = p["chains"][k][0]
        sp = SPLITS["Thumb" if name == "Thumb" else "_"]
        w = np.zeros(4)
        if s < BLEND:
            a = s / BLEND; w[0], w[1] = 0.5 - 0.5 * a, 0.5 + 0.5 * a
        else:
            for j in range(3):
                hi = sp[j + 1]
                if s < hi + BLEND / 2 or j == 2:
                    if j < 2 and s > hi - BLEND / 2:
                        a = (s - (hi - BLEND / 2)) / BLEND; w[j + 1], w[j + 2] = 1 - a, a
                    else:
                        w[j + 1] = 1.0
                    break
        vi = int(vi)
        hand_vg.remove([vi])
        if w[0] > 0:
            hand_vg.add([vi], float(w[0]), 'REPLACE')
        for j in range(3):
            if w[j + 1] > 0:
                gi[f"{p['side']}Hand{name}{j + 1}"].add([vi], float(w[j + 1]), 'REPLACE')
    print(f"  {p['hand']}: verts per finger {dict(zip(NAMES, counts))}")

bpy.ops.export_scene.gltf(filepath=DST, export_format='GLB', export_animations=True, export_skins=True,
                          export_image_format='AUTO')
print("exported", DST)
