extends GutTest
## The practice sparring partner (FightBrain): a dummy that fights back with the same PlayerInput as anyone.

const RULES: GameRules = preload("res://data/rules/game_rules.tres")
const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
const PRACTICE_SCENE: PackedScene = preload("res://scenes/match/practice.tscn")
const DT: float = 1.0 / 30.0
const PLAYER: int = 1
const BOT: int = 2


## You at the middle of the street and a fighter `distance` metres up it, armed with `first` and `second`.
func _duel(distance: float, first: StringName = &"bato_light", second: StringName = &"gunting_light", seed_value: int = 7) -> MatchSim:
	var sim: MatchSim = MatchSim.new(RULES, LAYOUT, seed_value)
	var me: PlayerState = sim.add_player(PLAYER, 0)
	me.position = Vector2(0.0, 0.0)
	var spot: Vector2 = Vector2(0.0, -distance)
	sim.add_dummy(BOT, 1, spot, FightBrain.fighter(spot))
	sim.set_loadout(BOT, first, second)
	sim.set_loadout(PLAYER, &"bola", &"papel_shield")
	return sim


func _run(sim: MatchSim, seconds: float) -> void:
	for i: int in ceili(seconds / DT):
		sim.step(DT)


func _casts(sim: MatchSim) -> Array[StringName]:
	var seen: Array[StringName] = []
	sim.weapon_cast.connect(func(id: int, weapon: StringName) -> void:
		if id == BOT:
			seen.append(weapon))
	return seen


func test_a_fighter_in_range_hurts_you() -> void:
	var sim: MatchSim = _duel(5.0)
	var casts: Array[StringName] = _casts(sim)
	_run(sim, 4.0)
	assert_gt(casts.size(), 0, "it cast")
	assert_lt(sim.players[PLAYER].hp, RULES.player_max_hp, "and it landed")


func test_a_fighter_does_not_react_to_an_enemy_out_of_its_aggro_range() -> void:
	var sim: MatchSim = _duel(RULES.bot_aggro_range + 4.0)
	var casts: Array[StringName] = _casts(sim)
	var start: Vector2 = sim.players[BOT].position
	_run(sim, 4.0)
	assert_eq(casts.size(), 0)
	assert_eq(sim.players[BOT].position, start, "it waits at its spot")


func test_a_fighter_comes_back_to_its_spot_when_you_leave() -> void:
	var sim: MatchSim = _duel(5.0)
	_run(sim, 2.0)
	sim.players[PLAYER].position = Vector2(0.0, 30.0)
	_run(sim, 8.0)
	assert_lt(sim.players[BOT].position.distance_to(Vector2(0.0, -5.0)), 0.5)


func test_it_casts_no_faster_than_its_gap() -> void:
	var sim: MatchSim = _duel(5.0, &"bato_light", &"tsinelas_light")
	var times: Array[float] = []
	sim.weapon_cast.connect(func(id: int, _weapon: StringName) -> void:
		if id == BOT:
			times.append(sim.match_time))
	_run(sim, 10.0)
	assert_gte(times.size(), 2)
	for i: int in range(1, times.size()):
		assert_gte(times[i] - times[i - 1], RULES.bot_cast_gap - 0.1)


func test_it_does_not_cast_a_weapon_that_cannot_reach() -> void:
	# gunting reaches 3 m only; the enemy is 6 m away and the other weapon is a heal
	var sim: MatchSim = _duel(6.0, &"gunting_light", &"heal")
	var casts: Array[StringName] = _casts(sim)
	_run(sim, 0.5)
	assert_false(casts.has(&"gunting_light"), "not yet in reach")


func test_a_healthy_fighter_keeps_its_heal_and_a_hurt_one_uses_it() -> void:
	var sim: MatchSim = _duel(5.0, &"heal", &"gunting_light")
	var casts: Array[StringName] = _casts(sim)
	_run(sim, 1.0)
	assert_false(casts.has(&"heal"), "full health: no heal")
	sim.players[BOT].hp = 20
	_run(sim, 2.0)
	assert_true(casts.has(&"heal"), "hurt: heals itself")
	assert_gt(sim.players[BOT].hp, 20)


