#!/usr/bin/env python3
"""Download the licence-checked free assets in tools/art/free_assets.json and make each one a single .glb.

Only CC0 sources are wired (Poly Haven). Each model is fetched as glTF with 1k textures, packed into one binary .glb
(buffers and images embedded), checked against the triangle budget, and logged in assets/CREDITS.md
(CLAUDE.md "Assets"). Run from the repo root: python tools/art/fetch_free_assets.py [--only ID ...]
"""
from __future__ import annotations

import argparse
import json
import struct
import sys
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
MANIFEST = ROOT / "tools" / "art" / "free_assets.json"
CREDITS = ROOT / "assets" / "CREDITS.md"
POLYHAVEN_API = "https://api.polyhaven.com"
USER_AGENT = "kalyeah-asset-fetch"


def fetch(url: str) -> bytes:
    req = urllib.request.Request(url, headers={"User-Agent": USER_AGENT})
    with urllib.request.urlopen(req, timeout=120) as resp:
        return resp.read()


def fetch_json(url: str) -> dict:
    return json.loads(fetch(url))


def pad4(data: bytes, fill: bytes = b"\x00") -> bytes:
    return data + fill * ((4 - len(data) % 4) % 4)


def pack_glb(gltf: dict, files: dict[str, bytes]) -> bytes:
    """Embeds every external buffer and image of `gltf` (uris looked up in `files`) into one GLB."""
    blob = bytearray()
    buffer_offsets = []
    for buf in gltf.get("buffers", []):
        data = files[buf["uri"]]
        buffer_offsets.append(len(blob))
        blob += pad4(data)
    for view in gltf.get("bufferViews", []):
        view["byteOffset"] = view.get("byteOffset", 0) + buffer_offsets[view.get("buffer", 0)]
        view["buffer"] = 0

    views = gltf.setdefault("bufferViews", [])
    for image in gltf.get("images", []):
        uri = image.pop("uri")
        data = files[uri]
        views.append({"buffer": 0, "byteOffset": len(blob), "byteLength": len(data)})
        image["bufferView"] = len(views) - 1
        image["mimeType"] = "image/png" if uri.lower().endswith(".png") else "image/jpeg"
        blob += pad4(data)

    gltf["buffers"] = [{"byteLength": len(blob)}]
    json_chunk = pad4(json.dumps(gltf, separators=(",", ":")).encode("utf-8"), b" ")
    bin_chunk = bytes(blob)
    total = 12 + 8 + len(json_chunk) + 8 + len(bin_chunk)
    out = bytearray(struct.pack("<4sII", b"glTF", 2, total))
    out += struct.pack("<I4s", len(json_chunk), b"JSON") + json_chunk
    out += struct.pack("<I4s", len(bin_chunk), b"BIN\x00") + bin_chunk
    return bytes(out)


def triangle_count(gltf: dict) -> int:
    tris = 0
    for mesh in gltf.get("meshes", []):
        for prim in mesh.get("primitives", []):
            if prim.get("mode", 4) != 4:
                continue
            if "indices" in prim:
                tris += gltf["accessors"][prim["indices"]]["count"] // 3
            else:
                tris += gltf["accessors"][prim["attributes"]["POSITION"]]["count"] // 3
    return tris


def polyhaven(asset_id: str) -> tuple[bytes, dict, int]:
    info = fetch_json(f"{POLYHAVEN_API}/info/{asset_id}")
    gltf_entry = fetch_json(f"{POLYHAVEN_API}/files/{asset_id}")["gltf"]["1k"]["gltf"]
    gltf = json.loads(fetch(gltf_entry["url"]))
    files = {name: fetch(entry["url"]) for name, entry in gltf_entry["include"].items()}
    meta = {
        "source_url": f"https://polyhaven.com/a/{asset_id}",
        "authors": ", ".join(info.get("authors", {}).keys()) or "Poly Haven",
        "licence": "CC0",
        "name": info.get("name", asset_id),
    }
    return pack_glb(gltf, files), meta, triangle_count(gltf)


def log_credit(out_rel: str, meta: dict, changes: str) -> None:
    text = CREDITS.read_text(encoding="utf-8")
    row = f"| `{out_rel}` | {meta['source_url']} ({meta['name']}) | {meta['authors']} | {meta['licence']} | {changes} |\n"
    lines = [line for line in text.splitlines(keepends=True) if not line.startswith(f"| `{out_rel}` |")]
    CREDITS.write_text("".join(lines).rstrip("\n") + "\n" + row, encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--only", nargs="*", help="asset ids to fetch (default: all)")
    args = parser.parse_args()

    manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
    budget = int(manifest["max_triangles"])
    failed = 0
    for asset in manifest["assets"]:
        if args.only and asset["id"] not in args.only:
            continue
        if asset["source"] != "polyhaven":
            print(f"skip {asset['id']}: source {asset['source']} is not wired", file=sys.stderr)
            failed += 1
            continue
        glb, meta, tris = polyhaven(asset["id"])
        if tris > budget:
            print(f"refuse {asset['id']}: {tris} triangles > {budget}", file=sys.stderr)
            failed += 1
            continue
        out = ROOT / asset["out"]
        out.parent.mkdir(parents=True, exist_ok=True)
        out.write_bytes(glb)
        log_credit(asset["out"], meta, f"Packed to one .glb at 1k textures ({tris} triangles); used as: {asset['role']}")
        print(f"ok {asset['id']} -> {asset['out']} ({tris} tris, {len(glb) // 1024} KB)")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
