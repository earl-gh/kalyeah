extends GutTest
## The blue tray behind the cards is one smooth shape: no outline of any dark colour, both top corners rounded by `UiSkin.RADIUS_M` and
## ending exactly at the screen's edge on the side away from the column (with the corner rounded on the screen too), lit by a rim that follows the curve, every layer built from the same
## outline (no gaps or overlaps between them), and a shade cast onto the street that fades out smoothly.

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


func test_both_top_corners_are_arcs_of_the_corner_radius() -> void:
	var outline: ConsolePanel.Outline = _panel(COLUMN, true).tray_outline()
	var radius: float = float(ConsolePanel.CORNER)
	assert_eq(radius, float(UiSkin.RADIUS_M), "the corner is the skin's medium radius")
	var steps: int = ConsolePanel.ARC_STEPS
	var arcs: Array[Vector2] = [Vector2(COLUMN + radius, radius), Vector2(_bounds(outline.points).end.x - radius, radius)]
	for corner: int in 2:
		for step: int in steps + 1:
			var i: int = outline.top_left - steps + step + corner * (steps + 1)
			assert_almost_eq(outline.points[i].distance_to(arcs[corner]), radius, 0.001, "corner %d point %d is on the arc" % [corner, step])
			assert_almost_eq(outline.normals[i].length(), 1.0, 0.001, "its normal is a unit vector")
			assert_almost_eq(outline.points[i].direction_to(arcs[corner]).dot(-outline.normals[i]), 1.0, 0.001, "pointing straight out of the arc")
	assert_almost_eq(outline.points[outline.top_left].y, 0.0, 0.001, "the arc ends on the top edge")
	assert_almost_eq(outline.points[outline.top_left + 1].y, 0.0, 0.001, "and the next one starts on it")


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
	var darkest: float = ConsolePanel.TRAY_BOTTOM.b - 0.001
	for i: int in range(count, layer.colors.size()):
		var color: Color = layer.colors[i]
		assert_eq(color.a, 1.0, "the tray itself is opaque")
		assert_gt(color.b, darkest, "the chamfer never falls below the tray body's darkest blue")
		assert_gt(color.b, color.r + 0.3, "every pixel of it is blue (never a black or navy line)")
	# along the top the edge itself is the light rim, lighter than the body
	var edge: Color = layer.colors[count + outline.top_left]
	assert_gt(edge.g, ConsolePanel.TRAY_BODY.g + 0.2, "the edge is lit")
	# the rim follows the curve: the corner's edge is as light as the top's, not a different band
	var corner_edge: Color = layer.colors[count + outline.top_left - floori(ConsolePanel.ARC_STEPS * 0.5)]
	assert_gt(corner_edge.g, ConsolePanel.TRAY_BODY.g + 0.15, "round the corner too")


func test_the_body_lightens_at_the_top_and_darkens_toward_the_bottom_in_one_blend() -> void:
	var panel: ConsolePanel = _panel(COLUMN, true)
	var outline: ConsolePanel.Outline = panel.tray_outline()
	var layer: ConsolePanel.Layer = panel.body_layer(outline)
	var count: int = outline.points.size()
	var inner: int = (floori(float(layer.points.size()) / float(count)) - 1) * count
	# down the straight left side, on the innermost ring (no rim or side shade there): never lighter going down past the body stop
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
