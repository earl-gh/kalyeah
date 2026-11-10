# The building's picture: the repaint (in the model's own layout, shrunk to the top-left SHRINK of the picture) and its sign in its own band
# along the bottom. Writes the band's place for uv_patch.py.
# usage: compose.py painted.png sign.png sign_box.json out.png band_out.json
import sys, json, numpy as np
from PIL import Image
SIZE = 2048; SHRINK = 1720 / 2048; BAND_Y = 1737; BAND_X = 20; BAND_HMAX = 303; BAND_WMAX = 2008
painted = Image.open(sys.argv[1]).convert("RGB"); sign = Image.open(sys.argv[2]).convert("RGB")
box = json.load(open(sys.argv[3]))
aspect = (box["x1"] - box["x0"]) / (box["y1"] - box["y0"])
bh = BAND_HMAX; bw = int(round(bh * aspect))
if bw > BAND_WMAX - BAND_X: bw = BAND_WMAX - BAND_X; bh = int(round(bw / aspect))
canvas = Image.new("RGB", (SIZE, SIZE))
canvas.paste(painted.resize((1720, 1720), Image.LANCZOS) if painted.size != (1720, 1720) else painted, (0, 0))
# the band: the sign, and its edge colour round it (so mipmaps never pull in a neighbour)
band = Image.new("RGB", (SIZE, SIZE - 1720), tuple(np.asarray(sign).reshape(-1, 3).mean(0).astype(int)))
canvas.paste(band, (0, 1720)); canvas.paste(sign.resize((bw, bh), Image.LANCZOS), (BAND_X, BAND_Y))
canvas.save(sys.argv[4])
json.dump({"u0": BAND_X / SIZE, "v0": BAND_Y / SIZE, "w": bw / SIZE, "h": bh / SIZE, "shrink": SHRINK, **box}, open(sys.argv[5], "w"))
print("band", bw, bh)
