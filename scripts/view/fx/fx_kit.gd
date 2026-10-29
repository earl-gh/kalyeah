class_name FxKit
extends RefCounted
## The shared pieces every skill effect draws with, built once per game run: flat meshes (a ring, a disc, a 4-point star,
## a plus, a quad, smear ribbons, a soft puff) and the see-through overlay materials. Nothing here is made per hit or per
## frame; pools ask for their own slot materials once, when they are built (docs/ART.md "Skill effects").
## Also the ground layer ladder: every flat effect sits at its own height and draws in its own order, so marks never
## flicker against each other or against the street (the street art is at 0.004, the kid's blob shadow at 0.08).

## Zone fill (a landing circle, a slow zone) and its rim / a shield's team ribbon, in metres above the street.
const Y_FILL: float = 0.03
const Y_RIM: float = 0.05
## A flying object's ground shadow.
const Y_SHADOW: float = 0.055
## Hit rings, cracks, flash discs and other short decals.
const Y_DECAL: float = 0.09
## The cone flash (a snip's wedge) and its rim.
const Y_FLASH: float = 0.10
const Y_FLASH_RIM: float = 0.12
## A delayed cone's warning (its rim sits a little higher, its growing fill in between).
const Y_WINDUP: float = 0.14
const Y_WINDUP_RIM: float = 0.16
## Draw order of the see-through layers (transparent layers do not write depth, so the order is set here).
const PRIO_FILL: int = 0
const PRIO_RIM: int = 1
const PRIO_SHADOW: int = 2
const PRIO_DECAL: int = 3
## A landing circle, cone flash or trap says who threw it with an opaque rim this wide (metres) round a see-through fill.
const RIM: float = 0.22
const RIM_ALPHA: float = 0.95
const FILL_ALPHA: float = 0.4
## The unit ring (radius 1) is this wide: a hit ring of 1 m radius has a 0.08 m line.
const RING_WIDTH: float = 0.08
## Points round a full circle of the shared ring, disc and puff.
const CIRCLE_POINTS: int = 32
## The smear arc spans this many degrees (the snip's whole wedge) and is this wide at its middle, per metre of radius.
const SMEAR_ARC_DEGREES: float = 60.0
const SMEAR_ARC_WIDTH: float = 0.12
const SMEAR_POINTS: int = 12
## The smack streak: 1 m long, this wide at its middle.
const STREAK_WIDTH: float = 0.16
## The 4-point star: its tips at 0.5 (a 1 m star), its waist at this radius; white in the middle, sari yellow at the tips.
const STAR_WAIST: float = 0.16
## The plus sparkle: arms this thick (of a 1 m plus); a white edge this much bigger behind the green.
const PLUS_ARM: float = 0.34
const PLUS_EDGE: float = 0.12
## The soft puff: lighter in the middle, a little darker at the rim (a chunky painted look, no texture).
const PUFF_RIM_SHADE: float = 0.86
## The crack decal: a dark ring of this radius (of the unit) and four spokes reaching the edge, this wide (of the unit).
const CRACK_RING: float = 0.75
const CRACK_RING_WIDTH: float = 0.07
const CRACK_SPOKE_WIDTH: float = 0.05
const CRACK_SPOKES: int = 4

static var _materials_made: int = 0
static var _overlays: Dictionary[int, StandardMaterial3D] = {}
static var _meshes: Dictionary[StringName, ArrayMesh] = {}


## The shared see-through, unshaded, vertex-coloured material drawn in `priority` order (both sides, no culling).
static func overlay(priority: int) -> StandardMaterial3D:
	if not _overlays.has(priority):
		_overlays[priority] = slot_material(priority)
	return _overlays[priority]


## A new material like `overlay`, for a pool slot that fades on its own. Only pools call this, while they are built.
static func slot_material(priority: int) -> StandardMaterial3D:
	var material: StandardMaterial3D = StandardMaterial3D.new()
	material.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	material.vertex_color_use_as_albedo = true
	material.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	material.cull_mode = BaseMaterial3D.CULL_DISABLED
	material.render_priority = priority
	count_material()
	return material


## A billboard particle material (the quad always faces the camera, the particle's colour tints it).
static func particle_material(billboard: bool) -> StandardMaterial3D:
	var material: StandardMaterial3D = StandardMaterial3D.new()
	material.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	material.vertex_color_use_as_albedo = true
	material.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	material.cull_mode = BaseMaterial3D.CULL_DISABLED
	if billboard:
		material.billboard_mode = BaseMaterial3D.BILLBOARD_PARTICLES
		# without this a billboard drops each particle's own size (every puff would be its 1 m mesh, never shrinking)
		material.billboard_keep_scale = true
	material.render_priority = PRIO_DECAL
	count_material()
	return material


