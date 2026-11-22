# Multi-LOD generator for Kalyeah's 4-tier graphics presets (Low / Medium / High / Ultra) & distance LODs:
#   Input:  Match LOD0 .glb (15k-25k tris, full R2 skeleton, R5 correctives, 2048/1024 PBR maps)
#   Output:
#     - <kid>_lod1.glb (Medium / 15-35m): ~50% tris (8k-12k), UV-locked seam-welded Collapse decimation,
#       finger chains 2-3 merged into chain 1 (identical contract bone names), 4 influences, correctives kept,
#       no duplicate embedded textures/clips (shares LOD0 material & Animation skeleton at runtime).
#     - <kid>_lod2.glb (Low / >35m): ~25% tris (4k-6k), grouped digit weights (HandThumb1 + HandIndex1),
#       extra cloth/hair chains folded into parent body bone, 2-bone friendly weights (max 2 infl),
#       zero corrective blendshapes, silhouette & true height preserved for fair >40m shots.
#   Updates <kid>_rig.json with exact measured LOD triangle/bone/size inventory.
#
# usage:
#   blender --background --python build_lod_tiers.py -- lod0.glb lod1.glb lod2.glb [--rig-json kid_rig.json] [--kid narda]
import argparse
import json
import os
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Vector, kdtree
from mathutils.bvhtree import BVHTree
from mathutils.geometry import barycentric_transform

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
ap = argparse.ArgumentParser()
ap.add_argument("lod0")
ap.add_argument("lod1")
ap.add_argument("lod2")
ap.add_argument("--rig-json", default="")
ap.add_argument("--kid", default="")
args = ap.parse_args(argv)

P = "mixamorig:"
FINGERS = ("Thumb", "Index", "Middle", "Ring", "Pinky")
NON_THUMB = ("Index", "Middle", "Ring", "Pinky")


def log(*a):
    print("[lods]", *a, flush=True)


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


def set_non_deform_markers(arm_obj):
    for b in arm_obj.data.bones:
        if b.name.startswith(P) and (b.name.endswith("_End") or b.name.endswith("Tip")):
            b.use_deform = False


def make_placeholder_material():
    mat = bpy.data.materials.get("KidMaterial_SharedRef")
    if mat is None:
        mat = bpy.data.materials.new("KidMaterial_SharedRef")
        if mat.node_tree is None:
            mat.use_nodes = True
        bsdf = mat.node_tree.nodes.get("Principled BSDF")
        if bsdf is not None:
            bsdf.inputs["Base Color"].default_value = (0.75, 0.72, 0.68, 1.0)
            bsdf.inputs["Roughness"].default_value = 0.7
            bsdf.inputs["Metallic"].default_value = 0.0
    return mat


def fold_vertex_groups(mesh_obj, fold_map):
    """Add weights from src_name into dst_name and remove src_name groups."""
    vgs = mesh_obj.vertex_groups
    for src_name, dst_name in fold_map.items():
        if src_name == dst_name:
            continue
        src = vgs.get(src_name)
        if src is None:
            continue
        dst = vgs.get(dst_name) or vgs.new(name=dst_name)
        for v in mesh_obj.data.vertices:
            for g in v.groups:
                if g.group == src.index and g.weight > 0:
                    dst.add([v.index], g.weight, "ADD")
        vgs.remove(src)


