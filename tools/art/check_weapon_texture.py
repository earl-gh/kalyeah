"""Checks a weapon's base-colour picture against the Clash Royale painted look (docs/art-briefs/weapon-textures.md).

usage: python3 tools/art/check_weapon_texture.py picture.png [more pictures ...]
       python3 tools/art/check_weapon_texture.py --glbs assets/models/weapons/*.glb   (the picture inside each GLB)

Prints the numbers and PASS / FAIL for each rule. It only reads pictures; it never writes or edits one.
Needs Pillow and numpy.
"""
import io
import json
import struct
import sys

import numpy as np
from PIL import Image

# the rules (see the brief): a painted picture is a few flat colours with soft transitions
MIN_SIZE = 512
MAX_COLOURS = 48  # distinct colours after quantising to 6 levels per channel (used pixels only)
MAX_NOISE = 0.012  # median 3x3 local deviation of the value: flat painted areas are near 0, photo grain is not
MIN_DARKEST = 0.20  # no black fills: even the darkest 2% of the picture keeps some colour (outlines are a deep tone)
USED = 0.06  # a pixel is used if its brightest channel is above this (the rest is black padding)


def glb_picture(path):
    data = open(path, "rb").read()
    length = struct.unpack("<I", data[12:16])[0]
    doc = json.loads(data[20:20 + length])
    image = doc["images"][0]
    view = doc["bufferViews"][image["bufferView"]]
    start = 20 + length + 8 + view["byteOffset"]
    return Image.open(io.BytesIO(data[start:start + view["byteLength"]]))


def measure(picture):
    rgb = np.asarray(picture.convert("RGB")).astype(float) / 255.0
    used = rgb.max(axis=2) > USED
    value = rgb.max(axis=2)
    padded = np.pad(value, 1, mode="edge")
    stack = np.stack([padded[y:y + value.shape[0], x:x + value.shape[1]] for y in range(3) for x in range(3)])
    local = stack.std(axis=0)
    levels = (rgb[used] * 5).round().astype(int)
    return {
        "size": picture.size,
        "colours": len(set(map(tuple, levels))),
        "noise": float(np.median(local[used])),
        "darkest": float(np.percentile(value[used], 2)) if used.any() else 0.0,
        "used": float(used.mean()),
    }


def check(name, picture):
    m = measure(picture)
    rules = [
        ("size %dx%d >= %d" % (m["size"][0], m["size"][1], MIN_SIZE), min(m["size"]) >= MIN_SIZE),
        ("colours %d <= %d" % (m["colours"], MAX_COLOURS), m["colours"] <= MAX_COLOURS),
        ("noise %.4f <= %.4f" % (m["noise"], MAX_NOISE), m["noise"] <= MAX_NOISE),
        ("darkest 2%% %.2f >= %.2f" % (m["darkest"], MIN_DARKEST), m["darkest"] >= MIN_DARKEST),
    ]
    print(name)
    for text, ok in rules:
        print("  %s  %s" % ("PASS" if ok else "FAIL", text))
    return all(ok for _, ok in rules)


def main(argv):
    if len(argv) < 2:
        print(__doc__)
        return 2
    glbs = argv[1] == "--glbs"
    ok = True
    for path in argv[2:] if glbs else argv[1:]:
        ok = check(path, glb_picture(path) if glbs else Image.open(path)) and ok
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv))
