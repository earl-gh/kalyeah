extends GutTest

var RULES: GameRules = load("res://data/rules/game_rules.tres") as GameRules
const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")

## Isolated test layout with a single cover wall so projectile/movement weapon tests never collide with interior houses.
static func _cover_street() -> MapLayout:
	var street: MapLayout = LAYOUT.duplicate() as MapLayout
	street.obstacles = [Rect2(-13.5, 10.5, 3.0, 0.5)] as Array[Rect2]
	street.water_obstacles = [] as Array[Rect2]
	return street

const DT: float = 1.0 / 30.0
const CASTER: int = 1


func _sim() -> MatchSim:
	return MatchSim.new(RULES, _cover_street(), 11)


func _open_layout() -> MapLayout:
	var street: MapLayout = LAYOUT.duplicate() as MapLayout
	street.obstacles = [] as Array[Rect2]
	street.water_obstacles = [] as Array[Rect2]
	return street


## Team-0 caster at `at` holding `first` (slot 0) and `second` (slot 1).
func _caster(sim: MatchSim, first: StringName, second: StringName = &"papel_shield", at: Vector2 = Vector2.ZERO) -> PlayerState:
	var caster: PlayerState = sim.add_player(CASTER, 0)
	caster.position = at
	assert_true(sim.set_loadout(CASTER, first, second))
	return caster


func _target(sim: MatchSim, id: int, at: Vector2) -> PlayerState:
	return sim.add_dummy(id, 1, at, DummyBrain.standing())


## Hold the slot's button for `hold_ticks`, then release with `aim` (or cancel).
func _cast(sim: MatchSim, slot: int, aim: Vector2 = Vector2.ZERO, hold_ticks: int = 1, cancel: bool = false, id: int = CASTER) -> void:
	var bit: int = PlayerInput.BTN_WEAPON_1 if slot == 0 else PlayerInput.BTN_WEAPON_2
	for i: int in hold_ticks:
		sim.set_input(id, PlayerInput.create(Vector2.ZERO, aim, bit, sim.tick))
		sim.step(DT)
	var release: int = PlayerInput.BTN_AIM_CANCEL if cancel else 0
	sim.set_input(id, PlayerInput.create(Vector2.ZERO, aim, release, sim.tick))
	sim.step(DT)


func _wait(sim: MatchSim, seconds: float) -> void:
	for i: int in ceili(seconds / DT):
		sim.step(DT)


func _def(id: StringName) -> WeaponDef:
	for i: int in RULES.weapons.size():
		var def: WeaponDef = RULES.weapons[i]
		if def.id == id:
			return def
	return null


# ---- data ------------------------------------------------------------------

func test_the_weapons_match_the_gdd_table() -> void:
	assert_eq(RULES.weapons.size(), 14)
	var expected: Dictionary = {
		# id: [kind, cooldown, range, damage]
		&"bato_light": [WeaponDef.Kind.ATTACK, 4.5, 20.0, 20],
		&"bato_heavy": [WeaponDef.Kind.ATTACK, 10.0, 24.0, 38],
		&"gunting_light": [WeaponDef.Kind.ATTACK, 5.0, 8.5, 11],
		&"gunting_heavy": [WeaponDef.Kind.ATTACK, 8.5, 10.5, 42],
		&"papel_trap": [WeaponDef.Kind.CROWD_CONTROL, 9.0, 18.0, 0],
		&"papel_shield": [WeaponDef.Kind.BLOCK, 10.5, 0.0, 0],
		&"tsinelas_light": [WeaponDef.Kind.ATTACK, 6.5, 21.0, 11],
		&"tsinelas_heavy": [WeaponDef.Kind.ATTACK, 8.5, 24.0, 32],
		&"lata": [WeaponDef.Kind.CROWD_CONTROL, 11.5, 20.0, 16],
		&"jacks": [WeaponDef.Kind.CROWD_CONTROL, 10.0, 19.0, 8],
		&"bola": [WeaponDef.Kind.CROWD_CONTROL, 9.5, 23.0, 16],
		&"trumpo": [WeaponDef.Kind.CROWD_CONTROL, 11.0, 22.0, 26],
	}
	for id: StringName in expected:
		var def: WeaponDef = _def(id)
		assert_not_null(def, String(id))
		var row: Array = expected[id] as Array
		assert_eq(def.kind, row[0], "%s kind" % id)
		assert_almost_eq(def.cooldown, float(row[1]), 0.001, "%s cooldown" % id)
		assert_almost_eq(def.max_range, float(row[2]), 0.001, "%s range" % id)
		assert_eq(def.damage, int(row[3]), "%s damage" % id)
		assert_ne(def.street_game, "", "%s has a street game" % id)
	assert_eq(_def(&"bato_heavy").effect, StatusEffects.Type.STUN)
	assert_eq(_def(&"papel_trap").effect, StatusEffects.Type.SLOW)
	assert_eq(_def(&"lata").effect, StatusEffects.Type.POLYMORPH)
	assert_eq(_def(&"bola").effect, StatusEffects.Type.BOUNCE)
	assert_eq(_def(&"trumpo").effect, StatusEffects.Type.AIRBORNE)


