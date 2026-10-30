extends GutTest
## The aim guide (owner): one clean white language. An area (a landing circle, a trap, a field, each bounce, a wall) is a white tint inside a
## thin white outline; a lane or a wedge is only the thin outline. The marble's guide is exactly its two bounce rings and NOTHING joins
## them. The cancel (red), "cannot cast now" (pale) and "nobody" (nothing) states stay. The whole guide is one triangle-strip surface and
## far fewer vertices than the six-a-segment ribbons it was (before this pass: 504 for the marble, 240 for a circle, 108 for a wedge).

const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
const RULES: GameRules = preload("res://data/rules/game_rules.tres")
const CASTER: int = 1
## Vertex colours are stored with 8 bits a channel.
const EPS: float = 1.5 / 255.0

var _sim: MatchSim
var _indicator: AimIndicator
var _guide: bool
var _motion: bool


func before_each() -> void:
	_guide = Settings.aim_guide
	_motion = Settings.reduced_motion
	Settings.set_aim_guide(true)
	_sim = MatchSim.new(RULES, LAYOUT, 3)
	_sim.add_player(CASTER, 0)
	_sim.add_player(2, 1)
	_sim.players[CASTER].position = Vector2(0.0, 8.0)
	_sim.phase = MatchSim.Phase.PLAYING
	_indicator = autofree(AimIndicator.new()) as AimIndicator
	add_child(_indicator)


func after_each() -> void:
	Settings.set_aim_guide(_guide)
	Settings.reduced_motion = _motion


func _def(id: StringName) -> WeaponDef:
	return _sim.weapon_defs[id]


func _show(id: StringName, aim: Vector2, cancelled: bool = false, manual: bool = true, target: int = -1) -> void:
	_indicator.show_aim(_sim, _sim.players[CASTER], _def(id), aim, target, cancelled, 0.0, manual)


func _vertices() -> PackedVector3Array:
	if _indicator.mesh.get_surface_count() == 0:
		return PackedVector3Array()
	return _indicator.mesh.surface_get_arrays(0)[Mesh.ARRAY_VERTEX] as PackedVector3Array


func _colors() -> PackedColorArray:
	return _indicator.mesh.surface_get_arrays(0)[Mesh.ARRAY_COLOR] as PackedColorArray


## The distinct colours drawn.
func _palette() -> Array[Color]:
	var found: Array[Color] = []
	for color: Color in _colors():
		var known: bool = false
		for other: Color in found:
			if absf(other.r - color.r) < EPS and absf(other.g - color.g) < EPS and absf(other.b - color.b) < EPS and absf(other.a - color.a) < EPS:
				known = true
				break
		if not known:
			found.append(color)
	return found


func _has(colors: Array[Color], want: Color) -> bool:
	for color: Color in colors:
		if absf(color.r - want.r) < EPS and absf(color.g - want.g) < EPS and absf(color.b - want.b) < EPS and absf(color.a - want.a) < EPS:
			return true
	return false


# ---- the marble: two rings and no line between them -----------------------------------------------------------------

func test_the_bounce_guide_is_exactly_its_two_rings() -> void:
	var bola: WeaponDef = _def(&"bola")
	var lines: Array[PackedVector2Array] = AimIndicator.outline(_sim, _sim.players[CASTER], bola, Vector2(0.0, -1.0))
	assert_eq(lines.size(), 1 + bola.count, "the range circle and one ring a bounce (no lane)")
	assert_eq(bola.count, 2)
	for i: int in range(1, lines.size()):
		assert_eq(lines[i].size(), AimIndicator.CIRCLE_POINTS + 1, "ring %d is a closed circle" % i)
		var middle: Vector2 = Vector2.ZERO
		for k: int in AimIndicator.CIRCLE_POINTS:
			middle += lines[i][k]
		middle /= float(AimIndicator.CIRCLE_POINTS)
		for point: Vector2 in lines[i]:
			assert_almost_eq(point.distance_to(middle), bola.radius, 0.001, "ring %d is a circle of the bounce radius" % i)


