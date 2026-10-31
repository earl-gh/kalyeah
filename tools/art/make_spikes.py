#!/usr/bin/env python3
"""Rebuilds the mesh of the jacks model (assets/models/weapons/spikes.glb) as the owner's photo of plastic jacks (owner, 2026-10-31).

It edits the model in place: the same file, node, material and picture slot as the owner's earlier Meshy model, whose eight-pronged mesh
(with sheets and slivers inside and between its prongs) could not be bent into a clean jack, so its mesh is replaced by a clean low-poly one:

  * a round hub and six slim shafts that thicken toward the middle, one jack lying on three of its balls (the prongs are 35.26 degrees
    off the street);
  * four prongs end in a round ball; the two prongs of one pole (opposite each other) end in a plain rounded tip, as the photo's jacks do;
  * smooth normals (the game's contour is pushed out along them), about 2,200 triangles, one mesh, one material.

The picture is a plain light plastic: the game paints each cluster in one of the photo's four colours (yellow, pink, green, orange; see
scripts/view/art/weapon_model.gd). The extracted picture beside the model is rewritten too.

    python3 tools/art/make_spikes.py assets/models/weapons/spikes.glb assets/models/weapons/spikes.glb

Needs numpy and Pillow.
"""
import io
import json
import struct
import sys

import numpy as np
from PIL import Image

# the jack, in model units (about one unit across, like the model it replaces)
REACH = 0.633          # hub centre to the outside of a ball (and of a bare tip)
BALL = 0.158           # ball radius
SHAFT_TIP = 0.063      # shaft radius where it meets a ball
SHAFT_HUB = 0.12       # shaft radius at the hub
HUB = 0.14             # the hub sphere
BARE_TIP = 0.085       # a bare prong ends in a rounded tip this wide (radius)
ELEVATION = np.arcsin(1.0 / np.sqrt(3.0))
PLASTIC = (244, 244, 246)
SIDES = 16             # around a shaft or a ball
BALL_RINGS = 11
# the two opposite prongs without a ball: up prong slot 0 and down prong slot 1
BARE = {(True, 0), (False, 1)}
PHI = 90.0


class Mesh:
    def __init__(self):
        self.positions = []
        self.normals = []
        self.uvs = []
        self.indices = []

    def add(self, positions, normals, uvs, rings, sides):
        """A grid of `rings` x `sides` vertices (the last column wraps), joined into quads (two triangles each)."""
        base = len(self.positions)
        self.positions.extend(positions)
        self.normals.extend(normals)
        self.uvs.extend(uvs)
        for r in range(rings - 1):
            for s in range(sides):
                a = base + r * (sides + 1) + s
                b = a + 1
                c = a + (sides + 1)
                d = c + 1
                self.indices.extend([(a, c, b), (b, c, d)])


def frame(direction):
    """Two unit vectors perpendicular to `direction` (and to each other)."""
    helper = np.array([0.0, 1.0, 0.0]) if abs(direction[1]) < 0.9 else np.array([1.0, 0.0, 0.0])
    u = np.cross(direction, helper)
    u /= np.linalg.norm(u)
    return u, np.cross(direction, u)


def sphere(mesh, centre, radius, axis, rings, hemisphere=False):
    """A UV sphere (or its half toward `axis`) with its poles on `axis`."""
    u, w = frame(axis)
    positions, normals, uvs = [], [], []
    top = rings - 1
    for r in range(rings):
        lat = (np.pi / 2.0) * (r / top) if hemisphere else np.pi * (r / top)  # 0 at the pole toward `axis`
        for s in range(SIDES + 1):
            lon = 2.0 * np.pi * s / SIDES
            n = axis * np.cos(lat) + (u * np.cos(lon) + w * np.sin(lon)) * np.sin(lat)
            positions.append(centre + n * radius)
            normals.append(n)
            uvs.append((s / SIDES, r / top))
    mesh.add(positions, normals, uvs, rings, SIDES)


def frustum(mesh, start, direction, length, r0, r1, rings=4):
    """A cone from `start` along `direction`: radius r0 at the start, r1 at `length`."""
    u, w = frame(direction)
    slope = (r0 - r1) / length
    positions, normals, uvs = [], [], []
    for r in range(rings):
        t = r / (rings - 1)
        radius = r0 + (r1 - r0) * t
        for s in range(SIDES + 1):
            lon = 2.0 * np.pi * s / SIDES
            radial = u * np.cos(lon) + w * np.sin(lon)
            n = radial + direction * slope
            n /= np.linalg.norm(n)
            positions.append(start + direction * (length * t) + radial * radius)
            normals.append(n)
            uvs.append((s / SIDES, t))
    mesh.add(positions, normals, uvs, rings, SIDES)


