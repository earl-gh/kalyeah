# The sign's band of the picture (bottom strip, as wide as its shape allows) and the shrink of the old layout; for uv_patch.py and paint3.py.
# usage: band.py sign_box.json shrink out.json
import sys, json
SIZE = 2048; BAND_Y = 1737; BAND_X = 20; BAND_HMAX = 303; BAND_WMAX = 2008
box = json.load(open(sys.argv[1])); shrink = float(sys.argv[2])
aspect = (box["x1"] - box["x0"]) / (box["y1"] - box["y0"])
bh = BAND_HMAX; bw = int(round(bh * aspect))
if bw > BAND_WMAX - BAND_X: bw = BAND_WMAX - BAND_X; bh = int(round(bw / aspect))
json.dump({"u0": BAND_X / SIZE, "v0": BAND_Y / SIZE, "w": bw / SIZE, "h": bh / SIZE, "shrink": shrink, **box}, open(sys.argv[3], "w"))
print("band", bw, bh)
