# Clean fingers welded into the hand.
#   pinky   : grow a pinky in line with the other fingers (hands with a thumb + 3 fingers)
#   rebuild : replace fused fingers past the knuckle with 4 clean fingers along their chains
# usage: python tube_fingers.py in.glb out.glb HandA,HandB pinky|rebuild
import sys, bpy, bmesh
import numpy as np
from mathutils import Vector, Matrix, kdtree

SRC, DST, HANDS, MODE = sys.argv[1], sys.argv[2], sys.argv[3].split(','), sys.argv[4]
RINGS, SEGS = 22, 18
BASE_SINK = 0.30      # tube starts this share of its length back inside the palm
S_DEL = 0.12          # rebuild: original finger skin beyond this share of the finger is removed
PINKY_LEN, PINKY_RAD, PINKY_DROP = 0.80, 0.88, 0.07
SEAM_SMOOTH = 4       # smoothing passes on the weld

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=SRC)
arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
arm.data.pose_position = 'REST'
body = max((o for o in bpy.data.objects if o.type == 'MESH' and o.vertex_groups), key=lambda o: len(o.data.vertices))
AM, MW = arm.matrix_world.copy(), body.matrix_world.copy()
AMi, MWi = AM.inverted(), MW.inverted()
A = lambda x: np.array(tuple(x), dtype=float)
V = lambda x: Vector(tuple(float(c) for c in x))
gname = {g.index: g.name for g in body.vertex_groups}
me = body.data
co = np.array([tuple(MW @ v.co) for v in me.vertices])
uvl = me.uv_layers.active.data
vert_uv = {}
for p_ in me.polygons:
    for li in p_.loop_indices:
        vert_uv.setdefault(me.loops[li].vertex_index, tuple(uvl[li].uv))
vert_mat = {}
for p_ in me.polygons:
    for vi in p_.vertices:
        vert_mat.setdefault(vi, p_.material_index)
dom = []
for v in me.vertices:
    ws = [(gname[g.group], g.weight) for g in v.groups]
    dom.append(max(ws, key=lambda q: q[1]) if ws else (None, 0.0))


def base_color_image(mi):
    mat = me.materials[mi] if mi < len(me.materials) else None
    if not mat or not mat.use_nodes:
        return None
    for n in mat.node_tree.nodes:
        if n.type == 'BSDF_PRINCIPLED':
            links = n.inputs['Base Color'].links
            node = links[0].from_node if links else None
            while node is not None and node.type != 'TEX_IMAGE':
                ins = [i for i in node.inputs if i.links]
                node = ins[0].links[0].from_node if ins else None
            return node.image if node else None
    return None


_img_cache = {}


def luminance(mi, uv_):
    img = base_color_image(mi)
    if img is None:
        return 1.0
    if img.name not in _img_cache:
        w, h = img.size
        px = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, img.channels)
        _img_cache[img.name] = (px[..., :3] @ np.array([0.299, 0.587, 0.114], dtype=np.float32), w, h)
    lum, w, h = _img_cache[img.name]
    x = int((uv_[0] % 1.0) * (w - 1)); y = int((uv_[1] % 1.0) * (h - 1))
    return float(lum[y, x])


def bright_only(smp):
    """drop the darkest source points (painted creases between fused fingers)"""
    if not smp:
        return smp
    lum = np.array([luminance(vert_mat.get(q[0], 0), vert_uv.get(q[0], (0.5, 0.5))) for q in smp])
    keep = lum >= np.quantile(lum, 0.45) * 0.97
    out = [q for q, k in zip(smp, keep) if k]
    return out if len(out) > 20 else smp


def poly_param(x, poly):
    best, bs, bi, acc = 9.0, 0.0, 0, 0.0
    for i, (a, b) in enumerate(zip(poly[:-1], poly[1:])):
        ab = b - a; L = np.linalg.norm(ab); u = np.clip(((x - a) @ ab) / max(L * L, 1e-12), 0, 1)
        d = np.linalg.norm(x - (a + u * ab))
        if d < best:
            best, bs, bi = d, acc + u * L, i
        acc += L
    return best, bs / acc, bi


