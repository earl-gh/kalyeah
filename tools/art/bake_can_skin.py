"""Bakes the Marikina corned-beef skin onto the can model (assets/models/weapons/can.glb).

Run: python3 tools/art/bake_can_skin.py
Input: tools/art/src/can_label_views.webp (the owner's picture of the can seen from four sides, transparent background)
       and the mesh and UV layout inside assets/models/weapons/can.glb.
Output: assets/models/weapons/can_prop_base_color.jpg (the texture Godot reads) and the same picture inside the .glb.

The model's UV layout is a scatter of small islands, so the picture cannot be laid over it flat. Instead every texel of the
texture is looked up in 3D: the texel's place on the mesh gives an angle round the can and a height, and the colour comes
from the picture. The label is unwrapped from the front view (the whole label) and the back view (the nutrition panel);
the rims, the bevels and the lid are taken from the same picture. The light baked into the picture is evened out round the
can, so the scene's own light shapes the model (ModelLook).
"""
import io
import json
import math
import os
import struct

import numpy as np
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
GLB = os.path.join(ROOT, "assets", "models", "weapons", "can.glb")
JPG = os.path.join(ROOT, "assets", "models", "weapons", "can_prop_base_color.jpg")
SRC = os.path.join(ROOT, "tools", "art", "src", "can_label_views.webp")
SIZE = 1024
PANO_W, PANO_H = 1536, 512

# the picture: each view's body centre x, body radius (px), and where its label starts and ends at the sides of the body (the
# rows grow by `LIFT` px toward the middle, because the camera looks down on the can a little)
VIEWS = {
    "front": {"cx": 200.5, "r": 168.5, "top": 368.0, "bottom": 714.0},
    "back": {"cx": 905.5, "r": 164.0, "top": 372.0, "bottom": 716.0},
}
LIFT = 30.0
# the picture is sampled this share of the way to each side of the body, so the white behind the can never blends in at the edge
EDGE_SAFE = 0.985
# the lid: the ellipse of the rim in the front view (centre x, y, half width, half height) for a lid of radius LID_R
LID_CENTRE = (202.0, 330.0)
LID_AXES = (173.0, 31.0)
LID_R = 0.416
# model measurements (metres, the model is 1 high): the label runs between the rims, and the rims have these heights
BODY_BOTTOM, BODY_TOP = 0.045, 0.93
RIM_TOP_ROWS = (362.0, 398.0)  # picture rows of the top rim's front wall
RIM_BOTTOM_ROWS = (758.0, 782.0)  # picture rows of the bottom rim's front wall
# the label shows ARC degrees of each view; the rest of the way round is plain yellow
FRONT_ARC, BACK_ARC = 82.0, 50.0
FEATHER = 8.0


def load_picture():
    src = Image.open(SRC).convert("RGBA")
    flat = Image.new("RGBA", src.size, (255, 255, 255, 255))
    flat.alpha_composite(src)
    return np.asarray(flat.convert("RGB"), dtype=np.float32) / 255.0


def bilinear(img, x, y):
    h, w, _ = img.shape
    x = np.clip(x, 0.0, w - 1.001)
    y = np.clip(y, 0.0, h - 1.001)
    x0 = np.floor(x).astype(int)
    y0 = np.floor(y).astype(int)
    fx = (x - x0)[..., None]
    fy = (y - y0)[..., None]
    top = img[y0, x0] * (1 - fx) + img[y0, x0 + 1] * fx
    bottom = img[y0 + 1, x0] * (1 - fx) + img[y0 + 1, x0 + 1] * fx
    return top * (1 - fy) + bottom * fy


