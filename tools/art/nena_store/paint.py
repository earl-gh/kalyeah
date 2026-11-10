# Paint the Nena store's texture: every texel gets the colour of its part (classes.json) with detail drawn from where the texel is on the
# model (its 3D position): cement hollow-block walls (40 x 20 cm blocks in running bond, mortar joints, a dirty foot, rain streaks), wood
# with grain, knots and worn edges, a corrugated rust-red roof with rust where water runs, pavers, goods with labels. The sign is the
# owner's own sign, cleaned (sign_clean.py), drawn into the band the build gives its front face.
# usage: paint.py <dir with glbmesh.py> model.glb piece_of_tri.npy classes.json font.otf out.png size sign_clean.png
import sys, json, numpy as np
sys.path.insert(0, sys.argv[1]); import glbmesh
from PIL import Image, ImageDraw, ImageFont

P, NRM, UV, I, _img, _j = glbmesh.load(sys.argv[2])
T = I.reshape(-1, 3)
piece_of = np.load(sys.argv[3])
cls = {int(k): v for k, v in json.load(open(sys.argv[4])).items()}
FONT = sys.argv[5]
OUT = sys.argv[6]
SIZE = int(sys.argv[7])
SIGN = np.asarray(Image.open(sys.argv[8]).convert("RGB"), float)
BOLD = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"
# the band the build (build_geo.py --remap) gives the sign's front, in glTF UVs
SIGN_U0, SIGN_V0, SIGN_VH = 0.01, 0.848, 0.148


def hexc(h): return np.array([int(h[i:i + 2], 16) for i in (1, 3, 5)], float)


PAL = {
	"block": hexc("#85837C"), "mortar": hexc("#67655F"), "cap": hexc("#8E8C85"),
	"wood": hexc("#7E573A"), "roof_wood": hexc("#65442E"), "roof_sheet": hexc("#9A4036"), "rust": hexc("#6E3A22"),
	"floor": [hexc("#9C9283"), hexc("#8F8577"), hexc("#A39889")], "fridge": hexc("#B83A30"), "fridge_glass": hexc("#A9C9D4"),
	"crate": hexc("#2D57A3"), "bottle": [hexc("#467F4D"), hexc("#7A5028")], "broom": hexc("#C9A35A"),
	"product": [hexc("#D4AE45"), hexc("#CF8540"), hexc("#BB5545"), hexc("#4B7FB5"), hexc("#D6CDB8")], "label": hexc("#E8E1D0"),
	"sign_edge": hexc("#8A2E24"),
}
# a Philippine hollow block is 40 x 20 cm; the wall (0.6 model units) is about 2.6 m, so 13 courses
BH = 0.046
BW = 2 * BH
JOINT = 0.0038

rng = np.random.default_rng(7)
pieces = int(piece_of.max()) + 1
jitter = 1.0 + rng.uniform(-0.05, 0.05, pieces)
pick = rng.integers(0, 1 << 20, pieces)
box_lo = np.zeros((pieces, 3)); box_hi = np.zeros((pieces, 3))
for k in range(pieces):
	v = np.unique(T[piece_of == k]); box_lo[k] = P[v].min(0); box_hi[k] = P[v].max(0)


def hash3(q):
	"""A stable 0..1 number for each integer cell q (n x 3)."""
	return np.modf(np.abs(np.sin(q @ np.array([12.9898, 78.233, 37.719])) * 43758.5453))[0]


def speckle(pos, cell=0.0016):
	return hash3(np.floor(pos / cell))


def soft_noise(a, b, seed):
	"""Low, smooth variation over a surface (sums of a few sines): 0..1."""
	r = np.random.default_rng(seed)
	acc = np.zeros_like(a)
	for f in (17.0, 31.0, 53.0):
		p = r.uniform(0, 6.3, 3)
		acc += np.sin(a * f + p[0]) * np.sin(b * f * 1.3 + p[1]) + 0.5 * np.sin((a + b) * f * 0.7 + p[2])
	return 0.5 + acc / 9.0


def edge_shade(k, pos, nrm, width=0.005, depth=0.18):
	"""Darker where a board or box turns its corner: a worn, painted edge."""
	axes = np.argsort(-np.abs(nrm[0]))[1:]
	d = np.full(len(pos), 1e9)
	for a in axes:
		d = np.minimum(d, np.minimum(pos[:, a] - box_lo[k, a], box_hi[k, a] - pos[:, a]))
	return (1.0 - depth) + depth * np.clip(d / width, 0, 1)


