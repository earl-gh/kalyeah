"""Modelling helpers for the Kalyeah kids (Blender 4.5 LTS, run with the bpy module).

Style guide (owner reference picture, game-art pipeline: concept -> blockout ->
model -> review):
  * Stylised chunky toy look, like Clash of Clans characters: a big head
    (about 40% of the height), short thick limbs, big hands, a strong silhouette.
  * Built like a box-modeller works, NOT from balloons: every body part is a
    tapered sweep of a squarish (super-ellipse) cross-section, with creased
    rims where cloth ends, so it has planes, corners and soft edges.
  * Clothes are offset shells over the body (hems, sleeves, trims), hair is a
    sculpted mass, not stacked spheres.
  * Matte only: roughness >= 0.8, no coat, no metal, low specular.
  * 1 unit = 1 metre, feet on z = 0, the kid faces -Y, +X is the kid's left.
"""

import math

import bmesh
import bpy
from mathutils import Matrix, Vector

# ---- colour and materials ---------------------------------------------------------

_MATS = {}


def srgb(hex_color):
    """'#rrggbb' to the linear RGB Blender wants."""
    h = hex_color.lstrip("#")
    out = []
    for i in (0, 2, 4):
        c = int(h[i:i + 2], 16) / 255.0
        out.append(c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4)
    return tuple(out)


def M(name, hex_color, rough=0.88):
    """A matte material. Same name + colour returns the same material."""
    key = (name, hex_color, rough)
    if key not in _MATS:
        m = bpy.data.materials.new(name)
        m.use_nodes = True
        p = m.node_tree.nodes["Principled BSDF"]
        p.inputs["Base Color"].default_value = (*srgb(hex_color), 1.0)
        p.inputs["Roughness"].default_value = rough
        p.inputs["Metallic"].default_value = 0.0
        p.inputs["Specular IOR Level"].default_value = 0.12
        p.inputs["Coat Weight"].default_value = 0.0
        _MATS[key] = m
    return _MATS[key]


# ---- kid container ----------------------------------------------------------------

class Kid:
    """The parts of one kid, joined and exported together."""

    def __init__(self, name, x=0.0):
        self.name = name
        self.x = x
        self.parts = []

    def adopt(self, obj, materials, subdiv=0, smooth=True):
        for m in (materials if isinstance(materials, (list, tuple)) else [materials]):
            obj.data.materials.append(m)
        if smooth:
            for poly in obj.data.polygons:
                poly.use_smooth = True
        if subdiv:
            mod = obj.modifiers.new("Subsurf", "SUBSURF")
            mod.levels = subdiv
            mod.render_levels = subdiv
            mod.use_creases = True
        self.parts.append(obj)
        return obj


# ---- sweeps (the main modelling tool) --------------------------------------------

def _superellipse(rx, ry, count, exponent):
    pts = []
    for i in range(count):
        a = 2.0 * math.pi * i / count
        c, s = math.cos(a), math.sin(a)
        pts.append((rx * math.copysign(abs(c) ** (2.0 / exponent), c),
                    ry * math.copysign(abs(s) ** (2.0 / exponent), s)))
    return pts


def _catmull(points, samples):
    """Smooth path through `points` (Catmull-Rom), `samples` points, with the
    parameter of every original point so radii can follow."""
    if len(points) == 2:
        return [points[0].lerp(points[1], i / (samples - 1)) for i in range(samples)], \
            [i / (samples - 1) for i in range(samples)]
    n = len(points) - 1
    out, params = [], []
    for s in range(samples):
        t = s / (samples - 1) * n
        i = min(int(t), n - 1)
        f = t - i
        p0 = points[max(i - 1, 0)]
        p1, p2 = points[i], points[i + 1]
        p3 = points[min(i + 2, n)]
        f2, f3 = f * f, f * f * f
        out.append(0.5 * ((2 * p1) + (-p0 + p2) * f + (2 * p0 - 5 * p1 + 4 * p2 - p3) * f2
                          + (-p0 + 3 * p1 - 3 * p2 + p3) * f3))
        params.append(t / n)
    return out, params


