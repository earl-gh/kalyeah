# The home screen's showcase as a real place (plan v7): Karding in front of a Palengke stall's roll-up shutter with his chalk
# map on it, sun shafts from the skylights through haze (MAPS.md Palengke), a hanging bulb, crates and sacks for depth.
import math, sys, bpy
from mathutils import Vector
argv = sys.argv[sys.argv.index("--") + 1:]
KID, CHALK, PIKO, OUT, SHOT, SAMPLES = argv[0], argv[1], argv[2], argv[3], argv[4], int(argv[5])
bpy.ops.wm.read_factory_settings(use_empty=True)
scn = bpy.context.scene

def mat(name, color, rough=0.7, metal=0.0):
    m = bpy.data.materials.new(name); m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (*color, 1); b.inputs["Roughness"].default_value = rough; b.inputs["Metallic"].default_value = metal
    return m

def cube(name, loc, size, m, bevel=0.0):
    bpy.ops.mesh.primitive_cube_add(location=loc); o = bpy.context.object; o.name = name
    o.scale = (size[0] / 2, size[1] / 2, size[2] / 2); bpy.ops.object.transform_apply(scale=True)
    if bevel:
        mod = o.modifiers.new("b", "BEVEL"); mod.width = bevel; mod.segments = 3
    o.data.materials.append(m); return o

# --- the kid
bpy.ops.import_scene.gltf(filepath=KID)
kid_objs = [o for o in scn.objects]
arm = next(o for o in kid_objs if o.type == "ARMATURE")
idle = bpy.data.actions.get("Idle")
if idle:
    if arm.animation_data is None: arm.animation_data_create()
    arm.animation_data.action = idle
    try: arm.animation_data.action_slot = idle.slots[0]
    except Exception: pass
scn.frame_set(int(idle.frame_range[0]) if idle else 0); bpy.context.view_layer.update()
pts = [arm.matrix_world @ pb.head for pb in arm.pose.bones] + [arm.matrix_world @ pb.tail for pb in arm.pose.bones]
zmin = min(p.z for p in pts); h = (max(p.z for p in pts) - zmin) * 1.04
root = bpy.data.objects.new("kid", None); scn.collection.objects.link(root)
for o in kid_objs:
    if o.parent is None: o.parent = root
s = 1.5 / h; root.scale = (s, s, s); root.location.z = -zmin * s; root.rotation_euler.z = math.radians(14)
if SHOT == 'home':
    for o in kid_objs: o.hide_render = True  # the game draws the live kid in front of this plate

# --- the stall: floor, shutter wall with the chalk map, side posts, roof with skylight gaps
concrete = mat("concrete", (0.07, 0.065, 0.06) if SHOT == "home" else (0.14, 0.13, 0.12), 0.9)
cube("floor", (0, 1.0, -0.05), (16, 14, 0.1), concrete)
sh = bpy.data.materials.new("shutter"); sh.use_nodes = True
nt = sh.node_tree; N = nt.nodes; L = nt.links; b = N["Principled BSDF"]
b.inputs["Roughness"].default_value = 0.55; b.inputs["Metallic"].default_value = 0.35
tc = N.new("ShaderNodeTexCoord"); mp = N.new("ShaderNodeMapping")
L.new(tc.outputs["Object"], mp.inputs["Vector"])
wave = N.new("ShaderNodeTexWave"); wave.wave_type = "BANDS"; wave.bands_direction = "Z"; wave.inputs["Scale"].default_value = 7.0
L.new(mp.outputs["Vector"], wave.inputs["Vector"])
bump = N.new("ShaderNodeBump"); bump.inputs["Strength"].default_value = 1.0
L.new(wave.outputs["Fac"], bump.inputs["Height"]); L.new(bump.outputs["Normal"], b.inputs["Normal"])
noise = N.new("ShaderNodeTexNoise"); noise.inputs["Scale"].default_value = 3.0; noise.inputs["Detail"].default_value = 8
L.new(mp.outputs["Vector"], noise.inputs["Vector"])
ramp = N.new("ShaderNodeValToRGB")
ramp.color_ramp.elements[0].color = (0.035, 0.085, 0.095, 1); ramp.color_ramp.elements[1].color = (0.07, 0.15, 0.155, 1)
rust = ramp.color_ramp.elements.new(0.9); rust.color = (0.16, 0.08, 0.04, 1)
L.new(noise.outputs["Fac"], ramp.inputs["Fac"])
img = N.new("ShaderNodeTexImage"); img.image = bpy.data.images.load(CHALK); img.extension = "CLIP"
uv = N.new("ShaderNodeUVMap")
L.new(uv.outputs["UV"], img.inputs["Vector"])
mix = N.new("ShaderNodeMix"); mix.data_type = "RGBA"
L.new(img.outputs["Alpha"], mix.inputs["Factor"]); L.new(ramp.outputs["Color"], mix.inputs[6]); mix.inputs[7].default_value = (0.62, 0.62, 0.58, 1)
L.new(mix.outputs[2], b.inputs["Base Color"])
# wall: a plane facing the camera; its UVs place the chalk map on a 5 x 2.5 m patch behind and left of the kid
bpy.ops.mesh.primitive_plane_add(size=1, location=(-0.2, 2.6, 2.3), rotation=(math.radians(90), 0, 0))
wall = bpy.context.object; wall.name = "shutter"; wall.scale = (13, 4.7, 1); bpy.ops.object.transform_apply(scale=True)
for loop in wall.data.loops:
    v = wall.data.vertices[loop.vertex_index].co
    wall.data.uv_layers.active.data[loop.index].uv = ((v.x + 0.2 + 3.1) / 5.0, (v.y + 1.9 - 0.75) / 2.5) if False else (0, 0)