def chain_poly(c):
    names = [c.name] + [x.name for x in c.children_recursive]
    last = c.children_recursive[-1] if c.children_recursive else c
    return names, [A(AM @ arm.data.bones[n].head_local) for n in names] + [A(AM @ last.tail_local)]


def samples_of(names, poly, lat, normal):
    out = []
    for vi, (n, w) in enumerate(dom):
        if n in names and w > 0.5:
            d, s, i = poly_param(co[vi], poly)
            seg = poly[i + 1] - poly[i]; seg /= np.linalg.norm(seg)
            r = co[vi] - poly[i] - ((co[vi] - poly[i]) @ seg) * seg
            out.append((vi, s, float(np.arctan2(r @ normal, r @ lat)), d))
    return out


# ---------------------------------------------------------------- plan the fingers
tubes = []          # dict(path, radius, samples, lat, normal, groups, hand, side)
delete_v = set()
for hand in HANDS:
    hb = arm.data.bones[hand]
    chains = list(hb.children)
    hhead = A(AM @ hb.head_local)
    info = {c.name: chain_poly(c) for c in chains}
    tips = {n: p[-1] for n, (_, p) in info.items()}
    bases = {n: p[0] for n, (_, p) in info.items()}
    named = [c for c in chains if "Thumb" in c.name]
    if named:
        thumb = named[0]
    else:
        down = np.mean(list(tips.values()), axis=0) - hhead; down /= np.linalg.norm(down)
        thumb = min(chains, key=lambda c: float((bases[c.name] - hhead) @ down))
    fingers = [c for c in chains if c != thumb]
    knuck = np.mean([bases[c.name] for c in fingers], axis=0)
    axis = np.mean([tips[c.name] for c in fingers], axis=0) - knuck; axis /= np.linalg.norm(axis)
    t = bases[thumb.name] - knuck; lat = t - (t @ axis) * axis; lat /= np.linalg.norm(lat)
    normal = np.cross(axis, lat)
    fingers.sort(key=lambda c: -float((bases[c.name] - knuck) @ lat))      # index .. outermost
    side = "Left" if hhead[0] > 0 else "Right"
    if MODE == "pinky":
        if len(fingers) != 3:
            print(f"{hand}: {len(fingers)} fingers, pinky mode needs 3, skipped"); continue
        a, b = fingers[-2], fingers[-1]
        na, pa = info[a.name]; nb, pb = info[b.name]
        # same row, touching the outer finger: one finger-width further along the knuckle and fingertip rows
        smp = samples_of(nb, pb, lat, normal)
        r_out = float(np.quantile([q[3] for q in smp], 0.75))
        rad = r_out * PINKY_RAD
        row_b = (pb[0] - pa[0]); row_b /= np.linalg.norm(row_b)
        row_t = (pb[-1] - pa[-1]); row_t /= np.linalg.norm(row_t)
        nbase = pb[0] + row_b * (r_out + 0.8 * rad) - axis * PINKY_DROP * np.linalg.norm(pb[-1] - pb[0])
        ntip_full = pb[-1] + row_t * (r_out + 0.9 * rad)
        ntip = nbase + (ntip_full - nbase) * PINKY_LEN
        src_v = pb[-1] - pb[0]; dst_v = ntip - nbase
        rot = V(src_v).rotation_difference(V(dst_v)).to_matrix()
        sc = np.linalg.norm(dst_v) / np.linalg.norm(src_v)
        path = [nbase + sc * A(rot @ V(q - pb[0])) for q in pb]
        inward = -row_b * (r_out + rad)      # toward the palm: the buried base curves in here
        groups = [f"{side}HandPinky{j + 1}" for j in range(len(path) - 1)]
        tubes.append(dict(path=path, radius=rad, samples=smp, lat=lat, normal=normal, groups=groups,
                          hand=hand, side=side, src_path=pb, new_bones=True, inward=inward))
        print(f"{hand}: pinky in the row after {b.name}; radius {rad:.4f} m")
    else:
        if len(fingers) != 4:
            print(f"{hand}: {len(fingers)} fingers, rebuild needs 4, skipped"); continue
        for c in fingers:
            names, path = info[c.name]
            smp = samples_of(names, path, lat, normal)
            rad = float(np.quantile([q[3] for q in smp], 0.70))
            tubes.append(dict(path=path, radius=rad, samples=smp, lat=lat, normal=normal, groups=names[:len(path) - 1],
                              hand=hand, side=side, src_path=path, new_bones=False))
            for vi, s, _, _ in smp:
                if s > S_DEL:
                    delete_v.add(vi)
            # also the skin between fused fingers that the chain does not own outright
            names_set = set(names)
            for vi, (n, w) in enumerate(dom):
                if n in names_set and w <= 0.5:
                    d, s, _ = poly_param(co[vi], path)
                    if s > S_DEL:
                        delete_v.add(vi)
        print(f"{hand}: rebuilding {[c.name for c in fingers]}")