def _interp(keys, t):
    """keys: list of (t, value or tuple); linear interpolation, smoothstepped."""
    if t <= keys[0][0]:
        return keys[0][1]
    for (t0, v0), (t1, v1) in zip(keys, keys[1:]):
        if t <= t1:
            f = (t - t0) / max(t1 - t0, 1e-9)
            f = f * f * (3 - 2 * f)
            if isinstance(v0, tuple):
                return tuple(a + (b - a) * f for a, b in zip(v0, v1))
            return v0 + (v1 - v0) * f
    return keys[-1][1]


def sweep(kid, name, path, profile, material, e=3.0, samples=14, ring=28, subdiv=1,
          crease_start=0.0, crease_end=0.0, caps=(True, True), hint=(0, -1, 0),
          t0=0.0, t1=1.0, twist=None, materials=None, ngon_caps=True):
    """A tapered tube along `path` (list of Vector): the box-modeller's workhorse.

    profile: [(t, (rx, ry)) or (t, (rx, ry, e))] along 0..1; the cross-section is a
    super-ellipse (e = 2 round, 4 squarish) so parts have planes and corners.
    crease_start/end: crease on the first / last ring so the ends stay crisp
    (cloth hems, sleeve ends, the sole of a foot) when subdivided.
    t0, t1: use only a slice of the path (shorts end half way down a leg).
    """
    dense, dense_t = _catmull(path, 240)
    keep = []
    for k in range(samples):
        tt = t0 + (t1 - t0) * k / (samples - 1)
        idx = min(int(round(tt * 239)), 239)
        keep.append((dense[idx], tt))
    bm = bmesh.new()
    rings = []
    hint_v = Vector(hint)
    for k, (c, t) in enumerate(keep):
        prev = keep[max(k - 1, 0)][0]
        nxt = keep[min(k + 1, len(keep) - 1)][0]
        tan = (nxt - prev)
        if tan.length < 1e-9:
            tan = Vector((0, 0, 1))
        tan.normalize()
        h = hint_v if abs(tan.dot(hint_v)) < 0.95 else Vector((1, 0, 0))
        u = tan.cross(h).normalized()
        v = u.cross(tan).normalized()
        prof = _interp(profile, t)
        rx, ry = prof[0], prof[1]
        ex = prof[2] if len(prof) > 2 else e
        tw = math.radians(_interp(twist, t)) if twist else 0.0
        verts = []
        for x, y in _superellipse(rx, ry, ring, ex):
            if tw:
                x, y = x * math.cos(tw) - y * math.sin(tw), x * math.sin(tw) + y * math.cos(tw)
            verts.append(bm.verts.new(c + u * x + v * y))
        rings.append(verts)
    bm.verts.ensure_lookup_table()
    crease = bm.edges.layers.float.new("crease_edge")
    for a, b in zip(rings, rings[1:]):
        for i in range(ring):
            bm.faces.new((a[i], a[(i + 1) % ring], b[(i + 1) % ring], b[i]))
    if caps[0]:
        bm.faces.new(list(reversed(rings[0])))
    if caps[1]:
        bm.faces.new(rings[-1])
    bm.edges.ensure_lookup_table()
    for edge in bm.edges:
        vs = {v for v in edge.verts}
        if crease_start and all(v in rings[0] for v in vs):
            edge[crease] = crease_start
        if crease_end and all(v in rings[-1] for v in vs):
            edge[crease] = crease_end
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    return kid.adopt(obj, materials or material, subdiv=subdiv)


