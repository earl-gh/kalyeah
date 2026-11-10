# Floating shards: assemblies (pieces whose boxes touch within GAP) that do not reach the ground assembly and have 3 triangles or fewer.
import sys, json, numpy as np
F=json.load(open(sys.argv[1])); GAP=0.004
n=len(F); lo=np.array([f["lo"] for f in F]); hi=np.array([f["hi"] for f in F]); par=list(range(n))
def r(x):
    while par[x]!=x: par[x]=par[par[x]]; x=par[x]
    return x
for i in range(n):
    for j in np.nonzero(np.all((lo[i]-GAP<=hi)&(hi[i]+GAP>=lo),axis=1))[0]:
        if j>i: par[r(j)]=r(i)
groups={}
for i in range(n): groups.setdefault(r(i),[]).append(i)
drop=[F[i]["k"] for g,m in groups.items() if lo[m,1].min()>=0.02 and sum(F[i]["tris"] for i in m)<=3 for i in m]
cfg=json.load(open(sys.argv[2])); cfg["drop"]=drop; cfg["drop_boxes"]=[[F[k]["lo"],F[k]["hi"]] for k in drop]
json.dump(cfg,open(sys.argv[2],"w"),indent=1); print(sys.argv[1], "drop", drop)