def clean_weights(mesh_obj, arm_obj, max_infl, prune_lim=0.01):
    """Enforce valid deforming groups only, seam-twin parity, KDTree fix for unweighted, top-N cap, normalize."""
    valid = {b.name for b in arm_obj.data.bones if b.use_deform}
    vgs = mesh_obj.vertex_groups
    for g in list(vgs):
        if g.name not in valid:
            vgs.remove(g)

    nv = len(mesh_obj.data.vertices)
    co = np.array([tuple(v.co) for v in mesh_obj.data.vertices], dtype=float)
    W = [{g.group: g.weight for g in v.groups if g.weight > prune_lim} for v in mesh_obj.data.vertices]

    unweighted = [i for i in range(nv) if not W[i]]
    if unweighted:
        good = [i for i in range(nv) if W[i]]
        kd = kdtree.KDTree(len(good))
        for k, i in enumerate(good):
            kd.insert(mesh_obj.data.vertices[i].co, k)
        kd.balance()
        for i in unweighted:
            _, k, _ = kd.find(mesh_obj.data.vertices[i].co)
            W[i] = dict(W[good[k]])

    pos = {}
    for i, p_key in enumerate(np.round(co / 1e-5).astype(np.int64)):
        pos.setdefault(tuple(p_key), []).append(i)
    for ids in pos.values():
        if len(ids) > 1:
            avg = {}
            for i in ids:
                for g, val in W[i].items():
                    avg[g] = avg.get(g, 0.0) + val / len(ids)
            for i in ids:
                W[i] = dict(avg)

    for i, v in enumerate(mesh_obj.data.vertices):
        w = {g: val for g, val in W[i].items() if val > prune_lim}
        if not w and W[i]:
            g_max = max(W[i], key=W[i].get)
            w = {g_max: W[i][g_max]}
        top = dict(sorted(w.items(), key=lambda q: -q[1])[:max_infl])
        tot = sum(top.values()) or 1.0
        for g in list(v.groups):
            vgs[g.group].remove([i])
        for g, val in top.items():
            vgs[g].add([i], val / tot, "REPLACE")


def transfer_corrective_shapes(src_obj, dst_obj):
    """Barycentric transfer of LOD0 corr_* shape keys onto LOD1 mesh with seam-twin parity."""
    if src_obj.data.shape_keys is None:
        return 0
    corr_keys = [kb for kb in src_obj.data.shape_keys.key_blocks if kb.name.startswith("corr_")]
    if not corr_keys:
        return 0

    src_me = src_obj.data
    src_me.calc_loop_triangles()
    src_verts = [v.co.copy() for v in src_me.vertices]
    tris = [(lt.vertices[0], lt.vertices[1], lt.vertices[2]) for lt in src_me.loop_triangles]
    bvh = BVHTree.FromPolygons(src_verts, tris)

    dst_me = dst_obj.data
    dst_rest = [v.co.copy() for v in dst_me.vertices]
    # Precompute closest triangle & barycentric weights for every LOD1 vertex once
    mapping = []
    for v_co in dst_rest:
        loc, _, tri_idx, _ = bvh.find_nearest(v_co)
        if loc is None or tri_idx is None:
            mapping.append(None)
            continue
        i0, i1, i2 = tris[tri_idx]
        p0, p1, p2 = src_verts[i0], src_verts[i1], src_verts[i2]
        # Compute barycentric coordinates of loc on triangle (p0, p1, p2)
        v0 = p1 - p0
        v1 = p2 - p0
        v2 = loc - p0
        d00 = v0.dot(v0)
        d01 = v0.dot(v1)
        d11 = v1.dot(v1)
        d20 = v2.dot(v0)
        d21 = v2.dot(v1)
        denom = d00 * d11 - d01 * d01
        if abs(denom) < 1e-12:
            w0, w1, w2 = 1.0, 0.0, 0.0
        else:
            w1 = (d11 * d20 - d01 * d21) / denom
            w2 = (d00 * d21 - d01 * d20) / denom
            w0 = 1.0 - w1 - w2
        mapping.append((i0, i1, i2, w0, w1, w2))

    # Seam-twin groups on dst
    dst_np = np.array([tuple(c) for c in dst_rest], dtype=float)
    twins = {}
    for idx_v, p_key in enumerate(np.round(dst_np / 1e-5).astype(np.int64)):
        twins.setdefault(tuple(p_key), []).append(idx_v)

    if dst_me.shape_keys is None:
        dst_obj.shape_key_add(name="Basis", from_mix=False)

    basis_src = src_obj.data.shape_keys.key_blocks[0]
    transferred = 0
    for kb in corr_keys:
        dst_kb = dst_obj.shape_key_add(name=kb.name, from_mix=False)
        dst_kb.value = 0.0
        deltas = [Vector((0, 0, 0)) for _ in range(len(dst_rest))]
        for vi, mp in enumerate(mapping):
            if mp is None:
                continue
            i0, i1, i2, w0, w1, w2 = mp
            d0 = kb.data[i0].co - basis_src.data[i0].co
            d1 = kb.data[i1].co - basis_src.data[i1].co
            d2 = kb.data[i2].co - basis_src.data[i2].co
            dv = d0 * w0 + d1 * w1 + d2 * w2
            if dv.length > 0.0005:
                deltas[vi] = dv
        for ids in twins.values():
            if len(ids) > 1:
                avg = sum((deltas[j] for j in ids), Vector((0, 0, 0))) / len(ids)
                for j in ids:
                    deltas[j] = avg
        for vi, dv in enumerate(deltas):
            dst_kb.data[vi].co = dst_rest[vi] + dv
        transferred += 1

    if "kalyeah_correctives" in src_me:
        dst_me["kalyeah_correctives"] = src_me["kalyeah_correctives"]
    return transferred