# ---- loadout -----------------------------------------------------------------

func test_loadout_rejects_duplicates_and_unknown_weapons() -> void:
	var sim: MatchSim = _sim()
	sim.add_player(CASTER, 0)
	assert_false(sim.set_loadout(CASTER, &"bato_light", &"bato_light"), "two copies of one weapon")
	assert_false(sim.set_loadout(CASTER, &"bato_light", &"nope"))
	assert_true(sim.set_loadout(CASTER, &"bato_light", &"bato_heavy"), "two of the same type is fine")
	assert_eq(sim.players[CASTER].weapons, [&"bato_light", &"bato_heavy"] as Array[StringName])
	assert_true(sim.set_loadout(CASTER, &"bato_light", &"bato_heavy", &"lata"), "three distinct superpowers")
	assert_eq(sim.players[CASTER].weapons, [&"bato_light", &"bato_heavy", &"lata"] as Array[StringName])


func test_random_loadout_is_distinct_weapons_and_seeded() -> void:
	var a: MatchSim = MatchSim.new(RULES, LAYOUT, 5)
	var b: MatchSim = MatchSim.new(RULES, LAYOUT, 5)
	for i: int in 50:
		var pick: Array[StringName] = a.random_loadout()
		assert_eq(pick.size(), RULES.loadout_size)
		assert_ne(pick[0], pick[1])
		assert_ne(pick[0], pick[2])
		assert_ne(pick[1], pick[2])
		assert_eq(pick, b.random_loadout())


# ---- casting rules -------------------------------------------------------------

func test_weapons_cast_on_release_and_go_on_cooldown() -> void:
	var sim: MatchSim = _sim()
	var caster: PlayerState = _caster(sim, &"bato_light")
	var enemy: PlayerState = _target(sim, 2, Vector2(0.0, -5.0))
	sim.set_input(CASTER, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, PlayerInput.BTN_WEAPON_1, 0))
	sim.step(DT)
	assert_eq(caster.weapon_cooldowns[0], 0.0, "holding does not cast yet")
	sim.set_input(CASTER, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, 0, 1))
	sim.step(DT)
	assert_almost_eq(caster.weapon_cooldowns[0], 4.5, 0.001)
	_wait(sim, 1.0)
	assert_eq(enemy.hp, 80)


func test_release_over_the_cancel_zone_does_not_cast() -> void:
	var sim: MatchSim = _sim()
	var caster: PlayerState = _caster(sim, &"bato_light")
	var enemy: PlayerState = _target(sim, 2, Vector2(0.0, -5.0))
	_cast(sim, 0, Vector2.ZERO, 1, true)
	_wait(sim, 1.0)
	assert_eq(caster.weapon_cooldowns[0], 0.0)
	assert_eq(enemy.hp, 100)


func test_cooldown_blocks_recasting() -> void:
	var sim: MatchSim = _sim()
	_caster(sim, &"bato_light")
	var enemy: PlayerState = _target(sim, 2, Vector2(0.0, -5.0))
	_cast(sim, 0)
	_wait(sim, 1.0)
	_cast(sim, 0)
	_wait(sim, 1.0)
	assert_eq(enemy.hp, 80, "second cast inside 4.5 s does nothing")
	_wait(sim, 3.0)
	_cast(sim, 0)
	_wait(sim, 1.0)
	assert_eq(enemy.hp, 60)


