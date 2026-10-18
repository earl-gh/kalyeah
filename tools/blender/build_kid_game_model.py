"""Makes a rigged, animated kid light enough for phones (the six kids from the owner's rig pass).

usage: python build_kid_game_model.py -- rigged_kid.glb out.glb [target_triangles] [keep_uv]
 * keep_uv: keep the owner's own UV layout and base colour image (scaled to KID_TEXTURE px, default
   2048) instead of unwrapping and baking again. Far sharper, and the only way that works for the
   dense rebuilt rigs (70,000+ triangles), where the bake came out as scrambled speckles.
The source GLB is the owner's full-resolution rigged kid (skeleton with .L/.R names, spring
bones, 8 corrective blend shapes, 12 clips at 30 fps); it is not stored in the repo. A Mixamo
rig (`mixamorig:` bones, clips named Idle / Run / Throw ...; Junjun's "Boy_rebuilt_rig") is
renamed to the same bone and clip names first (`normalize_mixamo`).
 * the mesh is welded and decimated to ~target triangles; skin weights (max 4, normalized)
   and the elbow / knee corrective blend shapes are re-sampled from the full mesh so they stay
   smooth (each blend shape is a full mesh copy in the game, so hip / shoulder ones are left out);
 * new UVs and one baked base-colour texture (decimation scrambles UVs along seams);
 * the texture is 1024 px (JPEG), no normal / metal-roughness maps (the game lights the kids
   matte, ModelLook);
 * the clips play in place: the root bone keeps its squash-and-stretch scale but not its
   travel (the sim owns the kid's position);
 * the mesh node is called Mesh0, like every kid model.
"""
import os
import sys

import bpy
import bmesh  # noqa: E402 (needs bpy imported first)
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

DEFAULT_TRIANGLES = 9000
## Correctives kept for phones: elbow and knee bends show most (squat, run, throw).
GAME_SHAPES = ("elbow", "knee")
TEXTURE_SIZE = 1024


# Welding distance as a fraction of the model's size: the glTF importer splits vertices at every
# UV and normal seam, and decimating that split mesh tears it into open pieces (the kids came
# out as ~1,200 loose parts with more open edges than triangles). Weld first.
WELD = 1e-5


def weld(obj):
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=max(obj.dimensions) * WELD)
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()


