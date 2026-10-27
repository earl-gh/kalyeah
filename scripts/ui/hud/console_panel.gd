class_name ConsolePanel
extends Control
## The tray at the bottom of the match screen that the hand sits on (and the tagged panel, while you wait on the sidewalk): a bright blue tray drawn in code like Clash Royale's (owner), edge to edge, as one smooth lit shape with no outline. It only draws; it never takes a touch. The health colours and the heart are shared with the hand, which shows your health at the bar's end.

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


## The tray's colours, from the Clash Royale reference: the body's bright blue lightening a little at the top and darkening toward the bottom
## of the screen, the light blue rim along the top (and, less, down the side beside the column) with a darker shade just under it. No
## dark outline anywhere: the edge is the lit rim, anti-aliased by a one pixel fade.
const TRAY_TOP: Color = Color(0.1, 0.6, 0.95)
const TRAY_BODY: Color = Color(0.03, 0.5, 0.83)
const TRAY_BOTTOM: Color = Color(0.02, 0.21, 0.52)
const TRAY_RIM_LIGHT: Color = Color(0.58, 0.85, 1.0)
const TRAY_RIM: Color = Color(0.2, 0.66, 1.0)
const TRAY_RIM_SHADE: Color = Color(0.0, 0.3, 0.62, 0.55)
const TRAY_SIDE_SHADE: Color = Color(0.0, 0.12, 0.38, 0.3)
## The shade the tray casts up onto the street (like the nav bar's).
const TRAY_CAST: Color = Color(0.0, 0.02, 0.08, 0.4)
const CORNER: int = UiSkin.RADIUS_M
## How many segments a rounded top corner is drawn with.
const ARC_STEPS: int = 10
## The body's colour stops, as shares of the tray's height from its top: lighter at the top, the body blue, darkening from the middle.
const BODY_STOPS: PackedFloat32Array = [0.0, 0.2, 0.5, 1.0]
## The light rim is this wide and rolls over into the shade under it over the next `RIM_ROLL`; the shade fades out by `INSET` (every layer
## that follows the edge stays inside this distance, so it is smaller than the corner's radius).
const RIM_WIDTH: float = 5.0
const RIM_ROLL: float = 1.5
const INSET: float = CORNER - 2.0
## The rim and the side shade are this share as strong along the tray's straight side as on its top.
const RIM_SIDE_SHARE: float = 0.45
## The edge fades out over this many pixels, which is what anti-aliases it.
const EDGE_FEATHER: float = 1.25
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
var _rim: Gradient = _gradient(PackedFloat32Array([0.0, RIM_WIDTH / INSET, (RIM_WIDTH + RIM_ROLL) / INSET, 1.0]), PackedColorArray([TRAY_RIM_LIGHT, TRAY_RIM, TRAY_RIM_SHADE, Color(TRAY_RIM_SHADE, 0.0)]))
var _side: Gradient = _gradient(PackedFloat32Array([0.0, 1.0]), PackedColorArray([TRAY_SIDE_SHADE, Color(TRAY_SIDE_SHADE, 0.0)]))


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