def unwrap(img, view, arc):
    """The label of one view as a strip: columns are the angle (-arc..arc degrees), rows the height (top to bottom)."""
    width = int(PANO_W * 2 * arc / 360.0)
    theta = np.radians(np.linspace(-arc, arc, width))
    v = np.linspace(0.0, 1.0, PANO_H)[:, None]
    x = view["cx"] + view["r"] * EDGE_SAFE * np.sin(theta)[None, :]
    lift = LIFT * np.cos(theta)[None, :]
    y = (view["top"] + lift) * (1.0 - v) + (view["bottom"] + lift) * v
    return bilinear(img, np.broadcast_to(x, (PANO_H, width)), y)


def is_yellow(rgb):
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    mx = rgb.max(-1)
    mn = rgb.min(-1)
    return (r > 0.75) & (g > 0.55) & (b < 0.35) & (mx - mn > 0.55) & (r - g < 0.35)


def even_out(strip, target):
    """Takes the light out of a strip: each column is scaled so its plain yellow matches `target` (the same light all round)."""
    yellow = is_yellow(strip)
    out = strip.copy()
    lum = strip.mean(-1)
    for column in range(strip.shape[1]):
        mask = yellow[:, column]
        if mask.sum() < 20:
            continue
        gain = target / max(lum[mask, column].mean(), 1e-3)
        out[:, column] = np.clip(strip[:, column] * np.clip(gain, 0.8, 1.4), 0.0, 1.0)
    return out


def yellow_column(strips):
    """The plain yellow of the label, one colour per row: the median of the yellow pixels of the strips."""
    rows = []
    for row in range(PANO_H):
        found = []
        for strip in strips:
            mask = is_yellow(strip[row])
            if mask.any():
                found.append(strip[row][mask])
        rows.append(np.median(np.concatenate(found), axis=0) if found else None)
    for i, value in enumerate(rows):
        if value is None:
            lower = next((rows[j] for j in range(i, PANO_H) if rows[j] is not None), None)
            upper = next((rows[j] for j in range(i, -1, -1) if rows[j] is not None), None)
            rows[i] = lower if lower is not None else upper
    column = np.array(rows, dtype=np.float32)
    for _ in range(3):  # a light vertical smoothing: no speckle in the flat part
        column[1:-1] = (column[:-2] + column[1:-1] * 2 + column[2:]) / 4.0
    return column


def build_panorama(img):
    """The whole label round the can: columns 0..PANO_W are the angle from -180 to 180 degrees (0 = the front)."""
    front = unwrap(img, VIEWS["front"], FRONT_ARC)
    back = unwrap(img, VIEWS["back"], BACK_ARC)
    target = float(np.median(front.mean(-1)[is_yellow(front)]))
    front = even_out(front, target)
    back = even_out(back, target)
    plain = yellow_column([front, back])
    pano = np.broadcast_to(plain[:, None, :], (PANO_H, PANO_W, 3)).copy()
    degrees = np.linspace(-180.0, 180.0, PANO_W, endpoint=False)
    for strip, centre, arc in ((front, 0.0, FRONT_ARC), (back, 180.0, BACK_ARC)):
        offset = (degrees - centre + 180.0) % 360.0 - 180.0
        inside = np.abs(offset) <= arc
        weight = np.clip((arc - np.abs(offset)) / FEATHER, 0.0, 1.0)[None, :, None]
        columns = np.clip(np.round((offset + arc) / (2 * arc) * (strip.shape[1] - 1)).astype(int), 0, strip.shape[1] - 1)
        sampled = strip[:, columns]
        pano = np.where(inside[None, :, None], pano * (1 - weight) + sampled * weight, pano)
    return pano


def rim_profile(img, rows, centre_x, half_width=50):
    """A rim's colour from top to bottom: the picture's rim rows averaged across the middle of the can."""
    height = 64
    ys = np.linspace(rows[0], rows[1], height)
    xs = np.linspace(centre_x - half_width, centre_x + half_width, 21)
    xx, yy = np.meshgrid(xs, ys)
    return bilinear(img, xx, yy).mean(1)


