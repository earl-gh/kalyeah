# usage: python inspect_hand.py model.glb hand_bone out.png
import sys, bpy, colorsys
from mathutils import Vector
src, hand, out = sys.argv[1:4]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
arm.data.pose_position = 'REST'
meshes = [o for o in bpy.data.objects if o.type == 'MESH']
print('meshes:', [(m.name, len(m.data.vertices), len(m.vertex_groups)) for m in meshes])
hb = arm.data.bones[hand]
sub = [hb] + list(hb.children_recursive)
names = [b.name for b in sub]
print("hand subtree:", [(b.name, b.parent.name, round(b.length, 3)) for b in sub])
chain_cols = [(1,0,0),(0,0.8,0),(0,0.3,1),(1,0.9,0),(1,0,1),(0,1,1)]
palette = {hb.name: (1,1,1)}
for i, c in enumerate(hb.children):
    for x in [c] + list(c.children_recursive): palette[x.name] = chain_cols[i % 6]
    print('CHAIN', i, ['red','green','blue','yellow','magenta','cyan'][i%6], c.name, tuple(round(v,3) for v in arm.matrix_world @ c.head_local))
hand_vs = []
mat = bpy.data.materials.new("dbg"); mat.use_nodes = True
nt = mat.node_tree; bsdf = nt.nodes["Principled BSDF"]
attr = nt.nodes.new("ShaderNodeVertexColor"); attr.layer_name = "dom"
nt.links.new(attr.outputs[0], bsdf.inputs["Base Color"])
for mesh in meshes:
    me = mesh.data
    col = me.color_attributes.new("dom", 'BYTE_COLOR', 'POINT')
    gi = {g.index: g.name for g in mesh.vertex_groups}
    for v in me.vertices:
        best = max(v.groups, key=lambda g: g.weight, default=None)
        n = gi.get(best.group) if best else None
        c = palette.get(n, (0.25, 0.25, 0.25))
        if n in palette: hand_vs.append(mesh.matrix_world @ v.co)
        col.data[v.index].color = (*c, 1.0)
    me.materials.clear(); me.materials.append(mat)
print('hand verts:', len(hand_vs))
ctr = (sum(hand_vs, Vector()) / len(hand_vs)) if hand_vs else (arm.matrix_world @ hb.head_local)
ext = max((p - ctr).length for p in hand_vs) if hand_vs else 0.2
scn = bpy.context.scene
for loc in [(2, -3, 3), (-2, -3, 2), (0, 3, -2)]:
    l = bpy.data.objects.new("l", bpy.data.lights.new("l", 'SUN')); l.rotation_euler = (Vector((0,0,0)) - Vector(loc)).to_track_quat('-Z','Y').to_euler(); l.data.energy = 3; scn.collection.objects.link(l)
scn.render.engine = 'CYCLES'; scn.cycles.samples = 12; scn.cycles.device = 'CPU'
scn.render.resolution_x = scn.render.resolution_y = 384
scn.world = bpy.data.worlds.new("w"); scn.world.color = (0.6, 0.6, 0.6)
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); scn.collection.objects.link(cam); scn.camera = cam; cam.data.lens = 50
import os
for k, dirv in enumerate([(0,-1,0.2), (0,1,0.2), (1 if ctr.x>0 else -1,0,0.1), (0,0,-1)]):
    dv = Vector(dirv).normalized()
    cam.location = ctr + dv * ext * 3.4
    cam.rotation_euler = (ctr - cam.location).to_track_quat('-Z', 'Y').to_euler()
    scn.render.filepath = out.replace('.png', f'_{k}.png')
    bpy.ops.render.render(write_still=True)