func test_cannot_cast_while_stunned_polymorphed_or_tagged() -> void:
	var sim: MatchSim = _sim()
	var caster: PlayerState = _caster(sim, &"bato_light")
	var enemy: PlayerState = _target(sim, 2, Vector2(0.0, -5.0))
	sim.apply_effect(CASTER, StatusEffects.Type.STUN, 1.0, 0.0)
	_cast(sim, 0)
	assert_eq(caster.weapon_cooldowns[0], 0.0)
	_wait(sim, 1.0)
	_wait(sim, 1.1)  # the stun's immunity (cc_immunity) has to wear off before a polymorph can land
	sim.apply_effect(CASTER, StatusEffects.Type.POLYMORPH, 1.0, 0.0)
	_cast(sim, 0)
	assert_eq(caster.weapon_cooldowns[0], 0.0)
	_wait(sim, 1.0)
	sim.damage(CASTER, 100)
	assert_true(caster.tagged)
	_cast(sim, 0)
	_wait(sim, 1.0)
	assert_eq(enemy.hp, 100)


func test_stun_stops_movement() -> void:
	var sim: MatchSim = _sim()
	var caster: PlayerState = _caster(sim, &"bato_light")
	sim.apply_effect(CASTER, StatusEffects.Type.STUN, 1.0, 0.0)
	var at: Vector2 = caster.position
	for i: int in 20:
		sim.set_input(CASTER, PlayerInput.create(Vector2(0.0, -1.0), Vector2.ZERO, 0, sim.tick))
		sim.step(DT)
	assert_eq(caster.position, at)


func test_slow_and_polymorph_reduce_walking_speed() -> void:
	var sim: MatchSim = _sim()
	var caster: PlayerState = _caster(sim, &"bato_light")
	sim.apply_effect(CASTER, StatusEffects.Type.SLOW, 5.0, 0.4)
	var start: Vector2 = caster.position
	for i: int in 30:
		sim.set_input(CASTER, PlayerInput.create(Vector2(1.0, 0.0), Vector2.ZERO, 0, sim.tick))
		sim.step(DT)
	assert_almost_eq(caster.position.x - start.x, RULES.move_speed * 0.6, 0.08)


func test_tagged_players_are_not_hit() -> void:
	var sim: MatchSim = _sim()
	var caster: PlayerState = _caster(sim, &"gunting_heavy")
	var enemy: PlayerState = sim.add_player(2, 1)
	sim.damage(2, 100)
	assert_true(enemy.tagged)
	caster.position = enemy.position + Vector2(0.0, 2.0)
	watch_signals(sim)
	_cast(sim, 0, Vector2(0.0, -1.0))
	_wait(sim, 1.0)
	assert_signal_not_emitted(sim, "player_damaged")


func test_a_hit_tells_which_weapon_landed() -> void:
	var sim: MatchSim = _sim()
	_caster(sim, &"bato_light")
	_target(sim, 2, Vector2(0.0, -4.0))
	watch_signals(sim)
	_cast(sim, 0)
	_wait(sim, 2.0)
	assert_signal_emitted_with_parameters(sim, "weapon_hit", [2, &"bato_light"])


func test_weapon_hits_are_relayed_to_online_clients() -> void:
	assert_true((ServerMatch.RELAYED["sim"] as Array).has("weapon_hit"))


# ---- shapes --------------------------------------------------------------------

func test_targeted_bato_hits_the_nearest_enemy_only_in_range() -> void:
	var sim: MatchSim = _sim()
	var caster: PlayerState = _caster(sim, &"bato_light")
	var far: PlayerState = _target(sim, 2, Vector2(0.0, -22.0))
	_cast(sim, 0)
	assert_eq(caster.weapon_cooldowns[0], 0.0, "nobody in range: nothing thrown, no cooldown")
	var near: PlayerState = _target(sim, 3, Vector2(3.0, -3.0))
	_cast(sim, 0)
	_wait(sim, 1.0)
	assert_eq(near.hp, 80)
	assert_eq(far.hp, 100)