def read_mesh():
    data = open(GLB, "rb").read()
    json_len = struct.unpack("<I", data[12:16])[0]
    gltf = json.loads(data[20:20 + json_len])
    blob = data[20 + json_len + 8:]

    def accessor(index):
        a = gltf["accessors"][index]
        view = gltf["bufferViews"][a["bufferView"]]
        kind = {5126: "f4", 5123: "u2", 5125: "u4"}[a["componentType"]]
        count = {"VEC3": 3, "VEC2": 2, "SCALAR": 1}[a["type"]]
        start = view.get("byteOffset", 0) + a.get("byteOffset", 0)
        return np.frombuffer(blob, dtype=kind, count=a["count"] * count, offset=start).reshape(-1, count)

    prim = gltf["meshes"][0]["primitives"][0]["attributes"]
    return gltf, accessor(prim["POSITION"]), accessor(prim["NORMAL"]), accessor(prim["TEXCOORD_0"]), accessor(gltf["meshes"][0]["primitives"][0]["indices"]).reshape(-1, 3)


def rasterize(pos, nrm, uv, tris):
    """For every texel inside a UV island: its place on the mesh (x, y, z) and surface normal; `filled` marks those texels."""
    place = np.zeros((SIZE, SIZE, 3), np.float32)
    normal = np.zeros((SIZE, SIZE, 3), np.float32)
    filled = np.zeros((SIZE, SIZE), bool)
    for tri in tris:
        p = uv[tri] * SIZE
        x0, x1 = int(math.floor(p[:, 0].min())), int(math.ceil(p[:, 0].max()))
        y0, y1 = int(math.floor(p[:, 1].min())), int(math.ceil(p[:, 1].max()))
        x0, y0 = max(x0, 0), max(y0, 0)
        x1, y1 = min(x1, SIZE - 1), min(y1, SIZE - 1)
        if x1 < x0 or y1 < y0:
            continue
        xs, ys = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
        a, b, c = p
        det = (b[1] - c[1]) * (a[0] - c[0]) + (c[0] - b[0]) * (a[1] - c[1])
        if abs(det) < 1e-9:
            continue
        w0 = ((b[1] - c[1]) * (xs - c[0]) + (c[0] - b[0]) * (ys - c[1])) / det
        w1 = ((c[1] - a[1]) * (xs - c[0]) + (a[0] - c[0]) * (ys - c[1])) / det
        w2 = 1.0 - w0 - w1
        inside = (w0 >= -0.02) & (w1 >= -0.02) & (w2 >= -0.02)
        if not inside.any():
            continue
        coords = np.stack([w0, w1, w2], -1)[inside]
        yy, xx = ys[inside].astype(int), xs[inside].astype(int)
        place[yy, xx] = coords @ pos[tri]
        normal[yy, xx] = coords @ nrm[tri]
        filled[yy, xx] = True
    return place, normal, filled


def dilate(image, filled, passes=6):
    """Spreads the island edges outward so the mip levels and the filter never pull in an empty texel."""
    image = image.copy()
    filled = filled.copy()
    for _ in range(passes):
        grow = np.zeros_like(filled)
        total = np.zeros_like(image)
        count = np.zeros(filled.shape, np.float32)
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (-1, -1), (1, -1), (-1, 1)):
            moved = np.roll(np.roll(filled, dy, 0), dx, 1)
            colour = np.roll(np.roll(image, dy, 0), dx, 1)
            total += np.where(moved[..., None], colour, 0.0)
            count += moved
        add = (~filled) & (count > 0)
        image[add] = total[add] / count[add][:, None]
        filled |= add
        grow |= add
    return image


