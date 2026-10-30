extends GutTest
## The blue tray behind the cards is a raised slab: no outline of any dark colour, both top corners chamfered and ending exactly at the
## screen's edge on the side away from the column, a 3D chamfer (a wide bevel whose facets are each lit by their own direction, with a
## highlight or shadow line where it meets the face) following the same outline, every layer built from it (no gaps or overlaps between
## them), and a shade cast onto the street that fades out smoothly.

const SIZE: Vector2 = Vector2(720.0, 260.0)
const COLUMN: float = 116.0


func _panel(column: float, at_left: bool) -> ConsolePanel:
	var panel: ConsolePanel = autofree(ConsolePanel.new()) as ConsolePanel
	panel.size = SIZE
	panel.set_column(column, at_left)
	return panel


func _bounds(points: PackedVector2Array) -> Rect2:
	var bounds: Rect2 = Rect2(points[0], Vector2.ZERO)
	for point: Vector2 in points:
		bounds = bounds.expand(point)
	return bounds


func test_the_outline_starts_at_the_column_and_ends_exactly_at_the_screen_edge() -> void:
	var bounds: Rect2 = _bounds(_panel(COLUMN, true).tray_outline().points)
	assert_almost_eq(bounds.position.x, COLUMN, 0.001, "its left edge is the column's edge")
	assert_almost_eq(bounds.end.x, SIZE.x, 0.001, "its far edge is exactly the screen's edge, no gap and nothing past it")
	assert_almost_eq(bounds.position.y, 0.0, 0.001, "the top edge is the panel's top")
	assert_gt(bounds.end.y, SIZE.y, "it runs off the bottom")


func test_left_handed_mirrors_the_outline() -> void:
	var right_hand: ConsolePanel.Outline = _panel(COLUMN, true).tray_outline()
	var left_hand: ConsolePanel.Outline = _panel(COLUMN, false).tray_outline()
	var count: int = right_hand.points.size()
	assert_eq(left_hand.points.size(), count)
	for i: int in count:
		var mirrored: Vector2 = left_hand.points[count - 1 - i]
		assert_almost_eq(SIZE.x - mirrored.x, right_hand.points[i].x, 0.001, "point %d is mirrored across the screen" % i)
		assert_almost_eq(mirrored.y, right_hand.points[i].y, 0.001)
	assert_almost_eq(_bounds(left_hand.points).end.x, SIZE.x - COLUMN, 0.001, "the column is at the right")


func test_without_a_column_the_tray_fills_the_screen_width_with_both_top_corners_rounded_on_it() -> void:
	for at_left: bool in [true, false]:
		var outline: ConsolePanel.Outline = _panel(0.0, at_left).tray_outline()
		var bounds: Rect2 = _bounds(outline.points)
		assert_almost_eq(bounds.position.x, 0.0, 0.001, "from the screen's left edge")
		assert_almost_eq(bounds.end.x, SIZE.x, 0.001, "to its right edge")
		var radius: float = float(ConsolePanel.CORNER)
		# the first point of the top edge is a radius in from the left corner, the last a radius in from the right one: street shows in both
		assert_almost_eq(outline.points[outline.top_left].x, radius, 0.001, "the left corner is rounded on the screen")
		assert_almost_eq(outline.points[outline.top_left + 1].x, SIZE.x - radius, 0.001, "and so is the right one")


func test_top_corners_have_straight_slants_with_small_rounded_joins() -> void:
	var outline: ConsolePanel.Outline = _panel(COLUMN, true).tray_outline()
	var slants: int = 0
	for i: int in outline.points.size() - 1:
		assert_almost_eq(outline.normals[i].length(), 1.0, 0.001)
		var edge: Vector2 = outline.points[i + 1] - outline.points[i]
		if absf(edge.x) > 5.0 and absf(edge.y) > 5.0:
			slants += 1
			assert_almost_eq(absf(edge.x), absf(edge.y), 0.001, "45 degree chamfer")
			assert_almost_eq(edge.normalized().dot(outline.normals[i]), 0.0, 0.001)
	assert_eq(slants, 2, "one straight slant per top corner")
	assert_lt(ConsolePanel.CORNER_RADIUS, float(ConsolePanel.CORNER) / 2.0)
	assert_gt(ConsolePanel.CORNER_RADIUS, ConsolePanel.GLINT_WIDTH, "the glint's ring cannot fold over itself")
	assert_almost_eq(outline.points[outline.top_left].y, 0.0, 0.001)
	assert_almost_eq(outline.points[outline.top_left + 1].y, 0.0, 0.001)