def grain(a, seed):
	return 0.5 * np.sin(a * 90 + seed) + 0.3 * np.sin(a * 210 + seed * 1.7) + 0.2 * np.sin(a * 470 + seed * 2.3)


def blocks(pos, nrm, h, v, course_seed, top_y):
	"""Hollow blocks laid in running bond on a face whose along-axis is h and up-axis v."""
	row = np.floor(v / BH)
	off = (row % 2) * BW / 2 + (course_seed % 7) * 0.013
	col = np.floor((h + off) / BW)
	lu = (h + off) - col * BW; lv = v - row * BH
	joint = (lu < JOINT) | (lv < JOINT)
	tone = 1.0 + (hash3(np.stack([row, col, np.full_like(row, course_seed % 97)], 1)) - 0.5) * 0.16
	warm = (hash3(np.stack([col, row, np.full_like(row, 5.0)], 1)) - 0.5) * 10
	col_rgb = PAL["block"][None, :] * tone[:, None] + np.stack([warm, warm * 0.6, -warm * 0.4], 1)
	col_rgb *= (0.93 + 0.07 * np.clip(lv / (BH * 0.25), 0, 1))[:, None]          # the block's lower lip sits in a little shadow
	s = speckle(pos)
	col_rgb *= (0.95 + 0.08 * s)[:, None]
	col_rgb[s > 0.97] *= 0.72                                                    # pits in the concrete
	col_rgb[joint] = PAL["mortar"] * (0.94 + 0.1 * s[joint, None])
	# rain streaks running down from under the roof on a few block columns, and dirt splashed up at the foot
	streak = hash3(np.stack([col, np.zeros_like(col), np.full_like(col, 3.0)], 1))
	runs = (streak > 0.7) & (v > top_y - 0.05 - streak * 0.25)
	col_rgb[runs] *= 0.88
	foot = np.clip(1 - v / 0.07, 0, 1)
	col_rgb = col_rgb * (1 - 0.22 * foot[:, None]) + hexc("#6B5A45") * 0.22 * foot[:, None] * 0.6
	return col_rgb


def text_image(word, font, w, h, fill, bg=(0, 0, 0, 0)):
	im = Image.new("RGBA", (w, h), bg); d = ImageDraw.Draw(im)
	size = h
	while True:
		f = ImageFont.truetype(font, size); b = d.textbbox((0, 0), word, font=f)
		if b[2] - b[0] <= w * 0.96 and b[3] - b[1] <= h * 0.8:
			break
		size -= 2
	d.text(((w - (b[2] - b[0])) / 2 - b[0], (h - (b[3] - b[1])) / 2 - b[1]), word, font=f, fill=fill)
	return np.asarray(im, float)


# the street's own painted words: the classic warning on the back wall, the prepaid-load board on the right, ICE WATER at the front
BAWAL = text_image("BAWAL UMIHI DITO", BOLD, 900, 110, (30, 30, 30, 255))
LOAD = text_image("LOAD DITO", BOLD, 600, 140, (190, 40, 32, 255))
POSTER_X = (-0.405, -0.305); POSTER_Y = (0.29, 0.46)
pw, ph = 400, int(400 * (POSTER_Y[1] - POSTER_Y[0]) / (POSTER_X[1] - POSTER_X[0]))
poster = Image.new("RGB", (pw, ph), "#E6E0D2"); d = ImageDraw.Draw(poster)
d.rectangle([0, 0, pw - 1, ph - 1], outline="#2F62C0", width=18)
for word, size, y in (("ICE", 120, 0.2), ("WATER", 78, 0.5)):
	f = ImageFont.truetype(FONT, size); b = d.textbbox((0, 0), word, font=f)
	d.text(((pw - (b[2] - b[0])) / 2 - b[0], ph * y - b[1]), word, font=f, fill="#2F62C0")
d.ellipse([pw * 0.4, ph * 0.74, pw * 0.6, ph * 0.9], fill="#7FC4EA")
POSTER = np.asarray(poster, float)


def sample(img, u, v):
	h, w = img.shape[:2]
	return img[np.clip((v * h).astype(int), 0, h - 1), np.clip((u * w).astype(int), 0, w - 1)]


def stamp(col, on, img, u, v, strength, pos):
	"""Paint `img` (RGBA) over `col` where `on`, a little worn by the concrete's speckle."""
	if not on.any():
		return
	px = sample(img, u[on], v[on]); a = px[:, 3:4] / 255.0 * strength
	a *= (speckle(pos[on], 0.0022) > 0.12)[:, None]
	col[on] = col[on] * (1 - a) + px[:, :3] * a


