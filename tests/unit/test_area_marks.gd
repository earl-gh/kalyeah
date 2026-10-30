extends GutTest
## The ground areas a player casts (owner): a landing circle, the paper trap and its slow zone, a jacks field and each bounce ring of the
## marble are a WHITE tint (45%) inside a thin (0.07 m) opaque WHITE outline when your team threw them (allies cast for your team, so they
## look the same), RED the same way when the other team did (orange when colour-blind). They stay unshaded and exact: the outline
## straddles the sim's radius, and the draw order of the ground ladder is kept.

const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
const QUANTUM: float = 1.5 / 255.0

var _rules: GameRules
var _blind: bool


func before_each() -> void:
	_rules = load("res://data/rules/game_rules.tres") as GameRules
	_blind = Settings.colorblind
	Settings.set_colorblind(false)


func after_each() -> void:
	Settings.set_colorblind(_blind)


func _fx() -> WeaponFxView:
	var sim: MatchSim = MatchSim.new(_rules, LAYOUT, 3)
	sim.add_player(1, 0)
	sim.add_player(2, 1)
	sim.add_player(3, 0)
	var fx: WeaponFxView = WeaponFxView.new()
	add_child_autofree(fx)
	fx.local_team = 0
	fx.watch(sim)
	return fx


func _def(id: StringName) -> WeaponDef:
	return _rules.weapons.filter(func(d: WeaponDef) -> bool: return d.id == id)[0] as WeaponDef


## The node of a zone of `kind` thrown by `team` with `weapon`.
func _zone(fx: WeaponFxView, weapon: StringName, kind: WeaponSystem.ZoneKind, team: int) -> Node3D:
	var zone: WeaponSystem.Zone = WeaponSystem.Zone.new()
	zone.team = team
	zone.kind = kind
	zone.def = _def(weapon)
	zone.radius = zone.def.trigger_radius if kind == WeaponSystem.ZoneKind.TRAP else zone.def.radius
	return autofree(fx.zones.make_node(zone)) as Node3D


## The vertices and colours of a mesh.
func _arrays(node: MeshInstance3D) -> Array:
	return node.mesh.surface_get_arrays(0)


## The alphas of the vertices of `node` that lie at height `y`, and their colours (alpha dropped).
func _layer(node: MeshInstance3D, y: float) -> Dictionary:
	var arrays: Array = _arrays(node)
	var vertices: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX] as PackedVector3Array
	var colors: PackedColorArray = arrays[Mesh.ARRAY_COLOR] as PackedColorArray
	var alphas: Array[float] = []
	var tints: Array[Color] = []
	for i: int in vertices.size():
		if absf(vertices[i].y - y) < 0.0001:
			alphas.append(colors[i].a)
			tints.append(Color(colors[i], 1.0))
	return {"alphas": alphas, "colors": tints}


func _near(color: Color, expected: Color) -> bool:
	return absf(color.r - expected.r) <= QUANTUM and absf(color.g - expected.g) <= QUANTUM and absf(color.b - expected.b) <= QUANTUM


func _all_alpha(layer: Dictionary, alpha: float) -> bool:
	var alphas: Array[float] = layer["alphas"] as Array[float]
	if alphas.is_empty():
		return false
	for value: float in alphas:
		if absf(value - alpha) > QUANTUM:
			return false
	return true


func _all_color(layer: Dictionary, expected: Color) -> bool:
	var colors: Array[Color] = layer["colors"] as Array[Color]
	if colors.is_empty():
		return false
	for color: Color in colors:
		if absf(color.r - expected.r) > QUANTUM or absf(color.g - expected.g) > QUANTUM or absf(color.b - expected.b) > QUANTUM:
			return false
	return true


func test_the_area_recipe_is_a_strong_tint_and_a_thin_opaque_outline() -> void:
	assert_between(FxKit.AREA_FILL_ALPHA, 0.35, 0.45, "less transparent than the 20 to 40% it was, still showing the street")
	assert_between(FxKit.AREA_RIM, 0.06, 0.08, "a thin outline")
	assert_lt(FxKit.AREA_RIM, FxKit.RIM / 3.0, "a third of the 0.22 m rim it was")
	assert_eq(FxKit.AREA_RIM_ALPHA, 1.0, "fully opaque")