func test_ground_aoe_lands_after_the_delay_with_stun() -> void:
	var sim: MatchSim = _sim()
	_caster(sim, &"bato_heavy")
	var hit: PlayerState = _target(sim, 2, Vector2(0.0, -6.0))
	var near_miss: PlayerState = _target(sim, 3, Vector2(5.2, -6.0))
	_cast(sim, 0, Vector2(0.0, -6.0) / 24.0)
	_wait(sim, 0.3)
	assert_eq(hit.hp, 100, "still in the air")
	_wait(sim, 0.4)
	assert_eq(hit.hp, 62)
	assert_true(hit.effects.has(StatusEffects.Type.STUN))
	assert_eq(near_miss.hp, 100, "5.2 m from the impact is outside r = 4.2 + 0.45")


func test_ground_aoe_auto_aims_at_the_nearest_enemy() -> void:
	var sim: MatchSim = _sim()
	_caster(sim, &"bato_heavy")
	var enemy: PlayerState = _target(sim, 2, Vector2(4.0, -5.0))
	_cast(sim, 0)
	_wait(sim, 1.0)
	assert_eq(enemy.hp, 62)


func test_gunting_light_snips_by_hold_time_in_a_cone() -> void:
	var sim: MatchSim = _sim()
	_caster(sim, &"gunting_light")
	var front: PlayerState = _target(sim, 2, Vector2(0.0, -2.0))
	var behind: PlayerState = _target(sim, 3, Vector2(0.0, 2.0))
	var too_far: PlayerState = _target(sim, 4, Vector2(0.5, -9.8))
	_cast(sim, 0, Vector2(0.0, -1.0), 1)
	assert_eq(front.hp, 78, "a tap is 2 snips of 11")
	assert_eq(behind.hp, 100)
	assert_eq(too_far.hp, 100)
	_wait(sim, 5.2)
	_cast(sim, 0, Vector2(0.0, -1.0), 40)
	assert_eq(front.hp, 23, "a long hold is 5 snips of 11")


func test_snip_count_from_hold_time() -> void:
	var def: WeaponDef = _def(&"gunting_light")
	assert_eq(def.snip_count(0.0), 2)
	assert_eq(def.snip_count(0.23), 3)
	assert_eq(def.snip_count(0.45), 4)
	assert_eq(def.snip_count(5.0), 5)


func test_gunting_heavy_is_one_delayed_snip() -> void:
	var sim: MatchSim = _sim()
	_caster(sim, &"gunting_heavy")
	var enemy: PlayerState = _target(sim, 2, Vector2(1.0, -2.5))
	_cast(sim, 0, Vector2(0.0, -1.0))
	assert_eq(enemy.hp, 100, "wind-up")
	_wait(sim, 0.6)
	assert_eq(enemy.hp, 58)


func test_papel_trap_arms_then_becomes_a_slow_zone() -> void:
	var sim: MatchSim = _sim()
	_caster(sim, &"papel_trap")
	var enemy: PlayerState = _target(sim, 2, Vector2(0.0, -4.0))
	_cast(sim, 0, Vector2(0.0, -4.0) / 18.0)
	_wait(sim, 0.4)
	assert_false(enemy.effects.has(StatusEffects.Type.SLOW), "not armed yet")
	_wait(sim, 0.5)
	assert_true(enemy.effects.has(StatusEffects.Type.SLOW))
	assert_eq(enemy.hp, 100, "no damage")
	_wait(sim, 4.3)
	assert_false(enemy.effects.has(StatusEffects.Type.SLOW), "zone and slow gone after duration")
	assert_true(sim.weapons.zones.is_empty())


func test_papel_trap_waits_for_an_enemy() -> void:
	var sim: MatchSim = _sim()
	_caster(sim, &"papel_trap")
	_cast(sim, 0, Vector2(0.0, -1.0))
	_wait(sim, 3.0)
	assert_eq(sim.weapons.zones.size(), 1)
	assert_eq(sim.weapons.zones[0].kind, WeaponSystem.ZoneKind.TRAP)


func test_papel_shield_blocks_enemy_projectiles_not_own() -> void:
	var sim: MatchSim = _sim()
	var me: PlayerState = _caster(sim, &"papel_shield", &"bato_light")
	var enemy: PlayerState = sim.add_player(2, 1)
	enemy.position = Vector2(0.0, -8.0)
	assert_true(sim.set_loadout(2, &"bato_light", &"bato_heavy"))
	_cast(sim, 0, Vector2(0.0, -1.0))
	assert_eq(sim.weapons.shields.size(), 1)
	_cast(sim, 0, Vector2.ZERO, 1, false, 2)
	_wait(sim, 1.0)
	assert_eq(me.hp, 100, "enemy rock stopped by the paper wall")
	_cast(sim, 1)
	_wait(sim, 1.0)
	assert_eq(enemy.hp, 80, "own rock passes own shield")
	_wait(sim, 2.0)
	assert_true(sim.weapons.shields.is_empty(), "gone after 3.5 s")


