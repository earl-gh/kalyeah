# Rebuild the four non-thumb finger chains of auto-rigged kids whose hands have fewer than 5 finger chains.
# usage: python fix_fingers.py in.glb out.glb HandBoneA,HandBoneB
import sys, bpy
import numpy as np
from mathutils import Vector, Matrix

SRC, DST = sys.argv[1], sys.argv[2]
HAND_NAMES = sys.argv[3].split(',')
FORCE = len(sys.argv) > 4 and sys.argv[4] == 'force'   # reweight hands that already have 5 chains
FINGERS = ("Index", "Middle", "Ring", "Pinky")
PHALANX_SPLIT = (0.0, 0.45, 0.75, 1.0)   # share of finger length per phalanx (proximal, middle, distal)
BLEND = 0.12                              # weight blend width around each joint (share of finger length)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=SRC)
arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
mesh = max((o for o in bpy.data.objects if o.type == 'MESH' and o.vertex_groups), key=lambda o: len(o.data.vertices))
AM, MW = arm.matrix_world, mesh.matrix_world
AMi = AM.inverted()


def is_chain(b):
    return all(len(x.children) <= 1 for x in [b] + list(b.children_recursive))


def find_hands():
    hands = []
    for b in arm.data.bones:
        if len(b.children) >= 2 and all(is_chain(c) for c in b.children):
            h = AM @ b.head_local
            if abs(h.x) > 0.25 and h.z > 0.5:
                hands.append(b.name)
    return hands


co = np.array([tuple(MW @ v.co) for v in mesh.data.vertices])
# welded adjacency (glTF splits vertices at UV seams; topology needs them joined)
from collections import deque
_key = {}
weld = np.empty(len(co), dtype=np.int64)
for i, p in enumerate(np.round(co / 1e-5).astype(np.int64)):
    weld[i] = _key.setdefault(tuple(p), i)
wadj = {}
for e in mesh.data.edges:
    a, b = weld[e.vertices[0]], weld[e.vertices[1]]
    if a != b:
        wadj.setdefault(a, set()).add(b); wadj.setdefault(b, set()).add(a)


def components(nodes):
    nodes = set(nodes); seen = set(); out = []
    for n in nodes:
        if n in seen: continue
        q = deque([n]); seen.add(n); comp = [n]
        while q:
            u = q.popleft()
            for w in wadj.get(u, ()):
                if w in nodes and w not in seen:
                    seen.add(w); q.append(w); comp.append(w)
        out.append(comp)
    return out


def weights_of(v):
    vgs = mesh.vertex_groups
    return {vgs[g.group].name: g.weight for g in v.groups if g.weight > 0}


