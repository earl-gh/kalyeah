# Moves a built GLB's UVs in place: everything shrinks to the picture's top-left (band["shrink"]), and the sign's face (the faces of the
# sign's pieces that turn to the street, the front half of the board) maps onto its band by x and y. Same bytes, same size.
# usage: uv_patch.py <dir> model.glb band.json sign_piece_boxes.json
import sys, json, struct, numpy as np
sys.path.insert(0, sys.argv[1]); import glbmesh
p = sys.argv[2]; band = json.load(open(sys.argv[3])); boxes = json.load(open(sys.argv[4]))
b = bytearray(open(p, "rb").read()); jl = struct.unpack("<I", b[12:16])[0]; j = json.loads(b[20:20 + jl]); o = 20 + jl + 8
pr = j["meshes"][0]["primitives"][0]; acc = j["accessors"][pr["attributes"]["TEXCOORD_0"]]; bv = j["bufferViews"][acc["bufferView"]]
start = o + bv.get("byteOffset", 0) + acc.get("byteOffset", 0); stride = bv.get("byteStride", 8)
assert stride == 8 and acc["componentType"] == 5126
uv = np.frombuffer(bytes(b[start:start + acc["count"] * 8]), np.float32).reshape(-1, 2).copy()
P, _n, _uv, I, _img, _ = glbmesh.load(p); T = I.reshape(-1, 3)
# the sign's pieces: corners joined by position and shared triangles, matched to the given boxes
key = np.round(P / 1e-4).astype(np.int64); _, wid = np.unique(key, axis=0, return_inverse=True); wid = wid.ravel()
par = np.arange(wid.max() + 1)
def f(x):
	while par[x] != x: par[x] = par[par[x]]; x = par[x]
	return x
for a_, b_, c_ in wid[T]:
	ra = f(a_); par[f(b_)] = ra; par[f(c_)] = ra
comp = np.array([f(w) for w in wid[T[:, 0]]])
a, bb, cc = P[T[:, 0]], P[T[:, 1]], P[T[:, 2]]
cr = np.cross(bb - a, cc - a); nz = np.abs(cr[:, 2]) / np.maximum(np.linalg.norm(cr, axis=1), 1e-12)
cz = (a[:, 2] + bb[:, 2] + cc[:, 2]) / 3
front = np.zeros(len(T), bool); found = 0
for c in np.unique(comp):
	tri = comp == c; pts = P[np.unique(T[tri])]; lo, hi = pts.min(0), pts.max(0)
	for bx in boxes:
		if np.abs(lo - bx[0]).max() < 0.003 and np.abs(hi - bx[1]).max() < 0.003:
			found += 1
			# a board with no thickness has its face and its mirror on one plane: both read the right way from the street;
			# a thick board's face is its front half
			if hi[2] - lo[2] < 0.012:
				front |= tri & (nz > 0.5)
			else:
				front |= tri & (nz > 0.5) & (cz >= (lo[2] + hi[2]) / 2)
assert found == len(boxes), (found, len(boxes))
# a face on the sign's plane that shares a corner with it (the frame's front) moves with it, so no triangle is torn between two places
for _ in range(4):
	touch = np.isin(T, np.unique(T[front])).any(1) & ~front & (nz > 0.5)
	if not touch.any(): break
	front |= touch
fv = np.unique(T[front]); other = np.unique(T[~front])
shared = np.intersect1d(fv, other)
print("sign faces", front.sum(), "corners", len(fv), "shared with other faces", len(shared))
uv *= band["shrink"]
x, y = P[fv, 0], P[fv, 1]
uv[fv, 0] = band["u0"] + (x - band["x0"]) / (band["x1"] - band["x0"]) * band["w"]
uv[fv, 1] = band["v0"] + (band["y1"] - y) / (band["y1"] - band["y0"]) * band["h"]
b[start:start + acc["count"] * 8] = uv.astype(np.float32).tobytes()
# keep the accessor's min/max honest
if "min" in acc: acc["min"] = uv.min(0).tolist(); acc["max"] = uv.max(0).tolist()
js = json.dumps(j, separators=(",", ":")).encode(); js += b" " * ((4 - len(js) % 4) % 4)
out = bytearray(b[:12]) + struct.pack("<II", len(js), 0x4E4F534A) + js + b[20 + jl:]
struct.pack_into("<I", out, 8, len(out))
open(p, "wb").write(out)