func test_tsinelas_light_three_boomerangs_hit_out_and_back() -> void:
	var sim: MatchSim = _sim()
	_caster(sim, &"tsinelas_light")
	var enemy: PlayerState = _target(sim, 2, Vector2(0.0, -4.0))
	_cast(sim, 0, Vector2(0.0, -1.0))
	_wait(sim, 2.5)
	assert_eq(enemy.hp, 100 - 3 * 2 * 11)
	assert_true(sim.weapons.projectiles.is_empty(), "all caught again")


func test_tsinelas_heavy_one_big_boomerang() -> void:
	var sim: MatchSim = _sim()
	_caster(sim, &"tsinelas_heavy")
	var enemy: PlayerState = _target(sim, 2, Vector2(0.0, -4.0))
	_cast(sim, 0, Vector2(0.0, -1.0))
	_wait(sim, 3.0)
	assert_eq(enemy.hp, 100 - 2 * 32)


func test_lata_polymorphs_after_its_flight() -> void:
	var sim: MatchSim = _sim()
	_caster(sim, &"lata")
	var enemy: PlayerState = _target(sim, 2, Vector2(0.0, -5.0))
	_cast(sim, 0, Vector2(0.0, -5.0) / 20.0)
	assert_eq(enemy.hp, 100, "still flying")
	_wait(sim, 0.5)
	assert_eq(enemy.hp, 84)
	assert_true(enemy.effects.has(StatusEffects.Type.POLYMORPH))
	assert_false(enemy.can_act())


func test_jacks_field_ticks_damage_and_slow() -> void:
	var sim: MatchSim = _sim()
	_caster(sim, &"jacks")
	var enemy: PlayerState = _target(sim, 2, Vector2(0.0, -3.0))
	_cast(sim, 0, Vector2(0.0, -3.0) / 19.0)
	_wait(sim, 0.2)
	assert_true(enemy.effects.has(StatusEffects.Type.SLOW))
	_wait(sim, 4.6)
	assert_between(enemy.hp, 20, 36, "about 9 ticks of 8 over 4.5 s")
	assert_true(sim.weapons.zones.is_empty())


func test_bola_bounces_twice_bouncing_enemies() -> void:
	var sim: MatchSim = _sim()
	_caster(sim, &"bola")
	var step_z: float = 23.0 / 3.0
	var first: PlayerState = _target(sim, 2, Vector2(0.0, -step_z))
	var second: PlayerState = _target(sim, 3, Vector2(0.5, -step_z * 2.0))
	var between: PlayerState = _target(sim, 4, Vector2(0.0, -step_z * 1.5))
	_cast(sim, 0, Vector2(0.0, -1.0))
	_wait(sim, 0.4)
	assert_eq(first.hp, 84)
	assert_true(first.effects.has(StatusEffects.Type.BOUNCE))
	_wait(sim, 0.7)
	assert_eq(second.hp, 84)
	assert_eq(between.hp, 100, "only the bounce points hit")
	assert_true(sim.weapons.projectiles.is_empty())


func test_trumpo_passes_through_and_launches_once_each() -> void:
	var sim: MatchSim = _sim()
	_caster(sim, &"trumpo")
	var a: PlayerState = _target(sim, 2, Vector2(0.0, -3.0))
	var b: PlayerState = _target(sim, 3, Vector2(0.3, -6.0))
	var beyond: PlayerState = _target(sim, 4, Vector2(0.0, -24.5))
	_cast(sim, 0, Vector2(0.0, -1.0))
	_wait(sim, 0.4)
	assert_true(a.effects.has(StatusEffects.Type.AIRBORNE))
	assert_false(a.can_act())
	_wait(sim, 1.0)
	assert_eq(a.hp, 74, "hit once, not every tick")
	assert_eq(b.hp, 74)
	assert_true(b.effects.has(StatusEffects.Type.AIRBORNE))
	assert_eq(beyond.hp, 100, "stops at 22 m")