report = []
for hand_name in HAND_NAMES:
    hb = arm.data.bones[hand_name]
    chains = list(hb.children)
    if len(chains) >= 5 and not FORCE:
        continue
    hhead = np.array(AM @ hb.head_local)
    bases = {c.name: np.array(AM @ c.head_local) for c in chains}
    tips = [np.array(AM @ (c.children_recursive[-1] if c.children_recursive else c).tail_local) for c in chains]
    rough_axis = np.mean(tips, axis=0) - hhead
    rough_axis /= np.linalg.norm(rough_axis)
    thumb = min(chains, key=lambda c: float((bases[c.name] - hhead) @ rough_axis))
    thumb_name = thumb.name
    other_names = [c.name for c in chains if c != thumb]
    others = [c for c in chains if c != thumb]
    thumb_set = {thumb.name} | {x.name for x in thumb.children_recursive}
    old_set = set()
    for c in others:
        old_set |= {c.name} | {x.name for x in c.children_recursive}
    knuck = np.mean([bases[c.name] for c in others], axis=0)
    axis = knuck - hhead
    axis /= np.linalg.norm(axis)
    # lateral = thumb direction in the palm plane
    tdir = bases[thumb.name] - hhead
    lat = tdir - (tdir @ axis) * axis
    lat /= np.linalg.norm(lat)
    normal = np.cross(axis, lat)

    # arm groups: the hand and its ancestors (fingers can be weighted to the forearm on some rigs)
    arm_groups = {hand_name, *old_set}
    p = hb.parent
    for _ in range(3):
        if p is None:
            break
        arm_groups.add(p.name)
        p = p.parent

    cand = []
    for v in mesh.data.vertices:
        w = weights_of(v)
        if not w:
            continue
        dom = max(w, key=w.get)
        if dom in thumb_set or dom not in arm_groups:
            continue
        rel = co[v.index] - knuck
        if rel @ axis < -0.006 or np.linalg.norm(rel) > 0.16:
            continue
        cand.append(v.index)
    cand = np.array(cand)
    if len(cand) < 40:
        report.append(f"{hand_name}: only {len(cand)} finger vertices, skipped")
        continue
    # length and width axes from the finger geometry itself (PCA), signed toward the tips and the thumb
    # fingertips are the points furthest from the wrist: point the finger axis at them
    dw = np.linalg.norm(co[cand] - hhead, axis=1)
    far = co[cand][dw >= np.quantile(dw, 0.9)].mean(axis=0)
    axis = (far - knuck) / np.linalg.norm(far - knuck)
    rel = co[cand] - knuck
    flat = rel - np.outer(rel @ axis, axis)
    _, _, vt = np.linalg.svd(flat - flat.mean(axis=0), full_matrices=False)
    lat = vt[0] if vt[0] @ lat > 0 else -vt[0]
    normal = np.cross(axis, lat)
    t = rel @ lat
    ax = rel @ axis
    distal = ax > np.quantile(ax, 0.55)   # the fingers separate toward the tips
    td = t[distal]
    cents = np.quantile(td, [0.875, 0.625, 0.375, 0.125])
    for _ in range(100):
        labd = np.argmin(np.abs(td[:, None] - cents[None, :]), axis=1)
        newc = np.array([td[labd == k].mean() if np.any(labd == k) else cents[k] for k in range(4)])
        if np.allclose(newc, cents):
            break
        cents = newc
    lab = np.argmin(np.abs(t[:, None] - cents[None, :]), axis=1)
    order = np.argsort(-cents)  # index, middle, ring, pinky
    side = "Left" if hhead[0] > 0 else "Right"   # glTF import: the kid faces -Y, its left is +X

    def seg_dist(P, a, b):
        ab = b - a; L2 = max(ab @ ab, 1e-12)
        u = np.clip(((P - a) @ ab) / L2, 0, 1)
        return np.linalg.norm(P - (a + np.outer(u, ab)), axis=1)

    def finger_joints(pts):
        base = knuck + ((pts - knuck) @ lat).mean() * lat + ((pts - knuck) @ normal).mean() * normal
        d = np.linalg.norm(pts - base, axis=1)
        L = np.quantile(d, 0.98)
        js = []
        for s_ in PHALANX_SPLIT:
            ring = (np.abs(d - s_ * L) < 0.08 * L)
            js.append(base if s_ == 0 else pts[ring].mean(axis=0) if ring.sum() > 3 else base + (pts[np.argmax(d)] - base) * s_)
        return js

    # preferred: the welded mesh splits into 4 fingers at some height; grow them back over the surface
    wc = weld[cand]
    axw = {}
    for vi, w in zip(cand, wc):
        axw[w] = float((co[vi] - knuck) @ axis)
    wnodes = list(axw)
    vals = np.array([axw[w] for w in wnodes])
    seeds = None
    for qq in np.linspace(0.25, 0.9, 27):
        thr = np.quantile(vals, qq)
        comps = [c for c in components([w for w in wnodes if axw[w] >= thr]) if len(c) >= 0.03 * len(wnodes)]
        if len(comps) == 4:
            seeds = comps
            break
    if seeds is not None:
        own = {}
        q = deque()
        for k, c in enumerate(seeds):
            for w in c:
                own[w] = k; q.append(w)
        wset = set(wnodes)
        while q:
            u = q.popleft()
            for w in wadj.get(u, ()):
                if w in wset and w not in own:
                    own[w] = own[u]; q.append(w)
        P = co[cand]
        cent = np.array([np.mean([co[w] for w in c], axis=0) for c in seeds])
        lab = np.array([own.get(w, -1) for w in wc])
        miss = lab < 0
        if miss.any():
            lab[miss] = np.argmin(np.linalg.norm(P[miss][:, None, :] - cent[None], axis=2), axis=1)
        order = np.argsort(-((cent - knuck) @ lat))
        method = "topology"
    else:
        method = "tracing"
        # refine: trace each finger from its tip back to the knuckles (fingers can slant), then assign each vertex
        # to the nearest traced centreline
        P = co[cand]
        ax2 = (P - knuck) @ axis
        L2 = (P - knuck) @ lat
        N2 = (P - knuck) @ normal
        # fingertips: the furthest points within narrow bands across the hand's width
        bins = np.linspace(L2.min(), L2.max(), 25)
        tip_idx = []
        for b0, b1 in zip(bins[:-1], bins[1:]):
            m = np.where((L2 >= b0) & (L2 <= b1))[0]
            if len(m) < 3:
                continue
            top = ax2[m].max()
            tip_idx.extend(m[ax2[m] >= top - 0.12 * (ax2.max() - ax2.min())])
        tip_idx = np.array(sorted(set(tip_idx)))
        tl = L2[tip_idx]
        tcl = np.quantile(tl, [0.875, 0.625, 0.375, 0.125])
        for _ in range(100):
            lt = np.argmin(np.abs(tl[:, None] - tcl[None]), axis=1)
            nc = np.array([tl[lt == k].mean() if np.any(lt == k) else tcl[k] for k in range(4)])
            if np.allclose(nc, tcl):
                break
            tcl = nc
        tc = np.stack([tcl, np.array([N2[tip_idx][lt == k].mean() if np.any(lt == k) else N2.mean() for k in range(4)])], axis=1)
        tip_ax = np.array([ax2[tip_idx][lt == k].max() if np.any(lt == k) else ax2.max() for k in range(4)])
        edges = np.linspace(ax2.max(), ax2.min(), 14)
        cur = tc.copy()
        tracks = [[] for _ in range(4)]
        for i in range(len(edges) - 1):
            m = (ax2 <= edges[i]) & (ax2 > edges[i + 1])
            if m.sum() < 8:
                continue
            S = np.stack([L2[m], N2[m]], axis=1)
            mid = (edges[i] + edges[i + 1]) / 2
            dd = ((S[:, None, :] - cur[None]) ** 2).sum(-1)
            dd[:, tip_ax < mid] = np.inf
            if not np.isfinite(dd).any():
                continue
            ls = np.argmin(dd, axis=1)
            for k in range(4):
                if mid > tip_ax[k]:
                    continue          # above this finger's tip: the points belong to longer fingers
                if np.any(ls == k):
                    cur[k] = 0.5 * cur[k] + 0.5 * S[ls == k].mean(0)
                tracks[k].append(knuck + mid * axis + cur[k][0] * lat + cur[k][1] * normal)
        for k in range(4):
            if len(tracks[k]) < 2:
                tracks[k] = [knuck + tip_ax[k] * axis + tc[k][0] * lat + tc[k][1] * normal, knuck + tc[k][0] * lat + tc[k][1] * normal]
        dist = np.full((len(P), 4), np.inf)
        for k in range(4):
            pl = tracks[k]
            for a, b in zip(pl[:-1], pl[1:]):
                dist[:, k] = np.minimum(dist[:, k], seg_dist(P, a, b))
        lab = np.argmin(dist, axis=1)
        order = np.argsort(-tc[:, 0])  # index (nearest the thumb) .. pinky

    plans = []
    for fi, k in enumerate(order):
        idx = cand[lab == k]
        pts = co[idx]
        base = knuck + ((pts - knuck) @ lat).mean() * lat + ((pts - knuck) @ normal).mean() * normal
        d = np.linalg.norm(pts - base, axis=1)
        L = np.quantile(d, 0.98)
        joints = []
        for s in PHALANX_SPLIT:
            ring = (np.abs(d - s * L) < 0.08 * L)
            joints.append(pts[ring].mean(axis=0) if s > 0 and ring.sum() > 3 else base if s == 0 else base + (pts[np.argmax(d)] - base) * s)
        plans.append((FINGERS[fi], idx, d / max(L, 1e-6), joints))

    # edit bones: drop the old shared chains, add 4 x 3 new ones
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode='EDIT')
    eb = arm.data.edit_bones
    for n in old_set:
        eb.remove(eb[n])
    # the bend axis must face the palm on both hands (palms face the thighs in the rest pose)
    palm_dir = normal if normal @ np.array([-np.sign(hhead[0]), 0.0, 0.0]) > 0 else -normal
    pal_n = Vector(tuple(AMi.to_3x3() @ Vector(tuple(palm_dir))))
    for fname, _, _, joints in plans:
        parent = eb[hand_name]
        for j in range(3):
            b = eb.new(f"{side}Hand{fname}{j + 1}")
            b.head = AMi @ Vector(tuple(joints[j]))
            b.tail = AMi @ Vector(tuple(joints[j + 1]))
            if (b.tail - b.head).length < 1e-4:
                b.tail = b.head + (AMi.to_3x3() @ Vector(tuple(axis))) * 0.01
            b.parent = parent
            b.use_connect = j > 0
            b.align_roll(pal_n)
            parent = b
    bpy.ops.object.mode_set(mode='OBJECT')

    # weights: old finger groups -> hand; finger vertices -> new phalanges with blends
    hand_vg = mesh.vertex_groups[hand_name]
    for n in old_set:
        vg = mesh.vertex_groups.get(n)
        if vg is None:
            continue
        for v in mesh.data.vertices:
            for g in v.groups:
                if g.group == vg.index and g.weight > 0:
                    hand_vg.add([v.index], g.weight, 'ADD')
        mesh.vertex_groups.remove(vg)
    for fname, idx, s_norm, _ in plans:
        vgs = [mesh.vertex_groups.get(f"{side}Hand{fname}{j + 1}") or mesh.vertex_groups.new(name=f"{side}Hand{fname}{j + 1}") for j in range(3)]
        for vi, s in zip(idx, s_norm):
            v = mesh.data.vertices[int(vi)]
            for g in list(v.groups):
                mesh.vertex_groups[g.group].remove([v.index])
            w = np.zeros(4)  # hand, p1, p2, p3
            cuts = PHALANX_SPLIT
            if s < BLEND:
                a = s / BLEND
                w[0], w[1] = 1 - a * 0.5 - 0.5, a * 0.5 + 0.5
            else:
                for j in range(3):
                    lo, hi = cuts[j], cuts[j + 1]
                    if s < hi + BLEND / 2 or j == 2:
                        if j < 2 and s > hi - BLEND / 2:
                            a = (s - (hi - BLEND / 2)) / BLEND
                            w[j + 1], w[j + 2] = 1 - a, a
                        else:
                            w[j + 1] = 1.0
                        break
            hand_vg.add([v.index], float(w[0]), 'REPLACE') if w[0] > 0 else None
            for j in range(3):
                if w[j + 1] > 0:
                    vgs[j].add([v.index], float(w[j + 1]), 'REPLACE')
    report.append(f"{hand_name} ({side}): thumb {thumb_name}, rebuilt 4 fingers from {len(other_names)} chain(s) {other_names}, "
                  f"{len(cand)} verts via {method}, per finger {[len(p[1]) for p in plans]}")