## The tray's one outline, from its bottom-left corner up, over its two rounded top corners and down to its bottom-right corner: the points,
## the outward unit normals, and where the top edge runs (between `top_left` and the next point). Every layer is built from it, so they all
## follow the same curve.
class Outline:
	extends RefCounted
	var points: PackedVector2Array = PackedVector2Array()
	var normals: PackedVector2Array = PackedVector2Array()
	var top_left: int = 0

	## `rows`: heights at which the straight sides get a point too, so a colour that changes down the side is exact there.
	static func build(x0: float, x1: float, top: float, bottom: float, radius: float, rows: Array[float]) -> Outline:
		var result: Outline = Outline.new()
		var ys: Array[float] = []
		for y: float in rows:
			if y > top + radius and y < bottom:
				ys.append(y)
		ys.sort()
		result._add(Vector2(x0, bottom), Vector2.LEFT)
		for i: int in range(ys.size() - 1, -1, -1):
			result._add(Vector2(x0, ys[i]), Vector2.LEFT)
		result._arc(Vector2(x0 + radius, top + radius), radius, PI)
		result.top_left = result.points.size() - 1
		result._arc(Vector2(x1 - radius, top + radius), radius, PI * 1.5)
		for y: float in ys:
			result._add(Vector2(x1, y), Vector2.RIGHT)
		result._add(Vector2(x1, bottom), Vector2.RIGHT)
		return result

	func _add(at: Vector2, normal: Vector2) -> void:
		points.append(at)
		normals.append(normal)

	## A quarter circle round `centre` from the angle `from`, a point on each of its `ARC_STEPS` + 1 steps.
	func _arc(centre: Vector2, radius: float, from: float) -> void:
		for step: int in ARC_STEPS + 1:
			var normal: Vector2 = Vector2.from_angle(from + PI * 0.5 * float(step) / float(ARC_STEPS))
			_add(centre + normal * radius, normal)

	## The point `i` moved `offset` along its normal (outward when positive).
	func ring_point(i: int, offset: float) -> Vector2:
		return points[i] + normals[i] * offset


func set_column(width: float, at_left: bool) -> void:
	column_width = width
	column_left = at_left
	queue_redraw()


## The tray's outline for its size and column: the side beside the column starts at the column's edge and the other side ends exactly at the
## screen's edge (both sides, with no column); both top corners are rounded on the screen, so a little street shows in each. Only the
## bottom runs past the screen.
func tray_outline() -> Outline:
	var at_column: bool = column_width > 0.0
	var x0: float = column_width if at_column and column_left else 0.0
	var x1: float = size.x - column_width if at_column and not column_left else size.x
	var rows: Array[float] = []
	for share: float in BODY_STOPS:
		rows.append(size.y * share)
	return Outline.build(x0, x1, 0.0, size.y + BLEED, float(CORNER), rows)


func _draw() -> void:
	# a bright blue tray drawn in code, copied from the Clash Royale reference (owner; replaces the picture of a cardboard plate), as one
	# smooth lit shape with no outline: a light blue rim that follows its rounded top corners, a shade under the rim, a body that lightens
	# at the top and darkens toward the bottom of the screen, and a soft shade cast up onto the street. It
	# ends at the screen's edge on the side away from the column (the column beside it shows the street), both top corners rounded.
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


## The tray itself: rings of vertices along the outline from just outside it (clear, the one pixel fade that anti-aliases the edge), over
## the edge and the rim, to `INSET` inside it, then the middle filled in rows. The colour of every vertex is the body's colour at its height
## with the side shade, the rim and the shade under it laid over.
func body_layer(outline: Outline) -> Layer:
	var layer: Layer = Layer.new()
	var depths: Array[float] = [-EDGE_FEATHER, 0.0, RIM_WIDTH, RIM_WIDTH + RIM_ROLL, INSET]
	var count: int = outline.points.size()
	for depth: float in depths:
		for i: int in count:
			var color: Color = _lit(outline.points[i].y, outline.normals[i], maxf(depth, 0.0))
			layer.points.append(outline.ring_point(i, -depth))
			layer.colors.append(Color(color, 0.0) if depth < 0.0 else color)
	for ring: int in depths.size() - 1:
		layer.add_strip(ring, count)
	# the middle: a strip of triangles zipped between the inner ring's left chain and its right chain, in order of height
	var inner: int = (depths.size() - 1) * count
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


## The colour of the tray at height `y`, `depth` inside its edge, where the edge faces `normal`.
func _lit(y: float, normal: Vector2, depth: float) -> Color:
	var unit: float = clampf(depth / INSET, 0.0, 1.0)
	var color: Color = _body.sample(clampf(y / size.y, 0.0, 1.0))
	var shade: Color = _side.sample(unit)
	color = color.lerp(Color(shade, 1.0), shade.a * absf(normal.x))
	var light: Color = _rim.sample(unit)
	var up: float = lerpf(RIM_SIDE_SHARE, 1.0, clampf(-normal.y, 0.0, 1.0))
	return color.lerp(Color(light, 1.0), light.a * up)


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
