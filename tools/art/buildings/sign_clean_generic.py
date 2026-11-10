# A sign as its maker painted it, cleaned: every texel goes to the nearest of the sign's named tones (in Lab, so hues stay apart), the
# grime tones (rust, stains) are painted as the board, specks smaller than a stroke vanish, and the tones' edges are smooth.
# usage: sign_clean_generic.py raw.png tones.json out.png
#   tones.json: [{"rgb": [r,g,b], "as": [r,g,b] (optional: what to paint it as)}, ...]
import sys, json, numpy as np
from PIL import Image, ImageFilter


def lab(rgb):
	c = rgb / 255.0
	c = np.where(c > 0.04045, ((c + 0.055) / 1.055) ** 2.4, c / 12.92)
	x = c @ np.array([[0.4124, 0.2126, 0.0193], [0.3576, 0.7152, 0.1192], [0.1805, 0.0722, 0.9505]])
	x = x / np.array([0.9505, 1.0, 1.089])
	f = np.where(x > 0.008856, np.cbrt(x), 7.787 * x + 16 / 116)
	return np.stack([116 * f[..., 1] - 16, 500 * (f[..., 0] - f[..., 1]), 200 * (f[..., 1] - f[..., 2])], -1)


raw = np.asarray(Image.open(sys.argv[1]).convert("RGB"), float)
tones = json.load(open(sys.argv[2]))
H, W = raw.shape[:2]
on = raw.sum(2) > 0
L = lab(raw); T = lab(np.array([t["rgb"] for t in tones], float))
lab_i = np.argmin(((L[..., None, :] - T[None, None]) ** 2).sum(3), 2)
paint = np.array([t.get("as", t["rgb"]) for t in tones], float)
# the same painted colour is one tone
uniq, group = np.unique(paint, axis=0, return_inverse=True)
lab_i = group.reshape(-1)[lab_i.reshape(-1)].reshape(H, W)
s = max(1.5, W / 500)
score = np.zeros((H, W, len(uniq)))
for j in range(len(uniq)):
	m = np.where((lab_i == j) & on, 255, 0).astype(np.uint8)
	m = Image.fromarray(m).filter(ImageFilter.MedianFilter(int(s * 2) | 1)).filter(ImageFilter.GaussianBlur(s))
	score[..., j] = np.asarray(m, float)
out = Image.fromarray(uniq[np.argmax(score, 2)].clip(0, 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.8))
out.save(sys.argv[3]); print("tones", len(uniq), "size", W, H)
