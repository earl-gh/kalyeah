# A building rebuilt with a real roof (owner, 2026-11-10: "realistic ... not planks as roof with gaps"): the owner's model welded and cleaned,
# its floating shards and its old roof covering and loose framing taken off (config "drop_boxes", by their boxes), and a roof built the way
# a Philippine house or store is roofed: beams on the walls and posts, a ridge board, rafters every ~11 cm, fascia and barge boards, plywood
# in the gable ends, and continuous corrugated GI sheets with a ridge cap. The new parts get their own column of the picture (their UVs are
# stored divided by SHRINK, so the uv_patch step that shrinks the old layout puts them in place) and are listed in regions.json for painting.
# usage: blender python roof_build.py -- src.glb config.json picture.png out.glb regions_out.json
import bpy, bmesh, sys, math, json
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:]
src, cfg_path, tex, out, regions_out = args[:5]
cfg = json.load(open(cfg_path))
SHRINK = cfg.get("shrink", 0.78)
COL_U0, COL_U1, COL_V0, COL_V1 = 0.79, 0.995, 0.005, 0.84      # the new parts' column of the picture (final UVs)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
ob = [o for o in bpy.context.scene.objects if o.type == "MESH"][0]; me = ob.data
bm = bmesh.new(); bm.from_mesh(me)
bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
bmesh.ops.dissolve_degenerate(bm, edges=bm.edges, dist=1e-6)
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")

# --- drop pieces by their boxes (glTF x, y, z is Blender x, -z, y)
def g2b(p): return Vector((p[0], -p[2], p[1]))
boxes = [((b[0][0], -b[1][2], b[0][1]), (b[1][0], -b[0][2], b[1][1])) for b in cfg.get("drop_boxes", [])]
bm.verts.ensure_lookup_table(); seen = set(); drop = []
for v in bm.verts:
	if v.index in seen: continue
	comp = []; stack = [v]
	while stack:
		x = stack.pop()
		if x.index in seen: continue
		seen.add(x.index); comp.append(x); stack.extend(e.other_vert(x) for e in x.link_edges)
	lo = [min(c.co[i] for c in comp) for i in range(3)]; hi = [max(c.co[i] for c in comp) for i in range(3)]
	for blo, bhi in boxes:
		if all(abs(lo[i] - blo[i]) < 0.002 and abs(hi[i] - bhi[i]) < 0.002 for i in range(3)):
			drop.extend(comp); break
bmesh.ops.delete(bm, geom=drop, context="VERTS")
old_tris = sum(len(f.verts) - 2 for f in bm.faces)
for e in bm.edges: e.smooth = not (len(e.link_faces) == 2 and e.calc_face_angle(0) > math.radians(30))
for f in bm.faces: f.smooth = True
uvl = bm.loops.layers.uv.active

# --- the new parts' picture column: rectangles handed out top to bottom
regions = []
cursor = [COL_V0]
def rect(height, name):
	v0 = cursor[0]; v1 = v0 + height; cursor[0] = v1 + 0.004
	if v1 > COL_V1: print("WARNING: the new parts' column is full at", name)
	regions.append({"material": name, "u0": COL_U0, "v0": v0, "u1": COL_U1, "v1": v1})
	return (COL_U0, v0, COL_U1, v1)
SWATCH = {}
def swatch(name, height=0.03):
	if name not in SWATCH: SWATCH[name] = rect(height, name)
	return SWATCH[name]

def add_face(points, uvs, smooth=True):
	vs = [bm.verts.new(g2b(p)) for p in points]
	f = bm.faces.new(vs); f.smooth = smooth
	for l, uv in zip(f.loops, uvs):
		l[uvl].uv = (uv[0] / SHRINK, 1.0 - uv[1] / SHRINK)        # stored for the shrink step; Blender's v runs up
	return f

def box(c, half, axes, material):
	"""A box (centre c, half sizes along its own axes a0, a1, a2 in glTF space), its faces all on one swatch of `material`."""
	u0, v0, u1, v1 = swatch(material)
	corners = []
	for sx in (-1, 1):
		for sy in (-1, 1):
			for sz in (-1, 1):
				corners.append([c[i] + sx * half[0] * axes[0][i] + sy * half[1] * axes[1][i] + sz * half[2] * axes[2][i] for i in range(3)])
	quads = [(0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)]
	uv = [(u0, v0), (u1, v0), (u1, v1), (u0, v1)]
	faces = [add_face([corners[i] for i in q], uv, smooth=False) for q in quads]
	centre = g2b(c)
	for f in faces:
		f.normal_update()
		if f.normal.dot(f.calc_center_median() - centre) < 0: f.normal_flip()
	return

def norm(v):
	l = math.sqrt(sum(x * x for x in v)); return [x / l for x in v]