# ---------------------------------------------------------------- new bones (pinky)
if any(tb["new_bones"] for tb in tubes):
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode='EDIT')
    eb = arm.data.edit_bones
    for tb in tubes:
        if not tb["new_bones"]:
            continue
        parent = eb[tb["hand"]]
        for j, n in enumerate(tb["groups"]):
            b = eb.new(n)
            b.head = AMi @ V(tb["path"][j]); b.tail = AMi @ V(tb["path"][j + 1])
            b.parent = parent; b.use_connect = j > 0
            b.align_roll(AMi.to_3x3() @ V(tb["normal"]))
            parent = b
    bpy.ops.object.mode_set(mode='OBJECT')
for tb in tubes:
    for n in tb["groups"] + [tb["hand"]]:
        if body.vertex_groups.get(n) is None:
            body.vertex_groups.new(name=n)

# ---------------------------------------------------------------- remove fused skin, close the openings
bm = bmesh.new(); bm.from_mesh(me)
bm.verts.ensure_lookup_table()
if delete_v:
    # glTF splits vertices along texture seams: weld them around the hands so the cut is one closed loop
    hand_pts = [tb["path"][0] for tb in tubes]
    near_hand = [v for v in bm.verts if min(np.linalg.norm(A(MW @ v.co) - p) for p in hand_pts) < 0.09]
    bmesh.ops.remove_doubles(bm, verts=near_hand, dist=1e-5)
    # a clean cut: whole faces whose centre is past the knuckle plane of their hand and near a finger line
    planes = {}
    for h in HANDS:
        tbs = [tb for tb in tubes if tb["hand"] == h]
        if not tbs:
            continue
        ax_ = np.mean([(tb["path"][1] - tb["path"][0]) / np.linalg.norm(tb["path"][1] - tb["path"][0]) for tb in tbs], axis=0)
        ax_ /= np.linalg.norm(ax_)
        k0 = np.mean([tb["path"][0] for tb in tbs], axis=0)
        planes[h] = (k0, ax_, tbs)
    kill = []
    for f in bm.faces:
        c = A(MW @ f.calc_center_median())
        for k0, ax_, tbs in planes.values():
            if (c - k0) @ ax_ < 0.004:
                continue
            if any(poly_param(c, tb["path"])[0] < tb["radius"] * 1.9 for tb in tbs):
                kill.append(f); break
    edges_near = {e for f in kill for e in f.edges}
    bmesh.ops.delete(bm, geom=kill, context='FACES_ONLY')
    loose = [v for v in bm.verts if not v.link_faces]
    bmesh.ops.delete(bm, geom=loose, context='VERTS')
    bnd = [e for e in bm.edges if e.is_valid and e.is_boundary and e in edges_near]
    if bnd:
        ring_v = list({v for e in bnd for v in e.verts})
        for _ in range(3):   # relax the jagged cut before capping
            bmesh.ops.smooth_vert(bm, verts=ring_v, factor=0.5, use_axis_x=True, use_axis_y=True, use_axis_z=True)
        print("open edges at the cut:", len(bnd), "non-manifold:", sum(1 for e in bnd if len(e.link_faces) != 1))
        res = bmesh.ops.holes_fill(bm, edges=bnd, sides=0)
        caps = res["faces"]
        print("cap faces:", len(caps), "still open:", sum(1 for e in bm.edges if e.is_valid and e.is_boundary and e in edges_near))
        tri = bmesh.ops.triangulate(bm, faces=caps)
        caps = tri["faces"]
        cap_v = list({v for f in caps for v in f.verts})
        for _ in range(4):
            bmesh.ops.smooth_vert(bm, verts=cap_v, factor=0.5, use_axis_x=True, use_axis_y=True, use_axis_z=True)
        hand_axes = [(np.mean([tb["path"][0] for tb in tubes if tb["hand"] == h], axis=0), h) for h in HANDS if any(tb["hand"] == h for tb in tubes)]
        for f in caps:
            f.smooth = True
            c = A(MW @ f.calc_center_median())
            pal, hname = min(hand_axes, key=lambda q: np.linalg.norm(c - q[0]))
            ax_ = np.mean([tb["path"][1] - tb["path"][0] for tb in tubes if tb["hand"] == hname], axis=0)
            if A(MW.to_3x3() @ f.normal) @ ax_ < 0:
                f.normal_flip()
    print(f"removed {len(kill)} fused finger faces, capped the knuckles")