func test_obstacles_stop_projectiles_for_both_teams() -> void:
	var cover: Rect2 = _cover_street().obstacle_rects()[0]
	var x: float = cover.get_center().x
	for team: int in [0, 1]:
		var sim: MatchSim = _sim()
		var caster: PlayerState = sim.add_player(CASTER, team)
		caster.position = Vector2(x, cover.end.y + 3.0)
		sim.set_loadout(CASTER, &"bato_light", &"papel_shield")
		var hidden: PlayerState = sim.add_dummy(2, 1 - team, Vector2(x, cover.position.y - 1.0), DummyBrain.standing())
		_cast(sim, 0)
		_wait(sim, 1.0)
		assert_eq(hidden.hp, 100, "hiding behind the cover works for team %d" % team)


# ---- box swap ----------------------------------------------------------------

func test_swap_only_while_tagged_and_kept_weapons_keep_their_cooldown() -> void:
	var sim: MatchSim = _sim()
	var caster: PlayerState = _caster(sim, &"bato_heavy", &"lata")
	assert_false(sim.swap_loadout(CASTER, &"jacks", &"bola"), "not while free")
	caster.weapon_cooldowns = [5.0, 3.0] as Array[float]
	sim.kill(CASTER)
	assert_true(sim.swap_loadout(CASTER, &"jacks", &"bato_heavy"))
	assert_eq(caster.weapons, [&"jacks", &"bato_heavy"] as Array[StringName])
	assert_eq(caster.weapon_cooldowns, [0.0, 5.0] as Array[float])
	assert_true(sim.swap_loadout(CASTER, &"bato_heavy", &"lata"), "again, no limit")
	assert_eq(caster.weapon_cooldowns, [5.0, 0.0] as Array[float], "swapping away and back clears nothing")
	assert_false(sim.swap_loadout(CASTER, &"lata", &"lata"))


# ---- no precast, no following -------------------------------------------------

func test_holding_a_weapon_through_its_cooldown_does_not_precast() -> void:
	var sim: MatchSim = _sim()
	var caster: PlayerState = _caster(sim, &"bato_light")
	var enemy: PlayerState = _target(sim, 2, Vector2(0.0, -5.0))
	_cast(sim, 0)
	_wait(sim, 1.0)
	assert_eq(enemy.hp, 80)
	# press during the cooldown and keep holding until it is over, then let go
	_cast(sim, 0, Vector2.ZERO, 150)
	_wait(sim, 1.0)
	assert_eq(enemy.hp, 80, "a press during the cooldown never starts an aim")
	assert_eq(caster.weapon_cooldowns[0], 0.0)


func test_auto_aim_is_taken_at_the_press_and_does_not_follow_the_enemy() -> void:
	var sim: MatchSim = _sim()
	_caster(sim, &"bato_heavy")
	var enemy: PlayerState = _target(sim, 2, Vector2(0.0, -6.0))
	sim.set_input(CASTER, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, PlayerInput.BTN_WEAPON_1, sim.tick))
	sim.step(DT)
	enemy.position = Vector2(6.0, -6.0)
	for i: int in 10:
		sim.set_input(CASTER, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, PlayerInput.BTN_WEAPON_1, sim.tick))
		sim.step(DT)
	sim.set_input(CASTER, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, 0, sim.tick))
	sim.step(DT)
	_wait(sim, 1.0)
	assert_eq(enemy.hp, 100, "the rock lands where the enemy was at the press")
	assert_eq(sim.weapons.zones.size(), 0)


func test_targeted_bato_throws_at_the_enemy_locked_at_the_press() -> void:
	var sim: MatchSim = _sim()
	var caster: PlayerState = _caster(sim, &"bato_light")
	var first: PlayerState = _target(sim, 2, Vector2(0.0, -3.0))
	var second: PlayerState = _target(sim, 3, Vector2(0.0, -5.0))
	sim.set_input(CASTER, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, PlayerInput.BTN_WEAPON_1, sim.tick))
	sim.step(DT)
	first.position = Vector2(0.0, -6.5)
	_cast(sim, 0, Vector2.ZERO, 0)
	_wait(sim, 1.0)
	assert_eq(first.hp, 80, "still the locked enemy, not the now-nearer one")
	assert_eq(second.hp, 100)
	assert_gt(caster.weapon_cooldowns[0], 0.0)


