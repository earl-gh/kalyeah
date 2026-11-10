# Nena store geometry: weld, clean, drop the pallet floating over the ridge (and the upright sheet holding it), hard edges over 30 deg.
import bpy, bmesh, sys, math
src, tex, out = sys.argv[sys.argv.index("--")+1:][:3]
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
bm.to_mesh(me); bm.free(); me.update()
mat=ob.material_slots[0].material
node=[n for n in mat.node_tree.nodes if n.type=="TEX_IMAGE"][0]
old=node.image; name=old.name; new=bpy.data.images.load(tex); new.pack(); old.name=name+"_old"; new.name=name; node.image=new
bpy.ops.export_scene.gltf(filepath=out, export_format="GLB", export_normals=True, export_texcoords=True, export_materials="EXPORT",
    export_image_format="JPEG", export_jpeg_quality=92, export_yup=True, export_tangents=False, export_attributes=False,
    export_cameras=False, export_lights=False, export_animations=False)
