"""Normalize a scaled authored kid GLB and remove its incompatible finger animation.

Run with Blender 4.3+:
    blender --background --factory-startup --python tools/blender/normalize_scaled_kid_rig.py -- input.glb output.glb

Ligaya and Migo arrived with the same uniform scale on the armature object and Hips. Applying that scale twice restores
the 1.70 m source height used by the other kids. The finger tracks target a different rest rig and stretch their hands;
the game previously removed those tracks at runtime, so this bakes that repair into the source model instead.
"""
import json
import os
import struct
import sys

import bpy


FINGERS = ("HandThumb", "HandIndex", "HandMiddle", "HandRing", "HandPinky")


def normalize_image_order(path):
    """Keep the committed <kid>_0 roughness, _1 colour, _2 normal extraction names stable."""
    with open(path, "rb") as handle:
        header = handle.read(12)
        magic, version, _ = struct.unpack("<4sII", header)
        if magic != b"glTF" or version != 2:
            raise RuntimeError("Expected a GLB 2.0 file")
        json_size, json_type = struct.unpack("<II", handle.read(8))
        if json_type != 0x4E4F534A:
            raise RuntimeError("Expected the GLB JSON chunk first")
        gltf = json.loads(handle.read(json_size).rstrip(b" \0"))
        bin_size, bin_type = struct.unpack("<II", handle.read(8))
        binary = handle.read(bin_size)
    material = gltf["materials"][0]
    pbr = material["pbrMetallicRoughness"]
    texture_ids = [
        pbr["metallicRoughnessTexture"]["index"],
        pbr["baseColorTexture"]["index"],
        material["normalTexture"]["index"],
    ]
    old_ids = [gltf["textures"][texture_id]["source"] for texture_id in texture_ids]
    if len(set(old_ids)) != 3:
        raise RuntimeError("Expected separate roughness, colour and normal images")
    old_images = gltf["images"]
    gltf["images"] = []
    for old_id in old_ids:
        image = dict(old_images[old_id])
        image.pop("name", None)
        gltf["images"].append(image)
    remap = {old_id: new_id for new_id, old_id in enumerate(old_ids)}
    for texture in gltf["textures"]:
        texture["source"] = remap[texture["source"]]
    encoded = json.dumps(gltf, separators=(",", ":")).encode("utf-8")
    encoded += b" " * (-len(encoded) % 4)
    binary += b"\0" * (-len(binary) % 4)
    total = 12 + 8 + len(encoded) + 8 + len(binary)
    with open(path, "wb") as handle:
        handle.write(struct.pack("<4sII", b"glTF", 2, total))
        handle.write(struct.pack("<II", len(encoded), 0x4E4F534A))
        handle.write(encoded)
        handle.write(struct.pack("<II", len(binary), bin_type))
        handle.write(binary)


separator = sys.argv.index("--")
source, target = [os.path.abspath(value) for value in sys.argv[separator + 1:separator + 3]]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=source)
roots = [obj for obj in bpy.context.scene.objects if obj.parent is None and obj.type == "EMPTY"]
armatures = [obj for root in roots for obj in root.children_recursive if obj.type == "ARMATURE"]
if len(roots) != 1 or len(armatures) != 1:
    raise RuntimeError("Expected one model root and one armature")
root, armature = roots[0], armatures[0]
source_scale = armature.scale.x
if max(abs(axis - source_scale) for axis in armature.scale) > 0.0001 or not 0.75 < source_scale < 0.85:
    raise RuntimeError("Expected the known uniform 0.8 rig scale, got %s" % tuple(armature.scale))

bpy.ops.object.select_all(action="DESELECT")
armature.select_set(True)
bpy.context.view_layer.objects.active = armature
bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
armature.scale = (1.0 / source_scale,) * 3
bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)

meshes = [obj for obj in root.children_recursive if obj.type == "MESH"]
lowest = min(
    (armature.matrix_world.inverted() @ obj.matrix_world @ vertex.co).z
    for obj in meshes
    for vertex in obj.data.vertices
)
armature.location.z = -lowest
bpy.ops.object.transform_apply(location=True, rotation=False, scale=False)
for action in bpy.data.actions:
    for curve in list(action.fcurves):
        if "Hips" in curve.data_path and curve.data_path.endswith(".location") and curve.array_index == 1:
            for key in curve.keyframe_points:
                key.co.y += lowest
                key.handle_left.y += lowest
                key.handle_right.y += lowest
        if any(finger in curve.data_path for finger in FINGERS):
            action.fcurves.remove(curve)

# The imported mesh already includes the armature scale, but its joint pivots do
# not. Normalize the bind skeleton too; unit object transforms alone leave the
# shoulders and knees outside the mesh and rotation tears the limbs apart.
bpy.context.view_layer.objects.active = armature
bpy.ops.object.mode_set(mode="EDIT")
for bone in armature.data.edit_bones:
    bone.head *= source_scale
    bone.tail *= source_scale
bpy.ops.object.mode_set(mode="OBJECT")
for action in bpy.data.actions:
    for curve in action.fcurves:
        if curve.data_path.endswith(".location"):
            for key in curve.keyframe_points:
                key.co.y *= source_scale
                key.handle_left.y *= source_scale
                key.handle_right.y *= source_scale

bpy.ops.object.select_all(action="DESELECT")
root.select_set(True)
for obj in root.children_recursive:
    obj.select_set(True)
bpy.context.view_layer.objects.active = armature
bpy.ops.export_scene.gltf(
    filepath=target,
    export_format="GLB",
    use_selection=True,
    export_animations=True,
    export_force_sampling=False,
    export_yup=True,
    export_cameras=False,
    export_lights=False,
)
normalize_image_order(target)