bm.to_mesh(me); bm.free(); me.update()

# ---------------------------------------------------------------- build the tubes as their own object
tm = bpy.data.meshes.new("tubes"); tob = bpy.data.objects.new("tubes", tm)
bpy.context.scene.collection.objects.link(tob)
tob.matrix_world = MW
for m in me.materials:
    tm.materials.append(m)
for g in body.vertex_groups:
    tob.vertex_groups.new(name=g.name)
tgi = {g.name: g.index for g in tob.vertex_groups}
tb_m = bmesh.new()
uv = tb_m.loops.layers.uv.new(me.uv_layers.active.name)
dl = tb_m.verts.layers.deform.verify()
for tb in tubes:
    path = tb["path"]
    seg = [np.linalg.norm(b - a) for a, b in zip(path[:-1], path[1:])]; tot = float(sum(seg))
    tb["samples"] = bright_only(tb["samples"])
    kd = kdtree.KDTree(len(tb["samples"]))
    for k, (vi, s, ang, _) in enumerate(tb["samples"]):
        kd.insert((s * 3.0, np.cos(ang), np.sin(ang)), k)
    kd.balance()
    # orientation: the source frame carried along (pinky: source path frame mapped by the same rotation)
    lat = tb["lat"]

    def at(s):
        d = s * tot
        if d <= 0:
            t0 = (path[1] - path[0]) / seg[0]; return path[0] + t0 * d, t0
        acc = 0.0
        for i, L in enumerate(seg):
            if d <= acc + L or i == len(seg) - 1:
                t0 = (path[i + 1] - path[i]) / L; return path[i] + t0 * (d - acc), t0
            acc += L

    R0 = tb["radius"]
    ss = np.concatenate([np.linspace(-BASE_SINK, 0.80, RINGS - 6), 0.80 + 0.19 * np.sin(np.linspace(0.25, 1.0, 6) * np.pi / 2)])
    rings = []
    for s in ss:
        c, tg = at(min(s, 0.985))
        if s < 0 and tb.get("inward") is not None:
            c = c + tb["inward"] * (-s / BASE_SINK)
        u_ = lat - (lat @ tg) * tg; u_ /= np.linalg.norm(u_); w_ = np.cross(tg, u_)
        taper = 1.0 - 0.16 * max(s, 0.0)
        if s < 0:
            taper *= 1.0 - 0.45 * (-s / BASE_SINK) ** 1.5      # buried base narrows, never shows a flat end
        knuckle = 1.0 + 0.10 * np.exp(-((s - 0.02) / 0.08) ** 2)     # a little fullness at the knuckle
        tipround = np.sqrt(max(0.04, 1 - ((s - 0.80) / 0.20) ** 2)) if s > 0.80 else 1.0
        r = R0 * taper * knuckle * tipround
        ring = []
        for k in range(SEGS):
            a = 2 * np.pi * k / SEGS
            # slightly flattened toward the palm, like a real finger
            x = c + r * (np.cos(a) * u_ * 1.0 + np.sin(a) * w_ * 0.86)
            v = tb_m.verts.new(MWi @ V(x)); ring.append((v, s, a))
        rings.append(ring)
    cap_tip = tb_m.verts.new(MWi @ V(at(0.985)[0] + at(0.985)[1] * R0 * 0.12))
    cap_base = tb_m.verts.new(MWi @ V(at(-BASE_SINK)[0]))
    faces = []
    for i in range(len(rings) - 1):
        for k in range(SEGS):
            faces.append(tb_m.faces.new((rings[i][k][0], rings[i][(k + 1) % SEGS][0],
                                         rings[i + 1][(k + 1) % SEGS][0], rings[i + 1][k][0])))
    for k in range(SEGS):
        faces.append(tb_m.faces.new((rings[-1][k][0], rings[-1][(k + 1) % SEGS][0], cap_tip)))
        faces.append(tb_m.faces.new((rings[0][(k + 1) % SEGS][0], rings[0][k][0], cap_base)))
    info = {cap_tip: (1.0, 0.0), cap_base: (-BASE_SINK, 0.0)}
    for ring in rings:
        for v, s, a in ring:
            info[v] = (s, a)
    votes = [vert_mat.get(q[0], 0) for q in tb["samples"]]
    mi = max(set(votes), key=votes.count) if votes else 0
    skin_uv = None
    if MODE == "rebuild":
        # one even skin tone per rebuilt finger: the source point at the 70th brightness percentile
        lums = [(luminance(vert_mat.get(q[0], 0), vert_uv.get(q[0], (0.5, 0.5))), q[0]) for q in tb["samples"]]
        lums.sort()
        skin_uv = vert_uv.get(lums[int(len(lums) * 0.7)][1], (0.5, 0.5)) if lums else None
    for f in faces:
        f.material_index = mi; f.smooth = True
        if skin_uv is not None:
            for loop in f.loops:
                loop[uv].uv = skin_uv
            continue
        luv = []
        for loop in f.loops:
            s_, a_ = info[loop.vert]
            _, k, _ = kd.find((min(max(s_, 0.02), 1.0) * 3.0, np.cos(a_), np.sin(a_)))
            luv.append(vert_uv.get(tb["samples"][k][0], (0.5, 0.5)))
        if max(np.hypot(p[0] - q[0], p[1] - q[1]) for p in luv for q in luv) > 0.02:
            ss_ = np.mean([info[l.vert][0] for l in f.loops])
            aa = np.angle(np.mean([np.exp(1j * info[l.vert][1]) for l in f.loops]))
            _, k, _ = kd.find((min(max(ss_, 0.02), 1.0) * 3.0, np.cos(aa), np.sin(aa)))
            luv = [vert_uv.get(tb["samples"][k][0], (0.5, 0.5))] * len(luv)
        for loop, u2 in zip(f.loops, luv):
            loop[uv].uv = u2
    tb["info"] = info
    tb["tot"] = tot
    tb["cuts"] = np.cumsum([0] + seg) / tot
    cuts, gs = tb["cuts"], tb["groups"]
    for v, (s_, _) in info.items():
        w = v[dl]; w.clear()
        if s_ < 0.08:
            a = np.clip((s_ + BASE_SINK) / (0.08 + BASE_SINK), 0, 1)
            w[tgi[tb["hand"]]] = 1 - 0.6 * a; w[tgi[gs[0]]] = 0.6 * a; continue
        j = int(np.clip(np.searchsorted(cuts, s_) - 1, 0, len(gs) - 1))
        lo, hi = cuts[j], cuts[j + 1]; f_ = (s_ - lo) / max(hi - lo, 1e-6)
        if f_ > 0.8 and j + 1 < len(gs):
            a = (f_ - 0.8) / 0.4; w[tgi[gs[j]]] = 1 - a; w[tgi[gs[j + 1]]] = a
        elif f_ < 0.2 and j > 0:
            a = 0.5 + f_ / 0.4; w[tgi[gs[j]]] = a; w[tgi[gs[j - 1]]] = 1 - a
        else:
            w[tgi[gs[j]]] = 1.0