func _signed_area(points: PackedVector2Array) -> float:
	var area: float = 0.0
	for i: int in points.size():
		var next: Vector2 = points[(i + 1) % points.size()]
		area += points[i].x * next.y - next.x * points[i].y
	return absf(area) / 2.0


func _triangles_area(layer: ConsolePanel.Layer, skip: int) -> float:
	var area: float = 0.0
	for t: int in range(skip * 3, layer.indices.size(), 3):
		var a: Vector2 = layer.points[layer.indices[t]]
		var b: Vector2 = layer.points[layer.indices[t + 1]]
		var c: Vector2 = layer.points[layer.indices[t + 2]]
		area += absf((b - a).cross(c - a)) / 2.0
	return area


func test_the_body_fills_the_outline_with_no_gap_and_no_overlap() -> void:
	for at_left: bool in [true, false]:
		var panel: ConsolePanel = _panel(COLUMN, at_left)
		var outline: ConsolePanel.Outline = panel.tray_outline()
		var layer: ConsolePanel.Layer = panel.body_layer(outline)
		var count: int = outline.points.size()
		# the first strip is the one pixel fade outside the edge; everything after it is the tray itself
		var fade_triangles: int = 2 * (count - 1)
		assert_almost_eq(_triangles_area(layer, fade_triangles), _signed_area(outline.points), 0.5, "the triangles tile the shape exactly (column at left: %s)" % at_left)
		for index: int in layer.indices:
			assert_between(index, 0, layer.points.size() - 1, "every triangle uses a real vertex")


func test_the_edge_is_clear_and_nothing_inside_it_is_a_dark_outline() -> void:
	var panel: ConsolePanel = _panel(COLUMN, true)
	var outline: ConsolePanel.Outline = panel.tray_outline()
	var layer: ConsolePanel.Layer = panel.body_layer(outline)
	var count: int = outline.points.size()
	for i: int in count:
		assert_eq(layer.colors[i].a, 0.0, "the ring outside the edge fades to clear: that is the anti-aliasing")
	var darkest: float = ConsolePanel.BEVEL_DARK.b - 0.001
	for i: int in range(count, layer.colors.size()):
		var color: Color = layer.colors[i]
		assert_eq(color.a, 1.0, "the tray itself is opaque")
		assert_gt(color.b, darkest, "the chamfer never falls below its darkest blue")
		assert_gt(color.b, color.r + 0.1, "every pixel of it is blue, even the lightest glint (never a black or navy line)")
	# along the top the edge itself is a light glint, lighter than the body
	var edge: Color = layer.colors[count + outline.top_left]
	assert_gt(edge.g, ConsolePanel.TRAY_BODY.g + 0.2, "the edge is lit")
	# the glint follows the curve: the corner's edge is as light as the top's, not a different band
	var corner_edge: Color = layer.colors[count + outline.top_left - floori(ConsolePanel.ARC_STEPS * 0.5)]
	assert_gt(corner_edge.g, ConsolePanel.TRAY_BODY.g + 0.15, "round the corner too")


func test_the_body_lightens_at_the_top_and_darkens_toward_the_bottom_in_one_blend() -> void:
	var panel: ConsolePanel = _panel(COLUMN, true)
	var outline: ConsolePanel.Outline = panel.tray_outline()
	var layer: ConsolePanel.Layer = panel.body_layer(outline)
	var count: int = outline.points.size()
	var inner: int = (floori(float(layer.points.size()) / float(count)) - 1) * count
	# down the straight left side, on the innermost ring (the face, no chamfer light there): never lighter going down past the body stop
	var last_g: float = 2.0
	var rows: int = 0
	for i: int in range(outline.top_left, -1, -1):
		var color: Color = layer.colors[inner + i]
		if layer.points[inner + i].y >= SIZE.y * ConsolePanel.BODY_STOPS[1]:
			assert_lte(color.g, last_g + 0.0001, "darker or equal going down (row %d)" % i)
			last_g = color.g
			rows += 1
	assert_gt(rows, 2, "rows at the colour stops give the blend its exact corners")
	assert_almost_eq(layer.colors[inner].b, ConsolePanel.TRAY_BOTTOM.b, 0.001, "ends in the bottom colour")


