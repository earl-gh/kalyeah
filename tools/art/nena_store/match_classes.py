# Carry each piece's class over to the same piece of a rebuilt model (matched by its box; only the UVs changed).
import sys, json, numpy as np
old = json.load(open(sys.argv[1])); oc = json.load(open(sys.argv[2])); new = json.load(open(sys.argv[3]))
keys = np.array([o["lo"] + o["hi"] for o in old]); out = {}
for f in new:
    d = np.abs(keys - np.array(f["lo"] + f["hi"])).max(1); i = int(d.argmin())
    assert d[i] < 0.002, (f["k"], d[i])
    out[f["k"]] = oc[str(old[i]["k"])]
json.dump(out, open(sys.argv[4], "w")); from collections import Counter; print(Counter(out.values()))
