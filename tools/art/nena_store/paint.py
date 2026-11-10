# Paint the Nena store's texture from scratch: every texel gets the colour of its part (classes.json), with a little painted detail drawn
# from where the texel is on the model (its 3D position), never from the old picture. The sign and the poster are lettered in the game's font.
# usage: paint.py <dir with glbmesh.py> model.glb piece_of_tri.npy classes.json font.otf out.png [size]
import sys, json, numpy as np
sys.path.insert(0, sys.argv[1]); import glbmesh
from PIL import Image, ImageDraw, ImageFont

P, NRM, UV, I, _img, _j = glbmesh.load(sys.argv[2])
T = I.reshape(-1, 3)
piece_of = np.load(sys.argv[3])
cls = {int(k): v for k, v in json.load(open(sys.argv[4])).items()}
FONT = sys.argv[5]
OUT = sys.argv[6]
SIZE = int(sys.argv[7]) if len(sys.argv) > 7 else 2048


def hexc(h): return np.array([int(h[i:i + 2], 16) for i in (1, 3, 5)], float)


# one calm palette: warm cream plaster, honey and brown wood, a single rust-red roof, warm grey pavers
PAL = {
	"plaster": hexc("#C4B497"), "plank": [hexc("#8A6142"), hexc("#7C573B"), hexc("#936A48")],
	"wood": hexc("#7E573A"), "roof_wood": hexc("#65442E"), "roof_sheet": hexc("#9A4036"),
	"floor": [hexc("#9C9283"), hexc("#8F8577"), hexc("#A39889")], "fridge": hexc("#B83A30"), "fridge_glass": hexc("#A9C9D4"),
	"crate": hexc("#2D57A3"), "bottle": [hexc("#467F4D"), hexc("#7A5028")], "broom": hexc("#C9A35A"),
	"product": [hexc("#D4AE45"), hexc("#CF8540"), hexc("#BB5545"), hexc("#4B7FB5"), hexc("#D6CDB8")],
	"sign_edge": hexc("#8A2E24"),
}

rng = np.random.default_rng(7)
pieces = int(piece_of.max()) + 1
jitter = 1.0 + rng.uniform(-0.05, 0.05, pieces)          # every piece a touch lighter or darker than its neighbour
pick = rng.integers(0, 1 << 20, pieces)                  # a stable choice per piece (plank tone, paver tone, product colour)

