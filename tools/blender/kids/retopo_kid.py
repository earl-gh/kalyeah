# Game-ready Match LOD0 kid from a contracted high-poly kid (contract_kid.py) and the roster clip source:
#   1. clean bake source (weld duplicates, drop loose geometry, outward normals)
#   2. QuadriFlow quad retopology with X symmetry at Match LOD0 budget (15k-25k, default 20k), shrinkwrap + relax
#   3. UV unwrap with 1.8x head/face texel density and >= 4 px padding
#   4. Cycles PBR baking:
#        - 2048 Base Color (color-only + gentle AO-guided Meshy de-lighting preserving warm Filipino skin tones)
#        - 1024 Tangent Normal (<stem>_2.png)
#        - 1024 Packed ORM (<stem>_orm.png: R=AO, G=Roughness, B=Metallic), wired to glTF PBR Material
#   5. Skin weight transfer from contracted high-poly rig + localized joint polish, seam-twin parity, top-4 normalize
#   6. Retarget all roster clips (zero scale curves) + copy or analytically bake R2 helper & twist bones
#   7. Export Match LOD0 .glb + <stem>_2.png + <stem>_orm.png
#
# usage:
#   blender --background --python retopo_kid.py -- contract.glb clips.glb ref_rest.json out.glb [target_tris]
#   python retopo_kid.py contract.glb clips.glb ref_rest.json out.glb [target_tris]
import json
import math
import os
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Quaternion, Vector, kdtree

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
RETARGET_ONLY = "--retarget-only" in argv
if RETARGET_ONLY:
    argv = [a for a in argv if a != "--retarget-only"]
SRC, CLIPS, REF, DST = argv[0:4]
TARGET_TRIS = int(argv[4]) if len(argv) > 4 else 20000
P = "mixamorig:"
BASE_PX, MAP_PX = 2048, 1024
ref = json.load(open(REF, encoding="utf-8"))


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


def extract_y_twist_angle(q):
    """Exact swing-twist decomposition around local bone Y axis; returns signed twist angle (radians)."""
    # Project quaternion vector part onto (0, 1, 0): q_twist = normalize(w, 0, y, 0)
    w, y = float(q.w), float(q.y)
    n = math.hypot(w, y)
    if n < 1e-8:
        return 0.0
    w /= n
    y /= n
    if w < 0.0:
        w, y = -w, -y
    return 2.0 * math.atan2(y, w)


def ensure_gltf_material_output_group():
    """Create Blender's standard 'glTF Material Output' node group so glTF exporter writes occlusionTexture from ORM R."""
    name = "glTF Material Output"
    ng = bpy.data.node_groups.get(name)
    if ng is not None:
        return ng
    ng = bpy.data.node_groups.new(name, "ShaderNodeTree")
    ng.interface.new_socket(name="Occlusion", in_out="INPUT", socket_type="NodeSocketFloat")
    ng.nodes.new("NodeGroupInput")
    return ng


# ---- Load contracted high-poly source ------------------------------------------------------------------------------
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

# Ensure _End and Tip bones stay non-deforming
for b in arm.data.bones:
    if b.name.startswith(P) and (b.name.endswith("_End") or b.name.endswith("Tip")):
        b.use_deform = False

world_z = [(hi.matrix_world @ v.co).z for v in hi.data.vertices]
H = max(world_z) - min(world_z)
log(f"source {os.path.basename(SRC)}: {tri_count(hi)} tris, {len(hi.data.vertices)} verts, height {H:.3f} m")

