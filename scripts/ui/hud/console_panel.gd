class_name ConsolePanel
extends Control
## The tray at the bottom of the match screen that the hand sits on (and the tagged panel, while you wait on the sidewalk): a bright blue tray drawn in code like Clash Royale's (owner), edge to edge, as a raised slab with a 3D chamfer: a wide bevel round its slanted top corners, top and sides whose facets are each lit by their own direction, with no dark outline. It only draws; it never takes a touch. The health colours and the heart are shared with the hand, which shows your health at the bar's end.

## The bar keeps your own colour above this share of your health, turns amber below it and red below the second.
const HEALTH_LOW: float = 0.5
const HEALTH_CRITICAL: float = 0.25
const HEALTH_AMBER: Color = Color(1.0, 0.76, 0.2)
const HEALTH_RED: Color = Color(1.0, 0.3, 0.28)

## The colour of your health: your own colour while healthy, amber below half, red below a quarter.
static func health_color(fraction: float) -> Color:
	if fraction >= HEALTH_LOW:
		return TeamColors.bar_self()
	return HEALTH_AMBER if fraction > HEALTH_CRITICAL else HEALTH_RED


## A heart centred on `at`, `radius` wide from its middle to a side: two lobes and a point, with a dark outline.
static func draw_heart(canvas: CanvasItem, at: Vector2, radius: float, color: Color) -> void:
	var lobe: float = radius * 0.56
	canvas.draw_circle(at + Vector2(-radius * 0.46, -radius * 0.22), lobe, Icons.OUTLINE)
	canvas.draw_circle(at + Vector2(radius * 0.46, -radius * 0.22), lobe, Icons.OUTLINE)
	canvas.draw_colored_polygon(PackedVector2Array([at + Vector2(-radius - 1.0, -radius * 0.12), at + Vector2(radius + 1.0, -radius * 0.12), at + Vector2(0.0, radius + 3.0)]), Icons.OUTLINE)
	canvas.draw_circle(at + Vector2(-radius * 0.46, -radius * 0.22), lobe - 1.5, color)
	canvas.draw_circle(at + Vector2(radius * 0.46, -radius * 0.22), lobe - 1.5, color)
	canvas.draw_colored_polygon(PackedVector2Array([at + Vector2(-radius, -radius * 0.12), at + Vector2(radius, -radius * 0.12), at + Vector2(0.0, radius)]), color)


## The tray's colours, from the Clash Royale reference: the face's bright blue lightening a little at the top and darkening toward the bottom
## of the screen. The chamfer round it is a bevel lit from the upper left: a facet that faces the light turns toward `BEVEL_LIGHT`, one that
## faces away toward `BEVEL_DARK` (still a clear blue, never a black or navy line). No dark outline anywhere: the edge is a thin light glint,
## anti-aliased by a one pixel fade.
const TRAY_TOP: Color = Color(0.18, 0.55, 0.84)
const TRAY_BODY: Color = Color(0.12, 0.45, 0.75)
const TRAY_BOTTOM: Color = Color(0.08, 0.27, 0.53)
const BEVEL_LIGHT: Color = Color(0.62, 0.88, 1.0)
const BEVEL_DARK: Color = Color(0.05, 0.2, 0.46)
const BEVEL_GLINT: Color = Color(0.86, 0.97, 1.0)
## The way the light travels from (towards the upper left, the same as the card art's).
const LIGHT: Vector2 = Vector2(-0.5, -0.87)
## How far a facet that faces the light (or away from it) goes toward `BEVEL_LIGHT` (`BEVEL_DARK`) at most.
const LIGHT_GAIN: float = 0.78
const DARK_GAIN: float = 0.62
## The shade the tray casts up onto the street (like the nav bar's).
const TRAY_CAST: Color = Color(0.0, 0.02, 0.08, 0.4)
const CORNER: int = UiSkin.RADIUS_XL
## How many segments a rounded top corner is drawn with.
const ARC_STEPS: int = 10
const CORNER_RADIUS: float = float(UiSkin.RADIUS_S)
## The body's colour stops, as shares of the tray's height from its top: lighter at the top, the body blue, darkening from the middle.
const BODY_STOPS: PackedFloat32Array = [0.0, 0.2, 0.5, 1.0]
## The chamfer: a band this wide (measured straight in from the edge) round the top corners, the top and the sides. Its flat facets meet the
## tray's face at a thin line (a highlight where the facet faces the light, a shadow where it faces away), and a thin glint runs along its
## outer edge. The bevel's inner edges are the tray's own outline moved in (`inset_outline`: the same points, so the rings join one to one).
const BEVEL: float = 16.0
## The bevel's flat facet stops this far short of its inner edge, and the junction line (highlight or shadow) takes this much, and the face
## starts after it.
const BEVEL_FLAT_END: float = 2.0
const LINE_WIDTH: float = 2.5
## The glint along the outer edge is this wide.
const GLINT_WIDTH: float = 2.5
## How strong the glint is on a facet that faces away from the light, and how strong the junction line is (both as shares of full).
const GLINT_BACK: float = 0.4
const LINE_STRENGTH: float = 0.55
## The inner edges of the bevel are joined with this radius (almost sharp: a mitre).
const INNER_JOIN: float = 1.0
## The tray's straight side at the screen's edge has the chamfer's contrast only near the top corner (nothing is beside it, so a full bevel
## down the screen's edge would only frame it): it fades out over this many pixels below the corner.
const EDGE_FADE: float = 90.0
## The edge fades out over this many pixels, which is what anti-aliases it.
const EDGE_FEATHER: float = 1.25
## The rings of `body_layer`, from outside in: the fade outside the edge, the edge's glint, the glint's inner side, the bevel's flat facets,
## the line where they meet the face, and the face.
const RING_FADE: int = 0
const RING_EDGE: int = 1
const RING_GLINT: int = 2
const RING_FLAT: int = 3
const RING_LINE: int = 4
const RING_FACE: int = 5
const RING_COUNT: int = 6
## The shade cast onto the street is this tall, and this share as strong along the side as above the tray.
const CAST_HEIGHT: float = 36.0
const CAST_SIDE_SHARE: float = 0.4
## The shade's offsets from the edge (the first is under the tray, where it is hidden).
const CAST_OFFSETS: PackedFloat32Array = [-2.0, 0.0, 6.0, 14.0, 24.0, 36.0]
## How far past the bottom of the screen the tray runs, so that its bottom corners and the shade round them are out of sight.
const BLEED: float = CAST_HEIGHT + CORNER