def paint(k, c, pos, nrm, uv):
	n = len(pos)
	j = jitter[k]
	x, y, z = pos[:, 0], pos[:, 1], pos[:, 2]
	lo, hi = box_lo[k], box_hi[k]
	if c in ("plaster", "plank"):
		if abs(nrm[0, 1]) > 0.7:                                                 # a wall's top: a plain concrete cap
			col = np.tile(PAL["cap"], (n, 1)) * (0.95 + 0.08 * speckle(pos))[:, None]
		elif c == "plank":
			# the left wall is built of boards stacked like courses: each one is a course of blocks
			along = z if abs(nrm[0, 0]) > abs(nrm[0, 2]) else x
			col = blocks(pos, nrm, along, (y - lo[1]) * (BH / max(hi[1] - lo[1], 1e-6)), pick[k], 1e9)
		else:
			along = z if abs(nrm[0, 0]) > abs(nrm[0, 2]) else x
			col = blocks(pos, nrm, along, y, 0, 0.6)
		# the words painted on the outside walls
		back = (nrm[:, 2] < -0.7) & (z < -0.23) & (x > -0.32) & (x < 0.32) & (y > 0.22) & (y < 0.31)
		stamp(col, back, BAWAL, (0.32 - x) / 0.64, (0.31 - y) / 0.09, 0.85, pos)
		side = (nrm[:, 0] > 0.7) & (x > 0.4) & (z > -0.22) & (z < -0.03) & (y > 0.3) & (y < 0.37)
		stamp(col, side, LOAD, (-0.03 - z) / 0.19, (0.37 - y) / 0.07, 0.9, pos)
		on = (nrm[:, 2] > 0.7) & (x > POSTER_X[0]) & (x < POSTER_X[1]) & (y > POSTER_Y[0]) & (y < POSTER_Y[1]) & (z > 0.15)
		if on.any():
			col[on] = sample(POSTER, (x[on] - POSTER_X[0]) / (POSTER_X[1] - POSTER_X[0]), (POSTER_Y[1] - y[on]) / (POSTER_Y[1] - POSTER_Y[0]))
		return col
	if c in ("wood", "roof_wood", "broom"):
		axis = int(np.argmax(hi - lo)); across = [a for a in range(3) if a != axis]
		col = np.tile(PAL[c] * j, (n, 1))
		g = grain(pos[:, across[0]] * 1.3 + pos[:, across[1]] + pos[:, axis] * 0.08, k)
		col *= (1.0 + (0.12 if c == "broom" else 0.07) * g)[:, None]
		if c != "broom" and pick[k] % 3 == 0:                                     # a knot on some boards
			kt = lo[axis] + (hi[axis] - lo[axis]) * ((pick[k] >> 4) % 100 / 100.0 * 0.6 + 0.2)
			dist = np.hypot((pos[:, axis] - kt) / 0.006, (pos[:, across[0]] + pos[:, across[1]] - (lo[across[0]] + hi[across[0]] + lo[across[1]] + hi[across[1]]) / 2) / 0.0035)
			col *= (1 - 0.35 * np.clip(1 - dist, 0, 1))[:, None]
		col *= edge_shade(k, pos, nrm)[:, None]
		return col
	if c == "roof_sheet":
		col = np.tile(PAL["roof_sheet"] * j, (n, 1))
		col *= (1.0 + 0.09 * np.sin(x * 2 * np.pi / 0.011))[:, None]
		# rust where water runs: patches over the sheet, heavier at its low (eave) end, and along its lapped edges
		patch = np.clip((soft_noise(x * 6, z * 6, int(pick[k] % 1000)) - 0.55) / 0.2, 0, 1)
		low = np.clip(1 - (y - lo[1]) / max(hi[1] - lo[1], 1e-6) / 0.25, 0, 1)
		r = np.clip(0.55 * patch + 0.45 * low, 0, 0.8)[:, None] * (0.8 + 0.4 * speckle(pos, 0.002))[:, None]
		col = col * (1 - r) + PAL["rust"] * r
		col *= edge_shade(k, pos, nrm, 0.004, 0.25)[:, None]
		return col
	if c == "floor":
		col = np.tile(PAL["floor"][pick[k] % 3], (n, 1)) * (0.93 + 0.1 * speckle(pos, 0.002))[:, None]
		col *= edge_shade(k, pos, nrm, 0.006, 0.22)[:, None]
		col *= (0.92 + 0.08 * soft_noise(x * 4, z * 4, int(pick[k] % 50)))[:, None]
		return col
	if c == "product":
		col = np.tile(PAL["product"][pick[k] % len(PAL["product"])] * j, (n, 1))
		axis = int(np.argmax(hi - lo)); t = (pos[:, axis] - lo[axis]) / max(hi[axis] - lo[axis], 1e-6)
		if pick[k] % 2 == 0:                                                      # a label across the middle
			band = (t > 0.38) & (t < 0.62)
			col[band] = col[band] * 0.35 + PAL["label"] * 0.65
		col *= edge_shade(k, pos, nrm, 0.003, 0.2)[:, None]
		return col
	if c == "bottle":
		return np.tile(PAL["bottle"][pick[k] % 2], (n, 1)) * (0.85 + 0.25 * np.clip((y - lo[1]) / max(hi[1] - lo[1], 1e-6), 0, 1))[:, None]
	if c == "fridge_glass":
		col = np.tile(PAL["fridge_glass"], (n, 1)) * (0.9 + 0.15 * np.clip((y - lo[1]) / max(hi[1] - lo[1], 1e-6), 0, 1))[:, None]
		shine = np.abs(((x + y) * 25) % 1.0 - 0.5) < 0.06
		col[shine] = col[shine] * 0.5 + 255 * 0.5
		return col
	if c == "sign":
		col = np.tile(PAL["sign_edge"], (n, 1))
		front = nrm[:, 2] > 0.9
		if front.any():
			band_w = SIGN.shape[1] / SIGN.shape[0] * SIGN_VH
			col[front] = sample(SIGN, (uv[front, 0] - SIGN_U0) / band_w, (uv[front, 1] - SIGN_V0) / SIGN_VH)
		return col
	col = np.tile(PAL[c] * j, (n, 1))
	return col * edge_shade(k, pos, nrm)[:, None]


