# Classify every piece of the Nena store into one material, by where it is and what it was (features.json from features.py).
# usage: classify.py features.json out_classes.json
import sys, json, colorsys

F = json.load(open(sys.argv[1]))


def inside(f, x=None, y=None, z=None):
	for axis, rng in enumerate((x, y, z)):
		if rng is None:
			continue
		if f["lo"][axis] < rng[0] - 0.005 or f["hi"][axis] > rng[1] + 0.005:
			return False
	return True


def size(f):
	return [f["hi"][i] - f["lo"][i] for i in range(3)]


def red(f): return (f["h"] < 15 or f["h"] > 340) and f["s"] > 0.45
def blue(f): return 190 < f["h"] < 260 and f["s"] > 0.2


def classify(f):
	sx, sy, sz = size(f)
	# removed: the slatted pallet that floats over the ridge and the upright sheet that props it
	if f["lo"][1] > 0.83 and f["hi"][1] > 0.9:
		return "remove"
	if f["lo"][2] > 0.26 and f["lo"][1] > 0.5 and sx > 0.6:
		return "sign"
	# roof: the corrugated sheets (long, thin, sloped) and the wood under them
	if f["lo"][1] > 0.6 or (f["hi"][1] > 0.75 and f["lo"][1] > 0.55):
		# the sheets were painted red or left galvanised grey; the rafters, purlins and fascia are brown wood
		if red(f) or f["s"] < 0.18 or (f["h"] < 15 or f["h"] > 330):
			return "roof_sheet"
		return "roof_wood"
	# the ground slab and its pavers
	if f["hi"][1] < 0.06 and sx * sz > 0.004:
		return "floor"
	# the fridge (left of the door): red body, everything else in it glass
	if inside(f, x=(-0.27, -0.13), y=(0.03, 0.26), z=(0.15, 0.29)):
		return "fridge" if red(f) or f["s"] > 0.45 else "fridge_glass"
	# the crate of bottles
	if inside(f, x=(-0.13, -0.02), y=(0.03, 0.18), z=(0.15, 0.27)):
		return "crate" if blue(f) and f["s"] > 0.45 else "bottle"
	# the snack table on the right: wood legs and top, packs on it
	if inside(f, x=(0.29, 0.47), y=(0.03, 0.2), z=(0.09, 0.25)):
		return "wood" if (15 < f["h"] < 40 and f["s"] > 0.3 and sy < 0.035 and (sx > 0.1 or sz > 0.08 or sy > 0.015)) else "product"
	# the broom leaning on the left
	if inside(f, x=(-0.5, -0.41), y=(0.02, 0.3), z=(0.17, 0.3)):
		return "broom"
	# the sachets hanging from the front beam, and the goods on the shelves
	if inside(f, x=(-0.27, 0.46), y=(0.28, 0.56), z=(0.1, 0.24)) and sy < 0.2 and max(sx, sz) < 0.08:
		return "product"
	if inside(f, x=(-0.27, 0.27), y=(0.06, 0.4), z=(-0.24, 0.16)) and max(sx, sy, sz) < 0.12:
		return "product"
	# the left wall's planks (the outside of the back room)
	if f["hi"][0] < -0.38 and abs(f["n"][0]) > 0.6:
		return "plank"
	# walls: big vertical boards
	if max(sx, sz) > 0.12 and sy > 0.3 and min(sx, sz) < 0.04:
		return "plaster"
	return "wood"


out = {}
for f in F:
	out[f["k"]] = classify(f)
json.dump(out, open(sys.argv[2], "w"))
from collections import Counter
print(Counter(out.values()))
