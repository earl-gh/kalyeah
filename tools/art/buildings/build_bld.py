# A building's mesh: weld, clean, drop the floating shards (by their boxes), hard edges over 30 deg, and its new picture.
# usage: blender python build_bld.py -- src.glb config.json picture_named_prop_base_color.png out.glb
import bpy, bmesh, sys, math, json
src, cfg_path, tex, out = sys.argv[sys.argv.index("--")+1:][:4]
cfg = json.load(open(cfg_path))
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
ob = [o for o in bpy.context.scene.objects if o.type == "MESH"][0]; me = ob.data
bm = bmesh.new(); bm.from_mesh(me)
bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
bmesh.ops.dissolve_degenerate(bm, edges=bm.edges, dist=1e-6)
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
# glTF (x, y, z) is Blender (x, -z, y)
boxes = [((b[0][0], -b[1][2], b[0][1]), (b[1][0], -b[0][2], b[1][1])) for b in cfg.get("drop_boxes", [])]
bm.verts.ensure_lookup_table(); seen = set(); drop = []
for v in bm.verts:
	if v.index in seen: continue
	comp = []; stack = [v]
	while stack:
		x = stack.pop()
		if x.index in seen: continue
		seen.add(x.index); comp.append(x); stack.extend(e.other_vert(x) for e in x.link_edges)
	lo = [min(c.co[i] for c in comp) for i in range(3)]; hi = [max(c.co[i] for c in comp) for i in range(3)]
	for blo, bhi in boxes:
		if all(abs(lo[i] - blo[i]) < 0.002 and abs(hi[i] - bhi[i]) < 0.002 for i in range(3)):
			drop.extend(comp); break
bmesh.ops.delete(bm, geom=drop, context="VERTS")
for e in bm.edges: e.smooth = not (len(e.link_faces) == 2 and e.calc_face_angle(0) > math.radians(30))
for f in bm.faces: f.smooth = True
print("dropped", len(drop), "verts; tris", sum(len(f.verts) - 2 for f in bm.faces))
bm.to_mesh(me); bm.free(); me.update()
mat = ob.material_slots[0].material
node = [n for n in mat.node_tree.nodes if n.type == "TEX_IMAGE"][0]
old = node.image; name = old.name; new = bpy.data.images.load(tex); new.pack(); old.name = name + "_old"; new.name = name; node.image = new
bpy.ops.export_scene.gltf(filepath=out, export_format="GLB", export_normals=True, export_texcoords=True, export_materials="EXPORT",
	export_image_format="JPEG", export_jpeg_quality=92, export_yup=True, export_tangents=False, export_attributes=False,
	export_cameras=False, export_lights=False, export_animations=False)
