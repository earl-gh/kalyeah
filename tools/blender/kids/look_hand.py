# textured close-up of one hand from 4 sides. usage: look_hand.py model.glb hand_bone out_prefix
import sys, bpy
from mathutils import Vector
src, hand, out = sys.argv[1:4]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
arm.data.pose_position = 'REST'
mesh = max((o for o in bpy.data.objects if o.type == 'MESH'), key=lambda o: len(o.data.vertices))
hb = arm.data.bones[hand]
names = {hb.name} | {b.name for b in hb.children_recursive}
pts = []
for v in mesh.data.vertices:
    for g in v.groups:
        if mesh.vertex_groups[g.group].name in names and g.weight > 0.5:
            pts.append(mesh.matrix_world @ v.co); break
ctr = sum(pts, Vector()) / len(pts); ext = max((p - ctr).length for p in pts)
scn = bpy.context.scene
for loc in [(2, -3, 3), (-2, -3, 2), (0, 3, -2)]:
    l = bpy.data.objects.new("l", bpy.data.lights.new("l", 'SUN')); l.rotation_euler = (Vector((0,0,0)) - Vector(loc)).to_track_quat('-Z','Y').to_euler(); l.data.energy = 2.5; scn.collection.objects.link(l)
scn.render.engine = 'CYCLES'; scn.cycles.samples = 16; scn.cycles.device = 'CPU'
scn.render.resolution_x = scn.render.resolution_y = 384
scn.world = bpy.data.worlds.new("w"); scn.world.color = (0.5, 0.5, 0.5)
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); scn.collection.objects.link(cam); scn.camera = cam; cam.data.lens = 50
for k, dv in enumerate([(0,-1,0.2), (0,1,0.2), (1 if ctr.x>0 else -1,0,0.1), (0,0,-1)]):
    dv = Vector(dv).normalized(); cam.location = ctr + dv * ext * 3.0
    cam.rotation_euler = (ctr - cam.location).to_track_quat('-Z', 'Y').to_euler()
    scn.render.filepath = f"{out}_{k}.png"; bpy.ops.render.render(write_still=True)
