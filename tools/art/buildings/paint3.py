# Paints a rebuilt building from its materials (owner, 2026-11-10: "a new one", not the old picture cleaned): every texel takes the look of the
# material its piece is made of (materials.py), from where it is on the model, in the building's own palette. The old picture is read for
# one thing only: where a window was painted on a wall (it becomes a jalousie window). The sign is the building's own, cleaned.
# usage: paint3.py <dir> final.glb geo.glb features.json mat.json regions.json config.json band.json sign.png out.png
import sys, io, json, colorsys, numpy as np
sys.path.insert(0, sys.argv[1]); import glbmesh
from PIL import Image, ImageFilter

P, _N, UV, I, _img, _j = glbmesh.load(sys.argv[2]); T = I.reshape(-1, 3)
GP, _gn, GUV, GI_, GIMG, _ = glbmesh.load(sys.argv[3]); GT = GI_.reshape(-1, 3)
F = json.load(open(sys.argv[4])); MAT = {int(k): v for k, v in json.load(open(sys.argv[5])).items()}
REG = json.load(open(sys.argv[6])); CFG = json.load(open(sys.argv[7])); BAND = json.load(open(sys.argv[8]))
SIGN = np.asarray(Image.open(sys.argv[9]).convert("RGB"), float) if sys.argv[9] != "-" else None
OUT = sys.argv[10]; SIZE = 2048
OLD = np.asarray(Image.open(io.BytesIO(GIMG)).convert("RGB"), float)
H = max(f["hi"][1] for f in F)
PALCFG = CFG["palette"]


def hexc(h): return np.array([int(h[i:i + 2], 16) for i in (1, 3, 5)], float)


# ---------------------------------------------------------------- pieces of the final model, and what each is made of
key = np.round(P / 1e-4).astype(np.int64); _, wid = np.unique(key, axis=0, return_inverse=True); wid = wid.ravel()
par = np.arange(wid.max() + 1)
def root(x):
	while par[x] != x: par[x] = par[par[x]]; x = par[x]
	return x
for a_, b_, c_ in wid[T]:
	ra = root(a_); par[root(b_)] = ra; par[root(c_)] = ra
comp = np.array([root(w) for w in wid[T[:, 0]]])
ids, piece = np.unique(comp, return_inverse=True)
n_pieces = len(ids)
lo = np.full((n_pieces, 3), 1e9); hi = np.full((n_pieces, 3), -1e9)
for i in range(3):
	np.minimum.at(lo[:, i], piece, P[T, i].min(1)); np.maximum.at(hi[:, i], piece, P[T, i].max(1))
feat_by_box = [(np.array(f["lo"]), np.array(f["hi"]), f) for f in F]
mat = []; feat = []
for k in range(n_pieces):
	m = None; fk = None
	for flo, fhi, f in feat_by_box:
		if np.abs(lo[k] - flo).max() < 0.002 and np.abs(hi[k] - fhi).max() < 0.002:
			m = MAT.get(f["k"]); fk = f; break
	if m is None:   # a new part: its picture region says what it is
		t = np.nonzero(piece == k)[0][0]; u, v = UV[T[t]].mean(0)
		for r in REG:
			if r["u0"] - 1e-4 <= u <= r["u1"] + 1e-4 and r["v0"] - 1e-4 <= v <= r["v1"] + 1e-4:
				m = {"sheet": "gi_roof", "lumber": "lumber", "fascia": "fascia", "cap": "gi_cap", "plywood": "plywood_gable", "chb": "chb"}[r["material"]]; break
	mat.append(m or "lumber"); feat.append(fk)

# old picture through the old UVs, for the walls only (to find painted windows): final triangle -> the same triangle of the welded model
gkey = {}
gc = np.round(GP[GT].mean(1) / 1e-4).astype(np.int64)
for t, c in enumerate(map(tuple, gc)): gkey[c] = t
fc = np.round(P[T].mean(1) / 1e-4).astype(np.int64)

rng = np.random.default_rng(5)
jit = 1.0 + rng.uniform(-0.05, 0.05, n_pieces)
pick = rng.integers(0, 1 << 20, n_pieces)