func test_targeted_bato_does_nothing_if_the_locked_enemy_left_range() -> void:
	var sim: MatchSim = _sim()
	var caster: PlayerState = _caster(sim, &"bato_light")
	var enemy: PlayerState = _target(sim, 2, Vector2(0.0, -5.0))
	sim.set_input(CASTER, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, PlayerInput.BTN_WEAPON_1, sim.tick))
	sim.step(DT)
	enemy.position = Vector2(0.0, -24.0)
	_cast(sim, 0, Vector2.ZERO, 0)
	assert_eq(caster.weapon_cooldowns[0], 0.0, "nothing thrown, no cooldown spent")


func test_a_stun_while_aiming_drops_the_aim() -> void:
	var sim: MatchSim = _sim()
	var caster: PlayerState = _caster(sim, &"bato_heavy")
	sim.set_input(CASTER, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, PlayerInput.BTN_WEAPON_1, sim.tick))
	sim.step(DT)
	sim.apply_effect(CASTER, StatusEffects.Type.STUN, 0.2, 0.0)
	for i: int in 15:
		sim.set_input(CASTER, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, PlayerInput.BTN_WEAPON_1, sim.tick))
		sim.step(DT)
	sim.set_input(CASTER, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, 0, sim.tick))
	sim.step(DT)
	assert_eq(caster.weapon_cooldowns[0], 0.0, "press again after the stun")


func test_targeted_bato_with_no_enemy_is_not_thrown() -> void:
	var sim: MatchSim = _sim()
	var caster: PlayerState = _caster(sim, &"bato_light")
	_cast(sim, 0)
	_wait(sim, 1.0)
	assert_eq(caster.weapon_cooldowns[0], 0.0, "nothing to throw at: no cooldown spent")


# ---- casting while moving ----------------------------------------------------------

func _cast_and_walk(weapon: StringName) -> float:
	var sim: MatchSim = MatchSim.new(RULES, _open_layout(), 1)
	var p: PlayerState = sim.add_player(1, 0)
	p.position = Vector2.ZERO
	sim.set_loadout(1, weapon, &"bato_light")
	var aim: Vector2 = Vector2(0.0, -1.0)
	# hold the weapon button while walking (aiming while moving), then release
	for i: int in 6:
		sim.set_input(1, PlayerInput.create(Vector2(1.0, 0.0), aim, PlayerInput.BTN_WEAPON_1, sim.tick))
		sim.step(DT)
	var before: float = p.position.x
	sim.set_input(1, PlayerInput.create(Vector2(1.0, 0.0), aim, 0, sim.tick))
	for i: int in 6:
		sim.step(DT)
		sim.set_input(1, PlayerInput.create(Vector2(1.0, 0.0), aim, 0, sim.tick))
	return p.position.x - before


func test_light_attacks_cast_while_the_player_keeps_moving() -> void:
	assert_gt(_cast_and_walk(&"gunting_light"), 0.8, "no rooting after a light cast")


func test_heavy_attacks_aim_while_moving_but_root_the_caster_while_casting() -> void:
	var moved: float = _cast_and_walk(&"gunting_heavy")
	assert_lt(moved, 0.4, "the heavy cast roots the player for its animation")
	var sim: MatchSim = MatchSim.new(RULES, _open_layout(), 1)
	var p: PlayerState = sim.add_player(1, 0)
	sim.set_loadout(1, &"gunting_heavy", &"bato_light")
	var start: float = p.position.x
	for i: int in 6:
		sim.set_input(1, PlayerInput.create(Vector2(1.0, 0.0), Vector2(0.0, -1.0), PlayerInput.BTN_WEAPON_1, sim.tick))
		sim.step(DT)
	assert_gt(p.position.x - start, 0.8, "aiming a heavy attack does not stop you")


# ---- Langit Lupa: heal and the soil block ---------------------------------------------

func _fire_slot(sim: MatchSim, id: int, aim: Vector2, hold_ticks: int = 2) -> void:
	for i: int in hold_ticks:
		sim.set_input(id, PlayerInput.create(Vector2.ZERO, aim, PlayerInput.BTN_WEAPON_1, sim.tick))
		sim.step(DT)
	sim.set_input(id, PlayerInput.create(Vector2.ZERO, aim, 0, sim.tick))
	sim.step(DT)