bmesh.ops.recalc_face_normals(tb_m, faces=tb_m.faces[:])
tb_m.to_mesh(tm); tb_m.free(); tm.update()

if MODE == "rebuild":
    # buried bases + capped knuckles: join the tubes as they are (no boolean on these open, messy hands)
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    tob.select_set(True); body.select_set(True)
    bpy.context.view_layer.objects.active = body
    bpy.ops.object.join()
    print(f"joined {len(tubes)} finger(s)")
    bpy.ops.export_scene.gltf(filepath=DST, export_format='GLB', export_animations=True, export_skins=True,
                              export_image_format='AUTO')
    print("exported", DST)
    raise SystemExit(0)

# ---------------------------------------------------------------- weld: union the tubes into the hand region
bpy.context.view_layer.objects.active = body
for o in bpy.context.view_layer.objects:
    o.select_set(False)
hand_names = set(HANDS)
for tb in tubes:
    hand_names |= set(tb["groups"])
for hand in HANDS:
    hand_names |= {b.name for b in arm.data.bones[hand].children_recursive}
body.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_mode(type='FACE')
bpy.ops.mesh.select_all(action='DESELECT')
bm = bmesh.from_edit_mesh(me)
dlb = bm.verts.layers.deform.active
gi_b = {g.name: g.index for g in body.vertex_groups}
hand_gi = {gi_b[n] for n in hand_names if n in gi_b}
for f in bm.faces:
    ok = True
    for v in f.verts:
        w = v[dlb]
        if not w or max(w.keys(), key=lambda k: w[k]) not in hand_gi:
            ok = False; break
    f.select_set(ok)