me = wall.data
for poly in me.polygons:
    for li in poly.loop_indices:
        co = wall.matrix_world @ me.vertices[me.loops[li].vertex_index].co
        me.uv_layers.active.data[li].uv = ((co.x + 2.9) / 3.8, (co.z - 1.0) / 1.9)
wall.data.materials.append(sh)
post = mat("post", (0.22, 0.16, 0.10), 0.8)
for x in (-3.4, 2.9):
    cube("post", (x, 2.45, 2.0), (0.22, 0.22, 4.2), post)
cube("lintel", (-0.2, 2.45, 3.95), (7.2, 0.3, 0.3), post)
tin = mat("tin", (0.35, 0.33, 0.30), 0.45, 0.6)
for x0, w in ((-6.6, 4.65), (-1.6, 2.2), (0.95, 2.0), (3.3, 3.6)):   # roof strips with skylight gaps between them
    cube("roof", (x0 + w / 2, -1.5, 4.6), (w, 8.4, 0.06), tin)
# crates (red, blue, yellow) on the right behind, sacks in the left foreground
for i, (c, z) in enumerate((((0.40, 0.09, 0.07), 0.2), ((0.07, 0.17, 0.38), 0.6), ((0.52, 0.40, 0.07), 1.0))):
    cube("crate", (2.75 - 0.05 * i, 2.2, z), (0.62, 0.46, 0.38), mat("crate%d" % i, c, 0.6), 0.02)
sack = mat("sack", (0.16, 0.12, 0.08), 0.95)
for i, (x, y, z) in enumerate(((-1.25, -0.55, 0.28), (-1.7, -0.35, 0.28), (-1.45, -0.45, 0.74))):
    o = cube("sack", (x, y, z), (0.62, 0.42, 0.5), sack, 0.12); o.rotation_euler.z = 0.3 * i
# the hanging bulb: cord, glass, light
glow = bpy.data.materials.new("bulb"); glow.use_nodes = True
em = glow.node_tree.nodes.new("ShaderNodeEmission"); em.inputs["Color"].default_value = (1.0, 0.72, 0.38, 1); em.inputs["Strength"].default_value = 40
glow.node_tree.links.new(em.outputs[0], glow.node_tree.nodes["Material Output"].inputs["Surface"])
bpy.ops.mesh.primitive_uv_sphere_add(radius=0.07, location=(1.75, 1.7, 2.35)); bpy.context.object.data.materials.append(glow)
cube("cord", (1.75, 1.7, 3.2), (0.012, 0.012, 1.6), post)
bl = bpy.data.objects.new("bulb_light", bpy.data.lights.new("bulb_light", "POINT")); bl.data.energy = 60; bl.data.color = (1, 0.7, 0.4); bl.data.shadow_soft_size = 0.07
bl.location = (1.75, 1.7, 2.33); scn.collection.objects.link(bl)
if SHOT == "yard":
    pm = bpy.data.materials.new("piko"); pm.use_nodes = True; pm.blend_method = "BLEND"
    pn = pm.node_tree.nodes; pb = pn["Principled BSDF"]; pt = pn.new("ShaderNodeTexImage"); pt.image = bpy.data.images.load(PIKO)
    pm.node_tree.links.new(pt.outputs["Color"], pb.inputs["Base Color"]); pm.node_tree.links.new(pt.outputs["Alpha"], pb.inputs["Alpha"])
    pb.inputs["Emission Color"].default_value = (0.35, 0.6, 1.0, 1); pb.inputs["Emission Strength"].default_value = 0.4
    pm.node_tree.links.new(pt.outputs["Alpha"], pb.inputs["Emission Strength"])
    bpy.ops.mesh.primitive_plane_add(size=1, location=(0.15, 0.1, 0.004)); dec = bpy.context.object
    dec.scale = (2.4, 1.2, 1); dec.rotation_euler.z = math.radians(8); dec.data.materials.append(pm)

