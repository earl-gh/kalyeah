import sys, bpy
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=sys.argv[1])
arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
def walk(b, d=0):
    h = arm.matrix_world @ b.head_local
    print('  ' * d + f"{b.name} ({h.x:+.2f},{h.y:+.2f},{h.z:+.2f}) len={b.length:.3f} kids={len(b.children)}")
    for c in sorted(b.children, key=lambda c: (arm.matrix_world @ c.head_local).x): walk(c, d + 1)
for b in arm.data.bones:
    if not b.parent: walk(b)
print('anims:', [a.name for a in bpy.data.actions])