bm.select_flush_mode()
print('weld region faces:', sum(f.select for f in bm.faces), 'of', len(bm.faces))
bmesh.update_edit_mesh(me)
bpy.ops.mesh.separate(type='SELECTED')
bpy.ops.object.mode_set(mode='OBJECT')
region = next(o for o in bpy.context.selected_objects if o != body)
mod = region.modifiers.new("weld", 'BOOLEAN')
mod.operation = 'UNION'; mod.solver = 'EXACT'; mod.object = tob
try:
    mod.use_hole_tolerant = True
    mod.use_self = False
except AttributeError:
    pass
bpy.context.view_layer.objects.active = region
bpy.ops.object.modifier_apply(modifier=mod.name)
bpy.data.objects.remove(tob, do_unlink=True)

# weights on the welded region: tube surfaces follow their chains, the rest keeps its weights
rb = bmesh.new(); rb.from_mesh(region.data)
dlr = rb.verts.layers.deform.verify()
rgi = {g.name: g.index for g in region.vertex_groups}
RW = region.matrix_world
for v in rb.verts:
    x = A(RW @ v.co)
    best = None
    for tb in tubes:
        d, s, _ = poly_param(x, tb["path"])
        ext = (x - tb["path"][0]) @ ((tb["path"][1] - tb["path"][0]) / np.linalg.norm(tb["path"][1] - tb["path"][0]))
        if d < tb["radius"] * 1.6 and ext > -0.008:
            if best is None or d < best[0]:
                best = (d, s, tb)
    if best is None:
        continue
    _, s, tb = best
    w = v[dlr]; w.clear()
    cuts, gs = tb["cuts"], tb["groups"]
    if s < 0.08:
        a = s / 0.08; w[rgi[tb["hand"]]] = 1 - 0.6 * a; w[rgi[gs[0]]] = 0.6 * a; continue
    j = int(np.clip(np.searchsorted(cuts, s) - 1, 0, len(gs) - 1))
    lo, hi = cuts[j], cuts[j + 1]; f_ = (s - lo) / max(hi - lo, 1e-6)
    if f_ > 0.8 and j + 1 < len(gs):
        a = (f_ - 0.8) / 0.4; w[rgi[gs[j]]] = 1 - a; w[rgi[gs[j + 1]]] = a
    elif f_ < 0.2 and j > 0:
        a = 0.5 + f_ / 0.4; w[rgi[gs[j]]] = a; w[rgi[gs[j - 1]]] = 1 - a
    else:
        w[rgi[gs[j]]] = 1.0
