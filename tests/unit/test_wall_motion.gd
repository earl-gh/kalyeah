extends GutTest
## The paper wall glides forward and the block of earth widens, in the sim (owner: "soil block and paper block hitbox must change too
## smoothly and accordingly without any delay"): the blocking line is a pure function of the wall's age and the weapon's data
## (`WallMotion`, `WeaponDef.push_*` / `expand_*`), the same on the server and on a mirror. Walls with no motion data stay put.

const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
const DT: float = 1.0 / 30.0

var _rules: GameRules


func before_each() -> void:
	_rules = load("res://data/rules/game_rules.tres") as GameRules


## A sim with you (1, team 0) at (0, 8) facing north and an enemy (2) far to the side.
func _setup(seed_value: int = 3) -> MatchSim:
	var sim: MatchSim = MatchSim.new(_rules, LAYOUT, seed_value)
	sim.add_player(1, 0)
	sim.add_player(2, 1)
	sim.players[1].position = Vector2(0.0, 8.0)
	sim.players[1].facing = Vector2(0.0, -1.0)
	sim.players[2].position = Vector2(12.0, 0.0)
	sim.phase = MatchSim.Phase.PLAYING
	return sim


func _cast(sim: MatchSim, weapon: StringName) -> WeaponSystem.Shield:
	assert_true(sim.weapons.fire(sim, sim.players[1], sim.weapon_defs[weapon], Vector2(0.0, -1.0), 0.0, 2))
	return sim.weapons.shields[sim.weapons.shields.size() - 1]


func test_the_numbers_are_data() -> void:
	var paper: WeaponDef = load("res://data/weapons/papel_shield.tres") as WeaponDef
	var soil: WeaponDef = load("res://data/weapons/langit_lupa.tres") as WeaponDef
	assert_eq(paper.push_distance, 1.5, "the paper wall glides 1.5 m")
	assert_eq(paper.push_time, 0.6, "over 0.6 s")
	assert_eq(paper.expand_factor, 1.0, "and does not widen")
	assert_eq(soil.expand_factor, 3.0, "the block triples its width")
	assert_eq(soil.push_distance, 0.0, "and does not move")
	assert_gte(soil.expand_delay, 0.2, "after it has risen")
	assert_eq(paper.duration, 2.5, "the paper wall's time is unchanged")
	assert_eq(soil.duration, 3.5, "so is the block's")
	assert_eq(paper.width, 3.0)
	assert_eq(soil.width, 1.2)
	var plain: WeaponDef = load("res://data/weapons/bato_light.tres") as WeaponDef
	assert_eq([plain.push_distance, plain.expand_factor], [0.0, 1.0], "any other weapon has the old defaults")


func test_the_push_is_an_ease_out_to_exactly_the_distance() -> void:
	var def: WeaponDef = load("res://data/weapons/papel_shield.tres") as WeaponDef
	assert_eq(WallMotion.push(def, 0.0), 0.0, "it starts where it was cast")
	assert_gt(WallMotion.push(def, def.push_time / 2.0), def.push_distance / 2.0, "fast first (ease-out)")
	var last: float = 0.0
	for i: int in 30:
		var now: float = WallMotion.push(def, float(i) * DT)
		assert_gte(now, last, "never backwards")
		last = now
	assert_eq(WallMotion.push(def, def.push_time), def.push_distance, "exactly the distance when the push is over")
	assert_eq(WallMotion.push(def, 99.0), def.push_distance, "and it holds")
	assert_eq(WallMotion.width_scale(def, 1.0), 1.0, "the paper wall never widens")


func test_the_widening_waits_then_eases_to_exactly_the_factor() -> void:
	var def: WeaponDef = load("res://data/weapons/langit_lupa.tres") as WeaponDef
	assert_eq(WallMotion.width_scale(def, 0.0), 1.0)
	assert_eq(WallMotion.width_scale(def, def.expand_delay), 1.0, "it has not begun while the block rises")
	assert_gt(WallMotion.width_scale(def, def.expand_delay + DT), 1.0, "it begins the moment the rise is done")
	assert_almost_eq(WallMotion.width_scale(def, def.expand_delay + def.expand_time / 2.0), 2.0, 0.001, "smooth in and out: half way at half the time")
	assert_eq(WallMotion.width_scale(def, def.expand_delay + def.expand_time), def.expand_factor, "exactly 3x when it is over")
	assert_eq(WallMotion.width_scale(def, def.duration), def.expand_factor, "and it keeps that width to the end")
	assert_eq(WallMotion.push(def, 1.0), 0.0, "the block never moves")