# ---------------------------------------------------------------- raster in the final layout
face_n = np.cross(P[T[:, 1]] - P[T[:, 0]], P[T[:, 2]] - P[T[:, 0]])
face_n /= np.maximum(np.linalg.norm(face_n, axis=1), 1e-12)[:, None]
X, Y, K, POS, NRM, OLDC = [], [], [], [], [], []
oh, ow = OLD.shape[:2]
for t in range(len(T)):
	k = int(piece[t])
	uv = UV[T[t]] * SIZE - 0.5
	x0, y0 = np.floor(uv.min(0)).astype(int) - 1; x1, y1 = np.ceil(uv.max(0)).astype(int) + 1
	x0, y0 = max(x0, 0), max(y0, 0); x1, y1 = min(x1, SIZE - 1), min(y1, SIZE - 1)
	if x1 < x0 or y1 < y0: continue
	gx, gy = np.meshgrid(np.arange(x0, x1 + 1), np.arange(y0, y1 + 1))
	a, b, c = uv
	den = (b[1] - c[1]) * (a[0] - c[0]) + (c[0] - b[0]) * (a[1] - c[1])
	if abs(den) < 1e-12: continue
	w0 = ((b[1] - c[1]) * (gx - c[0]) + (c[0] - b[0]) * (gy - c[1])) / den
	w1 = ((c[1] - a[1]) * (gx - c[0]) + (a[0] - c[0]) * (gy - c[1])) / den
	w2 = 1 - w0 - w1
	eps = 0.7 / max(np.abs(uv.max(0) - uv.min(0)).max(), 1.0)
	m = (w0 > -eps) & (w1 > -eps) & (w2 > -eps)
	if not m.any(): continue
	W = np.clip(np.stack([w0[m], w1[m], w2[m]], 1), 0, None); W /= W.sum(1, keepdims=True)
	pos = W @ P[T[t]]
	X.append(gx[m]); Y.append(gy[m]); K.append(np.full(m.sum(), k)); POS.append(pos); NRM.append(np.tile(face_n[t], (m.sum(), 1)))
	old = np.full((m.sum(), 3), -1.0)
	if mat[k] in ("chb", "shell", "plywood"):
		g = gkey.get(tuple(fc[t]))
		if g is not None:
			# the old triangle's corners in the order of this one's
			order = [int(np.argmin(np.linalg.norm(GP[GT[g]] - P[v], axis=1))) for v in T[t]]
			ouv = W @ GUV[GT[g]][order]
			old = OLD[np.clip((ouv[:, 1] * oh).astype(int), 0, oh - 1), np.clip((ouv[:, 0] * ow).astype(int), 0, ow - 1)]
	OLDC.append(old)
X = np.concatenate(X); Y = np.concatenate(Y); K = np.concatenate(K); POS = np.concatenate(POS); NRM = np.concatenate(NRM); OLDC = np.concatenate(OLDC)
kk = Y * SIZE + X; _, last = np.unique(kk[::-1], return_index=True); keep = len(kk) - 1 - last
X, Y, K, POS, NRM, OLDC = X[keep], Y[keep], K[keep], POS[keep], NRM[keep], OLDC[keep]


# ---------------------------------------------------------------- the materials
def hash3(q): return np.modf(np.abs(np.sin(q @ np.array([12.9898, 78.233, 37.719])) * 43758.5453))[0]
def speckle(pos, cell=0.0016): return hash3(np.floor(pos / cell))
def soft_noise(a, b, seed):
	r = np.random.default_rng(seed); acc = np.zeros_like(a)
	for f in (17.0, 31.0, 53.0):
		p = r.uniform(0, 6.3, 3)
		acc += np.sin(a * f + p[0]) * np.sin(b * f * 1.3 + p[1]) + 0.5 * np.sin((a + b) * f * 0.7 + p[2])
	return 0.5 + acc / 9.0
def edge_shade(k, pos, nrm, width=0.005, depth=0.18):
	d = np.full(len(pos), 1e9); order = np.argsort(-np.abs(nrm), axis=1)
	for col in (1, 2):
		ax = order[:, col]; p = pos[np.arange(len(pos)), ax]
		d = np.minimum(d, np.minimum(p - lo[k][ax], hi[k][ax] - p))
	return (1.0 - depth) + depth * np.clip(d / width, 0, 1)