## The width of the column beside the plate (NEXT and the chat button stand there, over the street) and whether it is at the left.
var column_width: float = 0.0
var column_left: bool = true

var _cast_mesh: ArrayMesh
var _body_mesh: ArrayMesh
var _body: Gradient = _gradient(BODY_STOPS, PackedColorArray([TRAY_TOP, TRAY_BODY, TRAY_BODY, TRAY_BOTTOM]))


## One layer of the tray as a triangle mesh with a colour on every vertex (the colours blend between vertices, so nothing has a seam).
class Layer:
	extends RefCounted
	var points: PackedVector2Array = PackedVector2Array()
	var colors: PackedColorArray = PackedColorArray()
	var indices: PackedInt32Array = PackedInt32Array()

	## The mesh to draw, or null while the layer is empty.
	func to_mesh() -> ArrayMesh:
		if indices.is_empty():
			return null
		var arrays: Array = []
		arrays.resize(Mesh.ARRAY_MAX)
		arrays[Mesh.ARRAY_VERTEX] = points
		arrays[Mesh.ARRAY_COLOR] = colors
		arrays[Mesh.ARRAY_INDEX] = indices
		var mesh: ArrayMesh = ArrayMesh.new()
		mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)
		return mesh

	func add_triangle(a: int, b: int, c: int) -> void:
		indices.append_array(PackedInt32Array([a, b, c]))

	## Joins ring `from` to the next one (rings are `count` vertices each, in order).
	func add_strip(from: int, count: int) -> void:
		for i: int in count - 1:
			var a: int = from * count + i
			var d: int = (from + 1) * count + i
			add_triangle(a, a + 1, d + 1)
			add_triangle(a, d + 1, d)


## The tray's one outline, from its bottom-left corner up, over its two slanted top corners with small rounded joins and down to its bottom-right corner: the points,
## the outward unit normals, and where the top edge runs (between `top_left` and the next point). Every layer is built from it, so they all
## follow the same curve.
class Outline:
	extends RefCounted
	var points: PackedVector2Array = PackedVector2Array()
	var normals: PackedVector2Array = PackedVector2Array()
	var top_left: int = 0

	## `rows`: heights at which the straight sides get a point too, so a colour that changes down the side is exact there (only those below
	## `row_min`, which is the corner's end unless given). `join` is the radius of the small rounded joins.
	static func build(x0: float, x1: float, top: float, bottom: float, radius: float, rows: Array[float], join: float = CORNER_RADIUS, row_min: float = NAN) -> Outline:
		var result: Outline = Outline.new()
		var ys: Array[float] = []
		var from: float = top + radius if is_nan(row_min) else row_min
		for y: float in rows:
			if y > from and y < bottom:
				ys.append(y)
		ys.sort()
		result._add(Vector2(x0, bottom), Vector2.LEFT)
		for i: int in range(ys.size() - 1, -1, -1):
			result._add(Vector2(x0, ys[i]), Vector2.LEFT)
		result._arc(Vector2(x0 + join, top + radius), join, PI, PI * 0.25)
		result._arc(Vector2(x0 + radius, top + join), join, PI * 1.25, PI * 0.25)
		result.top_left = result.points.size() - 1
		result._arc(Vector2(x1 - radius, top + join), join, PI * 1.5, PI * 0.25)
		result._arc(Vector2(x1 - join, top + radius), join, PI * 1.75, PI * 0.25)
		for y: float in ys:
			result._add(Vector2(x1, y), Vector2.RIGHT)
		result._add(Vector2(x1, bottom), Vector2.RIGHT)
		return result

	func _add(at: Vector2, normal: Vector2) -> void:
		points.append(at)
		normals.append(normal)

	## A rounded join through `sweep` radians, with the same samples for each side.
	func _arc(centre: Vector2, radius: float, from: float, sweep: float) -> void:
		for step: int in ARC_STEPS + 1:
			var normal: Vector2 = Vector2.from_angle(from + sweep * float(step) / float(ARC_STEPS))
			_add(centre + normal * radius, normal)

	## The point `i` moved `offset` along its normal (outward when positive).
	func ring_point(i: int, offset: float) -> Vector2:
		return points[i] + normals[i] * offset