func test_a_fighter_closes_the_distance_then_stands_off() -> void:
	var sim: MatchSim = _duel(12.0, &"bato_light", &"tsinelas_light")
	_run(sim, 6.0)
	var distance: float = sim.players[BOT].position.distance_to(sim.players[PLAYER].position)
	assert_lt(distance, 12.0, "it closed in")
	assert_gt(distance, FightBrain.STANDOFF_MIN - 1.0, "but it keeps a standoff, it does not climb on you")


func test_it_never_casts_while_stunned_or_tagged() -> void:
	var sim: MatchSim = _duel(5.0)
	var casts: Array[StringName] = _casts(sim)
	sim.players[BOT].effects.apply(StatusEffects.Type.STUN, 3.0)
	_run(sim, 2.5)
	assert_eq(casts.size(), 0, "stunned")
	sim.damage(BOT, 9999)
	_run(sim, 1.0)
	assert_eq(casts.size(), 0, "tagged")


func test_a_fighter_fetches_a_tagged_teammate_when_no_enemy_is_close() -> void:
	var sim: MatchSim = MatchSim.new(RULES, LAYOUT, 3)
	var spot: Vector2 = Vector2(-6.0, 6.0)
	sim.add_player(PLAYER, 1)
	sim.players[PLAYER].position = Vector2(20.0, 20.0)
	sim.add_dummy(BOT, 0, spot, FightBrain.fighter(spot))
	sim.add_player(3, 0)
	sim.damage(3, 9999)
	assert_true(sim.players[3].tagged)
	_run(sim, 12.0)
	assert_false(sim.players[3].tagged, "set free by the fighter")


func test_the_same_seed_plays_the_same_fight() -> void:
	var first: MatchSim = _duel(5.0, &"bato_light", &"tsinelas_light", 99)
	var second: MatchSim = _duel(5.0, &"bato_light", &"tsinelas_light", 99)
	_run(first, 6.0)
	_run(second, 6.0)
	assert_eq(first.players[PLAYER].hp, second.players[PLAYER].hp)
	assert_eq(first.players[BOT].position, second.players[BOT].position)


func test_a_fighter_is_still_a_dummy_for_captures() -> void:
	var sim: MatchSim = _duel(5.0)
	assert_true(sim.is_fighter(BOT))
	assert_false(sim.objectives.all_enemies_tagged(sim, 0, sim._brains), "a team of dummies never scores by being tagged")


func test_a_sparring_loadout_always_has_something_that_hurts() -> void:
	var sim: MatchSim = MatchSim.new(RULES, LAYOUT, 11)
	for i: int in 40:
		var pair: Array[StringName] = sim.fighting_loadout()
		assert_ne(pair[0], pair[1])
		assert_true(sim.weapon_defs[pair[0]].damage > 0 or sim.weapon_defs[pair[1]].damage > 0, "%s %s" % [pair[0], pair[1]])


# ---- practice ---------------------------------------------------------------------------

func _practice(fighters: int) -> PracticeMatch:
	var practice: PracticeMatch = autofree(PRACTICE_SCENE.instantiate()) as PracticeMatch
	practice.rules = practice.rules.duplicate() as GameRules
	practice.rules.practice_fighters = fighters
	add_child(practice)
	return practice


func test_practice_has_one_sparring_partner_by_default() -> void:
	var shipped: GameRules = ResourceLoader.load("res://data/rules/game_rules.tres", "", ResourceLoader.CACHE_MODE_IGNORE) as GameRules
	assert_eq(shipped.practice_fighters, 1, "the file says one (the test run switches it off for everyone else)")
	var practice: PracticeMatch = _practice(1)
	assert_true(practice.sim.is_fighter(PracticeMatch.ENEMY_PATROL_ID))
	assert_false(practice.sim.is_fighter(PracticeMatch.ENEMY_STAND_ID))
	assert_false(practice.sim.is_fighter(PracticeMatch.ENEMY_BOX_ID))
	assert_false(practice.sim.is_fighter(PracticeMatch.ALLY_ID), "the ally stays a rescuer")
	var arms: PlayerState = practice.sim.players[PracticeMatch.ENEMY_PATROL_ID]
	assert_eq(arms.weapons.size(), 2, "it is armed")


