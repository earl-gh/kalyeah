import sys, bpy
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=sys.argv[1])
arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
AM = arm.matrix_world
pts = [(b, AM @ b.tail_local) for b in arm.data.bones]
zs = [p.z for _, p in pts]; zmin, zmax = min(zs), max(zs); H = zmax - zmin
xs = max(abs(p.x) for _, p in pts)
out = {}
for side, sgn in (("L", 1), ("R", -1)):
    tips = [b for b, p in pts if not b.children and sgn * p.x > 0.7 * xs and zmin + 0.3 * H < p.z < zmin + 0.7 * H]
    # group by the hand: the common ancestor = parent of the chain roots
    roots = set()
    for t in tips:
        x = t
        while x.parent and len(x.parent.children) == 1: x = x.parent
        roots.add(x.parent.name if x.parent else None)
    out[side] = (len(tips), sorted(roots))
print('TIPS', f"H={H:.2f}", out)