func set_column(width: float, at_left: bool) -> void:
	column_width = width
	column_left = at_left
	queue_redraw()


## The tray's outline for its size and column: the side beside the column starts at the column's edge and the other side ends exactly at the
## screen's edge (both sides, with no column); both top corners are chamfered on the screen, so a little street shows in each. Only the
## bottom runs past the screen.
func tray_outline() -> Outline:
	return inset_outline(0.0)


## The tray's outline moved in `inset` pixels on every side but the bottom (which runs off the screen): the same points as `tray_outline` (the
## same rows down the sides and the same samples round the corners), so ring `i` of one joins ring `i` of the other. The 45 degree slants
## move in with the sides, so their legs shorten; the joins are mitred once there is an inset.
func inset_outline(inset: float) -> Outline:
	var at_column: bool = column_width > 0.0
	var x0: float = column_width if at_column and column_left else 0.0
	var x1: float = size.x - column_width if at_column and not column_left else size.x
	var rows: Array[float] = []
	for share: float in BODY_STOPS:
		rows.append(size.y * share)
	var leg: float = float(CORNER) - (2.0 - sqrt(2.0)) * inset
	return Outline.build(x0 + inset, x1 - inset, inset, size.y + BLEED, leg, rows, CORNER_RADIUS if inset <= 0.0 else INNER_JOIN, float(CORNER) + BEVEL + LINE_WIDTH + BEVEL_FLAT_END)


func _draw() -> void:
	# a bright blue tray drawn in code, copied from the Clash Royale reference (owner; replaces the picture of a cardboard plate), as one
	# smooth lit shape with no outline: a light blue rim that follows its slanted top corners with small rounded joins, a shade under the rim, a body that lightens
	# at the top and darkens toward the bottom of the screen, and a soft shade cast up onto the street. It
	# ends at the screen's edge on the side away from the column (the column beside it shows the street), both top corners chamfered.
	if size.x - column_width <= 0.0 or size.y <= 0.0:
		return
	var outline: Outline = tray_outline()
	# the draw commands only hold the meshes' ids, so the meshes are kept until the next redraw
	_cast_mesh = cast_layer(outline).to_mesh()
	_body_mesh = body_layer(outline).to_mesh()
	if _cast_mesh != null:
		draw_mesh(_cast_mesh, null)
	if _body_mesh != null:
		draw_mesh(_body_mesh, null)