func test_the_toggle_can_turn_the_fight_off_or_up() -> void:
	var none: PracticeMatch = _practice(0)
	assert_false(none.sim.is_fighter(PracticeMatch.ENEMY_PATROL_ID), "0: dummies as before")
	var two: PracticeMatch = _practice(2)
	assert_true(two.sim.is_fighter(PracticeMatch.ENEMY_PATROL_ID))
	assert_true(two.sim.is_fighter(PracticeMatch.ENEMY_STAND_ID))


func test_a_practice_fighter_goes_for_you_once_the_match_is_on() -> void:
	var practice: PracticeMatch = _practice(1)
	practice.finish_pick()
	practice.skip_intro()
	var me: PlayerState = practice.sim.players[PracticeMatch.LOCAL_ID]
	var bot: PlayerState = practice.sim.players[PracticeMatch.ENEMY_PATROL_ID]
	practice.sim.set_loadout(PracticeMatch.ENEMY_PATROL_ID, &"bato_light", &"tsinelas_light")
	me.position = bot.position + Vector2(0.0, 5.0)
	for i: int in 150:
		practice.advance(DT)
	assert_lt(me.hp, RULES.player_max_hp, "the sparring partner hit you")


# ---- aim, lead and cover ------------------------------------------------------------------

func _rules_without_error() -> GameRules:
	var rules: GameRules = RULES.duplicate() as GameRules
	rules.bot_aim_error_degrees = 0.0
	return rules


## A bot (team 1) at the origin and an enemy (team 0) `where` away, with `speed` along x remembered from the last step.
func _aim_setup(rules: GameRules, where: Vector2, speed: float, skill: float) -> Array:
	var sim: MatchSim = MatchSim.new(rules, LAYOUT, 4)
	var enemy: PlayerState = sim.add_dummy(1, 0, where - Vector2(speed / 30.0, 0.0), DummyBrain.standing())
	var brain: FightBrain = FightBrain.fighter(Vector2.ZERO)
	brain.skill = skill
	var me: PlayerState = sim.add_dummy(2, 1, Vector2.ZERO, brain)
	brain._track(me, sim)
	enemy.position = where
	brain._track(me, sim)
	return [sim, brain, me, enemy]


func test_a_ground_circle_is_thrown_at_the_enemys_distance_not_at_full_range() -> void:
	var made: Array = _aim_setup(_rules_without_error(), Vector2(0.0, 4.0), 0.0, 0.0)
	var def: WeaponDef = (made[0] as MatchSim).weapon_defs[&"bato_heavy"]
	var aim: Vector2 = (made[1] as FightBrain)._aim_at(made[2] as PlayerState, made[3] as PlayerState, def, made[0] as MatchSim)
	assert_almost_eq(aim.length(), 4.0 / def.max_range, 0.01, "the circle lands where the enemy is")
	assert_almost_eq(aim.normalized().y, 1.0, 0.01)


func test_a_thrown_weapon_is_aimed_along_a_direction_of_length_one() -> void:
	var made: Array = _aim_setup(_rules_without_error(), Vector2(0.0, 5.0), 0.0, 0.0)
	var def: WeaponDef = (made[0] as MatchSim).weapon_defs[&"tsinelas_light"]
	var aim: Vector2 = (made[1] as FightBrain)._aim_at(made[2] as PlayerState, made[3] as PlayerState, def, made[0] as MatchSim)
	assert_almost_eq(aim.length(), 1.0, 0.001)


func test_a_skilled_bot_aims_ahead_of_a_moving_enemy_and_an_unskilled_one_does_not() -> void:
	var rules: GameRules = _rules_without_error()
	var plain: Array = _aim_setup(rules, Vector2(0.0, 6.0), 5.0, 0.0)
	var sharp: Array = _aim_setup(rules, Vector2(0.0, 6.0), 5.0, 1.0)
	var def: WeaponDef = (plain[0] as MatchSim).weapon_defs[&"tsinelas_light"]
	var at_it: Vector2 = (plain[1] as FightBrain)._aim_at(plain[2] as PlayerState, plain[3] as PlayerState, def, plain[0] as MatchSim)
	var ahead: Vector2 = (sharp[1] as FightBrain)._aim_at(sharp[2] as PlayerState, sharp[3] as PlayerState, def, sharp[0] as MatchSim)
	assert_almost_eq(at_it.x, 0.0, 0.001, "skill 0: straight at where it is")
	assert_gt(ahead.x, 0.1, "skill 1: toward where it is going (it runs along +x)")