func test_the_paper_wall_line_follows_the_formula_every_tick() -> void:
	var sim: MatchSim = _setup()
	var shield: WeaponSystem.Shield = _cast(sim, &"papel_shield")
	var def: WeaponDef = shield.def
	var start: Vector2 = sim.players[1].position + Vector2(0.0, -1.0) * def.offset
	assert_eq(shield.center, start, "at first it is where it always was: 1.2 m ahead")
	assert_almost_eq(shield.half_width, 1.5, 0.0001, "and 3 m wide")
	var ticks: int = 0
	while not sim.weapons.shields.is_empty():
		sim.step(DT)
		ticks += 1
		if sim.weapons.shields.is_empty():
			break
		var age: float = def.duration - shield.time_left
		assert_almost_eq(shield.center.y, start.y - WallMotion.push(def, age), 0.0001, "tick %d: the line is where the formula puts it" % ticks)
		assert_eq(shield.center.x, start.x, "straight ahead, never sideways")
		assert_almost_eq(shield.half_width, 1.5, 0.0001, "the paper keeps its width")
		if age >= def.push_time + 0.01:
			assert_almost_eq(shield.center.y, start.y - def.push_distance, 0.0001, "pushed exactly 1.5 m, then it holds")
	assert_almost_eq(float(ticks) * DT, def.duration, DT, "and it goes at the same time as before (2.5 s)")


func test_the_block_widens_after_rising_and_keeps_its_width_to_the_end() -> void:
	var sim: MatchSim = _setup()
	var shield: WeaponSystem.Shield = _cast(sim, &"langit_lupa")
	var def: WeaponDef = shield.def
	var start: Vector2 = shield.center
	assert_almost_eq(shield.half_width, 0.6, 0.0001, "it begins person-wide")
	var widened: bool = false
	var ticks: int = 0
	while not sim.weapons.shields.is_empty():
		sim.step(DT)
		ticks += 1
		if sim.weapons.shields.is_empty():
			break
		var age: float = def.duration - shield.time_left
		assert_eq(shield.center, start, "the block stays where it rose")
		assert_almost_eq(shield.half_width, 0.6 * WallMotion.width_scale(def, age), 0.0001, "tick %d: its line is as long as the formula says" % ticks)
		if age <= def.expand_delay:
			assert_almost_eq(shield.half_width, 0.6, 0.0001, "unchanged while it rises")
		if age >= def.expand_delay + def.expand_time + 0.01:
			widened = true
			assert_almost_eq(shield.half_width, 1.8, 0.0001, "exactly three times as wide (3.6 m) from then until it is gone")
	assert_true(widened)
	assert_almost_eq(float(ticks) * DT, def.duration, DT, "and it goes at the same time as before (3.5 s)")


func test_a_shot_is_stopped_where_the_wall_is_now() -> void:
	var sim: MatchSim = _setup()
	var shield: WeaponSystem.Shield = _cast(sim, &"papel_shield")
	var old_y: float = shield.center.y
	for i: int in 24:
		sim.step(DT)
	var new_y: float = old_y - shield.def.push_distance
	assert_almost_eq(shield.center.y, new_y, 0.0001)
	assert_true(sim.weapons.blocks_segment(1, Vector2(0.0, new_y - 0.5), Vector2(0.0, new_y + 0.5)), "an enemy shot crossing the wall where it stands now stops")
	assert_false(sim.weapons.blocks_segment(1, Vector2(0.0, old_y - 0.3), Vector2(0.0, old_y + 0.3)), "one crossing where it was cast no longer does")
	assert_false(sim.weapons.blocks_segment(0, Vector2(0.0, new_y - 0.5), Vector2(0.0, new_y + 0.5)), "your own shots still pass")


func test_a_shot_the_moving_wall_overtakes_is_stopped() -> void:
	var sim: MatchSim = _setup()
	var shield: WeaponSystem.Shield = _cast(sim, &"papel_shield")
	sim.step(DT)
	assert_ne(shield.prev_center, shield.center, "the wall moved this tick")
	var middle: Vector2 = (shield.prev_center + shield.center) / 2.0
	assert_true(sim.weapons.blocks_segment(1, middle, middle + Vector2(0.0, -0.01)), "a shot standing where the wall just swept is stopped as if it had hit it")
	assert_false(sim.weapons.blocks_segment(1, shield.prev_center + Vector2(0.0, 0.5), shield.prev_center + Vector2(0.0, 0.6)), "while one behind it is not")