def open_edges(obj):
    """Edges used by one face only: the holes and cracks of the mesh (after welding)."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=max(obj.dimensions) * WELD)
    count = sum(1 for e in bm.edges if len(e.link_faces) == 1)
    bm.free()
    return count


def mesh_arrays(obj):
    me = obj.data
    co = np.empty(len(me.vertices) * 3)
    me.vertices.foreach_get("co", co)
    return co.reshape(-1, 3)


def channelbag_fcurves(action):
    for layer in action.layers:
        for strip in layer.strips:
            for bag in strip.channelbags:
                yield from bag.fcurves


def source_base_color(obj):
    """The image wired into the model's Base Color (the owner's own texture)."""
    for node in obj.data.materials[0].node_tree.nodes:
        if node.type == "BSDF_PRINCIPLED":
            link = node.inputs["Base Color"].links
            if link and link[0].from_node.type == "TEX_IMAGE":
                return link[0].from_node.image
    raise SystemExit("no base colour image on the model")


def use_source_texture(mesh, image):
    """Keeps the mesh's own UVs and gives it the owner's base colour image, matte, at KID_TEXTURE px."""
    size = int(os.environ.get("KID_TEXTURE", "2048"))
    image.name = "kid_base_color"
    if image.size[0] != size:
        image.scale(size, size)
    else:
        image.pixels[:] = image.pixels[:]  # a touched image is re-encoded as a JPEG, not copied as the big source
    image.pack()
    if mesh.data.has_custom_normals:
        bpy.context.view_layer.objects.active = mesh
        bpy.ops.object.select_all(action="DESELECT")
        mesh.select_set(True)
        bpy.ops.mesh.customdata_custom_splitnormals_clear()
    bpy.ops.object.shade_smooth()
    mat = bpy.data.materials.new("kid")
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    bsdf = next(n for n in nodes if n.type == "BSDF_PRINCIPLED")
    bsdf.inputs["Metallic"].default_value = 0.0
    bsdf.inputs["Roughness"].default_value = 1.0
    tex = nodes.new("ShaderNodeTexImage")
    tex.image = image
    mat.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    mesh.data.materials.clear()
    mesh.data.materials.append(mat)


def bake_texture(mesh, ref):
    """New UVs on the light mesh and one TEXTURE_SIZE base-colour texture baked from `ref`."""
    bpy.ops.object.select_all(action="DESELECT")
    bpy.context.view_layer.objects.active = mesh
    mesh.select_set(True)
    for uv in list(mesh.data.uv_layers):
        mesh.data.uv_layers.remove(uv)
    mesh.data.uv_layers.new(name="UVMap")
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=1.15, island_margin=0.012)
    bpy.ops.object.mode_set(mode="OBJECT")
    # the imported custom normals no longer fit the lighter mesh: plain smooth normals instead
    if mesh.data.has_custom_normals:
        bpy.ops.mesh.customdata_custom_splitnormals_clear()
    for attr in ("sharp_edge", "sharp_face"):
        if attr in mesh.data.attributes:
            mesh.data.attributes.remove(mesh.data.attributes[attr])
    bpy.ops.object.shade_smooth()
    image = bpy.data.images.new("kid_base_color", TEXTURE_SIZE, TEXTURE_SIZE)
    mat = bpy.data.materials.new("kid")
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    bsdf = next(n for n in nodes if n.type == "BSDF_PRINCIPLED")
    bsdf.inputs["Metallic"].default_value = 0.0
    bsdf.inputs["Roughness"].default_value = 1.0
    tex = nodes.new("ShaderNodeTexImage")
    tex.image = image
    mat.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    nodes.active = tex
    mesh.data.materials.clear()
    mesh.data.materials.append(mat)
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 1
    bake = scene.render.bake
    bake.use_pass_direct = bake.use_pass_indirect = False
    bake.use_pass_color = True
    bake.use_selected_to_active = True
    bake.cage_extrusion = float(os.environ.get("KID_CAGE", "0.015"))
    bake.max_ray_distance = float(os.environ.get("KID_RAY", "0.06"))
    # fill the gaps round every island with the island's own colour: empty gaps are dark and
    # bleed into the model as speckles when it is small on screen
    bake.margin_type = "EXTEND"
    bake.margin = max(8, TEXTURE_SIZE // 32)
    ref.select_set(True)
    mesh.select_set(True)
    bpy.context.view_layer.objects.active = mesh
    bpy.ops.object.bake(type="DIFFUSE")
    image.pack()


MIXAMO_CLIPS = {
    "Idle": "idle_alert", "Run": "run", "Throw": "throw", "RunningThrow": "throw_run",
    "Cast_OneHand": "cast_one_hand", "Cast_Run": "cast_run", "Stunned": "stunned",
    "Airborne": "airborne_fall", "Knockdown": "knockdown", "Tripped": "trip",
}
MIXAMO_BONES = {
    "Hips": "hips", "Spine": "spine.01", "Spine1": "spine.02", "Spine2": "spine.03", "Neck": "neck",
    "Head": "head", "HeadTop_End": "head_end", "headfront": "head_front",
    "Shoulder": "shoulder", "Arm": "upper_arm", "ForeArm": "forearm", "Hand": "hand",
    "UpLeg": "thigh", "Leg": "shin", "Foot": "foot", "ToeBase": "toe", "Toe_End": "toe_end",
    "LeftShorts": "shorts.L", "LeftShortsDrape": "shorts_drape.L",
    "RightShorts": "shorts.R", "RightShortsDrape": "shorts_drape.R",
}
FINGERS = {"Thumb": "thumb", "Index": "index", "Middle": "middle", "Ring": "ring", "Pinky": "pinky"}


def mixamo_bone_name(name):
    """`mixamorig:LeftForeArm` -> `forearm.L`, `mixamorig:RightHandIndex2` -> `index.02.R`."""
    base = name.split(":", 1)[-1]
    if base in MIXAMO_BONES:
        return MIXAMO_BONES[base]
    for prefix, side in (("Left", ".L"), ("Right", ".R")):
        if not base.startswith(prefix):
            continue
        rest = base[len(prefix):]
        if rest in MIXAMO_BONES:
            return MIXAMO_BONES[rest] + side
        if rest.startswith("Hand") and rest != "Hand":
            finger = rest[4:]
            for long, short in FINGERS.items():
                if finger.startswith(long):
                    tail = finger[len(long):]
                    number = 4 if tail == "Tip" else int(tail)
                    # Mixamo's middle finger has a spare 4th joint besides its tip: a palm stub
                    return "palm" + side if (long == "Middle" and tail == "4") else "%s.%02d%s" % (short, number, side)
    raise ValueError("unknown bone " + name)


def normalize_mixamo(rig):
    """Gives a Mixamo rig the bone and clip names the game uses (a no-op for the .L/.R rigs)."""
    if not any(b.name.startswith("mixamorig:") for b in rig.data.bones):
        return
    for bone in list(rig.data.bones):
        bone.name = mixamo_bone_name(bone.name)
    for act in bpy.data.actions:
        if act.name in MIXAMO_CLIPS:
            act.name = MIXAMO_CLIPS[act.name]


def main():
    argv = sys.argv[sys.argv.index("--") + 1:]
    src, out = argv[0], argv[1]
    target = int(argv[2]) if len(argv) > 2 else DEFAULT_TRIANGLES
    keep_uv = "keep_uv" in argv[3:] or os.environ.get("KID_KEEP_UV") == "1"
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=src, merge_vertices=True)
    rig = next(o for o in bpy.data.objects if o.type == "ARMATURE")
    meshes = [o for o in bpy.data.objects if o.type == "MESH"]
    mesh = max(meshes, key=lambda o: len(o.data.vertices))  # the importer adds a small bone-shape sphere
    for o in meshes:
        if o is not mesh:
            bpy.data.objects.remove(o)
    mesh.name = "Mesh0"
    source_image = source_base_color(mesh) if keep_uv else None
    normalize_mixamo(rig)

    # the full-resolution reference: positions, weights and blend-shape offsets per vertex
    ref = mesh.copy()
    ref.data = mesh.data.copy()
    bpy.context.scene.collection.objects.link(ref)
    rco = mesh_arrays(ref)
    groups = [g.name for g in ref.vertex_groups]
    rw = np.zeros((len(rco), len(groups)))
    for v in ref.data.vertices:
        for g in v.groups:
            rw[v.index, g.group] = g.weight
    keys = ref.data.shape_keys.key_blocks if ref.data.shape_keys else []
    # each blend shape is a full copy of the mesh in the game: keep the ones that show most
    # (KID_SHAPES=all keeps every corrective)
    wanted = os.environ.get("KID_SHAPES", ",".join(GAME_SHAPES))
    if wanted == "all":
        wanted = None
    key_names = [k.name for k in keys][1:]
    if wanted:
        key_names = [n for n in key_names if n.split("_")[0] in wanted.split(",")]
    deltas = []
    for k in [kb for kb in list(keys)[1:] if kb.name in key_names]:
        kc = np.empty(len(rco) * 3)
        k.data.foreach_get("co", kc)
        deltas.append(kc.reshape(-1, 3) - rco)
    ref.data.calc_loop_triangles()
    tris = np.array([t.vertices[:] for t in ref.data.loop_triangles])
    bvh = BVHTree.FromPolygons([Vector(p) for p in rco], [tuple(t) for t in tris], all_triangles=True)

    # decimate the game copy (shape keys are rebuilt afterwards; remember which clips drove them)
    bpy.context.view_layer.objects.active = mesh
    key_name = mesh.data.shape_keys.name if mesh.data.shape_keys else None
    mesh.shape_key_clear()
    weld(mesh)
    before = sum(len(p.vertices) - 2 for p in mesh.data.polygons)
    holes_before = open_edges(mesh)
    dec = mesh.modifiers.new("Decimate", "DECIMATE")
    dec.ratio = min(1.0, target / before)
    dec.use_collapse_triangulate = True
    bpy.ops.object.modifier_move_to_index(modifier=dec.name, index=0)
    bpy.ops.object.modifier_apply(modifier=dec.name)
    weld(mesh)
    after = sum(len(p.vertices) - 2 for p in mesh.data.polygons)
    print("OPEN EDGES source", holes_before, "-> game model", open_edges(mesh), "TRIS", after)

    # re-sample weights and blend shapes from the nearest point on the full surface
    gco = mesh_arrays(mesh)
    gw = np.zeros((len(gco), len(groups)))
    gd = [np.zeros((len(gco), 3)) for _ in key_names]
    for i, p in enumerate(gco):
        loc, _, face, _ = bvh.find_nearest(Vector(p))
        a, b, c = (rco[j] for j in tris[face])
        v0, v1, v2 = b - a, c - a, np.array(loc) - a
        d00, d01, d11 = v0 @ v0, v0 @ v1, v1 @ v1
        d20, d21 = v2 @ v0, v2 @ v1
        den = d00 * d11 - d01 * d01
        if abs(den) < 1e-14:
            bary = np.array([1.0, 0.0, 0.0])
        else:
            v = (d11 * d20 - d01 * d21) / den
            w = (d00 * d21 - d01 * d20) / den
            bary = np.clip(np.array([1 - v - w, v, w]), 0, 1)
            bary /= bary.sum()
        t = tris[face]
        gw[i] = bary @ rw[t]
        for k in range(len(key_names)):
            gd[k][i] = bary @ deltas[k][t]
    order = np.argsort(-gw, axis=1)
    keep = np.zeros_like(gw, dtype=bool)
    np.put_along_axis(keep, order[:, :4], True, axis=1)
    gw[~keep] = 0
    gw[gw < 0.01] = 0
    gw /= np.maximum(gw.sum(1, keepdims=True), 1e-9)
    for g in list(mesh.vertex_groups):
        mesh.vertex_groups.remove(g)
    for j, name in enumerate(groups):
        grp = mesh.vertex_groups.new(name=name)
        for i in np.nonzero(gw[:, j])[0]:
            grp.add([int(i)], float(gw[i, j]), "REPLACE")
    if key_names:
        mesh.shape_key_add(name="Basis", from_mix=False)
        for k, name in enumerate(key_names):
            kb = mesh.shape_key_add(name=name, from_mix=False)
            kb.data.foreach_set("co", (gco + gd[k]).ravel())
    # decimation scrambles the UVs along seams: give the light mesh its own UVs and bake the
    # full model's colours onto one small texture
    if keep_uv:
        use_source_texture(mesh, source_image)
    else:
        bake_texture(mesh, ref)
    bpy.data.objects.remove(ref)
    if key_name and mesh.data.shape_keys:
        for old in [k for k in bpy.data.shape_keys if k != mesh.data.shape_keys]:
            bpy.data.shape_keys.remove(old) if hasattr(bpy.data.shape_keys, "remove") else None
        mesh.data.shape_keys.name = key_name
    # one named track per clip for the skeleton and for the blend shapes: the exporter merges
    # tracks of the same name into one animation (bones + blend-shape weights)
    owners = [(rig, "OBJECT")]
    if mesh.data.shape_keys:
        owners.append((mesh.data.shape_keys, "KEY"))
    for owner, _ in owners:
        owner.animation_data_create()
        for t in list(owner.animation_data.nla_tracks):
            owner.animation_data.nla_tracks.remove(t)
        owner.animation_data.action = None
    for act in list(bpy.data.actions):
        for owner, kind in owners:
            slot = next((sl for sl in act.slots if sl.target_id_type == kind), None)
            if slot is None:
                continue
            track = owner.animation_data.nla_tracks.new()
            track.name = act.name
            strip = track.strips.new(act.name, int(act.frame_range[0]), act)
            strip.action_slot = slot


    # play in place: the root bone keeps its scale (squash and stretch) but not its travel
    for act in bpy.data.actions:
        for fc in channelbag_fcurves(act):
            if fc.data_path == 'pose.bones["root"].location':
                for kp in fc.keyframe_points:
                    kp.co.y = 0.0
                    kp.handle_left.y = kp.handle_right.y = 0.0
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.gltf(filepath=out, export_format="GLB", export_animations=True,
                              export_animation_mode="NLA_TRACKS", export_image_format="JPEG",
                              export_jpeg_quality=88, export_force_sampling=False,
                              export_morph=True, export_morph_normal=False, export_morph_animation=True,
                              export_optimize_animation_size=True)
    print("TRIS", before, "->", after, "EXPORTED", out, os.path.getsize(out))


if __name__ == "__main__":
    main()
