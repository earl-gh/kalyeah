extends GutTest
## Online the effects read a mirror sim written from snapshots (no zone time left, no projectile direction) plus the relayed
## signals; they must play like offline: the meteor falls from the zone's age, the heal cross shows from `player_healed`, and
## offline a heal draws one cross, not two.

const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
const DT: float = 1.0 / 30.0

var _rules: GameRules
var _motion: bool


func before_each() -> void:
	_rules = load("res://data/rules/game_rules.tres") as GameRules
	_motion = Settings.reduced_motion
	Settings.reduced_motion = false


func after_each() -> void:
	Settings.reduced_motion = _motion


func _sim() -> MatchSim:
	var sim: MatchSim = MatchSim.new(_rules, LAYOUT, 3)
	sim.add_player(1, 0)
	sim.add_player(2, 1)
	sim.players[1].position = Vector2(0.0, 8.0)
	sim.players[1].facing = Vector2(0.0, -1.0)
	sim.players[2].position = Vector2(0.0, 4.0)
	sim.phase = MatchSim.Phase.PLAYING
	return sim


func _fx(sim: MatchSim) -> WeaponFxView:
	var fx: WeaponFxView = WeaponFxView.new()
	add_child_autofree(fx)
	fx.view_scale = 2.0
	fx.local_team = 0
	fx.watch(sim)
	return fx


## Passes `source`'s relayed signals on to `mirror` the way the client does.
func _relay(source: MatchSim, mirror: MatchSim) -> void:
	source.weapon_cast.connect(func(id: int, weapon: StringName) -> void: mirror.weapon_cast.emit(id, weapon))
	source.player_damaged.connect(func(id: int, amount: int) -> void: mirror.player_damaged.emit(id, amount))
	source.player_healed.connect(func(id: int, amount: int) -> void: mirror.player_healed.emit(id, amount))
	source.effect_applied.connect(func(id: int, type: int) -> void: mirror.effect_applied.emit(id, type))
	source.weapons.cone_struck.connect(func(o: int, d: WeaponDef, a: Vector2, b: Vector2) -> void: mirror.weapons.cone_struck.emit(o, d, a, b))
	source.weapons.cone_winding.connect(func(o: int, d: WeaponDef, a: Vector2, b: Vector2) -> void: mirror.weapons.cone_winding.emit(o, d, a, b))


func _snapshot(source: MatchSim, mirror: MatchSim) -> void:
	var acks: Dictionary[int, int] = {}
	Snapshot.apply(mirror, Snapshot.capture(source, acks), -1)


func _cast(sim: MatchSim, weapon: StringName, aim: Vector2) -> void:
	var def: WeaponDef = sim.weapon_defs[weapon]
	assert_true(sim.weapons.fire(sim, sim.players[1], def, aim, 0.0, 2), "%s is cast" % weapon)
	sim.weapon_cast.emit(1, weapon)


func test_online_meteor_falls_from_zone_age() -> void:
	var server: MatchSim = _sim()
	var mirror: MatchSim = _sim()
	_relay(server, mirror)
	var fx: WeaponFxView = _fx(mirror)
	_cast(server, &"bato_heavy", Vector2(0.0, -4.0 / 7.0))
	var meteor: Node3D = null
	var last: float = INF
	var falls: int = 0
	for frame: int in 30:
		server.step(DT)
		if frame % 2 == 0:
			_snapshot(server, mirror)
		fx.sync(mirror, DT)
		if mirror.weapons.zones.is_empty():
			if meteor != null:
				break
			continue
		assert_eq(mirror.weapons.zones[0].time_left, 0.0, "the mirror has no time left, like a real client")
		meteor = fx.find_child("Meteor", true, false) as Node3D
		assert_not_null(meteor)
		var y: float = meteor.global_position.y
		assert_gte(y - WeaponModel.METEOR_SIZE / 2.0, -0.001, "never below the street")
		if frame < 12 and last != INF:
			assert_lt(y, last, "it keeps falling at frame %d" % frame)
			falls += 1
		last = y
	assert_gte(falls, 10, "it fell frame by frame from its age, not from a time left the client never gets")


func test_online_heal_shows_from_player_healed_only() -> void:
	var mirror: MatchSim = _sim()
	var fx: WeaponFxView = _fx(mirror)
	mirror.player_healed.emit(1, 30)
	fx.sync(mirror, DT)
	assert_eq(fx.heals.crosses().size(), 1, "the relayed heal draws its cross")


func test_offline_heal_is_one_cross_not_two() -> void:
	var sim: MatchSim = _sim()
	var fx: WeaponFxView = _fx(sim)
	sim.damage(1, 40)
	fx.sync(sim, DT)
	_cast(sim, &"heal", Vector2.ZERO)
	fx.sync(sim, DT)
	assert_eq(fx.heals.crosses().size(), 1, "one heal, one cross (weapons.healed and player_healed both fire offline)")


func test_online_bola_bounces_from_distance() -> void:
	var server: MatchSim = _sim()
	server.players[2].position = Vector2(30.0, 0.0)
	var mirror: MatchSim = _sim()
	mirror.players[2].position = Vector2(30.0, 0.0)
	_relay(server, mirror)
	var fx: WeaponFxView = _fx(mirror)
	_cast(server, &"bola", Vector2(0.0, -1.0))
	var radius: float = fx.projectiles.drawn_width(server.weapon_defs[&"bola"]) / 2.0
	var lowest: float = INF
	var highest: float = 0.0
	for frame: int in 40:
		server.step(DT)
		if frame % 2 == 0:
			_snapshot(server, mirror)
		fx.sync(mirror, DT)
		if mirror.weapons.projectiles.is_empty():
			continue
		var marble: Node3D = fx.projectiles.node_for(mirror.weapons.projectiles[0].id)
		var bottom: float = marble.position.y - radius * (marble.get_node("Squash") as Node3D).basis.get_scale().y
		assert_gte(bottom, -0.001, "never below the street")
		if frame > 3:
			lowest = minf(lowest, bottom)
			highest = maxf(highest, bottom)
	assert_lt(lowest, 0.05, "it touches down online too (from its distance, no bounce count in the snapshot)")
	assert_gt(highest, 1.0, "and arcs")


func test_online_can_flies() -> void:
	var server: MatchSim = _sim()
	server.players[2].position = Vector2(0.0, 2.0)
	var mirror: MatchSim = _sim()
	mirror.players[2].position = Vector2(0.0, 2.0)
	_relay(server, mirror)
	var fx: WeaponFxView = _fx(mirror)
	_cast(server, &"lata", Vector2(0.0, -6.0 / 7.0))
	var peak: float = 0.0
	var start_z: float = -INF
	var last_z: float = 0.0
	for frame: int in 30:
		server.step(DT)
		if frame % 2 == 0:
			_snapshot(server, mirror)
		fx.sync(mirror, DT)
		if mirror.weapons.zones.is_empty():
			continue
		var can: Node3D = fx.zones.node_for(mirror.weapons.zones[0].id).get_node("Can") as Node3D
		if start_z == -INF:
			start_z = can.global_position.z
		peak = maxf(peak, can.global_position.y)
		last_z = can.global_position.z
	assert_gt(start_z, 6.0, "online too it starts at the thrower (cast position from the relayed weapon_cast)")
	assert_gt(peak, 2.2, "and arcs")
	assert_lt(last_z, start_z - 4.0, "toward the circle")
