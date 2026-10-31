#!/usr/bin/env python3
"""Reshapes the jacks model (assets/models/weapons/spikes.glb) onto the owner's photo of plastic jacks, by instruction (owner, 2026-10-31).

Like the spinning top's edit it keeps the model: the same 3,011 vertices, 3,000 triangles, UVs, material and node. Only the vertex
positions and normals move (onto a six-pronged jack: a small round hub, six shafts that thicken toward the middle, a round ball at each
tip, lying on three of its balls), and the picture is rewritten to a plain light plastic so the game can paint each cluster in one of the
photo's four colours (yellow, pink, green, orange; see scripts/view/art/weapon_model.gd).

The old Meshy model has eight prongs (four up, four down); a photo jack has six, so one prong per side folds into the hub.

    python3 tools/art/reshape_spikes.py assets/models/weapons/spikes.glb assets/models/weapons/spikes.glb

Needs numpy and Pillow. Running it on an already reshaped model does nothing useful: reshape from the original (git history).
"""
import io
import json
import struct
import sys

import numpy as np
from PIL import Image

# The target jack, in the model's own units (the whole jack is about one unit across, like the model it replaces).
REACH = 0.633          # hub centre to the outside of a ball (about one unit across, like the model it replaces)
BALL = 0.158           # ball radius
SHAFT_TIP = 0.063      # shaft radius where it meets a ball
SHAFT_HUB = 0.12       # shaft radius at the hub
ELEVATION = np.arcsin(1.0 / np.sqrt(3.0))  # a jack resting on three balls: its prongs are 35.26 degrees off the street
PLASTIC = (244, 244, 246)  # the picture: plain light plastic; each cluster is tinted in the game
FOLD = 0.04            # a folded prong's vertices shrink to this part of their distance from the hub

OLD_HUB = np.array([0.0, 0.4303, 0.0])
OLD_ARM = 0.085        # the old shafts' radius
OLD_ARM_END = 0.30     # where the old shaft meets its ball, from the hub centre


def read_glb(path):
    data = open(path, "rb").read()
    json_len, = struct.unpack("<I", data[12:16])
    js = json.loads(data[20:20 + json_len])
    start = 20 + json_len + 8
    return js, data[start:]


def accessor(js, blob, index):
    a = js["accessors"][index]
    view = js["bufferViews"][a["bufferView"]]
    width = {"VEC3": 3, "VEC2": 2, "SCALAR": 1}[a["type"]]
    kind = {5126: "<f4", 5123: "<u2"}[a["componentType"]]
    offset = view.get("byteOffset", 0) + a.get("byteOffset", 0)
    return np.frombuffer(blob, dtype=kind, count=a["count"] * width, offset=offset).reshape(-1, width)


def fit_sphere(points):
    a = np.hstack([2.0 * points, np.ones((len(points), 1))])
    b = (points ** 2).sum(1)
    x = np.linalg.lstsq(a, b, rcond=None)[0]
    centre = x[:3]
    return centre, float(np.sqrt(x[3] + centre @ centre))


def unit(v):
    return v / np.maximum(np.linalg.norm(v, axis=-1, keepdims=True), 1e-9)


