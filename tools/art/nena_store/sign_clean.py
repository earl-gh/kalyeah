# The store's sign as the owner's model had it (the red board, the white wave and "Nena Store"), cleaned: read through the sign face's own
# UVs from the model's picture at the size it is drawn now, then its specks, grime and the wood bleeding in at its edges taken out, and
# every texel set to the board's red or the lettering's white, with smooth edges.
# usage: sign_clean.py <dir with glbmesh.py> model_with_old_uvs.glb old_picture.png width height raw_out.png clean_out.png
import sys, numpy as np
sys.path.insert(0, sys.argv[1]); import glbmesh
from PIL import Image, ImageFilter

P, _n, UV, I, _img, _j = glbmesh.load(sys.argv[2])
old = np.asarray(Image.open(sys.argv[3]).convert("RGB"), float)
W, H = int(sys.argv[4]), int(sys.argv[5])
T = I.reshape(-1, 3)
a, b, c = P[T[:, 0]], P[T[:, 1]], P[T[:, 2]]
cr = np.cross(b - a, c - a); area = np.linalg.norm(cr, axis=1) / 2; nz = cr[:, 2] / np.maximum(2 * area, 1e-12)
front = T[(area > 0.02) & (nz > 0.9) & ((a[:, 2] + b[:, 2] + c[:, 2]) / 3 > 0.26) & ((a[:, 1] + b[:, 1] + c[:, 1]) / 3 > 0.5)]
assert len(front) == 2, len(front)
pts = P[np.unique(front)]
x0, x1, y0, y1 = pts[:, 0].min(), pts[:, 0].max(), pts[:, 1].min(), pts[:, 1].max()
gx, gy = np.meshgrid(x0 + (np.arange(W) + 0.5) / W * (x1 - x0), y1 - (np.arange(H) + 0.5) / H * (y1 - y0))
uv = np.zeros((H, W, 2)); done = np.zeros((H, W), bool)
for t in front:
	A, B, C = P[t][:, :2]
	den = (B[1] - C[1]) * (A[0] - C[0]) + (C[0] - B[0]) * (A[1] - C[1])
	w0 = ((B[1] - C[1]) * (gx - C[0]) + (C[0] - B[0]) * (gy - C[1])) / den
	w1 = ((C[1] - A[1]) * (gx - C[0]) + (A[0] - C[0]) * (gy - C[1])) / den
	w2 = 1 - w0 - w1
	m = (w0 >= -1e-6) & (w1 >= -1e-6) & (w2 >= -1e-6) & ~done
	uv[m] = w0[m, None] * UV[t[0]] + w1[m, None] * UV[t[1]] + w2[m, None] * UV[t[2]]
	done |= m
# bilinear read of the old picture
h, w = old.shape[:2]
px = uv[..., 0] * w - 0.5; py = uv[..., 1] * h - 0.5
ix = np.clip(np.floor(px).astype(int), 0, w - 2); iy = np.clip(np.floor(py).astype(int), 0, h - 2)
fx = np.clip(px - ix, 0, 1)[..., None]; fy = np.clip(py - iy, 0, 1)[..., None]
raw = (old[iy, ix] * (1 - fx) * (1 - fy) + old[iy, ix + 1] * fx * (1 - fy) + old[iy + 1, ix] * (1 - fx) * fy + old[iy + 1, ix + 1] * fx * fy)
Image.fromarray(raw.clip(0, 255).astype(np.uint8)).save(sys.argv[6])

# white is bright and grey (lettering, wave); red is the board; anything else (grime, wood) is board. Specks smaller than a stroke go.
lum = raw.mean(2); sat = raw.max(2) - raw.min(2)
white = ((lum > 150) & (sat < 90)).astype(np.uint8) * 255
scale = W / 288.0                                     # texels of the old sign per new texel
mask = Image.fromarray(white).filter(ImageFilter.MedianFilter(int(scale * 2) | 1))
mask = mask.filter(ImageFilter.GaussianBlur(scale * 0.9))
m = np.asarray(mask, float) / 255.0
m = np.clip((m - 0.5) * 6 + 0.5, 0, 1)[..., None]      # a crisp edge, one or two texels soft
board = np.median(raw[(lum < 150) & (raw[..., 0] > raw[..., 1] + 60)], axis=0)
ink = np.median(raw[white > 0], axis=0)
ink = np.maximum(ink, [238, 236, 230])
clean = board * (1 - m) + ink * m
Image.fromarray(clean.clip(0, 255).astype(np.uint8)).save(sys.argv[7])
print("board", board.astype(int), "ink", ink.astype(int), "size", W, H)
