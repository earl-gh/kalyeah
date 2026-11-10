# Read the one mesh of a GLB into numpy: positions, normals, uvs, indices, and the embedded image bytes.
import json, struct, numpy as np
CT={5126:np.float32,5123:np.uint16,5125:np.uint32,5121:np.uint8}; NC={"SCALAR":1,"VEC2":2,"VEC3":3,"VEC4":4}
def load(p):
    b=open(p,"rb").read(); jl=struct.unpack("<I",b[12:16])[0]; j=json.loads(b[20:20+jl]); o=20+jl+8
    def acc(i):
        a=j["accessors"][i]; bv=j["bufferViews"][a["bufferView"]]; s=o+bv.get("byteOffset",0)+a.get("byteOffset",0)
        n=a["count"]*NC[a["type"]]; return np.frombuffer(b,CT[a["componentType"]],n,s).reshape(a["count"],-1).copy()
    pr=j["meshes"][0]["primitives"][0]; at=pr["attributes"]
    bv=j["bufferViews"][j["images"][0]["bufferView"]]; s=o+bv.get("byteOffset",0)
    return acc(at["POSITION"]), acc(at["NORMAL"]), acc(at["TEXCOORD_0"]), acc(pr["indices"]).ravel(), b[s:s+bv["byteLength"]], j