def retarget_and_export(arm, low, H, nodes=None, links=None, sep_orm=None, base_tmp=""):
    scn = bpy.context.scene
    scn.render.fps = 30
    scn.render.fps_base = 1.0

    # ---- 6. Retarget roster clips + copy or analytically bake R2 helper & twist bones ------------------------------
    arm.data.pose_position = "POSE"
    before_objs = set(bpy.data.objects)
    before_actions = set(bpy.data.actions)
    bpy.ops.import_scene.gltf(filepath=CLIPS)
    src = next(o for o in bpy.data.objects if o.type == "ARMATURE" and o not in before_objs)
    clip_actions = [a for a in bpy.data.actions if a not in before_actions]
    clip_names = {a: a.name for a in clip_actions}
    for a in clip_actions:
        a.name = "src_" + a.name
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
            local_q = {}
            for b in order:
                if b.name in driven:
                    continue
                d = D.get(b.name)
                if d is None:
                    d = (A[b.parent.name] @ krest[b.parent.name].inverted()) if b.parent else Matrix.Identity(3)
                A[b.name] = d @ krest[b.name]
                pb = arm.pose.bones[b.name]
                if b.parent:
                    L = krest[b.parent.name].inverted() @ krest[b.name]
                    basis = L.inverted() @ A[b.parent.name].inverted() @ A[b.name]
                else:
                    basis = krest[b.name].inverted() @ A[b.name]
                q = basis.to_quaternion()
                local_q[b.name] = q
                pb.rotation_quaternion = q
                pb.keyframe_insert("rotation_quaternion", frame=f - f0, group=b.name)

            # Compute & keyframe all 12 R2 twist/helper bones directly in the kid's exact A-pose bone rest space (#21 sign lock)
            for s in ("Left", "Right"):
                at_bn = P + s + "ArmTwist"
                if at_bn in arm.pose.bones:
                    ang = extract_y_twist_angle(local_q.get(P + s + "Arm", Quaternion((1, 0, 0, 0))))
                    q_tw = Quaternion(Vector((0, 1, 0)), -0.5 * ang)
                    arm.pose.bones[at_bn].rotation_quaternion = q_tw
                    arm.pose.bones[at_bn].keyframe_insert("rotation_quaternion", frame=f - f0, group=at_bn)

                fa_ang = extract_y_twist_angle(local_q.get(P + s + "ForeArm", Quaternion((1, 0, 0, 0))))
                hand_ang = extract_y_twist_angle(local_q.get(P + s + "Hand", Quaternion((1, 0, 0, 0))))
                tot_fa_tw = fa_ang + hand_ang
                for tw_name, share in (("ForeArmTwist1", 0.33), ("ForeArmTwist2", 0.66)):
                    tw_bn = P + s + tw_name
                    if tw_bn in arm.pose.bones:
                        q_tw = Quaternion(Vector((0, 1, 0)), share * tot_fa_tw)
                        arm.pose.bones[tw_bn].rotation_quaternion = q_tw
                        arm.pose.bones[tw_bn].keyframe_insert("rotation_quaternion", frame=f - f0, group=tw_bn)

                for h_name, src_name in (("ForeArmHelper", "ForeArm"), ("LegHelper", "Leg"), ("HandHelper", "Hand")):
                    h_bn = P + s + h_name
                    if h_bn in arm.pose.bones:
                        q_src = local_q.get(P + s + src_name, Quaternion((1, 0, 0, 0)))
                        q_half = Quaternion((1, 0, 0, 0)).slerp(q_src, 0.5)
                        arm.pose.bones[h_bn].rotation_quaternion = q_half
                        arm.pose.bones[h_bn].keyframe_insert("rotation_quaternion", frame=f - f0, group=h_bn)

            hs = src.pose.bones[P + "Hips"]
            delta = (src.matrix_world @ hs.head) - (src.matrix_world @ src.data.bones[P + "Hips"].head_local)
            delta = Vector((0, 0, delta.z)) * (kleg / max(sleg, 1e-4))
            hips = arm.pose.bones[P + "Hips"]
            hips.location = krest[P + "Hips"].inverted() @ delta
            hips.keyframe_insert("location", frame=f - f0, group=hips.name)
        # Set LINEAR interpolation (avoids 3x CUBICSPLINE tangent bloat in glTF!) and prune static identity tracks
        fcurves = []
        if hasattr(out, "layers") and out.layers:
            for layer in out.layers:
                for strip in layer.strips:
                    for bag in strip.channelbags:
                        fcurves.extend(bag.fcurves)
        elif hasattr(out, "fcurves"):
            fcurves = list(out.fcurves)
        for fc in fcurves:
            for kp in fc.keyframe_points:
                kp.interpolation = "LINEAR"

        # Prune bones whose rotation stays constant across the entire clip (e.g. un-animated fingers/_End)
        if hasattr(out, "layers") and out.layers:
            for layer in out.layers:
                for strip in layer.strips:
                    for bag in strip.channelbags:
                        by_path = {}
                        for fc in list(bag.fcurves):
                            by_path.setdefault(fc.data_path, []).append(fc)
                        for dp, fcs in by_path.items():
                            if not dp.endswith("rotation_quaternion"):
                                continue
                            # Keep Hips + any bone that actually moves (> 0.001 quat delta across clip)
                            if "Hips" in dp:
                                continue
                            is_const = True
                            for fc in fcs:
                                vals = [kp.co[1] for kp in fc.keyframe_points]
                                if vals and (max(vals) - min(vals)) > 1e-3:
                                    is_const = False
                                    break
                            if is_const:
                                # Keep 2 boundary keys only if non-identity; drop entirely if rest identity
                                w_vals = [kp.co[1] for fc in fcs if fc.array_index == 0 for kp in fc.keyframe_points]
                                is_ident = bool(w_vals) and abs(abs(w_vals[0]) - 1.0) < 1e-3
                                if is_ident:
                                    for fc in fcs:
                                        bag.fcurves.remove(fc)

        log(f"clip {out.name}: {f1 - f0 + 1} frames")
        arm.animation_data.action = None

    for a in clip_actions:
        bpy.data.actions.remove(a)
    bpy.data.objects.remove(src, do_unlink=True)
    for pb in arm.pose.bones:
        pb.rotation_quaternion = (1, 0, 0, 0)
        pb.location = (0, 0, 0)

    # ---- Optional review renders -----------------------------------------------------------------------------------
    render = os.environ.get("RENDER")
    if render:
        scn.render.engine = "CYCLES"
        scn.cycles.device = "CPU"
        for loc in [(2, -3, 3), (-2, -3, 2), (0, 3, 2)]:
            light = bpy.data.objects.new("l", bpy.data.lights.new("l", "SUN"))
            light.rotation_euler = (Vector((0, 0, 0)) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
            light.data.energy = 2.5
            scn.collection.objects.link(light)
        scn.world = bpy.data.worlds.new("w")
        scn.world.color = (0.5, 0.5, 0.5)
        scn.cycles.samples = 12
        scn.render.resolution_x, scn.render.resolution_y = 480, 640
        cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
        scn.collection.objects.link(cam)
        scn.camera = cam
        shots = [("rest", None, 0)] + [(n, n, k) for n in ("Run", "throw") if n in bpy.data.actions for k in (0.25, 0.6)]
        for tag, clip, t in shots:
            arm.animation_data.action = bpy.data.actions[clip] if clip else None
            if clip:
                a = bpy.data.actions[clip]
                scn.frame_set(int(a.frame_range[0] + (a.frame_range[1] - a.frame_range[0]) * t))
            for v, loc in enumerate([(0.0, -3.2, 0.9), (2.2, -2.2, 1.0)]):
                cam.location = Vector(loc) * (H / 1.3)
                cam.rotation_euler = (Vector((0, 0, H * 0.5)) - cam.location).to_track_quat("-Z", "Y").to_euler()
                scn.render.filepath = f"{render}_{tag}_{int(t * 100)}_{v}.png"
                bpy.ops.render.render(write_still=True)
        arm.animation_data.action = None

    # ---- 7. Export Match LOD0 .glb ---------------------------------------------------------------------------------
    if nodes is not None and links is not None and sep_orm is not None:
        gltf_out = nodes.new("ShaderNodeGroup")
        gltf_out.node_tree = ensure_gltf_material_output_group()
        links.new(sep_orm.outputs["Red"], gltf_out.inputs["Occlusion"])

    for o in bpy.context.view_layer.objects:
        o.select_set(o in (arm, low))
    bpy.ops.export_scene.gltf(
        filepath=DST, export_format="GLB", use_selection=True, export_yup=True,
        export_animations=True, export_animation_mode="ACTIONS",
        export_force_sampling=False, export_bake_animation=False,
        export_skins=True, export_all_influences=False, export_def_bones=False,
        export_image_format="AUTO", export_morph=True, export_extras=True,
        export_apply=False, export_texcoords=True, export_normals=True, export_tangents=True,
    )
    if base_tmp and os.path.isfile(base_tmp):
        os.remove(base_tmp)
    log(f"exported {DST}: {tri_count(low)} tris, {len(bpy.data.actions)} clips, {os.path.getsize(DST) // 1024} KB")


if RETARGET_ONLY:
    retarget_and_export(arm, hi, H)
    raise SystemExit(0)

# ---- 1. Clean the bake source --------------------------------------------------------------------------------------
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

# Check if source material has a metallic factor/texture so we preserve metallic accurately in ORM B.
src_metallic_val = 0.0
for m in hi.data.materials:
    if m and m.node_tree:
        bsdf_hi = next((n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None)
        if bsdf_hi is not None:
            mi = bsdf_hi.inputs.get("Metallic")
            if mi and not mi.is_linked:
                src_metallic_val = float(mi.default_value)

# ---- 2. Retopology (QuadriFlow X-symmetry quad mesh at Match LOD0 budget) -----------------------------------------
low = hi.copy()
low.data = hi.data.copy()
low.name = "KidLOD0"
bpy.context.collection.objects.link(low)
low.vertex_groups.clear()
for m in list(low.modifiers):
    low.modifiers.remove(m)
low.data.materials.clear()
for uv in list(low.data.uv_layers):
    low.data.uv_layers.remove(uv)
if low.data.shape_keys is not None:
    for kb in list(low.data.shape_keys.key_blocks):
        low.shape_key_remove(kb)

activate(low)
voxel = low.modifiers.new("voxel", "REMESH")
voxel.mode = "VOXEL"
voxel.voxel_size = H / 360.0  # finer shell preserves finger gaps, glasses frames, and facial silhouette
voxel.adaptivity = 0.0
bpy.ops.object.modifier_apply(modifier=voxel.name)
log(f"voxel shell: {len(low.data.vertices)} verts")

low.data.transform(Matrix.Scale(100.0, 4))
bm = bmesh.new()
bm.from_mesh(low.data)
short = [e for e in bm.edges if max(abs(a - b) for a, b in zip(e.verts[0].co, e.verts[1].co)) <= 2e-4]
bmesh.ops.collapse(bm, edges=short)
if not all(e.is_manifold for e in bm.edges):
    # Fallback slightly coarser voxel if fine shell had a microscopic non-manifold pinch
    bm.free()
    bpy.data.objects.remove(low, do_unlink=True)
    low = hi.copy()
    low.data = hi.data.copy()
    low.name = "KidLOD0"
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

target_faces = max(9000, min(10500, int(TARGET_TRIS // 2 * 1.02)))
result = bpy.ops.object.quadriflow_remesh(
    use_mesh_symmetry=True, use_preserve_sharp=False, use_preserve_boundary=False,
    smooth_normals=False, mode="FACES", target_faces=target_faces, seed=7,
)
low.data.transform(Matrix.Scale(0.01, 4))
if result != {"FINISHED"}:
    raise SystemExit(f"quadriflow failed: {result}")
log(f"quadriflow: {len(short)} short edges collapsed, {len(low.data.polygons)} quads, {tri_count(low)} tris")

wrap = low.modifiers.new("fit", "SHRINKWRAP")
wrap.target = hi
wrap.wrap_method = "NEAREST_SURFACEPOINT"
wrap.wrap_mode = "ON_SURFACE"
bpy.ops.object.modifier_apply(modifier=wrap.name)
relax = low.modifiers.new("relax", "CORRECTIVE_SMOOTH")
relax.factor = 0.32
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

# ---- 3. UVs (1.8x head/face texel density, >= 4px padding at 2048) -------------------------------------------------
low.data.uv_layers.new(name="UVMap")
activate(low, "EDIT")
bpy.ops.mesh.select_all(action="SELECT")
bpy.ops.uv.smart_project(angle_limit=math.radians(62), island_margin=0.004, area_weight=0.0, scale_to_bounds=False)
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

# ---- 4. Cycles PBR baking (2048 Base Color + 1024 Normal + 1024 ORM) -----------------------------------------------
out_dir = os.path.dirname(os.path.abspath(DST))
os.makedirs(out_dir, exist_ok=True)
stem = os.path.splitext(os.path.basename(DST))[0]

base_img = bpy.data.images.new(f"{stem}_base", BASE_PX, BASE_PX, alpha=False)
nrm_img = bpy.data.images.new(f"{stem}_normal", BASE_PX, BASE_PX, alpha=False)
nrm_img.colorspace_settings.name = "Non-Color"
ao_img = bpy.data.images.new(f"{stem}_ao_tmp", MAP_PX, MAP_PX, alpha=False)
ao_img.colorspace_settings.name = "Non-Color"
rough_img = bpy.data.images.new(f"{stem}_rough_tmp", MAP_PX, MAP_PX, alpha=False)
rough_img.colorspace_settings.name = "Non-Color"
orm_img = bpy.data.images.new(f"{stem}_orm", MAP_PX, MAP_PX, alpha=False)
orm_img.colorspace_settings.name = "Non-Color"

mat = bpy.data.materials.new("KidMaterial")
if mat.node_tree is None:
    mat.use_nodes = True
nodes, links = mat.node_tree.nodes, mat.node_tree.links
bsdf = nodes["Principled BSDF"]
bsdf.inputs["Roughness"].default_value = 0.68
bsdf.inputs["Metallic"].default_value = 0.0

tex_base = nodes.new("ShaderNodeTexImage")
tex_base.image = base_img
tex_nrm = nodes.new("ShaderNodeTexImage")
tex_nrm.image = nrm_img
tex_ao = nodes.new("ShaderNodeTexImage")
tex_ao.image = ao_img
tex_rough = nodes.new("ShaderNodeTexImage")
tex_rough.image = rough_img
tex_orm = nodes.new("ShaderNodeTexImage")
tex_orm.image = orm_img
nmap = nodes.new("ShaderNodeNormalMap")
low.data.materials.append(mat)

scn = bpy.context.scene
scn.render.fps = 30
scn.render.fps_base = 1.0
scn.render.engine = "CYCLES"
scn.cycles.device = "CPU"
scn.cycles.samples = 4
bake = scn.render.bake
bake.use_selected_to_active = True
bake.use_cage = False
bake.cage_extrusion = H * 0.02
bake.max_ray_distance = H * 0.045
bake.margin = 12
bake.margin_type = "EXTEND"

hi.data.update()
for o in bpy.context.view_layer.objects:
    o.select_set(False)
hi.select_set(True)
low.select_set(True)
bpy.context.view_layer.objects.active = low

# 4A) Diffuse Base Color (2048)
nodes.active = tex_base
bake.use_pass_direct = False
bake.use_pass_indirect = False
bake.use_pass_color = True
bpy.ops.object.bake(type="DIFFUSE")
log("baked 2048 base colour")

# 4B) Tangent-Space Normal (2048 -> 1024 clean downsample)
nodes.active = tex_nrm
bake.normal_space = "TANGENT"
bpy.ops.object.bake(type="NORMAL")
nrm_img.scale(MAP_PX, MAP_PX)
log("baked 1024 tangent normal")

# 4C) Ambient Occlusion from raw sculpt (1024, 12 samples for smooth soft-painted crevices)
if scn.world is None:
    scn.world = bpy.data.worlds.new("BakeWorld")
if hasattr(scn.world, "light_settings"):
    scn.world.light_settings.distance = H * 0.14
scn.cycles.samples = 12
nodes.active = tex_ao
bpy.ops.object.bake(type="AO")
log("baked 1024 AO")

# 4D) Roughness from raw sculpt (1024)
scn.cycles.samples = 4
nodes.active = tex_rough
bpy.ops.object.bake(type="ROUGHNESS")
log("baked 1024 roughness")

# 4E) Gentle AO-guided Meshy shadow de-lighting on Base Color (preserves warm Filipino skin tones!) + Pack ORM
ao_px = np.array(ao_img.pixels[:], dtype=np.float32).reshape(MAP_PX, MAP_PX, 4)
rough_px = np.array(rough_img.pixels[:], dtype=np.float32).reshape(MAP_PX, MAP_PX, 4)
base_px = np.array(base_img.pixels[:], dtype=np.float32).reshape(BASE_PX, BASE_PX, 4)

ao_gray = np.clip(ao_px[:, :, 0], 0.0, 1.0)
# Upsample AO 1024 -> 2048 (2x2 repeat) to guide subtle shadow lift on Meshy's baked dark crevices
ao_2k = np.repeat(np.repeat(ao_gray, 2, axis=0), 2, axis=1)
rgb = base_px[:, :, :3]
lum = 0.299 * rgb[:, :, 0] + 0.587 * rgb[:, :, 1] + 0.114 * rgb[:, :, 2]
# Only lift genuine mid-dark shadows in occluded crevices (never wash out intentional jet-black hair/pupils or bright highlights)
shadow_band = np.clip((lum - 0.05) / 0.12, 0.0, 1.0) * np.clip((0.55 - lum) / 0.35, 0.0, 1.0)
crevice = np.clip(1.0 - ao_2k, 0.0, 1.0)
lift = 1.0 + 0.22 * crevice * shadow_band
rgb_delit = np.clip(rgb * lift[:, :, None], 0.0, 1.0)
base_px[:, :, :3] = rgb_delit
base_img.pixels.foreach_set(base_px.ravel())
base_img.update()

# Pack ORM (1024): R = soft-painted AO [0.38..1.0], G = stylized roughness [0.42..0.88], B = metallic [0.0..0.85]
orm_r = np.clip(0.38 + 0.62 * ao_gray, 0.0, 1.0)
raw_rough = np.clip(rough_px[:, :, 0], 0.0, 1.0)
# If source lacked a roughness map (flat 0 or flat 1), default to stylized painterly 0.70 modulated gently by AO
if float(raw_rough.std()) < 0.01 and (float(raw_rough.mean()) < 0.1 or float(raw_rough.mean()) > 0.95):
    orm_g = np.clip(0.66 + 0.14 * (1.0 - ao_gray), 0.48, 0.86)
else:
    orm_g = np.clip(0.42 + 0.46 * raw_rough, 0.42, 0.88)
orm_b = np.full_like(orm_r, float(np.clip(src_metallic_val, 0.0, 0.85)))
orm_rgba = np.stack([orm_r, orm_g, orm_b, np.ones_like(orm_r)], axis=-1).astype(np.float32)
orm_img.pixels.foreach_set(orm_rgba.ravel())
orm_img.update()

# Remove temporary AO/Roughness bake images & nodes before export
nodes.remove(tex_ao)
nodes.remove(tex_rough)
bpy.data.images.remove(ao_img)
bpy.data.images.remove(rough_img)

# Wire clean glTF PBR material (Base Color + Normal + ORM Roughness/Metallic; Occlusion node wired after clip import
# so Blender 5.2's glTF importer doesn't expect extra sockets on a custom 'glTF Material Output' group)
sep_orm = nodes.new("ShaderNodeSeparateColor")

links.new(tex_base.outputs["Color"], bsdf.inputs["Base Color"])
links.new(tex_nrm.outputs["Color"], nmap.inputs["Color"])
links.new(nmap.outputs["Normal"], bsdf.inputs["Normal"])
links.new(tex_orm.outputs["Color"], sep_orm.inputs["Color"])
links.new(sep_orm.outputs["Green"], bsdf.inputs["Roughness"])
links.new(sep_orm.outputs["Blue"], bsdf.inputs["Metallic"])

# Save disk maps: <stem>_2.png (normal) and <stem>_orm.png (ORM); pack base as JPEG for compact .glb
base_tmp = os.path.join(out_dir, f"{stem}_base.jpg")
base_img.filepath_raw = base_tmp
base_img.file_format = "JPEG"
scn.render.image_settings.quality = 88
base_img.save()

nrm_path = os.path.join(out_dir, f"{stem}_2.png")
nrm_img.filepath_raw = nrm_path
nrm_img.file_format = "PNG"
nrm_img.save()

orm_path = os.path.join(out_dir, f"{stem}_orm.png")
orm_img.filepath_raw = orm_path
orm_img.file_format = "PNG"
orm_img.save()
log(f"saved {os.path.basename(nrm_path)} and {os.path.basename(orm_path)}")

# ---- 5. Skin weight transfer + R3 integrity enforcement on retopo mesh ---------------------------------------------
activate(low)
low.parent = arm
low.matrix_parent_inverse = arm.matrix_world.inverted()
valid_deform = {b.name for b in arm.data.bones if b.use_deform}
for vg in hi.vertex_groups:
    if vg.name in valid_deform:
        low.vertex_groups.new(name=vg.name)

dt = low.modifiers.new("weights", "DATA_TRANSFER")
dt.object = hi
dt.use_vert_data = True
dt.data_types_verts = {"VGROUP_WEIGHTS"}
dt.vert_mapping = "POLYINTERP_NEAREST"
dt.layers_vgroup_select_src = "ALL"
dt.layers_vgroup_select_dst = "NAME"
bpy.ops.object.modifier_apply(modifier=dt.name)

# Localized joint polish (1 gentle pass only at joints, NEVER global so rigid cores & twist ramps stay crisp!)
nv = len(low.data.vertices)
vgs = low.vertex_groups
co = np.array([tuple(v.co) for v in low.data.vertices], dtype=float)
cw = np.array([tuple(low.matrix_world @ v.co) for v in low.data.vertices], dtype=float)
W = [{g.group: g.weight for g in v.groups if g.weight > 0.01} for v in low.data.vertices]

# Reassign any unweighted vertices via KDTree
unweighted = [i for i in range(nv) if not W[i]]
if unweighted:
    good = [i for i in range(nv) if W[i]]
    kd = kdtree.KDTree(len(good))
    for k, i in enumerate(good):
        kd.insert(low.data.vertices[i].co, k)
    kd.balance()
    for i in unweighted:
        _, k, _ = kd.find(low.data.vertices[i].co)
        W[i] = dict(W[good[k]])

# Welded adjacency + seam-twin parity + 1 gentle joint-zone smooth pass
nbr = [[] for _ in range(nv)]
for e in low.data.edges:
    a_i, b_i = e.vertices
    nbr[a_i].append(b_i)
    nbr[b_i].append(a_i)
pos = {}
for i, p_key in enumerate(np.round(co / 1e-5).astype(np.int64)):
    pos.setdefault(tuple(p_key), []).append(i)
for ids in pos.values():
    if len(ids) > 1:
        for i in ids:
            nbr[i].extend(j for j in ids if j != i)

AM = arm.matrix_world.copy()
extra_gids = {g.index for g in vgs if not g.name.startswith(P)}
# Very tight localized joint polish (never bleeding into [0.24..0.76] rigid cores!)
jzone = set()
for s in ("Left", "Right"):
    for jn, jr in (("Arm", 0.055), ("ForeArm", 0.036), ("Hand", 0.028), ("UpLeg", 0.065), ("Leg", 0.042), ("Foot", 0.032)):
        bn = P + s + jn
        if bn in arm.data.bones:
            c = np.array(AM @ arm.data.bones[bn].head_local)
            jzone |= set(np.where(np.linalg.norm(cw - c, axis=1) < jr)[0].tolist())
for jn, jr in (("Spine1", 0.05), ("Neck", 0.04)):
    bn = P + jn
    if bn in arm.data.bones:
        c = np.array(AM @ arm.data.bones[bn].head_local)
        jzone |= set(np.where(np.linalg.norm(cw - c, axis=1) < jr)[0].tolist())

nxt = {}
for i in jzone:
    if any(g in extra_gids for g in W[i]):
        continue
    acc = {}
    cnt = 0
    for j in nbr[i]:
        if any(g in extra_gids for g in W[j]):
            continue
        cnt += 1
        for g, val in W[j].items():
            acc[g] = acc.get(g, 0.0) + val
    if cnt > 0:
        nxt[i] = {g: 0.75 * W[i].get(g, 0.0) + 0.25 * acc.get(g, 0.0) / cnt for g in set(acc) | set(W[i])}
for i, nw in nxt.items():
    W[i] = nw

# Re-enforce rigid limb cores (t in [0.24, 0.76]) AFTER joint polish so quad retopo never re-introduces noodle bleed
gid_low = {g.name: g.index for g in vgs}
for s in ("Left", "Right"):
    for seg_n, chain_ns in (
        ("Arm", ("Arm", "ArmTwist")),
        ("ForeArm", ("ForeArm", "ForeArmTwist1", "ForeArmTwist2")),
        ("UpLeg", ("UpLeg",)),
        ("Leg", ("Leg",)),
    ):
        bn = P + s + seg_n
        if bn not in arm.data.bones:
            continue
        allowed = {gid_low[P + s + c] for c in chain_ns if P + s + c in gid_low}
        if not allowed:
            continue
        ba = np.array(AM @ arm.data.bones[bn].head_local)
        bb = np.array(AM @ arm.data.bones[bn].tail_local)
        ab = bb - ba
        L2 = max(float(ab @ ab), 1e-12)
        for i in range(nv):
            if any(g in extra_gids for g in W[i]):
                continue
            own_sum = sum(W[i].get(g, 0.0) for g in allowed)
            if own_sum < 0.65:
                continue
            t = float(((cw[i] - ba) @ ab) / L2)
            if 0.24 < t < 0.76:
                W[i] = {g: val / own_sum for g, val in W[i].items() if g in allowed}

# Seam twins identical, prune cross-side beyond |x| > 0.028m, prune < 0.01, top 4, normalize
vg_names_low = {g.index: g.name for g in vgs}
left_g_low = {idx for idx, nm in vg_names_low.items() if nm.startswith(P + "Left") or "_L" in nm}
right_g_low = {idx for idx, nm in vg_names_low.items() if nm.startswith(P + "Right") or "_R" in nm}
hips_g_low = vgs.get(P + "Hips").index if vgs.get(P + "Hips") else None

for i in range(nv):
    vx = float(cw[i, 0])
    if abs(vx) <= 0.028:
        continue
    bad_g = right_g_low if vx > 0.028 else left_g_low
    for g in list(W[i]):
        if g in bad_g:
            del W[i][g]
    if not W[i] and hips_g_low is not None:
        W[i][hips_g_low] = 1.0

for ids in pos.values():
    if len(ids) > 1:
        avg = {}
        for i in ids:
            for g, val in W[i].items():
                avg[g] = avg.get(g, 0.0) + val / len(ids)
        for i in ids:
            W[i] = dict(avg)

for i, v in enumerate(low.data.vertices):
    w = {g: val for g, val in W[i].items() if val > 0.01}
    top = dict(sorted(w.items(), key=lambda q: -q[1])[:4])
    tot = sum(top.values()) or 1.0
    for g in list(v.groups):
        vgs[g.group].remove([i])
    for g, val in top.items():
        vgs[g].add([i], val / tot, "REPLACE")

skin = low.modifiers.new("Armature", "ARMATURE")
skin.object = arm
unweighted_final = sum(1 for v in low.data.vertices if not v.groups)
log(f"skin: {len(low.vertex_groups)} deform groups, {unweighted_final} unweighted verts")

bpy.data.objects.remove(hi, do_unlink=True)
retarget_and_export(arm, low, H, nodes=nodes, links=links, sep_orm=sep_orm, base_tmp=base_tmp)