def paint(pos, nrm, img, pano):
    x, y, z = pos[..., 0], pos[..., 1], pos[..., 2]
    ny = nrm[..., 1] / np.maximum(np.linalg.norm(nrm, axis=-1), 1e-6)
    theta = np.degrees(np.arctan2(x, z))
    out = np.zeros((SIZE, SIZE, 3), np.float32)
    # the label between the rims
    v = (BODY_TOP - y) / (BODY_TOP - BODY_BOTTOM)
    column = (theta + 180.0) / 360.0 * PANO_W
    row = np.clip(v, 0.0, 1.0) * (PANO_H - 1)
    wrapped = np.concatenate([pano, pano[:, :2]], axis=1)
    label = bilinear(wrapped, column, row)
    # the rims
    top_rim = rim_profile(img, RIM_TOP_ROWS, VIEWS["front"]["cx"])
    bottom_rim = rim_profile(img, RIM_BOTTOM_ROWS, VIEWS["front"]["cx"])
    top_t = np.clip((1.0 - y) / (1.0 - BODY_TOP), 0.0, 1.0)
    bottom_t = np.clip((BODY_BOTTOM - y) / BODY_BOTTOM, 0.0, 1.0)
    top_wall = top_rim[np.round(top_t * (len(top_rim) - 1)).astype(int)]
    bottom_wall = bottom_rim[np.round(bottom_t * (len(bottom_rim) - 1)).astype(int)]
    # the lid, seen from above in the picture's ellipse
    u = LID_CENTRE[0] + x * (LID_AXES[0] / LID_R)
    w = LID_CENTRE[1] + z * (LID_AXES[1] / LID_R)
    lid = bilinear(img, u, w)
    steel = bottom_rim.mean(0)
    is_lid = (y >= BODY_TOP + 0.005) & (ny > 0.35)
    is_top_wall = (y >= BODY_TOP) & ~is_lid
    is_bottom = y <= BODY_BOTTOM
    out[:] = label
    out[is_bottom] = bottom_wall[is_bottom]
    out[is_bottom & (ny < -0.5)] = steel * 0.7
    out[is_top_wall] = top_wall[is_top_wall]
    out[is_lid] = lid[is_lid]
    return out


def write_glb(jpeg_bytes):
    data = bytearray(open(GLB, "rb").read())
    json_len = struct.unpack("<I", data[12:16])[0]
    gltf = json.loads(bytes(data[20:20 + json_len]))
    blob = bytes(data[20 + json_len + 8:])
    views = gltf["bufferViews"]
    image_view = gltf["images"][0]["bufferView"]
    pieces = []
    cursor = 0
    for index, view in enumerate(views):
        start = view.get("byteOffset", 0)
        payload = jpeg_bytes if index == image_view else blob[start:start + view["byteLength"]]
        pad = (-cursor) % 4
        cursor += pad
        pieces.append(b"\0" * pad)
        view["byteOffset"] = cursor
        view["byteLength"] = len(payload)
        pieces.append(payload)
        cursor += len(payload)
    new_blob = b"".join(pieces) + b"\0" * ((-cursor) % 4)
    gltf["buffers"][0]["byteLength"] = len(new_blob)
    new_json = json.dumps(gltf, separators=(",", ":")).encode()
    new_json += b" " * ((-len(new_json)) % 4)
    total = 12 + 8 + len(new_json) + 8 + len(new_blob)
    out = struct.pack("<4sII", b"glTF", 2, total)
    out += struct.pack("<I4s", len(new_json), b"JSON") + new_json
    out += struct.pack("<I4s", len(new_blob), b"BIN\0") + new_blob
    open(GLB, "wb").write(out)


def main():
    img = load_picture()
    pano = build_panorama(img)
    _, pos, nrm, uv, tris = read_mesh()
    place, normal, filled = rasterize(pos, nrm, uv, tris)
    colour = paint(place, normal, img, pano)
    colour = dilate(colour, filled)
    result = Image.fromarray(np.clip(colour * 255.0 + 0.5, 0, 255).astype(np.uint8), "RGB")
    result.save(JPG, "JPEG", quality=92, subsampling=0)
    buffer = io.BytesIO()
    result.save(buffer, "JPEG", quality=92, subsampling=0)
    write_glb(buffer.getvalue())
    print("wrote", JPG, os.path.getsize(JPG), "bytes")


if __name__ == "__main__":
    main()