def build_decimated_mesh(src_mesh, name, target_tris):
    """Weld UV-seam splits, run UV-delimited Collapse decimation cleanly to target_tris, restore smooth shading."""
    dup = src_mesh.copy()
    dup.data = src_mesh.data.copy()
    dup.name = name
    bpy.context.collection.objects.link(dup)
    for m in list(dup.modifiers):
        dup.modifiers.remove(m)
    if dup.data.shape_keys is not None:
        for kb in list(dup.data.shape_keys.key_blocks):
            dup.shape_key_remove(kb)
    dup.vertex_groups.clear()

    # Weld glTF-imported UV-seam split vertices while preserving per-loop UV islands
    bm = bmesh.new()
    bm.from_mesh(dup.data)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    bm.to_mesh(dup.data)
    bm.free()
    dup.data.update()

    src_tris = max(tri_count(dup), 1)
    ratio = float(np.clip(target_tris / src_tris, 0.12, 0.92))
    activate(dup)
    dec = dup.modifiers.new("Decimate", "DECIMATE")
    dec.decimate_type = "COLLAPSE"
    dec.ratio = ratio
    dec.use_collapse_triangulate = False
    dec.delimit = {"UV"}
    bpy.ops.object.modifier_apply(modifier=dec.name)

    # Fine-tune ratio if UV delimit prevented reaching target band
    cur_tris = tri_count(dup)
    if cur_tris > target_tris * 1.12:
        dec2 = dup.modifiers.new("Decimate2", "DECIMATE")
        dec2.decimate_type = "COLLAPSE"
        dec2.ratio = float(np.clip(target_tris / max(cur_tris, 1), 0.35, 0.95))
        dec2.delimit = {"UV"}
        bpy.ops.object.modifier_apply(modifier=dec2.name)

    for p in dup.data.polygons:
        p.use_smooth = True

    # Replace material with lightweight shared placeholder (UV0 untouched!)
    dup.data.materials.clear()
    dup.data.materials.append(make_placeholder_material())
    return dup


def clone_armature(src_arm, name, bones_to_remove):
    dup_arm = src_arm.copy()
    dup_arm.data = src_arm.data.copy()
    dup_arm.name = name
    if dup_arm.animation_data:
        dup_arm.animation_data_clear()
    bpy.context.collection.objects.link(dup_arm)
    activate(dup_arm, "EDIT")
    eb = dup_arm.data.edit_bones
    for bn in bones_to_remove:
        if bn in eb:
            eb.remove(eb[bn])
    activate(dup_arm, "OBJECT")
    set_non_deform_markers(dup_arm)
    return dup_arm


def transfer_weights_from(src_mesh, dst_mesh, dst_arm):
    dst_mesh.parent = dst_arm
    dst_mesh.matrix_parent_inverse = dst_arm.matrix_world.inverted()
    for vg in src_mesh.vertex_groups:
        dst_mesh.vertex_groups.new(name=vg.name)
    activate(dst_mesh)
    dt = dst_mesh.modifiers.new("TransferWeights", "DATA_TRANSFER")
    dt.object = src_mesh
    dt.use_vert_data = True
    dt.data_types_verts = {"VGROUP_WEIGHTS"}
    dt.vert_mapping = "POLYINTERP_NEAREST"
    dt.layers_vgroup_select_src = "ALL"
    dt.layers_vgroup_select_dst = "NAME"
    bpy.ops.object.modifier_apply(modifier=dt.name)
    skin = dst_mesh.modifiers.new("Armature", "ARMATURE")
    skin.object = dst_arm