# safety: a vertex weighted to a bone on the other side of the body copies the nearest correct vertex
bone_x = {b.name: (AM @ b.head_local).x for b in arm.data.bones}
rnames = {g.index: g.name for g in region.vertex_groups}
good, bad = [], []
for v in rb.verts:
    x = (RW @ v.co).x
    w = v[dlr]
    wrong = any(abs(bone_x.get(rnames[g], 0.0)) > 0.2 and np.sign(bone_x[rnames[g]]) != np.sign(x) and val > 0.01
                for g, val in w.items())
    (bad if wrong else good).append(v)
if bad:
    kd2 = kdtree.KDTree(len(good))
    for i, v in enumerate(good):
        kd2.insert(RW @ v.co, i)
    kd2.balance()
    for v in bad:
        _, i, _ = kd2.find(RW @ v.co)
        src = good[i][dlr]; w = v[dlr]; w.clear()
        for g, val in src.items():
            w[g] = val
print(f"weld weights: fixed {len(bad)} vertices tied to the other hand")
# smooth the weld: relax vertices near each finger base (shape), then even out the weights there
near = []
for v in rb.verts:
    if v.is_boundary:          # the cut the region was separated along must stay put to be stitched back
        continue
    if any(e.is_boundary for e in v.link_edges):
        continue
    x = A(RW @ v.co)
    for tb in tubes:
        if np.linalg.norm(x - tb["path"][0]) < tb["radius"] * 2.2:
            near.append(v); break
for _ in range(SEAM_SMOOTH):
    bmesh.ops.smooth_vert(rb, verts=near, factor=0.5, use_axis_x=True, use_axis_y=True, use_axis_z=True)
rb.to_mesh(region.data); rb.free(); region.data.update()
bpy.ops.object.mode_set(mode='OBJECT')
for o in bpy.context.view_layer.objects:
    o.select_set(False)
region.select_set(True)
bpy.context.view_layer.objects.active = region
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='DESELECT')
bpy.ops.object.mode_set(mode='OBJECT')
idx = {v.index for v in near} if False else None
bpy.ops.object.mode_set(mode='OBJECT')

# join the region back and stitch the cut it was separated along
for o in bpy.context.view_layer.objects:
    o.select_set(False)
region.select_set(True); body.select_set(True)
bpy.context.view_layer.objects.active = body
bpy.ops.object.join()
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.mesh.remove_doubles(threshold=1e-5)
bpy.ops.object.mode_set(mode='OBJECT')
print(f"welded {len(tubes)} finger(s)")
bpy.ops.export_scene.gltf(filepath=DST, export_format='GLB', export_animations=True, export_skins=True,
                          export_image_format='AUTO')
print("exported", DST)