def grain(a, seed): return 0.5 * np.sin(a * 90 + seed) + 0.3 * np.sin(a * 210 + seed * 1.7) + 0.2 * np.sin(a * 470 + seed * 2.3)
def tile(c, n): return np.tile(np.asarray(c, float), (n, 1))

BH = 0.046; BW = 2 * BH; JOINT = 0.0038
WALL = None if PALCFG["wall"] == "bare" else hexc(PALCFG["wall"])
ROOF = hexc("#848A90") if PALCFG["roof"] == "galv" else hexc(PALCFG["roof"])
TRIM = hexc(PALCFG["trim"]); PLY = hexc(PALCFG["plywood"])
LUMBER = hexc("#6B4630"); CONCRETE = hexc("#A19D95"); RUST = hexc("#7A4326")
PLASTIC = [hexc(h) for h in ("#C8423A", "#E08A3A", "#E8C048", "#5BA05A", "#3E9E9A", "#3A6FB8", "#7A5AA8", "#D86A98")]
PLASTIC_HUES = [0, 28, 48, 110, 178, 215, 265, 330]


def blocks(pos, h, v, seed, top_y, base):
	row = np.floor(v / BH); off = (row % 2) * BW / 2 + (seed % 7) * 0.013
	col = np.floor((h + off) / BW); lu = (h + off) - col * BW; lv = v - row * BH
	joint = (lu < JOINT) | (lv < JOINT)
	tone = 1.0 + (hash3(np.stack([row, col, np.full_like(row, seed % 97)], 1)) - 0.5) * (0.16 if WALL is None else 0.06)
	c = base[None, :] * tone[:, None]
	c *= (0.93 + 0.07 * np.clip(lv / (BH * 0.25), 0, 1))[:, None]
	s = speckle(pos); c *= (0.95 + 0.08 * s)[:, None]
	if WALL is None: c[s > 0.97] *= 0.72
	c[joint] = base * (0.78 if WALL is None else 0.88) * (0.94 + 0.1 * s[joint, None])
	streak = hash3(np.stack([col, np.zeros_like(col), np.full_like(col, 3.0)], 1))
	c[(streak > 0.72) & (v > top_y - 0.05 - streak * 0.25)] *= 0.9
	foot = np.clip(1 - v / 0.07, 0, 1)
	return c * (1 - 0.2 * foot[:, None]) + hexc("#6B5A45") * 0.12 * foot[:, None]


def wall(idx, k):
	pos, nrm = POS[idx], NRM[idx]; col = np.zeros((len(idx), 3))
	top = np.abs(nrm[:, 1]) > 0.7
	col[top] = (WALL if WALL is not None else hexc("#8E8C85")) * (0.95 + 0.08 * speckle(pos[top]))[:, None]
	side = ~top
	along = np.where(np.abs(nrm[side, 0]) > np.abs(nrm[side, 2]), pos[side, 2], pos[side, 0])
	col[side] = blocks(pos[side], along, pos[side, 1], int(pick[k] % 5), hi[k][1], WALL if WALL is not None else hexc("#85837C"))
	# a window painted on the wall in the old picture becomes a jalousie window: glass louvres in a painted frame
	old = OLDC[idx]; have = old[:, 0] >= 0
	if CFG.get("windows") and have.sum() > 200:
		base = np.median(old[have], 0)
		win = have & (np.linalg.norm(old - base, axis=1) > 95)
		xs, ys = X[idx], Y[idx]; bx0, by0 = xs.min(), ys.min()
		m = np.zeros((ys.max() - by0 + 1, xs.max() - bx0 + 1), np.uint8); m[ys - by0, xs - bx0] = np.where(win, 255, 0)
		m = np.asarray(Image.fromarray(m).filter(ImageFilter.MedianFilter(9)).filter(ImageFilter.GaussianBlur(2.0)), float)[ys - by0, xs - bx0] / 255
		# one upright rectangle round what was painted there (a real window is square-cornered), framed
		sel = side & (m > 0.5)
		glass = np.zeros(len(idx), bool); frame = np.zeros(len(idx), bool)
		if sel.sum() > 80:
			al = np.where(np.abs(nrm[:, 0]) > np.abs(nrm[:, 2]), pos[:, 2], pos[:, 0])
			a0, a1 = np.percentile(al[sel], [4, 96]); y0, y1 = np.percentile(pos[sel, 1], [4, 96])
			inside = side & (al > a0) & (al < a1) & (pos[:, 1] > y0) & (pos[:, 1] < y1)
			fw = 0.008
			glass = inside & (al > a0 + fw) & (al < a1 - fw) & (pos[:, 1] > y0 + fw) & (pos[:, 1] < y1 - fw)
			frame = inside & ~glass
		slat = (pos[:, 1] / 0.012) % 1.0
		g = np.where((slat < 0.72)[:, None], hexc("#9DB3BC")[None] * (0.9 + 0.2 * (1 - slat))[:, None], hexc("#4C5A62")[None])
		col[glass] = g[glass]; col[frame] = TRIM * 0.95
	return col


