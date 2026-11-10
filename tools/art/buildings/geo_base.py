# Weld/clean (no drop, no remap) to get a stable base mesh to measure.
import bpy, bmesh, sys
src, out = sys.argv[sys.argv.index("--")+1:][:2]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
ob=[o for o in bpy.context.scene.objects if o.type=="MESH"][0]; me=ob.data
bm=bmesh.new(); bm.from_mesh(me)
bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
bmesh.ops.dissolve_degenerate(bm, edges=bm.edges, dist=1e-6)
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
bm.to_mesh(me); bm.free(); me.update()
bpy.ops.export_scene.gltf(filepath=out, export_format="GLB", export_normals=True, export_texcoords=True, export_materials="EXPORT", export_image_format="JPEG", export_jpeg_quality=95, export_yup=True, export_tangents=False, export_attributes=False, export_cameras=False, export_lights=False, export_animations=False)