## Counts a material made outside this file for the effects (a pool's shader material).
static func count_material() -> void:
	_materials_made += 1


## Every material the effects have made so far (pools build theirs once; nothing is made per hit or frame).
static func material_count() -> int:
	return _materials_made


## A ring of radius 1 on the ground plane (y 0), `RING_WIDTH` wide, white (the material tints it).
static func ring_mesh() -> ArrayMesh:
	if not _meshes.has(&"ring"):
		var mesh: ImmediateMesh = _begin()
		AimIndicator.ribbon(mesh, AimIndicator.circle(Vector2.ZERO, 1.0), RING_WIDTH, Color.WHITE, 0.0)
		_meshes[&"ring"] = bake(mesh)
	return _meshes[&"ring"]


## A filled disc of radius 1 on the ground plane, white.
static func disc_mesh() -> ArrayMesh:
	if not _meshes.has(&"disc"):
		var mesh: ImmediateMesh = _begin()
		AimIndicator.fill(mesh, _circle(1.0), Color.WHITE, 0.0)
		_meshes[&"disc"] = bake(mesh)
	return _meshes[&"disc"]


## A 1 m four-point comic star standing in the XY plane (faces +Z, for billboards): white core, sari-yellow tips.
static func star_quad() -> ArrayMesh:
	if not _meshes.has(&"star"):
		var mesh: ImmediateMesh = _begin()
		for i: int in 8:
			var a: float = TAU * float(i) / 8.0
			var b: float = TAU * float(i + 1) / 8.0
			var ra: float = 0.5 if i % 2 == 0 else STAR_WAIST
			var rb: float = 0.5 if (i + 1) % 2 == 0 else STAR_WAIST
			_vertex(mesh, Vector3.ZERO, Palette.WHITE)
			_vertex(mesh, Vector3(cos(a) * ra, sin(a) * ra, 0.0), Palette.SARI_YELLOW if i % 2 == 0 else Palette.WHITE)
			_vertex(mesh, Vector3(cos(b) * rb, sin(b) * rb, 0.0), Palette.SARI_YELLOW if (i + 1) % 2 == 0 else Palette.WHITE)
		_meshes[&"star"] = bake(mesh)
	return _meshes[&"star"]


## A 1 m plus sparkle in the XY plane: green (guava) in front of a slightly bigger white plus.
static func plus_quad() -> ArrayMesh:
	if not _meshes.has(&"plus"):
		var mesh: ImmediateMesh = _begin()
		_plus(mesh, 0.5 + PLUS_EDGE / 2.0, PLUS_ARM + PLUS_EDGE, Palette.WHITE, -0.002)
		_plus(mesh, 0.5, PLUS_ARM, Palette.GUAVA, 0.0)
		_meshes[&"plus"] = bake(mesh)
	return _meshes[&"plus"]


## A 1 m square lying on the ground plane (paper scraps, shadows, the soil crack strip), centred, white.
static func flat_quad() -> ArrayMesh:
	if not _meshes.has(&"flat"):
		var mesh: ImmediateMesh = _begin()
		AimIndicator.fill(mesh, PackedVector2Array([Vector2(-0.5, -0.5), Vector2(0.5, -0.5), Vector2(0.5, 0.5), Vector2(-0.5, 0.5)]), Color.WHITE, 0.0)
		_meshes[&"flat"] = bake(mesh)
	return _meshes[&"flat"]


## A 1 m soft puff standing in the XY plane (faces +Z, for billboards): a round blob, a little darker at its rim.
static func puff_quad() -> ArrayMesh:
	if not _meshes.has(&"puff"):
		var mesh: ImmediateMesh = _begin()
		var rim: Color = Color(PUFF_RIM_SHADE, PUFF_RIM_SHADE, PUFF_RIM_SHADE)
		for i: int in CIRCLE_POINTS:
			var a: float = TAU * float(i) / float(CIRCLE_POINTS)
			var b: float = TAU * float(i + 1) / float(CIRCLE_POINTS)
			_vertex(mesh, Vector3.ZERO, Color.WHITE)
			_vertex(mesh, Vector3(cos(a), sin(a), 0.0) * 0.5, rim)
			_vertex(mesh, Vector3(cos(b), sin(b), 0.0) * 0.5, rim)
		_meshes[&"puff"] = bake(mesh)
	return _meshes[&"puff"]


