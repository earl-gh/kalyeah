# Repaint a building's picture, piece by piece, in its own UV layout (owner, 2026-11-10: the same steps as the Nena store):
#   wall  - cement hollow blocks (40 x 20 cm, running bond, mortar, tone per block, pits, rain streaks, a dirty foot)
#   roof  - one sheet colour for the whole roof (the roof's own, calmed), corrugation, rust where water runs, worn edges
#   wood  - the street's brown, grain, a knot now and then, worn edges
#   floor - pavers: a tone each, speckle, worn edges
#   own   - everything else (tyres, tricycles, posters, plants, tarps, goods, signs' backs): its own colours cleaned into 1 to 4 flat
#           tones (k-means on its texels, the tones' borders smoothed), a little speckle and worn edges
# usage: paint_generic.py <dir> geo.glb piece.npy features.json config.json classes_out.json out.png size
import sys, io, json, colorsys, numpy as np
sys.path.insert(0, sys.argv[1]); import glbmesh
from PIL import Image, ImageDraw, ImageFont, ImageFilter

P, _N, UV, I, IMG, _j = glbmesh.load(sys.argv[2])
T = I.reshape(-1, 3)
piece_of = np.load(sys.argv[3])
F = {f["k"]: f for f in json.load(open(sys.argv[4]))}
CFG = json.load(open(sys.argv[5]))
OUT_CLASSES, OUT, SIZE = sys.argv[6], sys.argv[7], int(sys.argv[8])
OLD = np.asarray(Image.open(io.BytesIO(IMG)).convert("RGB"), float)
H_MODEL = max(f["hi"][1] for f in F.values())
pieces = int(piece_of.max()) + 1


def hexc(h): return np.array([int(h[i:i + 2], 16) for i in (1, 3, 5)], float)


PAL = {"block": hexc("#85837C"), "mortar": hexc("#67655F"), "cap": hexc("#8E8C85"), "wood": hexc("#7E573A"), "rust": hexc("#6E3A22"),
	"floor": [hexc("#9C9283"), hexc("#8F8577"), hexc("#A39889")]}
BH = 0.046
BW = 2 * BH
JOINT = 0.0038
rng = np.random.default_rng(11)
jitter = 1.0 + rng.uniform(-0.05, 0.05, pieces)
pick = rng.integers(0, 1 << 20, pieces)


# ---------------------------------------------------------------- classes
def classify(f):
	k = f["k"]
	if str(k) in CFG.get("classes", {}):
		return CFG["classes"][str(k)]
	if k in CFG.get("drop", []):
		return "drop"
	if k in CFG.get("sign", []):
		return "own"
	s = sorted([f["hi"][i] - f["lo"][i] for i in range(3)])
	ny = abs(f["n"][1]); h, sat, lum = f["h"], f["s"], f["l"]
	cy = (f["lo"][1] + f["hi"][1]) / 2
	colourful = sat > 0.3 and (70 < h < 260)
	# wood is a board, a post or a beam: long, or a thin plank (a slab or a kerb of wood colour keeps its own colours)
	woodish = 12 < h < 42 and 0.2 < sat < 0.75 and 0.2 < lum < 0.6 and (s[2] > 2.5 * s[1] or s[0] < 0.025)
	if f["hi"][1] < 0.06 and s[1] * s[2] > 0.008 and ny > 0.7:
		return "floor"
	if cy > CFG.get("roof_from", 0.55) * H_MODEL and s[0] < 0.06 and s[2] > 0.15 and s[1] > 0.06 and ny > 0.35 and not colourful and not woodish:
		return "roof"
	if ny < 0.35 and s[0] < 0.1 and s[1] > 0.12 and sat < 0.45 and not (woodish and sat > 0.35):
		return "wall"
	if woodish:
		return "wood"
	return "own"


cls = {k: classify(F[k]) for k in range(pieces)}
json.dump({str(k): v for k, v in cls.items()}, open(OUT_CLASSES, "w"))

