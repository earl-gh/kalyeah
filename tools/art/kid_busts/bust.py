# A bust and a full-body render of one kid on a transparent background, lit like the home shot (warm key from the
# skylight side, cool rim), for the Kids roster and the kid page.
import math, sys, bpy
argv = sys.argv[sys.argv.index("--") + 1:]
KID, OUT_BUST, OUT_FULL, SAMPLES = argv[0], argv[1], argv[2], int(argv[3])
bpy.ops.wm.read_factory_settings(use_empty=True)
scn = bpy.context.scene
bpy.ops.import_scene.gltf(filepath=KID)
objs = list(scn.objects)
arm = next(o for o in objs if o.type == "ARMATURE")
idle = bpy.data.actions.get("Idle")
if idle:
    if arm.animation_data is None: arm.animation_data_create()
    arm.animation_data.action = idle
    try: arm.animation_data.action_slot = idle.slots[0]
    except Exception: pass
scn.frame_set(int(idle.frame_range[0]) if idle else 0); bpy.context.view_layer.update()
def mesh_z():
    dg = bpy.context.evaluated_depsgraph_get(); zs = []
    for o in scn.objects:
        if o.type == "MESH":
            e = o.evaluated_get(dg); m = e.to_mesh(); zs += [(e.matrix_world @ v.co).z for v in m.vertices]; e.to_mesh_clear()
    return min(zs), max(zs)
zmin, zmax = mesh_z(); h = zmax - zmin
root = bpy.data.objects.new("kid", None); scn.collection.objects.link(root)
for o in objs:
    if o.parent is None: o.parent = root
s = 1.5 / h; root.scale = (s, s, s); root.location.z = -zmin * s; root.rotation_euler.z = math.radians(12)
bpy.context.view_layer.update(); top = mesh_z()[1]

def area(name, loc, target, energy, size, color):
    d = bpy.data.lights.new(name, "AREA"); d.energy = energy; d.size = size; d.color = color
    o = bpy.data.objects.new(name, d); scn.collection.objects.link(o); o.location = loc
    c = o.constraints.new("TRACK_TO"); t = bpy.data.objects.new(name + "_t", None); scn.collection.objects.link(t); t.location = target; c.target = t
area("key", (-1.6, -2.0, 2.6), (0, 0, 1.1), 260, 1.6, (1.0, 0.86, 0.68))
area("fill", (2.2, -2.4, 1.2), (0, 0, 1.0), 60, 2.5, (0.7, 0.8, 1.0))
area("rim", (1.2, 1.6, 2.4), (0, 0, 1.2), 420, 0.8, (1.0, 0.8, 0.55))
area("rim2", (-1.4, 1.5, 1.8), (0, 0, 1.2), 180, 0.8, (0.6, 0.75, 1.0))
w = bpy.data.worlds.new("w"); w.use_nodes = True; w.node_tree.nodes["Background"].inputs["Color"].default_value = (0.04, 0.045, 0.06, 1)
scn.world = w
scn.render.engine = "CYCLES"; scn.cycles.samples = SAMPLES; scn.cycles.use_denoising = True
scn.render.film_transparent = True; scn.render.image_settings.file_format = "PNG"; scn.render.image_settings.color_mode = "RGBA"
scn.view_settings.view_transform = "AgX"; scn.view_settings.look = "AgX - Medium High Contrast"; scn.view_settings.exposure = -0.3
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); scn.collection.objects.link(cam); scn.camera = cam
cam.rotation_euler = (math.radians(90), 0, 0)
# bust: chest up, 3:4
cam.data.lens_unit = "FOV"; cam.data.angle = math.radians(21)
cam.location = (0, -2.6, top - 0.27); scn.render.resolution_x, scn.render.resolution_y = 480, 640
scn.render.filepath = OUT_BUST; bpy.ops.render.render(write_still=True)
# full body, for the kid page
cam.data.angle = math.radians(27); cam.location = (0, -3.6, 0.80 * top / 1.5); cam.rotation_euler = (math.radians(91), 0, 0)
scn.render.resolution_x, scn.render.resolution_y = 600, 900
scn.render.filepath = OUT_FULL; bpy.ops.render.render(write_still=True)
print("rendered", OUT_BUST)