def roof_sheet(idx, k, base):
	pos, nrm = POS[idx], NRM[idx]
	col = tile(base * jit[k], len(idx))
	ha = np.cross(nrm, np.array([0.0, 1.0, 0.0])); hn = np.linalg.norm(ha, axis=1)
	ha = np.where(hn[:, None] > 0.2, ha / np.maximum(hn, 1e-9)[:, None], np.array([1.0, 0.0, 0.0]))
	hc = (pos * ha).sum(1)
	col *= (1.0 + 0.04 * np.sin(hc * 2 * np.pi / 0.025))[:, None]               # the waves are in the mesh; only a hint here
	col[(hc % 0.18) < 0.006] *= 0.82                                             # the lap where one sheet overlaps the next
	slope_t = np.clip((pos[:, 1] - lo[k][1]) / max(hi[k][1] - lo[k][1], 1e-6), 0, 1)
	streak = hash3(np.stack([np.floor(hc / 0.025), np.zeros_like(hc), np.full_like(hc, 7.0)], 1))
	# rust: a few soft runs from the lower edge (where water sits) and a few soft patches; no scratches
	run = (streak > 0.86) * np.clip(1 - slope_t / 0.35, 0, 1)
	rust = np.clip(0.45 * run + 0.3 * np.clip((soft_noise(pos[:, 0] * 4, pos[:, 2] * 4, int(pick[k] % 1000)) - 0.66) / 0.2, 0, 1), 0, 0.5)
	if PALCFG["roof"] != "galv": rust *= 0.6
	rust *= (0.8 + 0.4 * speckle(pos, 0.002))
	col = col * (1 - rust[:, None]) + RUST * rust[:, None]
	return col


def lumber(idx, k, base=LUMBER):
	pos, nrm = POS[idx], NRM[idx]
	axis = int(np.argmax(hi[k] - lo[k])); across = [a for a in range(3) if a != axis]
	col = tile(base * jit[k], len(idx))
	col *= (1.0 + 0.08 * grain(pos[:, across[0]] * 1.3 + pos[:, across[1]] + pos[:, axis] * 0.08, k))[:, None]
	if pick[k] % 3 == 0:
		kt = lo[k][axis] + (hi[k][axis] - lo[k][axis]) * ((pick[k] >> 4) % 100 / 100.0 * 0.6 + 0.2)
		mid = (lo[k][across[0]] + hi[k][across[0]] + lo[k][across[1]] + hi[k][across[1]]) / 2
		dist = np.hypot((pos[:, axis] - kt) / 0.006, (pos[:, across[0]] + pos[:, across[1]] - mid) / 0.0035)
		col *= (1 - 0.35 * np.clip(1 - dist, 0, 1))[:, None]
	return col * edge_shade(k, pos, nrm)[:, None]


def hazard(pos):
	stripe = ((pos[:, 0] + pos[:, 1] + pos[:, 2]) / 0.03) % 1.0 < 0.5
	return np.where(stripe[:, None], hexc("#E2B030")[None], hexc("#262422")[None]) * (0.95 + 0.08 * speckle(pos))[:, None]