def block(kid, name, centre, size, material, e=4.0, subdiv=1, rot=(0, 0, 0), crease=0.7,
          ring=24):
    """A rounded block (a squarish sweep, short and wide): pockets, belts, soles."""
    c = Vector(centre)
    path = [c - Vector((0, 0, size[2] / 2)), c + Vector((0, 0, size[2] / 2))]
    obj = sweep(kid, name, path, [(0.0, (size[0] / 2, size[1] / 2)), (1.0, (size[0] / 2, size[1] / 2))],
                material, e=e, samples=3, ring=ring, subdiv=subdiv,
                crease_start=crease, crease_end=crease)
    if any(rot):
        obj.rotation_euler = rot
        pivot = Matrix.Translation(-c)
        # rotate around its own centre
        obj.location = obj.location + Vector((0, 0, 0))
        mw = Matrix.Translation(c) @ Matrix.Rotation(rot[2], 4, "Z") @ Matrix.Rotation(rot[1], 4, "Y") \
            @ Matrix.Rotation(rot[0], 4, "X") @ pivot
        obj.rotation_euler = (0, 0, 0)
        obj.data.transform(mw)
    return obj


def ball(kid, name, centre, radii, material, subdiv=2, rot=(0, 0, 0)):
    """A smooth ellipsoid (eyes, nose, knuckles: small round things only)."""
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=20, v_segments=12, radius=1.0)
    for v in bm.verts:
        v.co = Vector((v.co.x * radii[0], v.co.y * radii[1], v.co.z * radii[2]))
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    mesh.transform(Matrix.Rotation(rot[2], 4, "Z") @ Matrix.Rotation(rot[1], 4, "Y")
                   @ Matrix.Rotation(rot[0], 4, "X"))
    mesh.transform(Matrix.Translation(Vector(centre)))
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    return kid.adopt(obj, material, subdiv=subdiv)


def apply_modifiers(obj):
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    for mod in list(obj.modifiers):
        bpy.ops.object.modifier_apply(modifier=mod.name)
    obj.select_set(False)


def bumpy(obj, scale, strength, seed=0, subdiv=3, kind="VORONOI"):
    """Subdivide the object and push the surface with a cellular texture: one smooth
    lumpy mass (curly hair, afro) instead of a pile of balls."""
    mod = obj.modifiers.get("Subsurf")
    if mod is None:
        mod = obj.modifiers.new("Subsurf", "SUBSURF")
    mod.levels = subdiv
    mod.render_levels = subdiv
    tex = bpy.data.textures.new("bump_" + obj.name, kind)
    tex.noise_scale = scale
    if kind == "VORONOI":
        tex.distance_metric = "DISTANCE"
        tex.noise_intensity = 0.0
    disp = obj.modifiers.new("Bump", "DISPLACE")
    disp.texture = tex
    disp.strength = strength
    disp.mid_level = 0.35
    # the texture is sampled at the object's local coordinates; offset by seed
    obj.location.x += 0.0
    return obj


def color_faces(obj, picker, materials):
    """Apply modifiers, then give each face the material picker(face_centre) chooses
    (real materials: stripes and tie-dye export to glTF without textures)."""
    apply_modifiers(obj)
    for m in materials:
        if m.name not in [x.name for x in obj.data.materials]:
            obj.data.materials.append(m)
    index = {m.name: i for i, m in enumerate(obj.data.materials)}
    mw = obj.matrix_world
    for poly in obj.data.polygons:
        poly.material_index = index[picker(mw @ poly.center).name]
        poly.use_smooth = True
    return obj


# ---- export / stats -----------------------------------------------------------------

def finish(kid):
    """Apply every part's modifiers and join the parts into one mesh object whose
    origin is at the feet (x = 0, y = 0, z = 0)."""
    bpy.ops.object.select_all(action="DESELECT")
    for obj in kid.parts:
        apply_modifiers(obj)
        obj.select_set(True)
    bpy.context.view_layer.objects.active = kid.parts[0]
    bpy.ops.object.join()
    joined = bpy.context.active_object
    joined.name = kid.name
    joined.data.name = kid.name
    kid.parts = [joined]
    return joined
