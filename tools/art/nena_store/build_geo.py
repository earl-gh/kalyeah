# Nena store geometry: weld, clean, drop the pallet floating over the ridge (and the upright sheet holding it), hard edges over 30 deg.
import bpy, bmesh, sys, math
SHRINK = 0.84
args = sys.argv[sys.argv.index("--")+1:]
src, tex, out = args[:3]
remap = "--remap" in args
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
ob=[o for o in bpy.context.scene.objects if o.type=="MESH"][0]; me=ob.data
bm=bmesh.new(); bm.from_mesh(me)
bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
bmesh.ops.dissolve_degenerate(bm, edges=bm.edges, dist=1e-6)
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
bm.verts.ensure_lookup_table()
seen=set(); drop=[]
for v in bm.verts:
    if v.index in seen: continue
    comp=[]; stack=[v]
    while stack:
        x=stack.pop()
        if x.index in seen: continue
        seen.add(x.index); comp.append(x); stack.extend(e.other_vert(x) for e in x.link_edges)
    zs=[c.co.z for c in comp]
    if min(zs) > 0.83 and max(zs) > 0.9:
        drop.extend(comp)
print("dropped verts", len(drop))
bmesh.ops.delete(bm, geom=drop, context="VERTS")
for e in bm.edges: e.smooth = not (len(e.link_faces)==2 and e.calc_face_angle(0) > math.radians(30))
for f in bm.faces: f.smooth=True
print("tris", sum(len(f.verts)-2 for f in bm.faces))
if remap:
    # give the sign's front its own big band of the picture: everything else shrinks to the top-left 84% (painted from 3D, so nothing is lost
    # but a little density), and the sign's face fills the freed band along the bottom (glTF v grows downward; Blender's v = 1 - glTF v)
    uvl = bm.loops.layers.uv.active
    front = [f for f in bm.faces if f.calc_area() > 0.02 and f.normal.y < -0.9 and f.calc_center_median().y < -0.26 and f.calc_center_median().z > 0.5 and f.calc_center_median().z < 0.7]
    fx = [v.co.x for f in front for v in f.verts]; fz = [v.co.z for f in front for v in f.verts]
    x0, x1, z0, z1 = min(fx), max(fx), min(fz), max(fz)
    band_v0, band_h = 0.848, 0.148
    band_w = band_h * 2048 * (x1 - x0) / (z1 - z0) / 2048
    print("sign front faces", len(front), "box", round(x0, 3), round(x1, 3), round(z0, 3), round(z1, 3), "band", round(band_w * 2048), round(band_h * 2048))
    for f in bm.faces:
        for l in f.loops:
            u, v = l[uvl].uv
            l[uvl].uv = (u * SHRINK, 1.0 - (1.0 - v) * SHRINK)
    for f in front:
        for l in f.loops:
            u = 0.01 + (l.vert.co.x - x0) / (x1 - x0) * band_w
            vg = band_v0 + (z1 - l.vert.co.z) / (z1 - z0) * band_h
            l[uvl].uv = (u, 1.0 - vg)
bm.to_mesh(me); bm.free(); me.update()
mat=ob.material_slots[0].material
node=[n for n in mat.node_tree.nodes if n.type=="TEX_IMAGE"][0]
old=node.image; name=old.name; new=bpy.data.images.load(tex); new.pack(); old.name=name+"_old"; new.name=name; node.image=new
bpy.ops.export_scene.gltf(filepath=out, export_format="GLB", export_normals=True, export_texcoords=True, export_materials="EXPORT",
    export_image_format="JPEG", export_jpeg_quality=92, export_yup=True, export_tangents=False, export_attributes=False,
    export_cameras=False, export_lights=False, export_animations=False)