func test_a_widening_block_stops_shots_in_the_new_width_only() -> void:
	var sim: MatchSim = _setup()
	var shield: WeaponSystem.Shield = _cast(sim, &"langit_lupa")
	var y: float = shield.center.y
	var wide: Vector2 = Vector2(1.5, y)
	assert_false(sim.weapons.blocks_segment(1, wide + Vector2(0.0, -0.5), wide + Vector2(0.0, 0.5)), "1.5 m to the side is open at first (the block is 1.2 m wide)")
	for i: int in 30:
		sim.step(DT)
	assert_true(sim.weapons.blocks_segment(1, wide + Vector2(0.0, -0.5), wide + Vector2(0.0, 0.5)), "and blocked once it has widened to 3.6 m")
	assert_false(sim.weapons.blocks_segment(1, Vector2(2.0, y - 0.5), Vector2(2.0, y + 0.5)), "but not past its new edge (1.8 m)")


## Your hp after an enemy rock is thrown at you from the side (3 m over, 6 m up), with the block cast first or not.
func _rock_from_the_side(with_block: bool) -> int:
	var sim: MatchSim = _setup()
	if with_block:
		_cast(sim, &"langit_lupa")
	for i: int in 30:
		sim.step(DT)
	var enemy: PlayerState = sim.players[2]
	enemy.position = Vector2(3.0, 2.0)
	var rock: WeaponDef = sim.weapon_defs[&"bato_light"]
	var hit: Vector2 = (sim.players[1].position - enemy.position).normalized()
	assert_true(sim.weapons.fire(sim, enemy, rock, hit, 0.0, 1))
	for i: int in 60:
		sim.step(DT)
	return sim.players[1].hp


func test_an_enemy_rock_thrown_at_the_widened_block_is_stopped_by_it() -> void:
	assert_lt(_rock_from_the_side(false), _rules.player_max_hp, "without the block the rock hits you")
	assert_eq(_rock_from_the_side(true), _rules.player_max_hp, "it crosses the line 0.8 m off centre, past the block's old edge (0.6 m), and the widened block stops it")


func test_a_wall_never_pushes_a_kid() -> void:
	var sim: MatchSim = _setup()
	var shield: WeaponSystem.Shield = _cast(sim, &"papel_shield")
	var kid: PlayerState = sim.players[2]
	kid.position = shield.center + Vector2(0.0, -0.5)
	var spot: Vector2 = kid.position
	for i: int in 20:
		sim.step(DT)
	assert_eq(kid.position, spot, "a kid the wall glides over is neither moved nor stopped: walls block shots only")


func test_the_same_seed_gives_the_same_walls() -> void:
	var lines: Array = []
	for run: int in 2:
		var sim: MatchSim = _setup(9)
		var shield: WeaponSystem.Shield = _cast(sim, &"papel_shield")
		var block: WeaponSystem.Shield = _cast(sim, &"langit_lupa")
		var seen: Array[Vector3] = []
		for i: int in 40:
			sim.step(DT)
			seen.append(Vector3(shield.center.y, shield.half_width, block.half_width))
		lines.append(seen)
	assert_eq(lines[0], lines[1], "deterministic")


func test_a_mirror_moves_and_widens_the_walls_like_the_server() -> void:
	var source: MatchSim = _setup(7)
	var paper: WeaponSystem.Shield = _cast(source, &"papel_shield")
	var block: WeaponSystem.Shield = _cast(source, &"langit_lupa")
	for i: int in 9:
		source.step(DT)
	var mirror: MatchSim = MatchSim.new(_rules, LAYOUT, 7)
	mirror.add_player(1, 0)
	Snapshot.apply(mirror, Snapshot.capture(source, {1: 1}), 1)
	assert_eq(mirror.weapons.shields.size(), 2)
	var ours: Array[WeaponSystem.Shield] = [paper, block]
	for i: int in 2:
		var theirs: WeaponSystem.Shield = mirror.weapons.shields[i]
		assert_eq(theirs.center, ours[i].center, "the same line when it arrives")
		assert_eq(theirs.half_width, ours[i].half_width)
		assert_eq(theirs.origin, ours[i].origin, "and it knows where the wall was cast")
		assert_eq(theirs.base_half_width, ours[i].base_half_width)
	for step: int in 25:
		source.step(DT)
		for shield: WeaponSystem.Shield in mirror.weapons.shields:
			shield.time_left -= DT
			shield.place()
	for i: int in 2:
		var theirs: WeaponSystem.Shield = mirror.weapons.shields[i]
		assert_almost_eq(theirs.center.y, ours[i].center.y, 0.0001, "a mirror that moves on its own stays with the server")
		assert_almost_eq(theirs.half_width, ours[i].half_width, 0.0001)
