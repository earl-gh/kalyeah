# Pieces that would poke up through the new roof (old beams and slats higher than the sheet where they stand) come off too.
import sys, json
m = sys.argv[1]; p = f"{m}/config.json"; c = json.load(open(p)); F = json.load(open(f"{m}/features.json")); byk = {f["k"]: f for f in F}
def roof_y(spec, z):
	if spec["type"] == "gable":
		rz, ry = spec["ridge"]; (fz, fy), (bz, by) = spec["eaves"]
		if z >= rz: return ry + (fy - ry) * (z - rz) / (fz - rz)
		return ry + (by - ry) * (z - rz) / (bz - rz)
	(hz, hy), (lz, ly) = spec["high"], spec["low"]
	return hy + (ly - hy) * (z - hz) / (lz - hz)
prev = set(c.get("poke", [])); drop = set(c["drop"]) - prev; base = set(drop); signs = set(c.get("sign", [])); n = 0
for f in F:
	if f["k"] in drop or f["k"] in signs or (55 < f["h"] < 170 and f["s"] > 0.25): continue
	for spec in c.get("roofs", []):
		x0, x1 = spec["x"]
		if f["hi"][0] < x0 or f["lo"][0] > x1: continue
		zs = [e[0] for e in spec["eaves"]] if spec["type"] == "gable" else [spec["high"][0], spec["low"][0]]
		if f["lo"][2] < min(zs) - 0.02 or f["hi"][2] > max(zs) + 0.02: continue
		over = max(f["hi"][1] - roof_y(spec, z) for z in (f["lo"][2], f["hi"][2], (f["lo"][2] + f["hi"][2]) / 2))
		if over > 0.01:
			drop.add(f["k"]); n += 1; break
c["poke"] = sorted(drop - base); c["drop"] = sorted(drop); c["drop_boxes"] = [[byk[k]["lo"], byk[k]["hi"]] for k in c["drop"]]
json.dump(c, open(p, "w"), indent=1); print(m, "poking through", n)