func test_your_areas_are_white_and_the_enemys_red() -> void:
	var fx: WeaponFxView = _fx()
	var kinds: Array = [[&"bato_heavy", WeaponSystem.ZoneKind.EXPLOSION], [&"lata", WeaponSystem.ZoneKind.EXPLOSION], [&"jacks", WeaponSystem.ZoneKind.FIELD],
		[&"papel_trap", WeaponSystem.ZoneKind.SLOW_ZONE]]
	for pair: Array in kinds:
		for team: int in [0, 1]:
			var node: Node3D = _zone(fx, pair[0] as StringName, pair[1] as WeaponSystem.ZoneKind, team)
			var mark: MeshInstance3D = node.get_node("Mark") as MeshInstance3D
			var want: Color = Color.WHITE if team == 0 else Palette.TEAM_ENEMY
			var rim: Dictionary = _layer(mark, FxKit.Y_RIM)
			assert_true(_all_color(rim, want), "%s %s team %d: outline %s" % [pair[0], pair[1], team, want])
			assert_true(_all_alpha(rim, FxKit.AREA_RIM_ALPHA), "%s: the outline is opaque" % pair[0])
			if pair[1] != WeaponSystem.ZoneKind.SLOW_ZONE:
				var fill: Dictionary = _layer(mark, FxKit.Y_FILL)
				assert_true(_all_color(fill, want), "%s team %d: tint %s" % [pair[0], team, want])
				assert_true(_all_alpha(fill, FxKit.AREA_FILL_ALPHA), "%s: the tint is %.0f%%" % [pair[0], FxKit.AREA_FILL_ALPHA * 100.0])


func test_an_allys_area_looks_like_yours() -> void:
	var fx: WeaponFxView = _fx()
	# a teammate (player 3) casts for team 0, the viewer's team
	assert_eq(fx.context().sim.players[3].team, fx.local_team)
	assert_eq(fx.context().area_color(fx.context().sim.players[3].team), Color.WHITE)
	assert_eq(fx.context().area_color(fx.context().sim.players[2].team), Palette.TEAM_ENEMY)


func test_the_trap_slow_zone_is_a_white_tint_inside_a_thin_white_outline() -> void:
	var fx: WeaponFxView = _fx()
	var node: Node3D = _zone(fx, &"papel_trap", WeaponSystem.ZoneKind.SLOW_ZONE, 0)
	var fill: MeshInstance3D = node.get_node("Fill") as MeshInstance3D
	var colors: PackedColorArray = _arrays(fill)[Mesh.ARRAY_COLOR] as PackedColorArray
	assert_gt(colors.size(), 0)
	for color: Color in colors:
		assert_true(_near(color, Color.WHITE), "a white tint")
		assert_almost_eq(color.a, FxKit.AREA_FILL_ALPHA, QUANTUM, "45%, it was 25%")
	var mark: MeshInstance3D = node.get_node("Mark") as MeshInstance3D
	assert_true(_all_alpha(_layer(mark, FxKit.Y_RIM), 1.0), "the outline is opaque")
	assert_eq(_layer(mark, FxKit.Y_FILL)["alphas"], [] as Array[float], "the outline mesh carries no tint of its own")


func test_the_trap_and_the_landing_grow_a_white_progress_disc() -> void:
	var fx: WeaponFxView = _fx()
	for pair: Array in [[&"bato_heavy", WeaponSystem.ZoneKind.EXPLOSION], [&"papel_trap", WeaponSystem.ZoneKind.TRAP]]:
		for team: int in [0, 1]:
			var node: Node3D = _zone(fx, pair[0] as StringName, pair[1] as WeaponSystem.ZoneKind, team)
			var disc: MeshInstance3D = node.get_node("Progress") as MeshInstance3D
			var want: Color = Color.WHITE if team == 0 else Palette.TEAM_ENEMY
			for color: Color in _arrays(disc)[Mesh.ARRAY_COLOR] as PackedColorArray:
				assert_true(_near(color, want), "%s team %d" % [pair[0], team])


func test_a_trap_arming_has_a_thin_outline_and_no_tint() -> void:
	var fx: WeaponFxView = _fx()
	var node: Node3D = _zone(fx, &"papel_trap", WeaponSystem.ZoneKind.TRAP, 1)
	var mark: MeshInstance3D = node.get_node("Mark") as MeshInstance3D
	assert_true(_all_color(_layer(mark, FxKit.Y_RIM), Palette.TEAM_ENEMY), "their trap's range is red")
	assert_true(_all_alpha(_layer(mark, FxKit.Y_RIM), FxZones.TRAP_ARMING_RIM))
	assert_eq(_layer(mark, FxKit.Y_FILL)["alphas"], [] as Array[float], "the arming fill grows inside, the mark has none")