# ---------------------------------------------------------------- raster: every texel's piece, place, facing and old colour
face_n = np.cross(P[T[:, 1]] - P[T[:, 0]], P[T[:, 2]] - P[T[:, 0]])
face_n /= np.maximum(np.linalg.norm(face_n, axis=1), 1e-12)[:, None]
tx_x, tx_y, tx_k, tx_pos, tx_n, tx_old = [], [], [], [], [], []
oh, ow = OLD.shape[:2]
for t in range(len(T)):
	k = int(piece_of[t])
	if cls[k] == "drop":
		continue
	uv = UV[T[t]] * SIZE - 0.5
	x0, y0 = np.floor(uv.min(0)).astype(int) - 1; x1, y1 = np.ceil(uv.max(0)).astype(int) + 1
	x0, y0 = max(x0, 0), max(y0, 0); x1, y1 = min(x1, SIZE - 1), min(y1, SIZE - 1)
	if x1 < x0 or y1 < y0:
		continue
	gx, gy = np.meshgrid(np.arange(x0, x1 + 1), np.arange(y0, y1 + 1))
	a, b, c = uv
	den = (b[1] - c[1]) * (a[0] - c[0]) + (c[0] - b[0]) * (a[1] - c[1])
	if abs(den) < 1e-12:
		continue
	w0 = ((b[1] - c[1]) * (gx - c[0]) + (c[0] - b[0]) * (gy - c[1])) / den
	w1 = ((c[1] - a[1]) * (gx - c[0]) + (a[0] - c[0]) * (gy - c[1])) / den
	w2 = 1 - w0 - w1
	eps = 0.7 / max(np.abs(uv.max(0) - uv.min(0)).max(), 1.0)
	m = (w0 > -eps) & (w1 > -eps) & (w2 > -eps)
	if not m.any():
		continue
	W = np.clip(np.stack([w0[m], w1[m], w2[m]], 1), 0, None); W /= W.sum(1, keepdims=True)
	tx_x.append(gx[m]); tx_y.append(gy[m]); tx_k.append(np.full(m.sum(), k)); tx_pos.append(W @ P[T[t]])
	tx_n.append(np.tile(face_n[t], (m.sum(), 1)))
	u = (gx[m] + 0.5) / SIZE; v = (gy[m] + 0.5) / SIZE
	tx_old.append(OLD[np.clip((v * oh).astype(int), 0, oh - 1), np.clip((u * ow).astype(int), 0, ow - 1)])
tx_x = np.concatenate(tx_x); tx_y = np.concatenate(tx_y); tx_k = np.concatenate(tx_k)
tx_pos = np.concatenate(tx_pos); tx_n = np.concatenate(tx_n); tx_old = np.concatenate(tx_old)
# one texel, one owner (the last triangle drawn there)
key = tx_y * SIZE + tx_x
_, last = np.unique(key[::-1], return_index=True)
keep = len(key) - 1 - last
tx_x, tx_y, tx_k, tx_pos, tx_n, tx_old = tx_x[keep], tx_y[keep], tx_k[keep], tx_pos[keep], tx_n[keep], tx_old[keep]


# a wood-coloured piece with strong light and dark bands (hazard stripes, a painted foot) keeps its own colours
for k in [k for k, c in cls.items() if c == "wood"]:
	lum_k = tx_old[tx_k == k].mean(1)
	if len(lum_k) > 30 and lum_k.std() > 36:
		cls[k] = "own"
json.dump({str(k): v for k, v in cls.items()}, open(OUT_CLASSES, "w"))

# ---------------------------------------------------------------- detail
def hash3(q): return np.modf(np.abs(np.sin(q @ np.array([12.9898, 78.233, 37.719])) * 43758.5453))[0]
def speckle(pos, cell=0.0016): return hash3(np.floor(pos / cell))


def soft_noise(a, b, seed):
	r = np.random.default_rng(seed); acc = np.zeros_like(a)
	for f in (17.0, 31.0, 53.0):
		p = r.uniform(0, 6.3, 3)
		acc += np.sin(a * f + p[0]) * np.sin(b * f * 1.3 + p[1]) + 0.5 * np.sin((a + b) * f * 0.7 + p[2])
	return 0.5 + acc / 9.0


