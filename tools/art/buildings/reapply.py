# The per-building choices made by looking (after roof_cfg.py): palettes, the Nena store's straight left wall, posts, hand-sorted parts,
# house 1's banana plant out of the roof, house 2 and 3's single-triangle shards and house 3's old roof sheet, which buildings have windows.
import json
PAL={"nena_store":{"wall":"bare","roof":"#A8382F","trim":"#E5E0D5","plywood":"#C9A774"},"vulcanizing":{"wall":"bare","roof":"galv","trim":"#2F5D8A","plywood":"#C9A774"},
 "barber_shop":{"wall":"#8FBFC0","roof":"galv","trim":"#B23A34","plywood":"#C9A774"},"house_1":{"wall":"#E2CC8C","roof":"#3D6FA3","trim":"#7A4E2E","plywood":"#C9A774"},
 "house_2":{"wall":"#E8E2D2","roof":"#9C3B30","trim":"#4F7F5A","plywood":"#D8CBB0"},"house_3":{"wall":"#D99A8C","roof":"galv","trim":"#E5E0D5","plywood":"#6FA0B6"}}
N="config/nena_store_v2_"
for m,pal in PAL.items():
    c=json.load(open(f"{m}/config.json")); c["palette"]=pal; c["shrink"]=0.78
    F=json.load(open(f"{m}/features.json")); byk={f["k"]:f for f in F}; extra=[]
    if m=="nena_store":
        c["roofs"]=[{"type":"gable","x":[-0.48,0.48],"ridge":[0.0,0.88],"eaves":[[0.33,0.70],[-0.33,0.63]],"posts":[[-0.465,0.315,0.67],[0.475,0.315,0.67]]}]
        c["map_from"]={"features":N+"features.json","classes":N+"classes.json","as":{"plaster":"chb","plank":"chb","wood":"lumber","product":"goods","floor":"concrete","fridge":"plastic","fridge_glass":"glass","crate":"plastic","bottle":"glass","broom":"straw","sign":"sign"}}
        region=([-0.50,-0.01,-0.27],[-0.27,0.64,0.205])
        extra=[f["k"] for f in F if all(f["lo"][i]>=region[0][i] and f["hi"][i]<=region[1][i] for i in range(3)) and f["k"] not in c["sign"]]
        c["walls"]=[{"box":[[-0.47,0.0,-0.27],[-0.41,0.63,0.20]]}]
    if m=="barber_shop":
        c["roofs"]=[{"type":"gable","x":[-0.48,0.48],"ridge":[-0.03,0.89],"eaves":[[0.29,0.76],[-0.33,0.63]],"posts":[[-0.465,0.275,0.73],[0.475,0.275,0.73]]}]
    if m in ("house_1","house_2","house_3","barber_shop"): c["windows"]=True
    if m=="house_1": extra=[f["k"] for f in F if 55<f["h"]<170 and f["s"]>0.25 and f["lo"][1]>0.5]
    if m in ("house_2","house_3"): extra=[f["k"] for f in F if f["tris"]<=1 and f["lo"][1]>0.3 and f["area"]>0.0004]
    if m=="house_3": extra.append(271)
    c.pop("poke",None)
    d=set(c["drop"])|set(extra); c["drop"]=sorted(d); c["drop_boxes"]=[[byk[k]["lo"],byk[k]["hi"]] for k in c["drop"]]
    json.dump(c,open(f"{m}/config.json","w"),indent=1)
print("reapplied")