func test_the_outline_straddles_the_sims_exact_radius() -> void:
	var fx: WeaponFxView = _fx()
	var cases: Array = [[&"bato_heavy", WeaponSystem.ZoneKind.EXPLOSION], [&"lata", WeaponSystem.ZoneKind.EXPLOSION], [&"jacks", WeaponSystem.ZoneKind.FIELD],
		[&"papel_trap", WeaponSystem.ZoneKind.SLOW_ZONE], [&"papel_trap", WeaponSystem.ZoneKind.TRAP]]
	for pair: Array in cases:
		var def: WeaponDef = _def(pair[0] as StringName)
		var radius: float = def.trigger_radius if pair[1] == WeaponSystem.ZoneKind.TRAP else def.radius
		var mark: MeshInstance3D = _zone(fx, pair[0] as StringName, pair[1] as WeaponSystem.ZoneKind, 0).get_node("Mark") as MeshInstance3D
		var near: float = INF
		var far: float = 0.0
		var vertices: PackedVector3Array = _arrays(mark)[Mesh.ARRAY_VERTEX] as PackedVector3Array
		for vertex: Vector3 in vertices:
			if absf(vertex.y - FxKit.Y_RIM) < 0.0001:
				var distance: float = Vector2(vertex.x, vertex.z).length()
				near = minf(near, distance)
				far = maxf(far, distance)
		assert_almost_eq((far + near) / 2.0, radius, 0.02, "%s: the outline's middle is the radius" % pair[0])
		assert_almost_eq(far - near, FxKit.AREA_RIM, 0.01, "%s: and it is %.2f m wide" % [pair[0], FxKit.AREA_RIM])


func test_the_marks_are_unshaded_and_keep_the_draw_order() -> void:
	var fx: WeaponFxView = _fx()
	var node: Node3D = _zone(fx, &"papel_trap", WeaponSystem.ZoneKind.SLOW_ZONE, 1)
	for name: String in ["Mark", "Fill"]:
		var material: StandardMaterial3D = (node.get_node(name) as MeshInstance3D).material_override as StandardMaterial3D
		assert_eq(material.shading_mode, BaseMaterial3D.SHADING_MODE_UNSHADED, "%s is not lit" % name)
		assert_true(material.vertex_color_use_as_albedo, "%s takes its exact colour from the mesh" % name)
		assert_eq(material.cull_mode, BaseMaterial3D.CULL_DISABLED)
		assert_eq(material.render_priority, FxKit.PRIO_FILL)
	var landing: Node3D = _zone(fx, &"bato_heavy", WeaponSystem.ZoneKind.EXPLOSION, 0)
	assert_eq(((landing.get_node("Progress") as MeshInstance3D).material_override as Material).render_priority, FxKit.PRIO_RIM, "the growing disc draws over the tint")
	assert_lt(FxKit.Y_FILL, FxKit.Y_RIM, "the outline sits over the tint")


func test_colour_blind_the_enemy_area_follows_the_setting_and_yours_stays_white() -> void:
	Settings.set_colorblind(true)
	var fx: WeaponFxView = _fx()
	var theirs: MeshInstance3D = _zone(fx, &"jacks", WeaponSystem.ZoneKind.FIELD, 1).get_node("Mark") as MeshInstance3D
	assert_true(_all_color(_layer(theirs, FxKit.Y_RIM), TeamColors.BLIND_ENEMY), "orange, as every enemy mark")
	var mine: MeshInstance3D = _zone(fx, &"jacks", WeaponSystem.ZoneKind.FIELD, 0).get_node("Mark") as MeshInstance3D
	assert_true(_all_color(_layer(mine, FxKit.Y_RIM), Color.WHITE), "white is safe for every eye")


func test_the_marbles_bounce_rings_are_areas_too() -> void:
	for team: int in [0, 1]:
		var sim: MatchSim = MatchSim.new(_rules, LAYOUT, 3)
		sim.add_player(1, 0)
		sim.add_player(2, 1)
		sim.players[1].position = Vector2(0.0, 8.0) if team == 0 else Vector2(20.0, 8.0)
		sim.players[2].position = Vector2(20.0, -8.0) if team == 0 else Vector2(0.0, -8.0)
		sim.phase = MatchSim.Phase.PLAYING
		var fx: WeaponFxView = WeaponFxView.new()
		add_child_autofree(fx)
		fx.local_team = 0
		fx.watch(sim)
		var caster: int = 1 if team == 0 else 2
		var aim: Vector2 = Vector2(0.0, -1.0) if team == 0 else Vector2(0.0, 1.0)
		assert_true(sim.weapons.fire(sim, sim.players[caster], _def(&"bola"), aim, 0.0, -1))
		sim.weapon_cast.emit(caster, &"bola")
		fx.sync(sim, 1.0 / 30.0)
		sim.step(1.0 / 30.0)
		fx.sync(sim, 1.0 / 30.0)
		var rings: Array[MeshInstance3D] = []
		for rec: FxProjectiles.Rec in fx.projectiles.recs():
			rings.append_array(rec.rings)
		assert_eq(rings.size(), _def(&"bola").count, "a ring on each bounce point")
		var want: Color = Color.WHITE if team == 0 else Palette.TEAM_ENEMY
		for ring: MeshInstance3D in rings:
			var mark: MeshInstance3D = ring
			assert_true(_all_color(_layer(mark, FxKit.Y_RIM), want), "team %d outline" % team)
			assert_true(_all_color(_layer(mark, FxKit.Y_FILL), want), "team %d tint" % team)
			assert_true(_all_alpha(_layer(mark, FxKit.Y_FILL), FxKit.AREA_FILL_ALPHA))