def edge_shade(k, pos, nrm, width=0.005, depth=0.18):
	f = F[k]; lo = np.array(f["lo"]); hi = np.array(f["hi"])
	d = np.full(len(pos), 1e9)
	order = np.argsort(-np.abs(nrm), axis=1)
	for col in (1, 2):
		ax = order[:, col]
		p = pos[np.arange(len(pos)), ax]
		d = np.minimum(d, np.minimum(p - lo[ax], hi[ax] - p))
	return (1.0 - depth) + depth * np.clip(d / width, 0, 1)


def grain(a, seed): return 0.5 * np.sin(a * 90 + seed) + 0.3 * np.sin(a * 210 + seed * 1.7) + 0.2 * np.sin(a * 470 + seed * 2.3)


def blocks(pos, h, v, seed, top_y):
	row = np.floor(v / BH)
	off = (row % 2) * BW / 2 + (seed % 7) * 0.013
	col = np.floor((h + off) / BW)
	lu = (h + off) - col * BW; lv = v - row * BH
	joint = (lu < JOINT) | (lv < JOINT)
	tone = 1.0 + (hash3(np.stack([row, col, np.full_like(row, seed % 97)], 1)) - 0.5) * 0.16
	warm = (hash3(np.stack([col, row, np.full_like(row, 5.0)], 1)) - 0.5) * 10
	c = PAL["block"][None, :] * tone[:, None] + np.stack([warm, warm * 0.6, -warm * 0.4], 1)
	c *= (0.93 + 0.07 * np.clip(lv / (BH * 0.25), 0, 1))[:, None]
	s = speckle(pos); c *= (0.95 + 0.08 * s)[:, None]; c[s > 0.97] *= 0.72
	c[joint] = PAL["mortar"] * (0.94 + 0.1 * s[joint, None])
	streak = hash3(np.stack([col, np.zeros_like(col), np.full_like(col, 3.0)], 1))
	c[(streak > 0.7) & (v > top_y - 0.05 - streak * 0.25)] *= 0.88
	foot = np.clip(1 - v / 0.07, 0, 1)
	return c * (1 - 0.22 * foot[:, None]) + hexc("#6B5A45") * 0.13 * foot[:, None]


def calm(c):
	h, l, s = colorsys.rgb_to_hls(*(np.clip(c, 0, 255) / 255))
	return np.array(colorsys.hls_to_rgb(h, l, s * 0.9)) * 255


def kmeans(X, k, iters=12):
	order = np.argsort(X.sum(1)); cen = X[order[np.linspace(0, len(X) - 1, k).astype(int)]].copy()
	for _ in range(iters):
		lab = np.argmin(((X[:, None, :] - cen[None]) ** 2).sum(2), 1)
		for j in range(k):
			if (lab == j).any():
				cen[j] = X[lab == j].mean(0)
	return cen, lab