func test_langit_lupa_skills_exist_with_their_numbers() -> void:
	var heal: WeaponDef = RULES.weapons.filter(func(w: WeaponDef) -> bool: return w.id == &"heal")[0]
	assert_eq(heal.kind, WeaponDef.Kind.HEAL)
	assert_eq(heal.street_game, "Langit Lupa")
	assert_eq(heal.heal, 42)
	var lupa: WeaponDef = RULES.weapons.filter(func(w: WeaponDef) -> bool: return w.id == &"langit_lupa")[0]
	assert_eq(lupa.kind, WeaponDef.Kind.BLOCK)
	assert_eq(lupa.shape, WeaponDef.Shape.SHIELD)
	assert_eq(lupa.street_game, "Langit Lupa")
	assert_almost_eq(lupa.width, 2.4, 0.01, "barangay eskinita soil pillar width")


func test_tapping_heal_heals_yourself() -> void:
	var sim: MatchSim = MatchSim.new(RULES, _open_layout(), 1)
	var me: PlayerState = sim.add_player(1, 0)
	sim.set_loadout(1, &"heal", &"bato_light")
	me.hp = 40
	_fire_slot(sim, 1, Vector2.ZERO)
	assert_eq(me.hp, 82)
	assert_gt(me.weapon_cooldowns[0], 0.0)


func test_dragging_heal_toward_a_teammate_heals_them_not_you() -> void:
	var sim: MatchSim = MatchSim.new(RULES, _open_layout(), 1)
	var me: PlayerState = sim.add_player(1, 0)
	me.position = Vector2(0.0, 5.0)
	var friend: PlayerState = sim.add_dummy(3, 0, Vector2(4.0, 5.0), DummyBrain.standing())
	var other: PlayerState = sim.add_dummy(4, 0, Vector2(-4.0, 5.0), DummyBrain.standing())
	sim.set_loadout(1, &"heal", &"bato_light")
	me.hp = 40
	friend.hp = 50
	other.hp = 50
	_fire_slot(sim, 1, Vector2(1.0, 0.0))
	assert_eq(friend.hp, 92, "the teammate on that side")
	assert_eq(other.hp, 50, "not the one on the other side")
	assert_eq(me.hp, 40, "and not yourself")


func test_a_heal_drag_with_nobody_there_does_nothing_and_costs_no_cooldown() -> void:
	var sim: MatchSim = MatchSim.new(RULES, _open_layout(), 1)
	var me: PlayerState = sim.add_player(1, 0)
	sim.set_loadout(1, &"heal", &"bato_light")
	me.hp = 40
	_fire_slot(sim, 1, Vector2(1.0, 0.0))
	assert_eq(me.hp, 40)
	assert_eq(me.weapon_cooldowns[0], 0.0)


func test_heal_never_reaches_enemies() -> void:
	var sim: MatchSim = MatchSim.new(RULES, _open_layout(), 1)
	var me: PlayerState = sim.add_player(1, 0)
	me.position = Vector2(0.0, 5.0)
	var enemy: PlayerState = sim.add_dummy(2, 1, Vector2(3.0, 5.0), DummyBrain.standing())
	sim.set_loadout(1, &"heal", &"bato_light")
	enemy.hp = 50
	_fire_slot(sim, 1, Vector2(1.0, 0.0))
	assert_eq(enemy.hp, 50)


func test_the_soil_block_blocks_shots_then_collapses() -> void:
	var sim: MatchSim = MatchSim.new(RULES, _open_layout(), 1)
	var me: PlayerState = sim.add_player(1, 0)
	me.position = Vector2(0.0, 5.0)
	sim.set_loadout(1, &"langit_lupa", &"bato_light")
	_fire_slot(sim, 1, Vector2(0.0, -1.0))
	assert_eq(sim.weapons.shields.size(), 1)
	var block: WeaponSystem.Shield = sim.weapons.shields[0]
	assert_almost_eq(block.half_width * 2.0, 2.4, 0.001)
	assert_true(sim.weapons.blocks_segment(1, me.position + Vector2(0.0, -4.0), me.position + Vector2(0.0, -1.0)), "enemy shots stop at the block")
	for i: int in 145:
		sim.step(DT)
	assert_eq(sim.weapons.shields.size(), 0, "collapsed after its 4.5 s duration")
