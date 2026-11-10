# a model with no sign: its UVs only shrink to the top-left (in place)
import sys, json, struct, numpy as np
p = sys.argv[1]; shrink = float(sys.argv[2])
b = bytearray(open(p, "rb").read()); jl = struct.unpack("<I", b[12:16])[0]; j = json.loads(b[20:20 + jl]); o = 20 + jl + 8
pr = j["meshes"][0]["primitives"][0]; acc = j["accessors"][pr["attributes"]["TEXCOORD_0"]]; bv = j["bufferViews"][acc["bufferView"]]
start = o + bv.get("byteOffset", 0) + acc.get("byteOffset", 0)
uv = np.frombuffer(bytes(b[start:start + acc["count"] * 8]), np.float32).reshape(-1, 2) * shrink
b[start:start + acc["count"] * 8] = uv.astype(np.float32).tobytes()
if "min" in acc: acc["min"] = uv.min(0).tolist(); acc["max"] = uv.max(0).tolist()
js = json.dumps(j, separators=(",", ":")).encode(); js += b" " * ((4 - len(js) % 4) % 4)
out = bytearray(b[:12]) + struct.pack("<II", len(js), 0x4E4F534A) + js + b[20 + jl:]
struct.pack_into("<I", out, 8, len(out)); open(p, "wb").write(out)
print("shrunk", p)