def posterize(idx):
	"""Its own colours in 1 to 4 flat tones with smooth borders."""
	X = tx_old[idx]
	if len(X) < 24 or X.std(0).mean() < 9:
		return np.tile(calm(np.median(X, 0)), (len(X), 1))
	best = None
	sample = X[:: max(1, len(X) // 4000)]
	base = ((sample - sample.mean(0)) ** 2).sum()
	for k in (2, 3, 4):
		cen, lab = kmeans(sample, k)
		sse = ((sample - cen[lab]) ** 2).sum()
		if best is None or sse < best[2] * 0.7:
			best = (k, cen, sse)
		if sse < base * 0.08:
			break
	k, cen = best[0], best[1]
	# tones closer than a clear step are one tone; a tone on under 6% of the piece is noise (grime, specks)
	lab = np.argmin(((sample[:, None, :] - cen[None]) ** 2).sum(2), 1)
	keep_t = [j for j in range(k) if (lab == j).mean() >= 0.06]
	merged = []
	for j in sorted(keep_t, key=lambda j: -(lab == j).mean()):
		if all(np.linalg.norm(cen[j] - cen[m]) > 42 for m in merged):
			merged.append(j)
	cen = cen[merged]
	# grime: a dull tone that is not much darker or lighter than another dull tone is that tone (black words on a white board stay)
	def dull(c): return (c.max() - c.min()) / max(c.max(), 1) < 0.3
	kept = []
	for j in range(len(cen)):
		if any(dull(cen[j]) and dull(cen[m]) and abs(cen[j].mean() - cen[m].mean()) < 120 for m in kept):
			continue
		kept.append(j)
	cen = cen[kept]; k = len(cen)
	if k == 1:
		return np.tile(calm(cen[0]), (len(X), 1))
	lab = np.argmin(((X[:, None, :] - cen[None]) ** 2).sum(2), 1)
	# smooth the tones' borders in the picture: each tone's mask blurred a little, the strongest wins
	xs, ys = tx_x[idx], tx_y[idx]
	bx0, by0 = xs.min(), ys.min(); w, h = xs.max() - bx0 + 1, ys.max() - by0 + 1
	score = np.zeros((len(idx), k))
	for j in range(k):
		m = np.zeros((h, w), np.uint8); m[ys[lab == j] - by0, xs[lab == j] - bx0] = 255
		b = np.asarray(Image.fromarray(m).filter(ImageFilter.GaussianBlur(2.4)), float)
		score[:, j] = b[ys - by0, xs - bx0]
	lab = np.argmax(score, 1)
	tones = np.array([calm(c) for c in cen])
	return tones[lab]


# ---------------------------------------------------------------- roof colour: the roof's own, one for the whole roof
roof_idx = np.isin(tx_k, [k for k, c in cls.items() if c == "roof"])
roof_rgb = np.array(CFG["roof_rgb"], float) if "roof_rgb" in CFG else (calm(np.median(tx_old[roof_idx], 0)) if roof_idx.any() else hexc("#9A4036"))

def paint_wall(idx, k, pos, nrm, hi):
	col = np.zeros((len(idx), 3))
	top = np.abs(nrm[:, 1]) > 0.7
	col[top] = PAL["cap"] * (0.95 + 0.08 * speckle(pos[top]))[:, None]
	side = ~top
	along = np.where(np.abs(nrm[side, 0]) > np.abs(nrm[side, 2]), pos[side, 2], pos[side, 0])
	col[side] = blocks(pos[side], along, pos[side, 1], int(pick[k] % 5), hi[1])
	# a window, a door or a frame painted on the wall: clearly unlike the wall's own colour (grime and specks are not)
	old = tx_old[idx]; base = np.median(old, 0)
	bare = np.linalg.norm(old - base, axis=1) < 95
	xs, ys = tx_x[idx], tx_y[idx]; bx0, by0 = xs.min(), ys.min()
	m = np.zeros((ys.max() - by0 + 1, xs.max() - bx0 + 1), np.uint8); m[ys - by0, xs - bx0] = np.where(bare, 255, 0)
	m = np.asarray(Image.fromarray(m).filter(ImageFilter.MedianFilter(9)).filter(ImageFilter.GaussianBlur(2.0)), float)[ys - by0, xs - bx0] / 255
	if (m < 0.5).any():
		# its own colours, only lightly cleaned (a 3-texel median), so the slats of a shutter or a door's panels stay
		o = np.zeros((ys.max() - by0 + 1, xs.max() - bx0 + 1, 3), np.uint8); o[ys - by0, xs - bx0] = old.clip(0, 255).astype(np.uint8)
		o = np.asarray(Image.fromarray(o).filter(ImageFilter.MedianFilter(3)), float)[ys - by0, xs - bx0]
		o = o * 0.96
		col = np.where((m[:, None] < 0.5), o, col)
	return col


img = np.zeros((SIZE, SIZE, 3)); filled = np.zeros((SIZE, SIZE), bool)
order = np.argsort(tx_k, kind="stable")
bounds = np.searchsorted(tx_k[order], np.arange(pieces + 1))
for k in range(pieces):
	idx = order[bounds[k]:bounds[k + 1]]
	if len(idx) == 0:
		continue
	c = cls[k]; pos = tx_pos[idx]; nrm = tx_n[idx]; f = F[k]
	lo = np.array(f["lo"]); hi = np.array(f["hi"])
	if c == "wall":
		col = paint_wall(idx, k, pos, nrm, hi)
	elif c == "roof":
		col = posterize(idx) * jitter[k]
		haxis = np.cross(nrm, np.array([0.0, 1.0, 0.0])); hn = np.linalg.norm(haxis, axis=1)
		haxis = np.where(hn[:, None] > 0.2, haxis / np.maximum(hn, 1e-9)[:, None], np.array([1.0, 0.0, 0.0]))
		hcoord = (pos * haxis).sum(1)
		col *= (1.0 + 0.09 * np.sin(hcoord * 2 * np.pi / 0.011))[:, None]
		patch = np.clip((soft_noise(pos[:, 0] * 6, pos[:, 2] * 6, int(pick[k] % 1000)) - 0.55) / 0.2, 0, 1)
		low = np.clip(1 - (pos[:, 1] - lo[1]) / max(hi[1] - lo[1], 1e-6) / 0.25, 0, 1)
		r = np.clip(0.5 * patch + 0.4 * low, 0, 0.75)[:, None] * (0.8 + 0.4 * speckle(pos, 0.002))[:, None]
		col = col * (1 - r) + PAL["rust"] * r
		col *= edge_shade(k, pos, nrm, 0.004, 0.25)[:, None]
	elif c == "wood":
		axis = int(np.argmax(hi - lo)); across = [a for a in range(3) if a != axis]
		col = np.tile(PAL["wood"] * jitter[k], (len(idx), 1))
		col *= (1.0 + 0.07 * grain(pos[:, across[0]] * 1.3 + pos[:, across[1]] + pos[:, axis] * 0.08, k))[:, None]
		if pick[k] % 3 == 0:
			kt = lo[axis] + (hi[axis] - lo[axis]) * ((pick[k] >> 4) % 100 / 100.0 * 0.6 + 0.2)
			mid = (lo[across[0]] + hi[across[0]] + lo[across[1]] + hi[across[1]]) / 2
			dist = np.hypot((pos[:, axis] - kt) / 0.006, (pos[:, across[0]] + pos[:, across[1]] - mid) / 0.0035)
			col *= (1 - 0.35 * np.clip(1 - dist, 0, 1))[:, None]
		col *= edge_shade(k, pos, nrm)[:, None]
	elif c == "floor":
		col = np.tile(PAL["floor"][pick[k] % 3], (len(idx), 1)) * (0.93 + 0.1 * speckle(pos, 0.002))[:, None]
		col *= edge_shade(k, pos, nrm, 0.006, 0.22)[:, None]
		col *= (0.92 + 0.08 * soft_noise(pos[:, 0] * 4, pos[:, 2] * 4, int(pick[k] % 50)))[:, None]
	else:
		col = posterize(idx) * (0.97 + 0.05 * speckle(pos, 0.002))[:, None]
		col *= edge_shade(k, pos, nrm, 0.003, 0.15)[:, None]
		if f["area"] > 0.3 and f["s"] < 0.3:
			# a big dull shell (a whole storey's walls in one piece): its upright faces are hollow-block walls too
			up = np.abs(nrm[:, 1]) < 0.35
			if up.sum() > 50:
				col[up] = paint_wall(idx[up], k, pos[up], nrm[up], hi)
	img[tx_y[idx], tx_x[idx]] = col
	filled[tx_y[idx], tx_x[idx]] = True

for _ in range(24):
	if filled.all():
		break
	acc = np.zeros_like(img); cnt = np.zeros(filled.shape)
	for dy, dx in ((0, 1), (0, -1), (1, 0), (-1, 0)):
		sh = np.roll(np.roll(filled, dy, 0), dx, 1); sv = np.roll(np.roll(img, dy, 0), dx, 1)
		acc += sv * sh[..., None]; cnt += sh
	grow = (~filled) & (cnt > 0)
	img[grow] = acc[grow] / cnt[grow, None]; filled |= grow
img[~filled] = img[filled].mean(0)
Image.fromarray(np.clip(img, 0, 255).astype(np.uint8)).save(OUT)
from collections import Counter
print("painted", OUT, SIZE, dict(Counter(cls.values())), "roof", np.round(roof_rgb).astype(int).tolist())