def build():
    mesh = Mesh()
    hub = np.zeros(3)
    sphere(mesh, hub, HUB, np.array([0.0, 1.0, 0.0]), 9)
    length = REACH - BALL
    for up in (True, False):
        for slot in range(3):
            azimuth = np.radians(PHI + (120.0 * slot if up else 60.0 + 120.0 * slot))
            e = ELEVATION if up else -ELEVATION
            d = np.array([np.cos(e) * np.cos(azimuth), np.sin(e), np.cos(e) * np.sin(azimuth)])
            if (up, slot) in BARE:
                end = REACH - BARE_TIP
                frustum(mesh, hub, d, end, SHAFT_HUB, BARE_TIP)
                sphere(mesh, hub + d * end, BARE_TIP, d, 6, hemisphere=True)
            else:
                joint = length - np.sqrt(BALL ** 2 - SHAFT_TIP ** 2)  # where the shaft is as wide as the ball
                frustum(mesh, hub, d, joint, SHAFT_HUB, SHAFT_TIP)
                sphere(mesh, hub + d * length, BALL, d, BALL_RINGS)
    positions = np.array(mesh.positions, dtype=np.float64)
    positions[:, 1] -= positions[:, 1].min()  # lying on the street
    normals = np.array(mesh.normals, dtype=np.float32)
    indices = np.array(mesh.indices, dtype=np.int64)
    # keep every triangle facing the way its normals point
    a, b, c = positions[indices[:, 0]], positions[indices[:, 1]], positions[indices[:, 2]]
    face = np.cross(b - a, c - a)
    towards = np.array(mesh.normals)[indices].sum(1)
    flip = (face * towards).sum(1) < 0.0
    indices[flip] = indices[flip][:, [0, 2, 1]]
    # the poles of a sphere make zero-area triangles: leave them out
    area = np.linalg.norm(np.cross(positions[indices[:, 1]] - positions[indices[:, 0]], positions[indices[:, 2]] - positions[indices[:, 0]]), axis=1)
    indices = indices[area > 1e-9]
    return positions.astype(np.float32), normals, np.array(mesh.uvs, dtype=np.float32), indices.astype(np.uint16)


def read_glb(path):
    data = open(path, "rb").read()
    json_len, = struct.unpack("<I", data[12:16])
    js = json.loads(data[20:20 + json_len])
    start = 20 + json_len + 8
    return js, data[start:]


def write_glb(path, js, blob, positions, normals, uvs, indices):
    picture = io.BytesIO()
    Image.new("RGB", (512, 512), PLASTIC).save(picture, "JPEG", quality=90)
    old_views = js["bufferViews"]
    image_view = js["images"][0]["bufferView"]
    out = bytearray()
    views = []
    accessors = []
    for name, array, target in (("POSITION", positions, 34962), ("NORMAL", normals, 34962), ("TEXCOORD_0", uvs, 34962), ("INDICES", indices, 34963)):
        while len(out) % 4:
            out.append(0)
        raw = array.tobytes()
        views.append({"buffer": 0, "byteOffset": len(out), "byteLength": len(raw), "target": target})
        out += raw
    while len(out) % 4:
        out.append(0)
    views.append({"buffer": 0, "byteOffset": len(out), "byteLength": len(picture.getvalue())})
    out += picture.getvalue()
    while len(out) % 4:
        out.append(0)
    count = len(positions)
    accessors = [
        {"bufferView": 0, "componentType": 5126, "count": count, "type": "VEC3", "min": [float(x) for x in positions.min(0)], "max": [float(x) for x in positions.max(0)]},
        {"bufferView": 1, "componentType": 5126, "count": count, "type": "VEC3"},
        {"bufferView": 2, "componentType": 5126, "count": count, "type": "VEC2"},
        {"bufferView": 3, "componentType": 5123, "count": int(indices.size), "type": "SCALAR"},
    ]
    js["bufferViews"] = views
    js["accessors"] = accessors
    js["buffers"] = [{"byteLength": len(out)}]
    js["images"][0]["bufferView"] = 4
    js["meshes"][0]["primitives"][0]["attributes"] = {"POSITION": 0, "NORMAL": 1, "TEXCOORD_0": 2}
    js["meshes"][0]["primitives"][0]["indices"] = 3
    with open(path.rsplit(".", 1)[0] + "_prop_base_color.jpg", "wb") as f:
        f.write(picture.getvalue())
    body = json.dumps(js, separators=(",", ":")).encode()
    while len(body) % 4:
        body += b" "
    total = 12 + 8 + len(body) + 8 + len(out)
    with open(path, "wb") as f:
        f.write(struct.pack("<III", 0x46546C67, 2, total))
        f.write(struct.pack("<II", len(body), 0x4E4F534A))
        f.write(body)
        f.write(struct.pack("<II", len(out), 0x004E4942))
        f.write(out)


def main():
    source, target = sys.argv[1], sys.argv[2]
    js, blob = read_glb(source)
    positions, normals, uvs, indices = build()
    write_glb(target, js, blob, positions, normals, uvs, indices.reshape(-1))
    print("vertices", len(positions), "triangles", len(indices), "size", np.round(positions.max(0) - positions.min(0), 3))


if __name__ == "__main__":
    main()
