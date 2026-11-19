# Game-ready kid from a contracted high-poly kid (contract_kid.py output) and the roster's clip source.
#
# The steps a character modeller takes, by script:
#   1. clean the source: weld duplicate vertices, drop loose geometry, outward normals (it stays the bake source)
#   2. retopology: voxel remesh to a watertight shell, QuadriFlow quad remesh with X symmetry at the triangle budget,
#      shrinkwrap back onto the source so the silhouette holds, smooth shading
#   3. UVs: angle-based islands with padding, packed with rotation
#   4. bake source -> game mesh in Cycles: base colour (colour only, no light) and a tangent-space normal map,
#      with a cage extrusion and a ray limit sized to the kid, edge padding against seams
#   5. skin: weights transferred from the source's repaired skin (nearest face, interpolated), smoothed, pruned under
#      1%, capped at 4 bones and normalized
#   6. clips: every action of the clip source retargeted onto the kid (world-space rotation transfer from the roster
#      reference rest, hips height only), one keyframe per frame, same clip names as the game plays
#   7. export one .glb: the low mesh, the skeleton, the clips, base colour JPEG 2048 and normal PNG 1024
#
# usage: python retopo_kid.py contract.glb clips.glb ref_rest.json out.glb [target_tris]
#   RENDER=<prefix>  also renders the rest pose and two clip frames for review
import json
import math
import os
import sys

import bpy
import bmesh
from mathutils import Matrix, Vector

SRC, CLIPS, REF, DST = sys.argv[1:5]
TARGET_TRIS = int(sys.argv[5]) if len(sys.argv) > 5 else 20000
P = "mixamorig:"
BASE_PX, NORMAL_PX = 2048, 1024
ref = json.load(open(REF))


def log(*a):
    print("[retopo]", *a, flush=True)


def activate(obj, mode="OBJECT"):
    if bpy.context.object and bpy.context.object.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    if mode != "OBJECT":
        bpy.ops.object.mode_set(mode=mode)


def tri_count(obj):
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)


# ---- load ----------------------------------------------------------------------------------------------------------
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=SRC)
for a in list(bpy.data.actions):
    bpy.data.actions.remove(a)
arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
if arm.animation_data:
    arm.animation_data_clear()
arm.data.pose_position = "REST"
hi = max((o for o in bpy.data.objects if o.type == "MESH" and o.vertex_groups), key=lambda o: len(o.data.vertices))
for o in [o for o in bpy.data.objects if o.type == "MESH" and o is not hi]:
    bpy.data.objects.remove(o, do_unlink=True)
bpy.context.view_layer.update()

world_z = [(hi.matrix_world @ v.co).z for v in hi.data.vertices]
H = max(world_z) - min(world_z)
log(f"source {os.path.basename(SRC)}: {tri_count(hi)} tris, {len(hi.data.vertices)} verts, height {H:.3f} m")

# ---- 1. clean the source -------------------------------------------------------------------------------------------
bm = bmesh.new()
bm.from_mesh(hi.data)
before = len(bm.verts)
bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=H * 0.0002)
loose = [v for v in bm.verts if not v.link_faces]
bmesh.ops.delete(bm, geom=loose, context="VERTS")
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
bm.to_mesh(hi.data)
bm.free()
hi.data.update()
log(f"clean: welded/removed {before - len(hi.data.vertices)} verts")

# ---- 2. retopology -------------------------------------------------------------------------------------------------
low = hi.copy()
low.data = hi.data.copy()
low.name = "KidLow"
bpy.context.collection.objects.link(low)
low.vertex_groups.clear()
for m in list(low.modifiers):
    low.modifiers.remove(m)
low.data.materials.clear()
for uv in list(low.data.uv_layers):
    low.data.uv_layers.remove(uv)

activate(low)
voxel = low.modifiers.new("voxel", "REMESH")
voxel.mode = "VOXEL"
voxel.voxel_size = H / 320.0
voxel.adaptivity = 0.0
bpy.ops.object.modifier_apply(modifier=voxel.name)
log(f"voxel shell: {len(low.data.vertices)} verts")

# QuadriFlow refuses any edge shorter than 1e-4 per axis (Blender's mesh_is_manifold_consistent), and a millimetre
# voxel shell always has a few: work at 100x scale and collapse the degenerate edges that are left.
low.data.transform(Matrix.Scale(100.0, 4))
bm = bmesh.new()
bm.from_mesh(low.data)
short = [e for e in bm.edges if max(abs(a - b) for a, b in zip(e.verts[0].co, e.verts[1].co)) <= 2e-4]
bmesh.ops.collapse(bm, edges=short)
if not all(e.is_manifold for e in bm.edges):
    raise SystemExit("voxel shell is not manifold after collapsing degenerate edges")