def sub(a, b): return [a[i] - b[i] for i in range(3)]
def addv(a, b, s=1.0): return [a[i] + b[i] * s for i in range(3)]
def cross(a, b): return [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]]

PITCH = cfg.get("corrugation_pitch", 0.025); AMP = cfg.get("corrugation_amp", 0.0045)

def sheet(x0, x1, top, bottom, name):
	"""Corrugated sheet from the `top` edge (z, y) down to the `bottom` edge (z, y), across x0..x1; waves run down the slope."""
	n_wave = max(4, int(round((x1 - x0) / PITCH))); cols = n_wave * 4
	length = math.hypot(top[0] - bottom[0], top[1] - bottom[1])
	rows = 1   # the waves only change across the sheet: one row down the slope
	up = norm(cross([1, 0, 0], [0, top[1] - bottom[1], top[0] - bottom[0]]))   # the sheet's outward normal (glTF), pointing up
	if up[1] < 0: up = [-x for x in up]
	u0, v0, u1, v1 = rect(min(0.35, max(0.05, (COL_U1 - COL_U0) * length / (x1 - x0))), name)
	grid = []
	for r in range(rows + 1):
		t = r / rows
		z = top[0] + (bottom[0] - top[0]) * t; y = top[1] + (bottom[1] - top[1]) * t
		row = []
		for c in range(cols + 1):
			s = c / cols; x = x0 + (x1 - x0) * s
			w = AMP * math.sin(2 * math.pi * (x - x0) / PITCH)
			p = [x + up[0] * w, y + up[1] * w, z + up[2] * w]
			row.append((bm.verts.new(g2b(p)), (u0 + (u1 - u0) * s, v0 + (v1 - v0) * t)))
		grid.append(row)
	for r in range(rows):
		for c in range(cols):
			q = [grid[r][c], grid[r][c + 1], grid[r + 1][c + 1], grid[r + 1][c]]
			f = bm.faces.new([q[i][0] for i in range(4)]); f.smooth = True
			for l, (_, uv) in zip(f.loops, q):
				l[uvl].uv = (uv[0] / SHRINK, 1.0 - uv[1] / SHRINK)
			f.normal_update()
			if f.normal.dot(g2b(up)) < 0: f.normal_flip()

def plane_roof(x0, x1, top, bottom, eave_over, side_over, name, ridge=False):
	"""One roof slope: rafters, sheet, fascia; `top`/`bottom` are where the slope rests (ridge or high wall, and the low wall)."""
	dz, dy = bottom[0] - top[0], bottom[1] - top[1]; L = math.hypot(dz, dy); dz, dy = dz / L, dy / L
	low = (bottom[0] + dz * eave_over, bottom[1] + dy * eave_over)                    # the eave, past the wall
	high = top if ridge else (top[0] - dz * eave_over, top[1] - dy * eave_over)
	xa, xb = x0 - side_over, x1 + side_over
	normal = norm(cross([1, 0, 0], [0, dy, dz])); normal = normal if normal[1] > 0 else [-v for v in normal]
	RD = 0.022; RW = 0.012
	# rafters under the sheet, every ~11 cm, from the high end to the eave
	n = max(2, int(round((x1 - x0) / 0.11)))
	for i in range(n + 1):
		x = x0 + (x1 - x0) * i / n
		mid = [x, (high[1] + low[1]) / 2, (high[0] + low[0]) / 2]
		mid = addv(mid, normal, -(AMP + RD / 2))
		Lr = math.hypot(low[0] - high[0], low[1] - high[1])
		box(mid, (RW / 2, Lr / 2, RD / 2), ([1, 0, 0], [0, (low[1] - high[1]) / Lr, (low[0] - high[0]) / Lr], normal), "lumber")
	# the corrugated sheet
	sheet(xa, xb, high, low, name)
	# fascia along the eave, barge boards up the two ends
	fa = addv([0, low[1], low[0]], normal, -0.012)
	box([(xa + xb) / 2, fa[1], fa[2]], ((xb - xa) / 2, 0.004, 0.016), ([1, 0, 0], [0, 0, 1], [0, 1, 0]), "fascia")
	Lb = math.hypot(low[0] - high[0], low[1] - high[1])
	for x in (xa, xb):
		c = addv([x, (high[1] + low[1]) / 2, (high[0] + low[0]) / 2], normal, -0.01)
		box(c, (0.004, Lb / 2, 0.016), ([1, 0, 0], [0, (low[1] - high[1]) / Lb, (low[0] - high[0]) / Lb], normal), "fascia")
	return low