def plastic_colour(k):
	f = feat[k]
	if f is None: return PLASTIC[pick[k] % len(PLASTIC)]
	if f["s"] < 0.2:
		return hexc("#E6E2D8") if f["l"] > 0.55 else (hexc("#8C8F92") if f["l"] > 0.3 else hexc("#34383C"))
	d = [min(abs(f["h"] - h), 360 - abs(f["h"] - h)) for h in PLASTIC_HUES]
	return PLASTIC[int(np.argmin(d))]


img = np.zeros((SIZE, SIZE, 3)); filled = np.zeros((SIZE, SIZE), bool)
order = np.argsort(K, kind="stable"); bounds = np.searchsorted(K[order], np.arange(n_pieces + 1))
for k in range(n_pieces):
	idx = order[bounds[k]:bounds[k + 1]]
	if len(idx) == 0: continue
	m = mat[k]; pos, nrm = POS[idx], NRM[idx]; n = len(idx)
	if m == "chb":
		col = wall(idx, k)
	elif m == "shell":
		col = tile(CONCRETE, n) * (0.93 + 0.1 * speckle(pos, 0.002))[:, None]
		up = np.abs(nrm[:, 1]) < 0.35
		if up.sum(): col[up] = wall(idx[up], k)
		high = (nrm[:, 1] > 0.35) & (pos[:, 1] > 0.6 * H)
		if high.sum(): col[high] = roof_sheet(idx[high], k, ROOF)
	elif m == "gi_roof":
		col = roof_sheet(idx, k, ROOF)
	elif m == "gi_cap":
		col = tile(ROOF * 0.88, n) * (0.95 + 0.08 * speckle(pos, 0.002))[:, None]
	elif m == "fascia":
		col = tile(TRIM, n) * (0.95 + 0.06 * speckle(pos, 0.003))[:, None] * edge_shade(k, pos, nrm, 0.003, 0.2)[:, None]
	elif m in ("lumber", "straw"):
		col = lumber(idx, k, LUMBER if m == "lumber" else hexc("#D2AE62"))
	elif m == "lumber_band":
		col = lumber(idx, k)
		band = pos[:, 1] < lo[k][1] + 0.18 * (hi[k][1] - lo[k][1])
		col[band] = hazard(pos[band])
	elif m in ("plywood", "plywood_gable"):
		base = PLY if m == "plywood" else (WALL if WALL is not None else PLY)
		col = tile(base * jit[k], n)
		col *= (1.0 + 0.04 * grain(pos[:, 1] * 0.4 + pos[:, 0] * 0.2 + pos[:, 2] * 0.2, k * 3))[:, None]
		seam = ((pos[:, 0] + pos[:, 2]) % 0.27) < 0.003
		col[seam] *= 0.8
		col *= edge_shade(k, pos, nrm, 0.004, 0.18)[:, None]
	elif m == "concrete":
		col = tile(CONCRETE * jit[k], n) * (0.93 + 0.1 * speckle(pos, 0.002))[:, None]
		col *= (0.92 + 0.08 * soft_noise(pos[:, 0] * 4, pos[:, 2] * 4, int(pick[k] % 50)))[:, None]
		col *= edge_shade(k, pos, nrm, 0.006, 0.22)[:, None]
	elif m == "stone":
		col = tile(hexc("#8D857A") * jit[k], n) * (0.85 + 0.25 * speckle(pos, 0.004))[:, None] * edge_shade(k, pos, nrm, 0.01, 0.3)[:, None]
	elif m == "hazard":
		col = hazard(pos) * edge_shade(k, pos, nrm, 0.004, 0.2)[:, None]
	elif m == "rubber":
		col = tile(hexc("#2C2B2A"), n) * (0.9 + 0.2 * speckle(pos, 0.002))[:, None]
		col *= (1.0 + 0.25 * (1 - edge_shade(k, pos, nrm, 0.004, 1.0)))[:, None]
	elif m == "steel":
		col = tile(hexc("#7D838A") * jit[k], n) * (0.92 + 0.12 * speckle(pos, 0.002))[:, None]
		col *= (1.0 + 0.2 * (1 - edge_shade(k, pos, nrm, 0.003, 1.0)))[:, None]
	elif m == "plant":
		f = feat[k]; g = [hexc("#4E9A3E"), hexc("#3F7F36"), hexc("#6DAA45")][pick[k] % 3]
		t = np.clip((pos[:, 1] - lo[k][1]) / max(hi[k][1] - lo[k][1], 1e-6), 0, 1)
		col = tile(g, n) * (0.85 + 0.3 * t)[:, None] * (0.95 + 0.08 * speckle(pos, 0.003))[:, None]
	elif m == "terracotta":
		col = tile(hexc("#B5603A") * jit[k], n) * (0.92 + 0.1 * speckle(pos, 0.002))[:, None] * edge_shade(k, pos, nrm, 0.004, 0.2)[:, None]
	elif m == "glass":
		col = tile(hexc("#A9C9D4"), n) * (0.9 + 0.15 * np.clip((pos[:, 1] - lo[k][1]) / max(hi[k][1] - lo[k][1], 1e-6), 0, 1))[:, None]
		shine = np.abs(((pos[:, 0] + pos[:, 1]) * 25) % 1.0 - 0.5) < 0.06
		col[shine] = col[shine] * 0.5 + 127
	elif m == "tarp":
		col = tile(hexc("#2F64B8"), n) * (0.94 + 0.1 * soft_noise(pos[:, 0] * 8, pos[:, 2] * 8, k))[:, None]
	elif m == "poster":
		col = tile(hexc("#E6DFCC"), n)
		t = np.clip((hi[k][1] - pos[:, 1]) / max(hi[k][1] - lo[k][1], 1e-6), 0, 1)
		col[t < 0.25] = PLASTIC[pick[k] % len(PLASTIC)]
		lines = (t > 0.4) & (t < 0.9) & (((t * 12) % 1.0) < 0.35)
		col[lines] = hexc("#6E6A62")
	elif m == "sign":
		col = tile(hexc("#5A2E24"), n)
		if SIGN is not None and BAND["w"] > 0:
			u = (X[idx] + 0.5) / SIZE; v = (Y[idx] + 0.5) / SIZE
			inb = (u >= BAND["u0"]) & (u <= BAND["u0"] + BAND["w"]) & (v >= BAND["v0"]) & (v <= BAND["v0"] + BAND["h"])
			if inb.any():
				sh, sw = SIGN.shape[:2]
				su = np.clip(((u[inb] - BAND["u0"]) / BAND["w"] * sw).astype(int), 0, sw - 1)
				sv = np.clip(((v[inb] - BAND["v0"]) / BAND["h"] * sh).astype(int), 0, sh - 1)
				col[inb] = SIGN[sv, su]
	elif m == "goods":   # a store's goods: packs and sachets in many colours, a label band on some
		col = tile(PLASTIC[pick[k] % len(PLASTIC)] * jit[k], n)
		axis = int(np.argmax(hi[k] - lo[k])); t = (pos[:, axis] - lo[k][axis]) / max(hi[k][axis] - lo[k][axis], 1e-6)
		if pick[k] % 2 == 0:
			band = (t > 0.38) & (t < 0.62); col[band] = col[band] * 0.3 + hexc("#EDE6D6") * 0.7
		col *= edge_shade(k, pos, nrm, 0.003, 0.2)[:, None]
	else:   # plastic: one clean colour each
		col = tile(plastic_colour(k) * jit[k], n) * (0.95 + 0.06 * speckle(pos, 0.002))[:, None] * edge_shade(k, pos, nrm, 0.003, 0.15)[:, None]
	img[Y[idx], X[idx]] = col; filled[Y[idx], X[idx]] = True

for _ in range(24):
	if filled.all(): break
	acc = np.zeros_like(img); cnt = np.zeros(filled.shape)
	for dy, dx in ((0, 1), (0, -1), (1, 0), (-1, 0)):
		sh = np.roll(np.roll(filled, dy, 0), dx, 1); sv = np.roll(np.roll(img, dy, 0), dx, 1)
		acc += sv * sh[..., None]; cnt += sh
	grow = (~filled) & (cnt > 0); img[grow] = acc[grow] / cnt[grow, None]; filled |= grow
img[~filled] = img[filled].mean(0)
Image.fromarray(np.clip(img, 0, 255).astype(np.uint8)).save(OUT)
from collections import Counter
print("painted", OUT, dict(Counter(mat)))