## The snip's smear: a flat arc of radius 1 round the origin across `SMEAR_ARC_DEGREES`, centred on -Z, thickest in the
## middle and thin at both ends (scaled to the reach; a node turns it to the aim).
static func smear_arc() -> ArrayMesh:
	if not _meshes.has(&"arc"):
		var mesh: ImmediateMesh = _begin()
		var half: float = deg_to_rad(SMEAR_ARC_DEGREES / 2.0)
		for i: int in SMEAR_POINTS:
			var t0: float = float(i) / float(SMEAR_POINTS)
			var t1: float = float(i + 1) / float(SMEAR_POINTS)
			var a0: float = lerpf(-half, half, t0)
			var a1: float = lerpf(-half, half, t1)
			var w0: float = SMEAR_ARC_WIDTH * sin(t0 * PI) / 2.0
			var w1: float = SMEAR_ARC_WIDTH * sin(t1 * PI) / 2.0
			var d0: Vector3 = Vector3(sin(a0), 0.0, -cos(a0))
			var d1: Vector3 = Vector3(sin(a1), 0.0, -cos(a1))
			_quad(mesh, d0 * (1.0 - w0), d1 * (1.0 - w1), d1 * (1.0 + w1), d0 * (1.0 + w0), Color.WHITE)
		_meshes[&"arc"] = bake(mesh)
	return _meshes[&"arc"]


## A smack streak: a flat 1 m lens along X (thick in the middle, pointed ends) on the ground plane.
static func smear_streak() -> ArrayMesh:
	if not _meshes.has(&"streak"):
		var mesh: ImmediateMesh = _begin()
		for i: int in SMEAR_POINTS:
			var t0: float = float(i) / float(SMEAR_POINTS)
			var t1: float = float(i + 1) / float(SMEAR_POINTS)
			var w0: float = STREAK_WIDTH * sin(t0 * PI) / 2.0
			var w1: float = STREAK_WIDTH * sin(t1 * PI) / 2.0
			_quad(mesh, Vector3(t0 - 0.5, 0.0, -w0), Vector3(t1 - 0.5, 0.0, -w1), Vector3(t1 - 0.5, 0.0, w1), Vector3(t0 - 0.5, 0.0, w0), Color.WHITE)
		_meshes[&"streak"] = bake(mesh)
	return _meshes[&"streak"]


## The impact cracks on the ground (unit size: a ring at `CRACK_RING` and four spokes out to 1), white (tinted dark).
static func crack_spokes() -> ArrayMesh:
	if not _meshes.has(&"cracks"):
		var mesh: ImmediateMesh = _begin()
		AimIndicator.ribbon(mesh, AimIndicator.circle(Vector2.ZERO, CRACK_RING), CRACK_RING_WIDTH, Color.WHITE, 0.0)
		for i: int in CRACK_SPOKES:
			var angle: float = TAU * (float(i) + 0.3) / float(CRACK_SPOKES)
			var tip: Vector2 = Vector2(cos(angle), sin(angle))
			var kink: Vector2 = tip.rotated(0.18) * 0.45
			AimIndicator.ribbon(mesh, PackedVector2Array([tip * 0.2, kink, tip]), CRACK_SPOKE_WIDTH, Color.WHITE, 0.0)
		_meshes[&"cracks"] = bake(mesh)
	return _meshes[&"cracks"]


## Turns a filled ImmediateMesh into a plain ArrayMesh (cacheable, shared by every node that draws it).
static func bake(mesh: ImmediateMesh) -> ArrayMesh:
	mesh.surface_end()
	var baked: ArrayMesh = ArrayMesh.new()
	if mesh.get_surface_count() > 0:
		baked.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, mesh.surface_get_arrays(0))
	return baked


## A fresh triangle list to draw into (closed by `bake`).
static func _begin() -> ImmediateMesh:
	var mesh: ImmediateMesh = ImmediateMesh.new()
	mesh.surface_begin(Mesh.PRIMITIVE_TRIANGLES)
	return mesh


static func _circle(radius: float) -> PackedVector2Array:
	var points: PackedVector2Array = PackedVector2Array()
	for i: int in CIRCLE_POINTS:
		var a: float = TAU * float(i) / float(CIRCLE_POINTS)
		points.append(Vector2(cos(a), sin(a)) * radius)
	return points


static func _vertex(mesh: ImmediateMesh, at: Vector3, color: Color) -> void:
	mesh.surface_set_color(color)
	mesh.surface_add_vertex(at)


static func _quad(mesh: ImmediateMesh, a: Vector3, b: Vector3, c: Vector3, d: Vector3, color: Color) -> void:
	for corner: Vector3 in [a, b, c, a, c, d]:
		_vertex(mesh, corner, color)


## A plus of half-length `half` and arm thickness `arm` in the XY plane at depth `z`.
static func _plus(mesh: ImmediateMesh, half: float, arm: float, color: Color, z: float) -> void:
	var t: float = arm / 2.0
	_quad(mesh, Vector3(-half, -t, z), Vector3(half, -t, z), Vector3(half, t, z), Vector3(-half, t, z), color)
	_quad(mesh, Vector3(-t, -half, z), Vector3(t, -half, z), Vector3(t, half, z), Vector3(-t, half, z), color)