for spec in cfg["roofs"]:
	x0, x1 = spec["x"]; ov = spec.get("eave_over", 0.05); so = spec.get("side_over", 0.03)
	if spec["type"] == "gable":
		rz, ry = spec["ridge"]; front, back = spec["eaves"]
		for i, e in enumerate((front, back)):
			plane_roof(x0, x1, (rz, ry), (e[0], e[1]), ov, so, "sheet", ridge=True)
		# ridge cap over the meeting of the two slopes, a ridge board under it
		box([(x0 + x1) / 2, ry + AMP + 0.004, rz], ((x1 - x0) / 2 + so, 0.004, 0.03), ([1, 0, 0], [0, 1, 0], [0, 0, 1]), "cap")
		box([(x0 + x1) / 2, ry - 0.03, rz], ((x1 - x0) / 2, 0.022, 0.008), ([1, 0, 0], [0, 1, 0], [0, 0, 1]), "lumber")
		# beams on the walls under each eave, plywood closing the gable ends
		for e in (front, back):
			box([(x0 + x1) / 2, e[1] - 0.03, e[0]], ((x1 - x0) / 2, 0.016, 0.014), ([1, 0, 0], [0, 1, 0], [0, 0, 1]), "lumber")
		if spec.get("gable_ends", True):
			u0, v0, u1, v1 = swatch("plywood", 0.06)
			for x in (x0 + 0.004, x1 - 0.004):
				pts = [[x, front[1] - 0.045, front[0]], [x, back[1] - 0.045, back[0]], [x, ry - 0.01, rz]]
				f = add_face(pts, [(u0, v1), (u1, v1), ((u0 + u1) / 2, v0)], smooth=False)
				f.normal_update()
				if f.normal.dot(Vector((1.0 if x > 0 else -1.0, 0.0, 0.0))) < 0: f.normal_flip()
	else:   # shed: one slope from the high wall to the low wall
		hi_, lo_ = spec["high"], spec["low"]
		plane_roof(x0, x1, (hi_[0], hi_[1]), (lo_[0], lo_[1]), ov, so, "sheet", ridge=False)
		for e in (hi_, lo_):
			box([(x0 + x1) / 2, e[1] - 0.03, e[0]], ((x1 - x0) / 2, 0.016, 0.014), ([1, 0, 0], [0, 1, 0], [0, 0, 1]), "lumber")
	for post in spec.get("posts", []):        # [x, z, y_top]: a coco lumber post from the ground to a beam
		box([post[0], post[2] / 2, post[1]], (0.014, post[2] / 2, 0.014), ([1, 0, 0], [0, 1, 0], [0, 0, 1]), "lumber")

# straight hollow-block walls where the model had stacked, uneven boards; each face its own part of the picture (blocks are painted per place)
for w in cfg.get("walls", []):
	lo_, hi_ = w["box"]
	c = [(lo_[i] + hi_[i]) / 2 for i in range(3)]; hs = [(hi_[i] - lo_[i]) / 2 for i in range(3)]
	corners = []
	for sx in (-1, 1):
		for sy in (-1, 1):
			for sz in (-1, 1):
				corners.append([c[0] + sx * hs[0], c[1] + sy * hs[1], c[2] + sz * hs[2]])
	quads = [((0, 1, 3, 2), (2, 1)), ((4, 6, 7, 5), (2, 1)), ((0, 4, 5, 1), (0, 2)), ((2, 3, 7, 6), (0, 2)), ((0, 2, 6, 4), (0, 1)), ((1, 5, 7, 3), (0, 1))]
	for q, (a0, a1) in quads:
		wa, wb = 2 * hs[a0], 2 * hs[a1]
		u0, v0, u1, v1 = rect(min(0.2, max(0.006, 0.5 * (COL_U1 - COL_U0) * wb / max(wa, 1e-6))), "chb")
		f = add_face([corners[i] for i in q], [(u0, v0), (u1, v0), (u1, v1), (u0, v1)], smooth=False)
		f.normal_update()
		if f.normal.dot(f.calc_center_median() - g2b(c)) < 0: f.normal_flip()

bm.to_mesh(me); bm.free(); me.update()
print("old kept", old_tris, "tris; now", sum(len(p.vertices) - 2 for p in me.polygons))
mat = ob.material_slots[0].material
node = [n for n in mat.node_tree.nodes if n.type == "TEX_IMAGE"][0]
old = node.image; name = old.name; new = bpy.data.images.load(tex); new.pack(); old.name = name + "_old"; new.name = name; node.image = new
bpy.ops.export_scene.gltf(filepath=out, export_format="GLB", export_normals=True, export_texcoords=True, export_materials="EXPORT",
	export_image_format="JPEG", export_jpeg_quality=92, export_yup=True, export_tangents=False, export_attributes=False,
	export_cameras=False, export_lights=False, export_animations=False)
json.dump(regions, open(regions_out, "w"), indent=1)
