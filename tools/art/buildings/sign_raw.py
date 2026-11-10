# Read a sign's front (the faces of the given pieces that face the street, +Z or -Z wound, at the pieces' front) from the model's own
# picture through its own UVs, at width W: one image laid out by the faces' x and y.
# usage: sign_raw.py <dir> geo.glb piece.npy "id,id" W out.png
import sys, io, numpy as np
sys.path.insert(0, sys.argv[1]); import glbmesh
from PIL import Image
P, _n, UV, I, img, _j = glbmesh.load(sys.argv[2]); T = I.reshape(-1, 3); piece = np.load(sys.argv[3])
ids = [int(x) for x in sys.argv[4].split(",")]; W = int(sys.argv[5])
old = np.asarray(Image.open(io.BytesIO(img)).convert("RGB"), float)
sel = np.isin(piece, ids)
a, b, c = P[T[:, 0]], P[T[:, 1]], P[T[:, 2]]
cr = np.cross(b - a, c - a); ar = np.linalg.norm(cr, axis=1) / 2; nz = np.abs(cr[:, 2]) / np.maximum(2 * ar, 1e-12)
# the faces turned toward the street (either winding); at each point of the sign the one nearest the street is the one you see
front = np.nonzero(sel & (nz > 0.5))[0]
pts = P[np.unique(T[front])]
x0, x1, y0, y1 = pts[:, 0].min(), pts[:, 0].max(), pts[:, 1].min(), pts[:, 1].max()
front_z = pts[:, 2].max()
H = max(8, int(W * (y1 - y0) / (x1 - x0)))
gx, gy = np.meshgrid(x0 + (np.arange(W) + 0.5) / W * (x1 - x0), y1 - (np.arange(H) + 0.5) / H * (y1 - y0))
uv = np.zeros((H, W, 2)); done = np.zeros((H, W), bool); best = np.full((H, W), -1e9)
for t in T[front]:
	A, Bv, C = P[t][:, :2]
	den = (Bv[1] - C[1]) * (A[0] - C[0]) + (C[0] - Bv[0]) * (A[1] - C[1])
	if abs(den) < 1e-12: continue
	w0 = ((Bv[1] - C[1]) * (gx - C[0]) + (C[0] - Bv[0]) * (gy - C[1])) / den
	w1 = ((C[1] - A[1]) * (gx - C[0]) + (A[0] - C[0]) * (gy - C[1])) / den
	w2 = 1 - w0 - w1
	z = w0 * P[t[0], 2] + w1 * P[t[1], 2] + w2 * P[t[2], 2]
	m = (w0 >= -1e-6) & (w1 >= -1e-6) & (w2 >= -1e-6) & (z > best)
	uv[m] = w0[m, None] * UV[t[0]] + w1[m, None] * UV[t[1]] + w2[m, None] * UV[t[2]]; done |= m; best[m] = z[m]
h, w = old.shape[:2]
px = uv[..., 0] * w - 0.5; py = uv[..., 1] * h - 0.5
ix = np.clip(np.floor(px).astype(int), 0, w - 2); iy = np.clip(np.floor(py).astype(int), 0, h - 2)
fx = np.clip(px - ix, 0, 1)[..., None]; fy = np.clip(py - iy, 0, 1)[..., None]
raw = old[iy, ix] * (1 - fx) * (1 - fy) + old[iy, ix + 1] * fx * (1 - fy) + old[iy + 1, ix] * (1 - fx) * fy + old[iy + 1, ix + 1] * fx * fy
raw[~done] = 0
Image.fromarray(raw.clip(0, 255).astype(np.uint8)).save(sys.argv[6])
import json as _json
_json.dump({"x0": float(x0), "x1": float(x1), "y0": float(y0), "y1": float(y1)}, open(sys.argv[6].rsplit(".", 1)[0] + "_box.json", "w"))
print("front faces", len(front), "box", round(x0, 3), round(x1, 3), round(y0, 3), round(y1, 3), "z", round(front_z, 3), "size", W, H, "covered", round(done.mean(), 3))
