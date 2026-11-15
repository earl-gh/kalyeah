# curl every finger of one hand into a loose fist and render it textured. usage: curl_test.py model.glb hand out.png
import sys, bpy, math
from mathutils import Vector, Matrix
src, hand, out = sys.argv[1:4]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
for a in bpy.data.actions:
    bpy.data.actions.remove(a)
arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
arm.animation_data_clear() if arm.animation_data else None
mesh = max((o for o in bpy.data.objects if o.type == 'MESH'), key=lambda o: len(o.data.vertices))
hb = arm.data.bones[hand]
pts = [mesh.matrix_world @ v.co for v in mesh.data.vertices if any(mesh.vertex_groups[g.group].name in ({hb.name} | {b.name for b in hb.children_recursive}) and g.weight > 0.5 for g in v.groups)]
ctr = sum(pts, Vector()) / len(pts); ext = sorted((p - ctr).length for p in pts)[int(len(pts) * 0.95)]
# palm normal: cross of finger direction and the line across the knuckles
chains = list(hb.children)
AM = arm.matrix_world
bases = [AM @ c.head_local for c in chains]
tips = [AM @ (c.children_recursive[-1] if c.children_recursive else c).tail_local for c in chains]
fdir = (sum(tips, Vector()) / len(tips) - AM @ hb.head_local).normalized()
for c in chains:
    for j, b in enumerate([c] + list(c.children_recursive)):
        pb = arm.pose.bones[b.name]
        # bend toward the palm: axis = bone x (rolls were aligned to the palm); try both signs, keep the one moving toward the thigh
        ang = math.radians(55 if j < 3 else 0)
        pb.rotation_mode = 'XYZ'
        pb.rotation_euler = (ang, 0, 0)
bpy.context.view_layer.update()
scn = bpy.context.scene
for loc in [(2, -3, 3), (-2, -3, 2), (0, 3, -2)]:
    l = bpy.data.objects.new("l", bpy.data.lights.new("l", 'SUN')); l.rotation_euler = (Vector((0,0,0)) - Vector(loc)).to_track_quat('-Z','Y').to_euler(); l.data.energy = 2.5; scn.collection.objects.link(l)
scn.render.engine = 'CYCLES'; scn.cycles.samples = 12; scn.render.resolution_x = scn.render.resolution_y = 384
scn.world = bpy.data.worlds.new("w"); scn.world.color = (0.5, 0.5, 0.5)
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); scn.collection.objects.link(cam); scn.camera = cam
for k, dv in enumerate([(0,-1,0.2), (1 if ctr.x>0 else -1,0,0.1), (0,0,-1)]):
    dv = Vector(dv).normalized(); cam.location = ctr + dv * ext * 3.2
    cam.rotation_euler = (ctr - cam.location).to_track_quat('-Z', 'Y').to_euler()
    scn.render.filepath = f"{out}_{k}.png"; bpy.ops.render.render(write_still=True)