# ---- Load LOD0 -----------------------------------------------------------------------------------------------------
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=args.lod0)
for a in list(bpy.data.actions):
    bpy.data.actions.remove(a)
arm0 = next(o for o in bpy.data.objects if o.type == "ARMATURE")
if arm0.animation_data:
    arm0.animation_data_clear()
arm0.data.pose_position = "REST"
set_non_deform_markers(arm0)
mesh0 = max((o for o in bpy.data.objects if o.type == "MESH" and o.vertex_groups), key=lambda o: len(o.data.vertices))
for o in [o for o in bpy.data.objects if o.type == "MESH" and o is not mesh0]:
    bpy.data.objects.remove(o, do_unlink=True)
bpy.context.view_layer.update()

lod0_tris = tri_count(mesh0)
lod0_deform = sum(1 for b in arm0.data.bones if b.use_deform and mesh0.vertex_groups.get(b.name) is not None)
lod0_corr = bool(mesh0.data.shape_keys and any(kb.name.startswith("corr_") for kb in mesh0.data.shape_keys.key_blocks))
kid_id = (args.kid or os.path.splitext(os.path.basename(args.lod0))[0]).lower()
log(f"LOD0 {kid_id}: {lod0_tris} tris, {lod0_deform} deform bones, correctives={lod0_corr}")

# ---- Build LOD1 (~50% tris, 8k-12k, finger 2-3 merged into chain 1, 4 infl, correctives kept) ----------------------
lod1_target = max(8500, min(11500, int(round(lod0_tris * 0.50))))
lod1_fold = {}
lod1_drop_bones = []
for s in ("Left", "Right"):
    for fn in FINGERS:
        dst_b = P + f"{s}Hand{fn}1"
        for suf in ("2", "3", "Tip"):
            bn = P + f"{s}Hand{fn}{suf}"
            if bn in arm0.data.bones:
                lod1_fold[bn] = dst_b
                lod1_drop_bones.append(bn)

arm1 = clone_armature(arm0, "Armature_LOD1", lod1_drop_bones)
mesh1 = build_decimated_mesh(mesh0, "KidLOD1", lod1_target)
transfer_weights_from(mesh0, mesh1, arm1)
fold_vertex_groups(mesh1, lod1_fold)
clean_weights(mesh1, arm1, max_infl=4, prune_lim=0.01)
lod1_corr_n = transfer_corrective_shapes(mesh0, mesh1)

for o in bpy.context.view_layer.objects:
    o.select_set(o in (arm1, mesh1))
bpy.ops.export_scene.gltf(
    filepath=args.lod1, export_format="GLB", use_selection=True, export_yup=True,
    export_animations=False, export_skins=True, export_all_influences=False, export_def_bones=False,
    export_morph=True, export_extras=True, export_image_format="NONE",
    export_texcoords=True, export_normals=True, export_tangents=True,
)
lod1_tris = tri_count(mesh1)
lod1_deform = sum(1 for b in arm1.data.bones if b.use_deform and mesh1.vertex_groups.get(b.name) is not None)
log(f"LOD1 exported {args.lod1}: {lod1_tris} tris ({100*lod1_tris/max(lod0_tris,1):.1f}%), {lod1_deform} deform bones, {lod1_corr_n} shapes, {os.path.getsize(args.lod1)//1024} KB")

# Clean up LOD1 objects before building LOD2
bpy.data.objects.remove(mesh1, do_unlink=True)
bpy.data.objects.remove(arm1, do_unlink=True)