# ---- weight refinement on the hands: smooth across finger borders, normalize, at most 4 bones per vertex
SMOOTH_ITERS, SMOOTH_F = 3, 0.5
hand_sets = set()
for hand_name in HAND_NAMES:
    hb = arm.data.bones[hand_name]
    hand_sets |= {hand_name} | {b.name for b in hb.children_recursive}
vgs = mesh.vertex_groups
gidx = {g.name: g.index for g in vgs}
hand_g = {gidx[n] for n in hand_sets if n in gidx}
W = [{g.group: g.weight for g in v.groups if g.weight > 0} for v in mesh.data.vertices]
nbr = [[] for _ in mesh.data.vertices]
for e in mesh.data.edges:
    a, b = e.vertices; nbr[a].append(b); nbr[b].append(a)
# vertices split along texture seams share a position: treat them as neighbours too
pos = {}
for i, v in enumerate(mesh.data.vertices):
    pos.setdefault(tuple(np.round(np.array(v.co) / 1e-5).astype(int)), []).append(i)
for ids in pos.values():
    for i in ids:
        nbr[i].extend(j for j in ids if j != i)
target = [i for i, w in enumerate(W) if any(g in hand_g for g in w)]
for _ in range(SMOOTH_ITERS):
    new = {}
    for i in target:
        acc = {}
        for j in nbr[i]:
            for g, val in W[j].items():
                if g in hand_g:
                    acc[g] = acc.get(g, 0) + val
        n = max(1, len(nbr[i]))
        mix = dict(W[i])
        for g in set(acc) | {g for g in W[i] if g in hand_g}:
            mix[g] = (1 - SMOOTH_F) * W[i].get(g, 0) + SMOOTH_F * acc.get(g, 0) / n
        new[i] = mix
    for i, w in new.items():
        W[i] = w
# seam twins (same position, split for texture) must carry identical weights or the skin cracks when it bends
tset = set(target)
for ids in pos.values():
    ids = [i for i in ids if i in tset]
    if len(ids) < 2:
        continue
    avg = {}
    for i in ids:
        for g, val in W[i].items():
            avg[g] = avg.get(g, 0) + val / len(ids)
    for i in ids:
        W[i] = dict(avg)
changed = 0
for i in target:
    w = {g: val for g, val in W[i].items() if val > 0.01}
    top = dict(sorted(w.items(), key=lambda q: -q[1])[:4])
    tot = sum(top.values()) or 1.0
    v = mesh.data.vertices[i]
    for g in list(v.groups):
        vgs[g.group].remove([i])
    for g, val in top.items():
        vgs[g].add([i], val / tot, 'REPLACE')
    changed += 1
report.append(f"weight refinement: {changed} hand vertices smoothed, normalized, max 4 bones")
print("\n".join(report))
bpy.ops.export_scene.gltf(filepath=DST, export_format='GLB', use_selection=False, export_animations=True,
                          export_skins=True, export_image_format='AUTO')
print("exported", DST)
