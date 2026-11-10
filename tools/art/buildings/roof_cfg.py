# Each building's roof (measured from its side and front profiles) and what comes off: the old roof covering and loose framing above the cut
# (signs and plants stay), plus named pieces. Writes <id>/config.json (keeps "sign").
import json, colorsys
SPECS = {
	"nena_store": {"cut": 0.60, "roofs": [{"type": "gable", "x": [-0.48, 0.48], "ridge": [0.0, 0.85], "eaves": [[0.33, 0.63], [-0.33, 0.63]]}]},
	"vulcanizing": {"cut": 0.62, "roofs": [{"type": "gable", "x": [-0.44, 0.43], "ridge": [0.13, 0.87], "eaves": [[0.33, 0.77], [-0.33, 0.69]]}]},
	"barber_shop": {"cut": 0.61, "roofs": [{"type": "gable", "x": [-0.48, 0.48], "ridge": [-0.03, 0.87], "eaves": [[0.29, 0.66], [-0.33, 0.63]]}]},
	"house_1": {"cut": 0.52, "roofs": [{"type": "gable", "x": [-0.47, 0.40], "ridge": [0.12, 0.79], "eaves": [[0.35, 0.56], [-0.41, 0.54]]}]},
	"house_2": {"cut": 0.69, "porch_cut": [0.25, 0.50], "roofs": [
		{"type": "gable", "x": [-0.46, 0.48], "ridge": [0.05, 0.99], "eaves": [[0.35, 0.80], [-0.39, 0.77]]},
		{"type": "shed", "x": [-0.46, 0.48], "high": [0.22, 0.74], "low": [0.47, 0.60], "posts": []}]},
	"house_3": {"cut": 0.88, "roofs": [{"type": "shed", "x": [-0.37, 0.43], "high": [0.40, 1.0], "low": [-0.47, 0.90]}]},
}
def plant(f): return 70 < f["h"] < 170 and f["s"] > 0.25
for m, spec in SPECS.items():
	F = json.load(open(f"{m}/features.json")); cfg = json.load(open(f"{m}/config.json"))
	signs = set(cfg.get("sign", [])); shards = set(cfg.get("drop", []))
	drop = set(shards)
	for f in F:
		if f["k"] in signs or plant(f): continue
		if f["lo"][1] >= spec["cut"]: drop.add(f["k"])
		if "porch_cut" in spec:   # house 2's porch: its plank strips and the tarp under them
			z0, z1 = spec["porch_cut"]; cz = (f["lo"][2] + f["hi"][2]) / 2
			if f["lo"][1] >= 0.5 and z0 <= cz <= z1 and abs(f["n"][1]) > 0.3: drop.add(f["k"])
			if f["lo"][1] >= 0.5 and 200 < f["h"] < 240 and f["s"] > 0.4: drop.add(f["k"])
	byk = {f["k"]: f for f in F}
	cfg["roofs"] = spec["roofs"]; cfg["drop"] = sorted(drop); cfg["drop_boxes"] = [[byk[k]["lo"], byk[k]["hi"]] for k in sorted(drop)]
	cfg["shrink"] = 0.78
	json.dump(cfg, open(f"{m}/config.json", "w"), indent=1)
	print(m, "drop", len(drop), "of", len(F), "tris", sum(byk[k]["tris"] for k in drop))
