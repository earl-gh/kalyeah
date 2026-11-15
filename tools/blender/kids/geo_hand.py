# plain grey render (no texture), backfaces red: usage geo_hand.py model.glb hand out.png
import sys, bpy
from mathutils import Vector
src, hand, out = sys.argv[1:4]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE'); arm.data.pose_position = 'REST'
mesh = max((o for o in bpy.data.objects if o.type == 'MESH'), key=lambda o: len(o.data.vertices))
hb = arm.data.bones[hand]; names = {hb.name} | {b.name for b in hb.children_recursive}
pts = [mesh.matrix_world @ v.co for v in mesh.data.vertices if any(mesh.vertex_groups[g.group].name in names and g.weight > 0.5 for g in v.groups)]
ctr = sum(pts, Vector()) / len(pts); ext = sorted((p - ctr).length for p in pts)[int(len(pts) * 0.95)]
mat = bpy.data.materials.new("g"); mat.use_nodes = True; nt = mat.node_tree
bs = nt.nodes["Principled BSDF"]; geo = nt.nodes.new("ShaderNodeNewGeometry"); mix = nt.nodes.new("ShaderNodeMix"); mix.data_type = 'RGBA'
mix.inputs['A'].default_value = (0.7, 0.7, 0.7, 1); mix.inputs['B'].default_value = (1, 0, 0, 1)
nt.links.new(geo.outputs['Backfacing'], mix.inputs['Factor']); nt.links.new(mix.outputs['Result'], bs.inputs['Base Color'])
for o in bpy.data.objects:
    if o.type == 'MESH':
        o.data.materials.clear(); o.data.materials.append(mat)
scn = bpy.context.scene
for loc in [(2, -3, 3), (-2, -3, 2), (0, 3, -2)]:
    l = bpy.data.objects.new("l", bpy.data.lights.new("l", 'SUN')); l.rotation_euler = (Vector((0,0,0)) - Vector(loc)).to_track_quat('-Z','Y').to_euler(); l.data.energy = 2.5; scn.collection.objects.link(l)
scn.render.engine = 'CYCLES'; scn.cycles.samples = 12; scn.render.resolution_x = scn.render.resolution_y = 384
scn.world = bpy.data.worlds.new("w"); scn.world.color = (0.3, 0.3, 0.35)
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); scn.collection.objects.link(cam); scn.camera = cam
for k, dv in enumerate([(0,-1,0.2), (0,1,0.2), (1 if ctr.x>0 else -1,0,0.1), (0,0,-1)]):
    dv = Vector(dv).normalized(); cam.location = ctr + dv * ext * 3.0
    cam.rotation_euler = (ctr - cam.location).to_track_quat('-Z', 'Y').to_euler()
    scn.render.filepath = f"{out}_{k}.png"; bpy.ops.render.render(write_still=True)