func test_the_shade_cast_on_the_street_fades_out_without_a_step() -> void:
	var panel: ConsolePanel = _panel(COLUMN, true)
	var outline: ConsolePanel.Outline = panel.tray_outline()
	var layer: ConsolePanel.Layer = panel.cast_layer(outline)
	var count: int = outline.points.size()
	var rings: int = floori(float(layer.points.size()) / float(count))
	for ring: int in rings:
		for i: int in count:
			var alpha: float = layer.colors[ring * count + i].a
			assert_lte(alpha, ConsolePanel.TRAY_CAST.a + 0.0001, "never darker than the cast shade")
			if ring > 0:
				assert_lte(alpha, layer.colors[(ring - 1) * count + i].a + 0.0001, "fades going away from the tray (ring %d)" % ring)
			if ring == rings - 1:
				assert_eq(alpha, 0.0, "clear at its far edge")
	# strongest above the tray, weaker down the side beside the column, none below
	var above: float = layer.colors[count + outline.top_left].a
	var beside: float = layer.colors[count + 1].a
	assert_almost_eq(above, ConsolePanel.TRAY_CAST.a, 0.0001)
	assert_almost_eq(beside, ConsolePanel.TRAY_CAST.a * ConsolePanel.CAST_SIDE_SHARE, 0.0001, "weaker along the side, so the shade fades out there")
	# the first rings are under the tray, so the shade has no edge of its own where the tray's anti-aliased edge is
	assert_almost_eq(layer.colors[count + 3].a, layer.colors[3].a, 0.0001, "the same strength under the edge as at it")
	assert_not_null(layer.to_mesh(), "a mesh can be built")


# ---- the 3D chamfer ----

## The first point of `outline` whose normal is `normal` (to a hair) and whose height is from `y_min` to `y_max` (the straight sides have one
## in the middle of the tray; the first of them on the way up is the one at the screen's bottom, whose colour is the darkest).
func _find(outline: ConsolePanel.Outline, normal: Vector2, y_min: float = 0.0, y_max: float = INF) -> int:
	for i: int in outline.points.size():
		if outline.normals[i].is_equal_approx(normal) and outline.points[i].y >= y_min and outline.points[i].y <= y_max:
			return i
	return -1


func _color(layer: ConsolePanel.Layer, outline: ConsolePanel.Outline, ring: int, i: int) -> Color:
	return layer.colors[ring * outline.points.size() + i]


func test_the_chamfer_is_a_wide_bevel_in_from_the_edge() -> void:
	var panel: ConsolePanel = _panel(COLUMN, true)
	var outline: ConsolePanel.Outline = panel.tray_outline()
	var layer: ConsolePanel.Layer = panel.body_layer(outline)
	var count: int = outline.points.size()
	var top: int = outline.top_left
	assert_almost_eq(layer.points[ConsolePanel.RING_FLAT * count + top].y - outline.points[top].y, ConsolePanel.BEVEL - ConsolePanel.BEVEL_FLAT_END, 0.01, "the flat facet runs this far in from the top edge")
	assert_almost_eq(layer.points[ConsolePanel.RING_LINE * count + top].y - outline.points[top].y, ConsolePanel.BEVEL, 0.01, "to the line where it meets the face")
	var side: int = _find(outline, Vector2.LEFT, SIZE.y * 0.15, SIZE.y * 0.8)
	assert_almost_eq(layer.points[ConsolePanel.RING_LINE * count + side].x - outline.points[side].x, ConsolePanel.BEVEL, 0.01, "and as wide down the side")
	assert_gt(ConsolePanel.BEVEL, 3.0 * ConsolePanel.GLINT_WIDTH, "a bevel, not the old thin rim")


func test_the_rings_are_the_outline_moved_in_and_never_fold() -> void:
	for at_left: bool in [true, false]:
		var panel: ConsolePanel = _panel(COLUMN, at_left)
		var outline: ConsolePanel.Outline = panel.tray_outline()
		var count: int = outline.points.size()
		for inset: float in [ConsolePanel.BEVEL - ConsolePanel.BEVEL_FLAT_END, ConsolePanel.BEVEL, ConsolePanel.BEVEL + ConsolePanel.LINE_WIDTH]:
			var inner: ConsolePanel.Outline = panel.inset_outline(inset)
			assert_eq(inner.points.size(), count, "ring %.1f has the outline's points one for one" % inset)
			assert_eq(inner.top_left, outline.top_left)
			for i: int in count:
				assert_true(inner.normals[i].is_equal_approx(outline.normals[i]), "point %d faces the same way" % i)
				if outline.points[i].y < SIZE.y:
					var moved: Vector2 = inner.points[i] - outline.points[i]
					assert_gt(-moved.dot(outline.normals[i]), 0.0, "point %d is further in than the edge" % i)