## The tray itself, as a raised slab: rings of vertices from just outside the edge (clear, the one pixel fade that anti-aliases it), over the
## glint on the outer edge and the bevel's flat facets, to the thin line where the bevel meets the face, then the face filled in rows. A ring
## is the tray's outline moved in; every vertex's colour is the face's colour at its height with the facet's light laid over it, so the
## facets turn with the corner (a rounded join sweeps from one to the next) and nothing has a seam.
func body_layer(outline: Outline) -> Layer:
	var count: int = outline.points.size()
	var weights: PackedFloat32Array = _weights(outline)
	var light: Vector2 = LIGHT.normalized()
	var flat_end: Outline = inset_outline(BEVEL - BEVEL_FLAT_END)
	var line: Outline = inset_outline(BEVEL)
	var face: Outline = inset_outline(BEVEL + LINE_WIDTH)
	var layer: Layer = Layer.new()
	layer.points.resize(RING_COUNT * count)
	layer.colors.resize(RING_COUNT * count)
	for i: int in count:
		var facing: float = outline.normals[i].dot(light)
		var weight: float = weights[i]
		var edge: Color = _facet(_face(outline.points[i].y), facing, weight)
		var glint: Color = edge.lerp(BEVEL_GLINT, GLINT_BACK + (1.0 - GLINT_BACK) * maxf(facing, 0.0)).lerp(edge, 1.0 - weight)
		var flat_at: Vector2 = flat_end.points[i]
		var line_at: Vector2 = line.points[i]
		var face_at: Vector2 = face.points[i]
		_vertex(layer, RING_FADE, i, outline.ring_point(i, EDGE_FEATHER), Color(glint, 0.0))
		_vertex(layer, RING_EDGE, i, outline.points[i], glint)
		_vertex(layer, RING_GLINT, i, outline.ring_point(i, -GLINT_WIDTH), edge)
		_vertex(layer, RING_FLAT, i, flat_at, _facet(_face(flat_at.y), facing, weight))
		_vertex(layer, RING_LINE, i, line_at, _junction(_face(line_at.y), facing, weight))
		_vertex(layer, RING_FACE, i, face_at, _face(face_at.y))
	for ring: int in RING_COUNT - 1:
		layer.add_strip(ring, count)
	# the face: a strip of triangles zipped between the last ring's left chain and its right chain, in order of height
	var inner: int = RING_FACE * count
	var left: int = outline.top_left
	var right: int = outline.top_left + 1
	while left > 0 or right < count - 1:
		var step_left: bool = right >= count - 1 or (left > 0 and layer.points[inner + left - 1].y <= layer.points[inner + right + 1].y)
		if step_left:
			layer.add_triangle(inner + left, inner + right, inner + left - 1)
			left -= 1
		else:
			layer.add_triangle(inner + left, inner + right, inner + right + 1)
			right += 1
	return layer


## Vertex `i` of ring `ring`.
static func _vertex(layer: Layer, ring: int, i: int, at: Vector2, color: Color) -> void:
	var slot: int = ring * layer.points.size() / RING_COUNT + i
	layer.points[slot] = at
	layer.colors[slot] = color


## The face's colour at height `y`.
func _face(y: float) -> Color:
	return _body.sample(clampf(y / size.y, 0.0, 1.0))


## A bevel facet whose normal faces the light by `facing` (1 straight at it, -1 straight away): the face's colour turned toward the light or
## away from it, by `weight` of the full amount.
static func _facet(base: Color, facing: float, weight: float) -> Color:
	if facing >= 0.0:
		return base.lerp(BEVEL_LIGHT, facing * LIGHT_GAIN * weight)
	return base.lerp(BEVEL_DARK, -facing * DARK_GAIN * weight)


## The thin line where a facet meets the face: a highlight on the facets that face the light, a shadow on the ones that face away.
static func _junction(base: Color, facing: float, weight: float) -> Color:
	var facet: Color = _facet(base, facing, weight)
	if facing >= 0.0:
		return facet.lerp(BEVEL_GLINT, LINE_STRENGTH * facing * weight)
	return facet.lerp(BEVEL_DARK, LINE_STRENGTH * -facing * weight)


## How much of the chamfer's contrast each outline point gets: all of it, except down a straight side that is at the screen's edge, where it
## fades out below the corner (a bevel beside nothing would only frame the screen).
func _weights(outline: Outline) -> PackedFloat32Array:
	var weights: PackedFloat32Array = PackedFloat32Array()
	for i: int in outline.points.size():
		var at: Vector2 = outline.points[i]
		var on_screen_edge: bool = is_zero_approx(at.x) or is_equal_approx(at.x, size.x)
		weights.append(1.0 - smoothstep(float(CORNER), float(CORNER) + EDGE_FADE, at.y) if on_screen_edge else 1.0)
	return weights


## The soft shade the tray casts up onto the street: rings outward from under the edge that fade to clear (quickly at first, like a real
## shade), weaker along the side and nothing below.
func cast_layer(outline: Outline) -> Layer:
	var layer: Layer = Layer.new()
	var count: int = outline.points.size()
	for offset: float in CAST_OFFSETS:
		var fade: float = 1.0 - clampf(offset / CAST_HEIGHT, 0.0, 1.0)
		for i: int in count:
			var up: float = lerpf(CAST_SIDE_SHARE, 1.0, clampf(-outline.normals[i].y, 0.0, 1.0))
			layer.points.append(outline.ring_point(i, offset))
			layer.colors.append(Color(TRAY_CAST, TRAY_CAST.a * fade * fade * up))
	for ring: int in CAST_OFFSETS.size() - 1:
		layer.add_strip(ring, count)
	return layer


static func _gradient(offsets: PackedFloat32Array, colors: PackedColorArray) -> Gradient:
	var gradient: Gradient = Gradient.new()
	gradient.offsets = offsets
	gradient.colors = colors
	return gradient