def smoothstep(lo, hi, x):
    t = np.clip((x - lo) / (hi - lo), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def reshape(P):  # returns positions, normals, the prong of every vertex and the hub
    """The new positions and normals of the old positions `P` (N x 3)."""
    v = P - OLD_HUB
    dist = np.linalg.norm(v, axis=1)
    az = np.degrees(np.arctan2(v[:, 2], v[:, 0])) % 360.0
    # the eight old prongs: four azimuths, up and down
    outer = dist > 0.38
    groups = []
    for up in (True, False):
        for centre_az in (0.0, 90.0, 180.0, 270.0):
            spread = np.abs(((az - centre_az) + 180.0) % 360.0 - 180.0)
            mask = outer & (spread < 40.0) & ((v[:, 1] > 0.0) == up)
            groups.append((up, centre_az, mask))
    balls = []
    for up, centre_az, mask in groups:
        cloud = P[mask & (dist > 0.45)]
        centre, radius = fit_sphere(cloud)
        balls.append((centre, radius))
    old_dirs = [unit(centre - OLD_HUB) for centre, _ in balls]
    # new jack: three prongs up (azimuth phi, +120, +240), three down (phi+60, +180, +300); the last of the four on each side folds in
    phi = 90.0
    new_az = {True: [phi, phi + 120.0, phi + 240.0], False: [phi + 60.0, phi + 180.0, phi + 300.0]}
    length = REACH - BALL
    hub = np.array([0.0, length * np.sin(ELEVATION) + BALL, 0.0])
    new_dirs, folded = [], []
    for index, (up, centre_az, mask) in enumerate(groups):
        slot = index % 4
        if slot == 3:
            new_dirs.append(None)
            folded.append(True)
            continue
        a = np.radians(new_az[up][slot])
        e = ELEVATION if up else -ELEVATION
        new_dirs.append(np.array([np.cos(e) * np.cos(a), np.sin(e), np.cos(e) * np.sin(a)]))
        folded.append(False)

    # each vertex belongs to the prong it points along (the nearest direction)
    cosines = np.stack([unit(v) @ d for d in old_dirs], axis=1)
    owner = np.argmax(cosines, axis=1)
    out = np.zeros_like(P)
    normal = np.zeros_like(P)
    joint_old = np.array([np.linalg.norm(balls[g][0] - OLD_HUB) - balls[g][1] for g in range(8)])
    joint_new = length - BALL
    for g in range(8):
        sel = owner == g
        if not sel.any():
            continue
        p = v[sel]
        d_old = old_dirs[g]
        if folded[g]:
            out[sel] = hub + p * FOLD
            normal[sel] = unit(p)
            continue
        d_new = new_dirs[g]
        centre, radius = balls[g]
        s = p @ d_old
        rho = p - np.outer(s, d_old)
        rho_len = np.linalg.norm(rho, axis=1)
        rho_dir = np.where(rho_len[:, None] > 1e-6, rho / np.maximum(rho_len[:, None], 1e-9), np.array([0.0, 1.0, 0.0]))
        # the ball: the old ball's points scaled onto the new ball
        to_ball = P[sel] - centre
        ball_dist = np.linalg.norm(to_ball, axis=1)
        w_ball = 1.0 - smoothstep(1.12 * radius, 1.55 * radius, ball_dist)
        w_ball = np.where(s > 0.1, w_ball, 0.0)
        new_centre = hub + d_new * length
        # the shaft's frame turns with the prong: its radial directions are carried over by the same rotation
        rot = rotation_between(d_old, d_new)
        ball_pos = new_centre + (to_ball @ rot.T) * (BALL / radius)
        ball_nrm = unit(to_ball @ rot.T)
        # the shaft: along its axis the old joint maps to the new one, the radius to a taper (thick at the hub, slim at the ball)
        s_new = np.where(s <= joint_old[g], s * (joint_new / joint_old[g]), joint_new + (s - joint_old[g]))
        t = np.clip(s_new / joint_new, 0.0, 1.0)
        r_new = SHAFT_HUB + (SHAFT_TIP - SHAFT_HUB) * t
        rho_new = r_new * np.minimum(rho_len / OLD_ARM, 1.0)  # the outside of the old shaft lands on the new cone
        rho_vec = (rho_dir @ rot.T) * rho_new[:, None]
        shaft_pos = hub + np.outer(s_new, d_new) + rho_vec
        slope = (SHAFT_HUB - SHAFT_TIP) / joint_new
        shaft_nrm = unit((rho_dir @ rot.T) + slope * d_new)
        out[sel] = shaft_pos * (1.0 - w_ball[:, None]) + ball_pos * w_ball[:, None]
        normal[sel] = unit(shaft_nrm * (1.0 - w_ball[:, None]) + ball_nrm * w_ball[:, None])
    # lying on three balls: the lowest point is the street
    lowest = float(out[:, 1].min())
    out[:, 1] -= lowest
    hub = hub - np.array([0.0, lowest, 0.0])
    return out.astype(np.float32), normal.astype(np.float32), owner, hub


def rotation_between(a, b):
    """The rotation matrix that turns unit vector `a` onto unit vector `b`."""
    c = float(a @ b)
    if c > 0.999999:
        return np.eye(3)
    axis = np.cross(a, b)
    s = np.linalg.norm(axis)
    k = np.array([[0.0, -axis[2], axis[1]], [axis[2], 0.0, -axis[0]], [-axis[1], axis[0], 0.0]])
    return np.eye(3) + k + k @ k * ((1.0 - c) / (s * s))


def drop_strays(indices, positions, owner, hub):
    """The old mesh has long thin triangles from one prong to another (hidden inside the old shape); on the new one they would show as
    streaks. Any triangle whose corners belong to different prongs and reach out of the hub is collapsed to one point (still 3,000 of
    them, none visible)."""
    kept = indices.copy()
    reach = np.linalg.norm(positions - hub, axis=1)
    for t, tri in enumerate(indices):
        if len({int(owner[i]) for i in tri}) > 1 and reach[tri].max() > 0.24:
            kept[t] = (tri[0], tri[0], tri[0])
    return kept


def write_glb(path, js, blob, positions, normals, indices):
    views = js["bufferViews"]
    arrays = {}
    for index, view in enumerate(views):
        arrays[index] = blob[view.get("byteOffset", 0):view.get("byteOffset", 0) + view["byteLength"]]
    pos_view = js["accessors"][0]["bufferView"]
    nrm_view = js["accessors"][1]["bufferView"]
    arrays[pos_view] = positions.tobytes()
    arrays[nrm_view] = normals.tobytes()
    arrays[js["accessors"][3]["bufferView"]] = indices.astype("<u2").tobytes()
    image_view = js["images"][0]["bufferView"]
    picture = io.BytesIO()
    Image.new("RGB", (512, 512), PLASTIC).save(picture, "JPEG", quality=90)
    arrays[image_view] = picture.getvalue()
    # the extracted picture beside the model (the importer keeps it) is the same plain plastic
    with open(path.rsplit(".", 1)[0] + "_prop_base_color.jpg", "wb") as f:
        f.write(picture.getvalue())
    out = bytearray()
    for index, view in enumerate(views):
        while len(out) % 4:
            out.append(0)
        view["byteOffset"] = len(out)
        view["byteLength"] = len(arrays[index])
        out += arrays[index]
    while len(out) % 4:
        out.append(0)
    js["buffers"][0]["byteLength"] = len(out)
    js["accessors"][0]["min"] = [float(x) for x in positions.min(0)]
    js["accessors"][0]["max"] = [float(x) for x in positions.max(0)]
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
    positions = accessor(js, blob, 0).astype(np.float64)
    new_positions, new_normals, owner, hub = reshape(positions)
    indices = drop_strays(accessor(js, blob, 3).reshape(-1, 3), new_positions, owner, hub)
    write_glb(target, js, blob, new_positions, new_normals, indices)
    folded = int((indices[:, 0] == indices[:, 1]).sum())
    print("vertices", len(new_positions), "triangles", len(indices), "collapsed", folded, "size", np.round(new_positions.max(0) - new_positions.min(0), 3))


if __name__ == "__main__":
    main()