func test_a_delayed_circle_is_led_by_its_delay() -> void:
	var rules: GameRules = _rules_without_error()
	var sharp: Array = _aim_setup(rules, Vector2(0.0, 6.0), 5.0, 1.0)
	var def: WeaponDef = (sharp[0] as MatchSim).weapon_defs[&"bato_heavy"]
	var aim: Vector2 = (sharp[1] as FightBrain)._aim_at(sharp[2] as PlayerState, sharp[3] as PlayerState, def, sharp[0] as MatchSim)
	var landing: Vector2 = aim * def.max_range
	assert_almost_eq(landing.x, 5.0 * def.delay, 0.2, "it lands where the enemy will be after the wind-up")


func test_skill_comes_from_the_rules_unless_it_is_set() -> void:
	var sim: MatchSim = MatchSim.new(RULES, LAYOUT, 1)
	var brain: FightBrain = FightBrain.fighter(Vector2.ZERO)
	assert_eq(brain._skill(sim), RULES.bot_skill)
	brain.skill = 1.0
	assert_eq(brain._skill(sim), 1.0)
	brain.skill = 7.0
	assert_eq(brain._skill(sim), 1.0, "clamped")


func test_a_segment_is_stopped_by_a_rect_it_crosses_and_not_by_one_it_misses() -> void:
	var wall: Rect2 = Rect2(-2.0, -1.0, 4.0, 2.0)
	assert_true(FightBrain.segment_hits_rect(Vector2(0.0, -5.0), Vector2(0.0, 5.0), wall), "straight through")
	assert_false(FightBrain.segment_hits_rect(Vector2(5.0, -5.0), Vector2(5.0, 5.0), wall), "beside it")
	assert_false(FightBrain.segment_hits_rect(Vector2(0.0, -5.0), Vector2(0.0, -2.0), wall), "stops short")
	assert_true(FightBrain.segment_hits_rect(Vector2(0.0, 0.0), Vector2(0.0, 5.0), wall), "starts inside")
	assert_true(FightBrain.segment_hits_rect(Vector2(-5.0, 0.0), Vector2(5.0, 0.0), wall), "across, parallel to an edge")
	assert_false(FightBrain.segment_hits_rect(Vector2(-5.0, 3.0), Vector2(5.0, 3.0), wall), "parallel and outside")


func test_a_bot_behind_cover_steps_aside_and_never_throws_into_it() -> void:
	var layout: MapLayout = LAYOUT.duplicate() as MapLayout
	var wall: Rect2 = Rect2(-3.0, -0.5, 6.0, 1.0)
	var rects: Array[Rect2] = [wall]
	layout.obstacles = rects
	var sim: MatchSim = MatchSim.new(RULES, layout, 8)
	var target: PlayerState = sim.add_player(PLAYER, 0)
	target.position = Vector2(0.0, 5.0)
	var spot: Vector2 = Vector2(0.0, -5.0)
	sim.add_dummy(BOT, 1, spot, FightBrain.fighter(spot))
	sim.set_loadout(BOT, &"bato_light", &"tsinelas_light")
	sim.set_loadout(PLAYER, &"bola", &"papel_shield")
	var bad_casts: Array[int] = []
	var casts: Array[StringName] = []
	sim.weapon_cast.connect(func(id: int, weapon: StringName) -> void:
		if id == BOT:
			casts.append(weapon)
			if FightBrain.segment_hits_rect(sim.players[BOT].position, target.position, wall.grow(0.3)):
				bad_casts.append(sim.tick))
	_run(sim, 12.0)
	assert_gt(casts.size(), 0, "it found a way to throw")
	assert_eq(bad_casts.size(), 0, "never into the wall")
	assert_lt(target.hp, RULES.player_max_hp, "and it landed")
