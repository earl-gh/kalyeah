# What every piece of a building is made of (owner, 2026-11-10: "ask what this material is, for everything that builds the building: realistic
# for the Philippines"). Each piece of the owner's model gets one material from a short list of what a Philippine street building is built
# from, judged by its shape, where it is and what its old picture showed (only to tell parts apart; nothing of the old picture is painted):
#   chb       cement hollow-block wall (ground storey), bare or painted        concrete  slab, step, kerb, footing
#   plywood   wall above the ground storey, door, shelf, counter, panel       lumber    coco lumber post, beam, rafter, plank, bench, fence slat
#   gi        galvanized iron sheet (roof, wall sheeting)                      steel     rims, tools, pipes, frames, hooks
#   rubber    tyres, hoses, black parts                                         plastic   drums, basins, crates, chairs, goods, clothes (one colour each)
#   tarp      big thin coloured sheets                                          plant     leaves, stems         terracotta  clay pots
#   stone     pebbles, rubble                                                   poster    paper on a wall       hazard   yellow-and-black painted bands
#   sign      the building's own sign (cleaned, its own band of the picture)
# usage: materials.py features.json config.json out.json
import sys, json

F = json.load(open(sys.argv[1])); CFG = json.load(open(sys.argv[2]))
H = max(f["hi"][1] for f in F)
OV = {int(k): v for k, v in CFG.get("materials", {}).items()}
# a building whose parts were already sorted by hand (the Nena store, tools/art/nena_store/classify.py) takes those, matched by box
if "map_from" in CFG:
	import os
	here = os.path.dirname(os.path.abspath(__file__))
	def at(path): return path if os.path.isabs(path) else os.path.join(here, path)
	old_f = json.load(open(at(CFG["map_from"]["features"]))); old_c = json.load(open(at(CFG["map_from"]["classes"])))
	conv = CFG["map_from"]["as"]
	for f in F:
		for o in old_f:
			if all(abs(f["lo"][i] - o["lo"][i]) < 0.002 and abs(f["hi"][i] - o["hi"][i]) < 0.002 for i in range(3)):
				c = old_c[str(o["k"])]
				if c in conv and f["k"] not in OV: OV[f["k"]] = conv[c]
				break
SIGNS = set(CFG.get("sign", []))
DROPPED = set(CFG.get("drop", []))


def pick(f):
	k = f["k"]
	if k in OV: return OV[k]
	if k in SIGNS: return "sign"
	s = sorted(f["hi"][i] - f["lo"][i] for i in range(3))
	h, sat, lum = f["h"], f["s"], f["l"]
	ny = abs(f["n"][1]); cy = (f["lo"][1] + f["hi"][1]) / 2
	area = f["area"]
	long_ = s[2] > 2.5 * s[1]
	thin = s[0] < 0.025
	woodish = 12 < h < 42 and 0.18 < sat < 0.75 and 0.15 < lum < 0.6
	green = 55 < h < 170 and sat > 0.25 and lum > 0.15
	upright = f["hi"][1] - f["lo"][1] >= s[2] - 1e-6
	yellow = 40 <= h < 65 and sat > 0.45 and lum > 0.35
	dark = lum < 0.17
	dull = sat < 0.22
	# the ground: slabs, steps, kerbs, footings
	if f["hi"][1] < 0.08 * H and s[1] * s[2] > 0.004 and not green:
		return "hazard" if yellow else ("stone" if s[2] < 0.05 else "concrete")
	if f["hi"][1] < 0.06 * H and s[2] < 0.08 and sat < 0.3:
		return "stone"
	if green and not woodish:
		return "plant"
	if dark and s[2] > 0.03:
		return "rubber"
	# a tyre: round (two of its sizes alike), dull and darkish
	if dull and lum < 0.33 and s[2] > 0.04 and s[2] < 1.6 * s[1]:
		return "rubber"
	# painted bands: a low kerb in yellow and black stripes; a post with a painted foot
	if woodish and f.get("lstd", 0) > 40 and f.get("yfrac", 0) > 0.1 and long_ and not upright and f["hi"][1] < 0.15 * H:
		return "hazard"
	if woodish and f.get("lstd", 0) > 33 and long_ and upright:
		return "lumber_band"
	# walls: big, upright, thin; ground storey hollow blocks, above it plywood
	if ny < 0.35 and s[0] < 0.1 and s[1] > 0.12 and sat < 0.45 and not (woodish and sat > 0.35 and long_):
		return "chb" if f["lo"][1] < 0.2 * H else "plywood"
	if area > 0.3 and dull:
		return "shell"                     # a whole storey in one piece: its upright faces are walls, the rest slab
	if woodish and (long_ or thin):
		return "lumber" if long_ or s[1] < 0.06 else "plywood"
	if woodish:
		return "lumber"
	if 5 <= h <= 25 and sat > 0.45 and 0.3 < lum < 0.6 and s[2] < 0.12:
		return "terracotta"
	if yellow and s[2] > 0.1:
		return "hazard"
	if thin and s[1] > 0.15 and sat > 0.35:
		return "tarp"
	if thin and dull and lum > 0.55 and s[1] > 0.03 and ny < 0.4:
		return "poster"
	if dull and 0.25 < lum < 0.7:
		return "steel"
	if dull and lum >= 0.7:
		return "plastic"
	return "plastic"


out = {}
for f in F:
	if f["k"] in DROPPED: continue
	out[str(f["k"])] = pick(f)
json.dump(out, open(sys.argv[3], "w"))
from collections import Counter
print(sys.argv[1], dict(Counter(out.values())))