func test_nothing_is_drawn_between_the_two_bounce_rings() -> void:
	var bola: WeaponDef = _def(&"bola")
	_show(&"bola", Vector2(0.0, -1.0))
	var origin: Vector2 = _sim.players[CASTER].position
	var centres: Array[Vector2] = []
	for i: int in bola.count:
		centres.append(origin + Vector2(0.0, -1.0) * bola.max_range * float(i + 1) / float(bola.count))
	var vertices: PackedVector3Array = _vertices()
	assert_gt(vertices.size(), 0)
	var reach: float = bola.radius + AimIndicator.LINE_WIDTH
	for vertex: Vector3 in vertices:
		var at: Vector2 = Vector2(vertex.x, vertex.z)
		var near: float = INF
		for centre: Vector2 in centres:
			near = minf(near, at.distance_to(centre))
		assert_lte(near, reach, "every vertex belongs to one of the two rings (no lane, no connecting segment) at %s" % at)


func test_the_bounce_rings_are_white_tint_and_thin_white_outline() -> void:
	_show(&"bola", Vector2(0.0, -1.0))
	var palette: Array[Color] = _palette()
	assert_true(_has(palette, AimIndicator.LINE_COLOR), "a white outline")
	assert_true(_has(palette, AimIndicator.tint_of(AimIndicator.LINE_COLOR)), "and a white tint")
	assert_eq(palette.size(), 2, "nothing else")
	assert_eq(AimIndicator.LINE_COLOR, Color.WHITE)
	assert_between(AimIndicator.LINE_WIDTH, 0.05, 0.09, "thin")
	assert_between(AimIndicator.tint_of(AimIndicator.LINE_COLOR).a, 0.2, 0.5, "a tint, not a solid")


# ---- the same language for the other guides -------------------------------------------------------------------------

func test_areas_have_a_tint_lanes_and_wedges_do_not() -> void:
	for id: StringName in [&"bato_heavy", &"lata", &"jacks", &"papel_trap", &"papel_shield", &"langit_lupa"]:
		_show(id, Vector2(0.0, -0.8))
		assert_true(AimIndicator.is_area(_def(id)), "%s is an area" % id)
		assert_true(_has(_palette(), AimIndicator.tint_of(AimIndicator.LINE_COLOR)), "%s: white tint inside" % id)
	for id: StringName in [&"gunting_light", &"gunting_heavy", &"tsinelas_light", &"tsinelas_heavy", &"trumpo"]:
		_show(id, Vector2(0.0, -0.8))
		assert_false(AimIndicator.is_area(_def(id)), "%s is a lane or a wedge" % id)
		assert_true(_has(_palette(), AimIndicator.LINE_COLOR), "%s: the thin outline" % id)
		assert_false(_has(_palette(), AimIndicator.tint_of(AimIndicator.LINE_COLOR)) and id != &"gunting_light" and id != &"gunting_heavy", "%s: no tint" % id)


func test_a_circle_guide_is_one_strip_surface_without_heavy_ribbons() -> void:
	_show(&"bato_heavy", Vector2(0.0, -0.5))
	assert_eq(_indicator.mesh.get_surface_count(), 1, "one surface: one draw call")
	# a circle area: 32 tint points + 33 outline pairs + the join between them (two degenerate points) and the first point written twice
	var points: int = AimIndicator.CIRCLE_POINTS
	assert_lte(_vertices().size(), points + 2 * (points + 1) + 4, "a circle area is about 100 vertices")


func test_vertex_counts_are_far_below_the_old_ribbons() -> void:
	# before this pass (6 vertices a segment of 40 per circle, fans for fills): marble 504, circle 240, wedge 108
	_show(&"bola", Vector2(0.0, -1.0))
	assert_lt(_vertices().size(), 504 / 2, "the marble's guide")
	_show(&"bato_heavy", Vector2(0.0, -0.5))
	assert_lt(_vertices().size(), 240 / 2, "a landing circle, tint and outline")
	_show(&"gunting_light", Vector2(0.0, -1.0))
	assert_lt(_vertices().size(), 108, "a wedge's thin outline (and the pips)")