# --- haze for the shafts, the sun through the skylights, and the kid's own lights
vol = bpy.data.materials.new("haze"); vol.use_nodes = True
vn = vol.node_tree.nodes; vn.remove(vn["Principled BSDF"])
pv = vn.new("ShaderNodeVolumePrincipled"); pv.inputs["Density"].default_value = 0.0045; pv.inputs["Anisotropy"].default_value = 0.45
pv.inputs["Color"].default_value = (1.0, 0.93, 0.82, 1)
vol.node_tree.links.new(pv.outputs[0], vn["Material Output"].inputs["Volume"])
bpy.ops.mesh.primitive_cube_add(location=(0.1, -0.6, 2.28)); hz = bpy.context.object; hz.scale = (6.4, 3.15, 2.26)
for x, sy in ((-6.7, 1), (6.95, 1)):   # side walls close the stall so the sun only gets in through the skylights
    cube("side", (x, -1.5, 2.3), (0.1, 8.4, 4.7), post)
cube("front_lintel", (0.2, -5.6, 4.0), (14, 0.2, 1.3), post); hz.data.materials.append(vol)
sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN")); sun.data.energy = 7; sun.data.angle = math.radians(0.6)
sun.data.color = (1.0, 0.88, 0.70); sun.rotation_euler = (math.radians(24), math.radians(-18), math.radians(12)); scn.collection.objects.link(sun)
def area(name, loc, color, power, size, target=(0, 0, 1.05)):
    l = bpy.data.objects.new(name, bpy.data.lights.new(name, "AREA")); l.data.energy = power; l.data.size = size; l.data.color = color
    l.location = Vector(loc); l.rotation_euler = (Vector(target) - l.location).to_track_quat("-Z", "Y").to_euler(); scn.collection.objects.link(l)
    return l
area("key", (-1.6, -2.0, 2.3), (1.0, 0.88, 0.74), 80, 1.6)
beam = bpy.data.objects.new("beam", bpy.data.lights.new("beam", "SPOT")); beam.data.energy = 1900; beam.data.spot_size = math.radians(30)
beam.data.spot_blend = 0.35; beam.data.color = (1.0, 0.86, 0.66); beam.data.shadow_soft_size = 0.15
beam.location = (-1.3, -0.4, 4.5); beam.rotation_euler = (Vector((0, 0, 1.2)) - beam.location).to_track_quat("-Z", "Y").to_euler()
scn.collection.objects.link(beam)
area("rim", (1.3, 1.4, 1.9), (1.0, 0.86, 0.62), 380, 0.8)
area("rim2", (-1.2, 1.3, 1.8), (1.0, 0.80, 0.58), 170, 0.7)
area("fill", (2.0, -1.6, 0.9), (0.50, 0.64, 1.0), 55, 2.5)
w = bpy.data.worlds.new("w"); w.use_nodes = True; w.node_tree.nodes["Background"].inputs["Color"].default_value = (0.03, 0.035, 0.05, 1)
w.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.5; scn.world = w

# --- camera
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); scn.collection.objects.link(cam); scn.camera = cam
cam.data.sensor_fit = "VERTICAL"; cam.data.dof.use_dof = True
scn.render.resolution_x, scn.render.resolution_y = 2160, 998
if SHOT == "home":
    for o in scn.objects:
        if o.name.startswith(("crate", "sack")): o.hide_render = True  # a clean stall: no prop blocks in the home shot
    beam.data.energy = 700  # no kid in the beam, so it would leave a hot patch on the bare floor
    # HeroStage's camera (4.3 m back, 1.0 m up, aimed at 0.8 m), opened to 40 degrees tall so the plate covers the screen
    scn.render.resolution_x, scn.render.resolution_y = 2400, 1000
    cam.data.angle = math.radians(40); cam.location = (0, -4.3, 1.0)
    cam.rotation_euler = (Vector((0, 0, 0.8)) - cam.location).to_track_quat("-Z", "Y").to_euler()
    cam.data.dof.focus_distance = 4.3; cam.data.dof.aperture_fstop = 2.8
elif SHOT == "showcase":
    cam.data.angle = math.radians(28); cam.location = (0, -2.4, 1.045); cam.rotation_euler = (math.radians(90), 0, 0)
    cam.data.shift_x = 0.035; cam.data.dof.focus_distance = 2.4; cam.data.dof.aperture_fstop = 1.6
else:
    cam.data.angle = math.radians(34); cam.location = (0.1, -6.2, 2.15)
    cam.rotation_euler = (Vector((0.1, 0.4, 0.75)) - cam.location).to_track_quat("-Z", "Y").to_euler()
    cam.data.shift_x = 0.035; cam.data.dof.focus_distance = 6.3; cam.data.dof.aperture_fstop = 5.6

c = scn.cycles; scn.render.engine = "CYCLES"; c.device = "CPU"; c.samples = SAMPLES; c.use_denoising = True
c.max_bounces = 4; c.diffuse_bounces = 2; c.glossy_bounces = 2; c.volume_bounces = 0; c.transmission_bounces = 2
c.volume_step_rate = 4.0; c.volume_max_steps = 256
scn.view_settings.view_transform = "AgX"; scn.view_settings.look = "AgX - Medium High Contrast"; scn.view_settings.exposure = -0.55
scn.render.image_settings.file_format = "PNG"; scn.render.filepath = OUT
bpy.ops.render.render(write_still=True); print("rendered", OUT)