func test_facets_turn_toward_the_light_or_away_from_it() -> void:
	var panel: ConsolePanel = _panel(COLUMN, true)
	var outline: ConsolePanel.Outline = panel.tray_outline()
	var layer: ConsolePanel.Layer = panel.body_layer(outline)
	var slant_ul: int = _find(outline, Vector2(-1.0, -1.0).normalized())
	var slant_ur: int = _find(outline, Vector2(1.0, -1.0).normalized())
	var top: int = outline.top_left
	var side: int = _find(outline, Vector2.LEFT, SIZE.y * 0.15, SIZE.y * 0.8)
	var luma_ul: float = _color(layer, outline, ConsolePanel.RING_FLAT, slant_ul).get_luminance()
	var luma_top: float = _color(layer, outline, ConsolePanel.RING_FLAT, top).get_luminance()
	var luma_side: float = _color(layer, outline, ConsolePanel.RING_FLAT, side).get_luminance()
	var luma_ur: float = _color(layer, outline, ConsolePanel.RING_FLAT, slant_ur).get_luminance()
	assert_gt(luma_ul, luma_top, "the slant that faces the light is the brightest facet")
	assert_gt(luma_top, luma_side, "the top is brighter than the side beside the column")
	assert_gt(luma_side, luma_ur, "and the slant that faces away from the light is the dimmest of these")
	# a facet that turns away from the light is darker than the face beside it, one that faces it is lighter
	var face_ul: float = _color(layer, outline, ConsolePanel.RING_FACE, slant_ul).get_luminance()
	assert_gt(luma_ul, face_ul + 0.1, "a lit facet is clearly lighter than the face")
	var left_handed: ConsolePanel = _panel(COLUMN, false)
	var other: ConsolePanel.Outline = left_handed.tray_outline()
	var other_layer: ConsolePanel.Layer = left_handed.body_layer(other)
	var away: int = _find(other, Vector2.RIGHT, SIZE.y * 0.15, SIZE.y * 0.8)
	var dark: float = _color(other_layer, other, ConsolePanel.RING_FLAT, away).get_luminance()
	var face: float = _color(other_layer, other, ConsolePanel.RING_FACE, away).get_luminance()
	assert_lt(dark, face - 0.03, "the side that faces away from the light, beside the column, is darker than the face")


func test_the_face_and_the_bevel_meet_at_a_highlight_or_a_shadow_line() -> void:
	var panel: ConsolePanel = _panel(COLUMN, true)
	var outline: ConsolePanel.Outline = panel.tray_outline()
	var layer: ConsolePanel.Layer = panel.body_layer(outline)
	var top: int = outline.top_left
	assert_gt(_color(layer, outline, ConsolePanel.RING_LINE, top).get_luminance(), _color(layer, outline, ConsolePanel.RING_FLAT, top).get_luminance(), "a highlight where a lit facet meets the face")
	var slant_ur: int = _find(outline, Vector2(1.0, -1.0).normalized())
	var shaded: ConsolePanel = _panel(COLUMN, false)
	var other: ConsolePanel.Outline = shaded.tray_outline()
	var other_layer: ConsolePanel.Layer = shaded.body_layer(other)
	var away: int = _find(other, Vector2.RIGHT, SIZE.y * 0.15, SIZE.y * 0.8)
	assert_lt(_color(other_layer, other, ConsolePanel.RING_LINE, away).get_luminance(), _color(other_layer, other, ConsolePanel.RING_FLAT, away).get_luminance(), "a shadow where a facet that faces away meets it")
	assert_gt(slant_ur, 0)
	for ring: int in [ConsolePanel.RING_FACE]:
		for i: int in outline.points.size():
			var want: Color = panel._face(layer.points[ring * outline.points.size() + i].y)
			assert_true(_color(layer, outline, ring, i).is_equal_approx(want), "the face is the plain body gradient (point %d), so the big fill has no tint to smear" % i)


func test_the_screen_edge_side_loses_the_chamfer_down_the_screen() -> void:
	var panel: ConsolePanel = _panel(COLUMN, true)
	var outline: ConsolePanel.Outline = panel.tray_outline()
	var layer: ConsolePanel.Layer = panel.body_layer(outline)
	var low: int = _find(outline, Vector2.RIGHT, SIZE.y * 0.45, SIZE.y * 0.8)
	assert_true(_color(layer, outline, ConsolePanel.RING_FLAT, low).is_equal_approx(_color(layer, outline, ConsolePanel.RING_FACE, low)), "no bevel down the straight side at the screen's edge")
	var column_side: int = _find(outline, Vector2.LEFT, SIZE.y * 0.45, SIZE.y * 0.8)
	assert_false(_color(layer, outline, ConsolePanel.RING_FLAT, column_side).is_equal_approx(_color(layer, outline, ConsolePanel.RING_FACE, column_side)), "but the side beside the column keeps it all the way down")
