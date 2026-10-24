extends GutTest
## Casting a card at a street point, or a tap (docs/GDD.md "Controls: tap to move, cards to cast" > Casting: drag a card onto the street).
## Seam: MatchSim (PlayerInput with an aim point in, weapon effects, heals and elixir out). The server clamps; the client is never trusted.

const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
const DT: float = 1.0 / 30.0

var _rules: GameRules


func before_each() -> void:
	_rules = (load("res://data/rules/game_rules.tres") as GameRules).duplicate() as GameRules
	_rules.card_cycle = true
	_rules.elixir_enabled = true


## Player 1 (team 0) at (0, 5), teammate 3 at (-4, 5), enemy 2 far away unless moved.
func _sim() -> MatchSim:
	var sim: MatchSim = MatchSim.new(_rules, LAYOUT, 5)
	sim.add_player(1, 0)
	sim.add_player(2, 1)
	sim.add_player(3, 0)
	sim.players[1].position = Vector2(0.0, 5.0)
	sim.players[2].position = Vector2(12.0, -20.0)
	sim.players[3].position = Vector2(-12.0, 20.0)
	for id: int in [1, 2, 3]:
		sim.deal_cards(id)
	sim.players[1].elixir = _rules.elixir_max
	# a tap with nobody in range goes right, so no aim-point test can pass by the facing
	sim.players[1].facing = Vector2.RIGHT
	return sim


## Press slot 0 holding `weapon`, then release; `point` (if any) is sent with the release as the aim point.
func _cast(sim: MatchSim, weapon: StringName, point: Variant = null) -> void:
	sim.players[1].weapons[0] = weapon
	sim.set_input(1, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, PlayerInput.BTN_CARD_1, sim.tick))
	sim.step(DT)
	var release: PlayerInput = PlayerInput.create(Vector2.ZERO, Vector2.ZERO, 0, sim.tick)
	if point != null:
		release.with_aim_point(point as Vector2)
	sim.set_input(1, release)
	sim.step(DT)


func _zone(sim: MatchSim) -> WeaponSystem.Zone:
	assert_eq(sim.weapons.zones.size(), 1)
	return sim.weapons.zones[0] if not sim.weapons.zones.is_empty() else null


func test_a_ground_card_lands_on_the_point() -> void:
	var sim: MatchSim = _sim()
	_cast(sim, &"lata", Vector2(3.0, 1.0))
	assert_almost_eq(_zone(sim).center, Vector2(3.0, 1.0), Vector2(0.0001, 0.0001))


func test_a_point_past_the_range_lands_at_full_range_in_its_direction() -> void:
	var sim: MatchSim = _sim()
	_cast(sim, &"lata", Vector2(0.0, -95.0))
	assert_almost_eq(_zone(sim).center, Vector2(0.0, 5.0 - 7.0), Vector2(0.0001, 0.0001), "clamped to lata's 7 m from where the kid stands")


func test_the_range_is_measured_from_where_the_kid_is_at_the_release() -> void:
	var sim: MatchSim = _sim()
	sim.players[1].weapons[0] = &"lata"
	sim.set_input(1, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, PlayerInput.BTN_CARD_1, sim.tick).with_aim_point(Vector2(0.0, -2.0)))
	sim.step(DT)
	sim.players[1].position = Vector2(0.0, 10.0)
	sim.set_input(1, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, 0, sim.tick).with_aim_point(Vector2(0.0, -2.0)))
	sim.step(DT)
	assert_almost_eq(_zone(sim).center, Vector2(0.0, 3.0), Vector2(0.0001, 0.0001))


func test_a_directional_card_goes_toward_the_point() -> void:
	var sim: MatchSim = _sim()
	_cast(sim, &"trumpo", Vector2(3.0, 9.0))
	assert_eq(sim.weapons.projectiles.size(), 1)
	assert_almost_eq(sim.weapons.projectiles[0].direction, Vector2(0.6, 0.8), Vector2(0.0001, 0.0001))