func test_the_outline_keeps_its_width_round_a_corner() -> void:
	var points: PackedVector2Array = AimIndicator.lane(Vector2.ZERO, Vector2(0.0, -1.0), 4.0, 1.0)
	_indicator.show_lines([points] as Array[PackedVector2Array], Color.WHITE)
	var half: float = AimIndicator.LINE_WIDTH / 2.0
	for vertex: Vector3 in _vertices():
		assert_lte(absf(absf(vertex.x) - 1.0), half + 0.001, "an outline vertex is half a line width from the lane's side: %s" % vertex)
		assert_true(absf(vertex.z) <= half + 0.001 or absf(vertex.z + 4.0) <= half + 0.001, "and from its end: %s" % vertex)


func test_a_snips_wedge_tip_stays_thin() -> void:
	_show(&"gunting_light", Vector2(0.0, -1.0))
	var tip: Vector2 = _sim.players[CASTER].position
	for vertex: Vector3 in _vertices():
		var at: Vector2 = Vector2(vertex.x, vertex.z)
		if at.distance_to(tip) < 1.0:
			assert_lt(at.distance_to(tip), AimIndicator.LINE_WIDTH * AimIndicator.MITER_LIMIT, "the 60 degree tip sticks out at most the miter limit")


# ---- the states stay ------------------------------------------------------------------------------------------------

func test_over_cancel_the_whole_guide_goes_red() -> void:
	_show(&"bola", Vector2(0.0, -1.0), true)
	var palette: Array[Color] = _palette()
	assert_true(_has(palette, AimIndicator.CANCEL_COLOR), "a red outline")
	assert_true(_has(palette, AimIndicator.tint_of(AimIndicator.CANCEL_COLOR)), "and a red tint")
	assert_eq(palette.size(), 2)
	assert_gt(AimIndicator.CANCEL_COLOR.r, AimIndicator.CANCEL_COLOR.b + 0.4)


func test_when_nothing_would_be_cast_the_guide_goes_pale() -> void:
	assert_eq(AimIndicator.color_for(false, false), AimIndicator.NO_CAST_COLOR, "nothing to hit: pale")
	_indicator.show_lines([AimIndicator.circle(Vector2(0.0, 2.0), 1.5)] as Array[PackedVector2Array], AimIndicator.NO_CAST_COLOR, [] as Array[PackedVector2Array], [] as Array[Color], [] as Array[PackedVector2Array], 0, true)
	var palette: Array[Color] = _palette()
	assert_true(_has(palette, AimIndicator.NO_CAST_COLOR))
	assert_true(_has(palette, AimIndicator.tint_of(AimIndicator.NO_CAST_COLOR)), "pale tint too")
	assert_lt(AimIndicator.tint_of(AimIndicator.NO_CAST_COLOR).a, AimIndicator.tint_of(AimIndicator.LINE_COLOR).a, "quieter than a live guide")


func test_nobody_in_reach_and_not_dragged_draws_nothing() -> void:
	_show(&"bato_light", Vector2(0.0, -1.0), false, false)
	assert_eq(_indicator.mesh.get_surface_count(), 0, "a homing rock with nobody locked, and a tap: nothing")


func test_a_ring_under_a_kid_in_reach_stays_red_for_an_enemy() -> void:
	_sim.players[2].position = Vector2(0.0, 4.0)
	_show(&"bato_heavy", Vector2(0.0, -0.5), false, false)
	assert_true(_has(_palette(), AimIndicator.REACH_ENEMY), "the tap still shows who it reaches")
	assert_eq(_palette().size(), 1, "and only that (no landing circle on a tap)")


func test_reduced_motion_does_not_change_the_guide() -> void:
	Settings.reduced_motion = false
	_show(&"bola", Vector2(0.0, -1.0))
	var moving: PackedVector3Array = _vertices()
	Settings.reduced_motion = true
	_show(&"bola", Vector2(0.0, -1.0))
	assert_eq(_vertices(), moving, "the guide is a still picture either way")


func test_the_guide_switch_still_turns_it_off() -> void:
	Settings.set_aim_guide(false)
	_show(&"bola", Vector2(0.0, -1.0))
	assert_eq(_indicator.mesh.get_surface_count(), 0)