# the sign: lettered once, then looked up by the sign's front face position
sign = [i for i, c in cls.items() if c == "sign"][0]
sign_v = np.unique(T[piece_of == sign]); slo, shi = P[sign_v].min(0), P[sign_v].max(0)
SW, SH = 2048, int(2048 * (shi[1] - slo[1]) / (shi[0] - slo[0]))
board = Image.new("RGB", (SW, SH), "#E2B54C")
d = ImageDraw.Draw(board)
border = int(SH * 0.09)
d.rectangle([0, 0, SW - 1, SH - 1], outline="#8A2E24", width=border)
d.rectangle([border * 1.6, border * 1.6, SW - border * 1.6, SH - border * 1.6], outline="#F0D58C", width=max(4, border // 4))
font = ImageFont.truetype(FONT, int(SH * 0.56))
text = "Nena Store"
box = d.textbbox((0, 0), text, font=font)
tx, ty = (SW - (box[2] - box[0])) / 2 - box[0], (SH - (box[3] - box[1])) / 2 - box[1]
d.text((tx, ty + SH * 0.035), text, font=font, fill="#6A2016")
d.text((tx, ty), text, font=font, fill="#A53328")
SIGN = np.asarray(board, float)

# the poster on the front-left board (where a real brand's poster was): ICE WATER, the street's own drink
PX = (-0.405, -0.305); PY = (0.29, 0.46)
pw, ph = 400, int(400 * (PY[1] - PY[0]) / (PX[1] - PX[0]))
poster = Image.new("RGB", (pw, ph), "#E6E0D2")
d = ImageDraw.Draw(poster)
d.rectangle([0, 0, pw - 1, ph - 1], outline="#2F62C0", width=18)
f1 = ImageFont.truetype(FONT, 120); f2 = ImageFont.truetype(FONT, 78)
for word, fnt, y in (("ICE", f1, 0.2), ("WATER", f2, 0.5)):
	b = d.textbbox((0, 0), word, font=fnt)
	d.text(((pw - (b[2] - b[0])) / 2 - b[0], ph * y - b[1]), word, font=fnt, fill="#2F62C0")
d.ellipse([pw * 0.4, ph * 0.74, pw * 0.6, ph * 0.9], fill="#7FC4EA")
POSTER = np.asarray(poster, float)


def sample(img, u, v):
	h, w = img.shape[:2]
	return img[np.clip((v * h).astype(int), 0, h - 1), np.clip((u * w).astype(int), 0, w - 1)]


def grain(a, seed):
	"""Soft streaks along a board: a few low sine waves of the position across it."""
	return 0.5 * np.sin(a * 90 + seed) + 0.3 * np.sin(a * 210 + seed * 1.7) + 0.2 * np.sin(a * 470 + seed * 2.3)


def paint(k, c, pos, nrm):
	n = len(pos)
	j = jitter[k]
	x, y, z = pos[:, 0], pos[:, 1], pos[:, 2]
	if c == "plaster":
		col = np.tile(PAL["plaster"], (n, 1))
		col *= (0.86 + 0.14 * np.clip(y / 0.12, 0, 1))[:, None]          # a soft dirty band at the foot of the wall
		col *= (1.0 + 0.015 * grain(y * 3 + x * 2, k))[:, None]
	elif c in ("plank", "wood", "roof_wood", "broom"):
		base = PAL[c][pick[k] % 3] if c == "plank" else PAL[c]
		lo = P[np.unique(T[piece_of == k])].min(0); hi = P[np.unique(T[piece_of == k])].max(0)
		axis = int(np.argmax(hi - lo))                                   # the board runs along its longest side
		across = [a for a in range(3) if a != axis]
		col = np.tile(base * j, (n, 1))
		col *= (1.0 + 0.035 * grain(pos[:, across[0]] + pos[:, across[1]], k))[:, None]
	elif c == "roof_sheet":
		# one rust-red: lighter crests and darker troughs of the corrugation, which runs down the slope (along z)
		col = np.tile(PAL["roof_sheet"] * j, (n, 1))
		col *= (1.0 + 0.06 * np.sin(x * 2 * np.pi / 0.011))[:, None]
	elif c == "floor":
		col = np.tile(PAL["floor"][pick[k] % 3], (n, 1))
	elif c == "product":
		col = np.tile(PAL["product"][pick[k] % len(PAL["product"])] * j, (n, 1))
	elif c == "bottle":
		col = np.tile(PAL["bottle"][pick[k] % 2], (n, 1))
	elif c == "sign":
		col = np.tile(PAL["sign_edge"], (n, 1))
		front = nrm[:, 2] > 0.7
		u = (x - slo[0]) / (shi[0] - slo[0]); v = (shi[1] - y) / (shi[1] - slo[1])
		col[front] = sample(SIGN, u[front], v[front])
	else:
		col = np.tile(PAL[c] * j, (n, 1))
	if c in ("plaster", "wood", "plank"):
		on = (nrm[:, 2] > 0.7) & (x > PX[0]) & (x < PX[1]) & (y > PY[0]) & (y < PY[1]) & (z > 0.15)
		if on.any():
			col[on] = sample(POSTER, (x[on] - PX[0]) / (PX[1] - PX[0]), (PY[1] - y[on]) / (PY[1] - PY[0]))
	return col


# rasterise every triangle into the UV square: each texel learns its piece, 3D position and normal
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
	eps = 0.7 / max(np.abs(uv.max(0) - uv.min(0)).max(), 1.0)          # cover the triangle's edge texels too
	m = (w0 > -eps) & (w1 > -eps) & (w2 > -eps)
	if not m.any():
		continue
	W = np.stack([w0[m], w1[m], w2[m]], 1)
	W = np.clip(W, 0, None); W /= W.sum(1, keepdims=True)
	pos = W @ P[T[t]]
	img[gy[m], gx[m]] = paint(k, c, pos, np.tile(face_n[t], (len(pos), 1)))
	filled[gy[m], gx[m]] = True

# fill the unused texels from their painted neighbours (so nothing else bleeds in at a distance or in the mipmaps)
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