func test_a_point_on_the_kid_uses_its_facing() -> void:
	var sim: MatchSim = _sim()
	sim.players[1].facing = Vector2.RIGHT
	_cast(sim, &"trumpo", Vector2(0.05, 5.0))
	assert_almost_eq(sim.weapons.projectiles[0].direction, Vector2.RIGHT, Vector2(0.0001, 0.0001))


func test_a_tap_auto_aims_at_the_nearest_enemy_in_range() -> void:
	var sim: MatchSim = _sim()
	sim.players[2].position = Vector2(4.0, 2.0)
	_cast(sim, &"lata")
	assert_almost_eq(_zone(sim).center, Vector2(4.0, 2.0), Vector2(0.0001, 0.0001))


func test_a_tap_with_nobody_in_range_casts_the_way_the_kid_faces_at_full_range() -> void:
	var sim: MatchSim = _sim()
	sim.players[1].facing = Vector2.LEFT
	_cast(sim, &"lata")
	assert_almost_eq(_zone(sim).center, Vector2(-7.0, 5.0), Vector2(0.0001, 0.0001))


func test_the_homing_rock_locks_the_enemy_nearest_the_point() -> void:
	var sim: MatchSim = _sim()
	sim.add_player(4, 1)
	sim.deal_cards(4)
	sim.players[2].position = Vector2(-3.0, 3.0)
	sim.players[4].position = Vector2(3.5, 3.0)
	_cast(sim, &"bato_light", Vector2(3.0, 2.5))
	assert_eq(sim.weapons.projectiles.size(), 1)
	assert_eq(sim.weapons.projectiles[0].target_id, 4, "not 2, the enemy nearest the kid")


func test_the_homing_rock_at_a_point_with_nobody_in_range_casts_nothing_and_spends_nothing() -> void:
	var sim: MatchSim = _sim()
	sim.players[2].position = Vector2(0.0, -10.0)
	watch_signals(sim)
	_cast(sim, &"bato_light", Vector2(0.0, -10.0))
	assert_signal_not_emitted(sim, "weapon_cast")
	assert_almost_eq(sim.players[1].elixir, _rules.elixir_max, 0.0001)


func test_a_heal_tap_heals_yourself() -> void:
	var sim: MatchSim = _sim()
	sim.players[1].hp = 50
	sim.players[3].hp = 50
	sim.players[3].position = Vector2(-2.0, 5.0)
	_cast(sim, &"heal")
	assert_eq(sim.players[1].hp, 80)
	assert_eq(sim.players[3].hp, 50)


func test_a_heal_dropped_near_your_kid_heals_yourself() -> void:
	var sim: MatchSim = _sim()
	sim.players[1].hp = 50
	sim.players[3].hp = 50
	sim.players[3].position = Vector2(-3.0, 5.0)
	_cast(sim, &"heal", Vector2(-0.9, 5.0))
	assert_eq(sim.players[1].hp, 80, "within 1.0 m of your kid")
	assert_eq(sim.players[3].hp, 50)


func test_a_heal_dragged_at_a_teammate_heals_them() -> void:
	var sim: MatchSim = _sim()
	sim.players[1].hp = 50
	sim.players[3].hp = 50
	sim.players[3].position = Vector2(-4.0, 5.0)
	_cast(sim, &"heal", Vector2(-4.0, 5.5))
	assert_eq(sim.players[3].hp, 80)
	assert_eq(sim.players[1].hp, 50)


func test_a_heal_dragged_at_nobody_casts_nothing_and_spends_nothing() -> void:
	var sim: MatchSim = _sim()
	sim.players[1].hp = 50
	watch_signals(sim)
	_cast(sim, &"heal", Vector2(5.0, 5.0))
	assert_signal_not_emitted(sim, "weapon_cast")
	assert_almost_eq(sim.players[1].elixir, _rules.elixir_max, 0.0001)
	assert_eq(sim.players[1].weapons[0], &"heal")


func test_a_non_finite_point_is_never_used() -> void:
	var sim: MatchSim = _sim()
	sim.players[1].facing = Vector2.LEFT
	_cast(sim, &"lata", Vector2(INF, NAN))
	assert_almost_eq(_zone(sim).center, Vector2(-7.0, 5.0), Vector2(0.0001, 0.0001), "taken as a tap")