img = np.zeros((SIZE, SIZE, 3), float)
filled = np.zeros((SIZE, SIZE), bool)
face_n = np.cross(P[T[:, 1]] - P[T[:, 0]], P[T[:, 2]] - P[T[:, 0]])
face_n /= np.maximum(np.linalg.norm(face_n, axis=1), 1e-12)[:, None]
for t in range(len(T)):
	k = int(piece_of[t]); c = cls[k]
	if c == "remove":
		continue
	uv = UV[T[t]] * SIZE - 0.5
	x0, y0 = np.floor(uv.min(0)).astype(int) - 1; x1, y1 = np.ceil(uv.max(0)).astype(int) + 1
	x0, y0 = max(x0, 0), max(y0, 0); x1, y1 = min(x1, SIZE - 1), min(y1, SIZE - 1)
	if x1 < x0 or y1 < y0:
		continue
	gx, gy = np.meshgrid(np.arange(x0, x1 + 1), np.arange(y0, y1 + 1))
	a, b, cc = uv
	den = (b[1] - cc[1]) * (a[0] - cc[0]) + (cc[0] - b[0]) * (a[1] - cc[1])
	if abs(den) < 1e-12:
		continue
	w0 = ((b[1] - cc[1]) * (gx - cc[0]) + (cc[0] - b[0]) * (gy - cc[1])) / den
	w1 = ((cc[1] - a[1]) * (gx - cc[0]) + (a[0] - cc[0]) * (gy - cc[1])) / den
	w2 = 1 - w0 - w1
	eps = 0.7 / max(np.abs(uv.max(0) - uv.min(0)).max(), 1.0)
	m = (w0 > -eps) & (w1 > -eps) & (w2 > -eps)
	if not m.any():
		continue
	W = np.stack([w0[m], w1[m], w2[m]], 1)
	W = np.clip(W, 0, None); W /= W.sum(1, keepdims=True)
	pos = W @ P[T[t]]
	texel_uv = (np.stack([gx[m], gy[m]], 1) + 0.5) / SIZE
	img[gy[m], gx[m]] = paint(k, c, pos, np.tile(face_n[t], (len(pos), 1)), texel_uv)
	filled[gy[m], gx[m]] = True

for _ in range(24):
	if filled.all():
		break
	acc = np.zeros_like(img); cnt = np.zeros(filled.shape)
	for dy, dx in ((0, 1), (0, -1), (1, 0), (-1, 0)):
		sh = np.roll(np.roll(filled, dy, 0), dx, 1); sv = np.roll(np.roll(img, dy, 0), dx, 1)
		acc += sv * sh[..., None]; cnt += sh
	grow = (~filled) & (cnt > 0)
	img[grow] = acc[grow] / cnt[grow, None]
	filled |= grow
img[~filled] = img[filled].mean(0)
Image.fromarray(np.clip(img, 0, 255).astype(np.uint8)).save(OUT)
print("painted", OUT, SIZE)
