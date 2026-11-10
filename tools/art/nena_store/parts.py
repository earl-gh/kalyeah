import sys, numpy as np, io
sys.path.insert(0, sys.argv[1]); import glbmesh
from PIL import Image
P,Nn,UV,I,img,j=glbmesh.load(sys.argv[2])
tex=np.asarray(Image.open(io.BytesIO(img)).convert("RGB")).astype(float)
T=I.reshape(-1,3)
# weld by position, union-find over triangles
key=np.round(P/1e-4).astype(np.int64); _,wid=np.unique(key,axis=0,return_inverse=True); wid=wid.ravel()
par=np.arange(wid.max()+1)
def f(x):
    while par[x]!=x: par[x]=par[par[x]]; x=par[x]
    return x
for a,b_,c in wid[T]:
    ra,rb,rc=f(a),f(b_),f(c); par[rb]=ra; par[f(rc)]=ra
comp=np.array([f(w) for w in wid[T[:,0]]])
ids,inv,cnt=np.unique(comp,return_inverse=True,return_counts=True)
print("tris",len(T),"pieces",len(ids))
H,W=tex.shape[:2]
rows=[]
for k in np.argsort(-cnt):
    tri=T[inv==k]; v=np.unique(tri); p=P[v]; lo,hi=p.min(0),p.max(0)
    uvc=UV[tri].mean(1); px=np.clip((uvc*[W,H]).astype(int),0,[W-1,H-1]); col=tex[px[:,1],px[:,0]].mean(0)
    rows.append((k,cnt[k],lo,hi,col))
np.save(sys.argv[3], inv)
for k,c,lo,hi,col in rows[:60]:
    print(f"{k:3d} tris {c:4d} x {lo[0]:+.2f}..{hi[0]:+.2f} y {lo[1]:.2f}..{hi[1]:.2f} z {lo[2]:+.2f}..{hi[2]:+.2f} rgb {col.astype(int)}")
print("pieces with <=4 tris:", (cnt<=4).sum(), "tris in them", cnt[cnt<=4].sum())