# ---- Build LOD2 (~25% tris, 4k-6k, grouped digits Thumb1+Index1, 2-bone skinning, 0 correctives) -------------------
lod2_target = max(4300, min(5700, int(round(lod0_tris * 0.25))))
lod2_fold = {}
lod2_drop_bones = []
for s in ("Left", "Right"):
    # Group thumb into HandThumb1
    for suf in ("2", "3", "Tip"):
        bn = P + f"{s}HandThumb{suf}"
        if bn in arm0.data.bones:
            lod2_fold[bn] = P + f"{s}HandThumb1"
            lod2_drop_bones.append(bn)
    # Group all 4 fingers into HandIndex1 (identical contract bone name)
    for fn in NON_THUMB:
        for suf in ("1", "2", "3", "Tip"):
            if fn == "Index" and suf == "1":
                continue
            bn = P + f"{s}Hand{fn}{suf}"
            if bn in arm0.data.bones:
                lod2_fold[bn] = P + f"{s}HandIndex1"
                lod2_drop_bones.append(bn)
    # Fold twist/helper bones into parent limb bones for minimal Low/far skinning cost
    for tw, par in (
        ("ArmTwist", "Arm"),
        ("ForeArmTwist1", "ForeArm"),
        ("ForeArmTwist2", "ForeArm"),
        ("ForeArmHelper", "ForeArm"),
        ("LegHelper", "Leg"),
        ("HandHelper", "Hand"),
    ):
        bn = P + s + tw
        if bn in arm0.data.bones:
            lod2_fold[bn] = P + s + par
            lod2_drop_bones.append(bn)

# Fold extra hair/skirt/cape leaf chains into their parent body bone (Low / >35m has no spring physics)
for b in arm0.data.bones:
    if not b.name.startswith(P):
        anc = b.parent
        while anc is not None and not anc.name.startswith(P):
            anc = anc.parent
        lod2_fold[b.name] = anc.name if anc is not None else P + "Hips"
        lod2_drop_bones.append(b.name)

arm2 = clone_armature(arm0, "Armature_LOD2", lod2_drop_bones)
mesh2 = build_decimated_mesh(mesh0, "KidLOD2", lod2_target)
transfer_weights_from(mesh0, mesh2, arm2)
fold_vertex_groups(mesh2, lod2_fold)
clean_weights(mesh2, arm2, max_infl=2, prune_lim=0.02)
if "kalyeah_correctives" in mesh2.data:
    del mesh2.data["kalyeah_correctives"]

for o in bpy.context.view_layer.objects:
    o.select_set(o in (arm2, mesh2))
bpy.ops.export_scene.gltf(
    filepath=args.lod2, export_format="GLB", use_selection=True, export_yup=True,
    export_animations=False, export_skins=True, export_all_influences=False, export_def_bones=False,
    export_morph=False, export_extras=True, export_image_format="NONE",
    export_texcoords=True, export_normals=True, export_tangents=False,
)
lod2_tris = tri_count(mesh2)
lod2_deform = sum(1 for b in arm2.data.bones if b.use_deform and mesh2.vertex_groups.get(b.name) is not None)
log(f"LOD2 exported {args.lod2}: {lod2_tris} tris ({100*lod2_tris/max(lod0_tris,1):.1f}%), {lod2_deform} deform bones, 0 shapes, {os.path.getsize(args.lod2)//1024} KB")

# ---- Update <kid>_rig.json with exact measured LOD inventory -------------------------------------------------------
if args.rig_json and os.path.isfile(args.rig_json):
    rig = json.load(open(args.rig_json, encoding="utf-8"))
    rig["lods"] = {
        "lod0": {
            "asset": f"models/characters/{kid_id}",
            "tris": lod0_tris,
            "deformBones": lod0_deform,
            "maxInfluences": 4,
            "correctives": lod0_corr,
            "fileSizeKb": os.path.getsize(args.lod0) // 1024,
        },
        "lod1": {
            "asset": f"models/characters/{kid_id}_lod1",
            "tris": lod1_tris,
            "deformBones": lod1_deform,
            "maxInfluences": 4,
            "correctives": lod1_corr_n > 0,
            "fileSizeKb": os.path.getsize(args.lod1) // 1024,
        },
        "lod2": {
            "asset": f"models/characters/{kid_id}_lod2",
            "tris": lod2_tris,
            "deformBones": lod2_deform,
            "maxInfluences": 2,
            "correctives": False,
            "fileSizeKb": os.path.getsize(args.lod2) // 1024,
        },
    }
    with open(args.rig_json, "w", encoding="utf-8", newline="\n") as f:
        json.dump(rig, f, indent=2)
        f.write("\n")
    log(f"updated {args.rig_json}")