bm.to_mesh(low.data)
bm.free()
low.data.update()
target_faces = max(2000, TARGET_TRIS // 2)
result = bpy.ops.object.quadriflow_remesh(
    use_mesh_symmetry=True, use_preserve_sharp=False, use_preserve_boundary=False,
    smooth_normals=False, mode="FACES", target_faces=target_faces, seed=7)
low.data.transform(Matrix.Scale(0.01, 4))
if result != {"FINISHED"}:
    raise SystemExit(f"quadriflow failed: {result}")
log(f"quadriflow: {len(short)} degenerate edges collapsed, {len(low.data.polygons)} quads, {tri_count(low)} tris")

wrap = low.modifiers.new("fit", "SHRINKWRAP")
wrap.target = hi
wrap.wrap_method = "NEAREST_SURFACEPOINT"
wrap.wrap_mode = "ON_SURFACE"
bpy.ops.object.modifier_apply(modifier=wrap.name)
relax = low.modifiers.new("relax", "CORRECTIVE_SMOOTH")
relax.factor = 0.35
relax.iterations = 3
relax.smooth_type = "LENGTH_WEIGHTED"
relax.use_only_smooth = True
bpy.ops.object.modifier_apply(modifier=relax.name)
wrap = low.modifiers.new("fit2", "SHRINKWRAP")
wrap.target = hi
wrap.wrap_method = "NEAREST_SURFACEPOINT"
bpy.ops.object.modifier_apply(modifier=wrap.name)
for p in low.data.polygons:
    p.use_smooth = True

# ---- 3. UVs --------------------------------------------------------------------------------------------------------
low.data.uv_layers.new(name="UVMap")
activate(low, "EDIT")
bpy.ops.mesh.select_all(action="SELECT")
bpy.ops.uv.smart_project(angle_limit=math.radians(62), island_margin=0.004, area_weight=0.0, scale_to_bounds=False)
# texel density by importance: the head and face get HEAD_TEXEL x the body's resolution (packing keeps relative scale)
neck = arm.data.bones.get(P + "Neck")
neck_z = (arm.matrix_world @ neck.head_local).z if neck else min(world_z) + H * 0.8
bm = bmesh.from_edit_mesh(low.data)
uv = bm.loops.layers.uv.active
HEAD_TEXEL = 1.8
for f in bm.faces:
    if (low.matrix_world @ f.calc_center_median()).z > neck_z:
        for loop in f.loops:
            loop[uv].uv *= HEAD_TEXEL
bmesh.update_edit_mesh(low.data)
bpy.ops.uv.pack_islands(rotate=True, margin=0.004, scale=True)
bpy.ops.object.mode_set(mode="OBJECT")

# ---- 4. bake -------------------------------------------------------------------------------------------------------
base_img = bpy.data.images.new("kid_base", BASE_PX, BASE_PX, alpha=False)
nrm_img = bpy.data.images.new("kid_normal", BASE_PX, BASE_PX, alpha=False, float_buffer=False)
nrm_img.colorspace_settings.name = "Non-Color"

mat = bpy.data.materials.new("KidMaterial")
mat.use_nodes = True
nodes, links = mat.node_tree.nodes, mat.node_tree.links
bsdf = nodes["Principled BSDF"]
bsdf.inputs["Roughness"].default_value = 0.65
tex_base = nodes.new("ShaderNodeTexImage")
tex_base.image = base_img
tex_nrm = nodes.new("ShaderNodeTexImage")
tex_nrm.image = nrm_img
nmap = nodes.new("ShaderNodeNormalMap")
low.data.materials.append(mat)

scn = bpy.context.scene
scn.render.engine = "CYCLES"
scn.cycles.device = "CPU"
scn.cycles.samples = 4
bake = scn.render.bake
bake.use_selected_to_active = True
bake.use_cage = False
bake.cage_extrusion = H * 0.02         # start rays outside thin strands (hair, brows) so they don't hit back faces
bake.max_ray_distance = H * 0.045
bake.margin = 12
bake.margin_type = "EXTEND"

hi.data.update()
for o in bpy.context.view_layer.objects:
    o.select_set(False)
hi.select_set(True)
low.select_set(True)
bpy.context.view_layer.objects.active = low

nodes.active = tex_base
bake.use_pass_direct = False
bake.use_pass_indirect = False
bake.use_pass_color = True
bpy.ops.object.bake(type="DIFFUSE")
log("baked base colour")

nodes.active = tex_nrm
bake.normal_space = "TANGENT"
bpy.ops.object.bake(type="NORMAL")
log("baked normal map")

links.new(tex_base.outputs["Color"], bsdf.inputs["Base Color"])
links.new(tex_nrm.outputs["Color"], nmap.inputs["Color"])
links.new(nmap.outputs["Normal"], bsdf.inputs["Normal"])
nrm_img.scale(NORMAL_PX, NORMAL_PX)
out_dir = os.path.dirname(os.path.abspath(DST))
stem = os.path.splitext(os.path.basename(DST))[0]
base_img.filepath_raw = os.path.join(out_dir, f"{stem}_base.jpg")
base_img.file_format = "JPEG"
base_img.save()
nrm_img.filepath_raw = os.path.join(out_dir, f"{stem}_normal.png")
nrm_img.file_format = "PNG"
nrm_img.save()

# ---- 5. skin -------------------------------------------------------------------------------------------------------
activate(low)
low.parent = arm
low.matrix_parent_inverse = arm.matrix_world.inverted()
for vg in hi.vertex_groups:
    low.vertex_groups.new(name=vg.name)
dt = low.modifiers.new("weights", "DATA_TRANSFER")
dt.object = hi
dt.use_vert_data = True
dt.data_types_verts = {"VGROUP_WEIGHTS"}
dt.vert_mapping = "POLYINTERP_NEAREST"
dt.layers_vgroup_select_src = "ALL"
dt.layers_vgroup_select_dst = "NAME"
bpy.ops.object.modifier_apply(modifier=dt.name)
activate(low, "WEIGHT_PAINT")
bpy.ops.object.vertex_group_smooth(group_select_mode="ALL", factor=0.4, repeat=2, expand=0.0)
bpy.ops.object.vertex_group_clean(group_select_mode="ALL", limit=0.01, keep_single=True)
bpy.ops.object.vertex_group_limit_total(group_select_mode="ALL", limit=4)
bpy.ops.object.vertex_group_normalize_all(group_select_mode="ALL", lock_active=False)
bpy.ops.object.mode_set(mode="OBJECT")
skin = low.modifiers.new("Armature", "ARMATURE")
skin.object = arm
unweighted = sum(1 for v in low.data.vertices if not v.groups)
log(f"skin: {len(low.vertex_groups)} groups, {unweighted} unweighted verts")

bpy.data.objects.remove(hi, do_unlink=True)

# ---- 6. clips ------------------------------------------------------------------------------------------------------
arm.data.pose_position = "POSE"
before_objs = set(bpy.data.objects)
before_actions = set(bpy.data.actions)
bpy.ops.import_scene.gltf(filepath=CLIPS)
src = next(o for o in bpy.data.objects if o.type == "ARMATURE" and o not in before_objs)
clip_actions = [a for a in bpy.data.actions if a not in before_actions]
clip_names = {a: a.name for a in clip_actions}
for a in clip_actions:
    a.name = "src_" + a.name          # free the clip names for the kid's own actions
for o in [o for o in bpy.data.objects if o not in before_objs and o is not src]:
    bpy.data.objects.remove(o, do_unlink=True)

SW, KW = src.matrix_world.to_3x3(), arm.matrix_world.to_3x3()
srest = {b.name: (SW @ b.matrix_local.to_3x3()) for b in src.data.bones}
for n, v in ref.items():
    if P + n in srest:
        srest[P + n] = Matrix(v["rot"])
krest = {b.name: (KW @ b.matrix_local.to_3x3()) for b in arm.data.bones}
order = sorted(arm.data.bones, key=lambda b: len(b.parent_recursive))
sleg = (src.data.bones[P + "LeftUpLeg"].head_local - src.data.bones[P + "LeftFoot"].head_local).length
kleg = (arm.data.bones[P + "LeftUpLeg"].head_local - arm.data.bones[P + "LeftFoot"].head_local).length
driven = {b.name for b in arm.data.bones if any(k in b.name for k in ("Helper", "Twist"))}
if src.animation_data is None:
    src.animation_data_create()
if arm.animation_data is None:
    arm.animation_data_create()
for pb in arm.pose.bones:
    pb.rotation_mode = "QUATERNION"

for act in clip_actions:
    src.animation_data.action = act
    try:
        src.animation_data.action_slot = act.slots[0]
    except Exception:
        pass
    f0, f1 = (int(round(x)) for x in act.frame_range)
    out = bpy.data.actions.new(clip_names[act])
    out.use_fake_user = True
    arm.animation_data.action = out
    for f in range(f0, f1 + 1):
        scn.frame_set(f)
        D = {b.name: (SW @ src.pose.bones[b.name].matrix.to_3x3()) @ srest[b.name].inverted() for b in src.data.bones}
        A = {}
        for b in order:
            d = D.get(b.name)
            if d is None:
                d = (A[b.parent.name] @ krest[b.parent.name].inverted()) if b.parent else Matrix.Identity(3)
            A[b.name] = d @ krest[b.name]
            if b.name in driven:
                continue
            pb = arm.pose.bones[b.name]
            if b.parent:
                L = krest[b.parent.name].inverted() @ krest[b.name]
                basis = L.inverted() @ A[b.parent.name].inverted() @ A[b.name]
            else:
                basis = krest[b.name].inverted() @ A[b.name]
            pb.rotation_quaternion = basis.to_quaternion()
            pb.keyframe_insert("rotation_quaternion", frame=f - f0, group=b.name)
        hs = src.pose.bones[P + "Hips"]
        delta = (src.matrix_world @ hs.head) - (src.matrix_world @ src.data.bones[P + "Hips"].head_local)
        delta = Vector((0, 0, delta.z)) * (kleg / sleg)
        hips = arm.pose.bones[P + "Hips"]
        hips.location = krest[P + "Hips"].inverted() @ delta
        hips.keyframe_insert("location", frame=f - f0, group=hips.name)
    log(f"clip {out.name}: {f1 - f0 + 1} frames")
    arm.animation_data.action = None

for a in clip_actions:
    bpy.data.actions.remove(a)
bpy.data.objects.remove(src, do_unlink=True)
for pb in arm.pose.bones:
    pb.rotation_quaternion = (1, 0, 0, 0)
    pb.location = (0, 0, 0)

# ---- review renders ------------------------------------------------------------------------------------------------
render = os.environ.get("RENDER")
if render:
    for loc in [(2, -3, 3), (-2, -3, 2), (0, 3, 2)]:
        light = bpy.data.objects.new("l", bpy.data.lights.new("l", "SUN"))
        light.rotation_euler = (Vector((0, 0, 0)) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
        light.data.energy = 2.5
        scn.collection.objects.link(light)
    scn.world = bpy.data.worlds.new("w")
    scn.world.color = (0.5, 0.5, 0.5)
    scn.cycles.samples = 12
    scn.render.resolution_x, scn.render.resolution_y = 360, 520
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    scn.collection.objects.link(cam)
    scn.camera = cam
    shots = [("rest", None, 0)] + [(n, n, k) for n in ("Run", "throw") if n in bpy.data.actions for k in (0.25, 0.6)]
    for tag, clip, t in shots:
        arm.animation_data.action = bpy.data.actions[clip] if clip else None
        if clip:
            a = bpy.data.actions[clip]
            scn.frame_set(int(a.frame_range[0] + (a.frame_range[1] - a.frame_range[0]) * t))
        for v, loc in enumerate([(0.0, -3.4, 0.9), (2.4, -2.4, 1.0)]):
            cam.location = Vector(loc) * (H / 1.3)
            cam.rotation_euler = (Vector((0, 0, H * 0.5)) - cam.location).to_track_quat("-Z", "Y").to_euler()
            scn.render.filepath = f"{render}_{tag}_{int(t * 100)}_{v}.png"
            bpy.ops.render.render(write_still=True)
    arm.animation_data.action = None

# ---- 7. export -----------------------------------------------------------------------------------------------------
for o in bpy.context.view_layer.objects:
    o.select_set(o in (arm, low))
bpy.ops.export_scene.gltf(
    filepath=DST, export_format="GLB", use_selection=True, export_yup=True,
    export_animations=True, export_animation_mode="ACTIONS", export_skins=True, export_all_influences=False,
    export_image_format="AUTO", export_morph=False, export_apply=False, export_texcoords=True, export_normals=True,
    export_tangents=True)
log(f"exported {DST}: {tri_count(low)} tris, {len(bpy.data.actions)} clips, {os.path.getsize(DST) // 1024} KB")
