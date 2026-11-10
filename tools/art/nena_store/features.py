# per piece: area share, box, normal, mean colour (texels sampled across each triangle), saturation
import sys, numpy as np, io, json, colorsys
sys.path.insert(0, sys.argv[1]); import glbmesh
from PIL import Image
P,Nn,UV,I,img,j=glbmesh.load(sys.argv[2]); T=I.reshape(-1,3); inv=np.load(sys.argv[3])
tex=np.asarray(Image.open(io.BytesIO(img)).convert("RGB")).astype(float); H,W=tex.shape[:2]
a,b,c=P[T[:,0]],P[T[:,1]],P[T[:,2]]; cr=np.cross(b-a,c-a); ar=np.linalg.norm(cr,axis=1)/2; fn=cr/np.maximum(2*ar,1e-12)[:,None]
bary=np.array([[1/3,1/3,1/3],[.6,.2,.2],[.2,.6,.2],[.2,.2,.6],[.45,.45,.1],[.1,.45,.45],[.45,.1,.45]])
uvs=np.einsum('sk,tkd->tsd',bary,UV[T]); px=np.clip((uvs*[W,H]).astype(int),0,[W-1,H-1]); col=tex[px[...,1],px[...,0]].mean(1)
out=[]
for k in range(inv.max()+1):
    m=inv==k; v=np.unique(T[m]); p=P[v]; A=ar[m].sum()
    n=(fn[m]*ar[m,None]).sum(0); n/=max(np.linalg.norm(n),1e-9)
    cl=(col[m]*ar[m,None]).sum(0)/max(A,1e-12)
    h,l,s=colorsys.rgb_to_hls(*(cl/255))
    out.append(dict(k=k,tris=int(m.sum()),area=float(A),lo=p.min(0).round(3).tolist(),hi=p.max(0).round(3).tolist(),n=n.round(2).tolist(),rgb=cl.astype(int).tolist(),h=round(h*360),l=round(l,2),s=round(s,2)))
json.dump(out,open(sys.argv[4],"w"))
