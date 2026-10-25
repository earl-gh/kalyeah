extends GutTest
## Tag (docs/GDD.md "Tag"): at 0 HP a player is tagged and sent to the sidewalk beside their team's rescue area, facing in;
## a free teammate who stays in the rescue area for five seconds rescues them back to their post.

const RULES: GameRules = preload("res://data/rules/game_rules.tres")
const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
const DT: float = 1.0 / 30.0


func _sim() -> MatchSim:
	return MatchSim.new(RULES, LAYOUT, 11)


func _wait(sim: MatchSim, seconds: float) -> void:
	for i: int in ceili(seconds / DT):
		sim.step(DT)


func _area(sim: MatchSim, team: int) -> Rect2:
	return LAYOUT.rescue_rect(sim.side_for_team(team))


func test_zero_hp_tags_and_sends_you_to_the_sidewalk_beside_your_teams_rescue_area() -> void:
	var sim: MatchSim = _sim()
	var me: PlayerState = sim.add_player(1, 0)
	me.position = Vector2(3.0, -10.0)
	watch_signals(sim)
	sim.damage(1, 500)
	assert_true(me.tagged)
	assert_eq(me.hp, 0)
	assert_false(LAYOUT.lane_rect().has_point(me.position), "outside the play area")
	assert_lt(me.position.x, 0.0, "your team's tagged kids are on the left sidewalk")
	assert_lt(me.position.y, 0.0, "at the top")
	assert_eq(me.facing, Vector2.RIGHT, "facing in, toward the street")
	var area: Rect2 = _area(sim, 0)
	assert_between(me.position.y, area.position.y, area.end.y, "level with the rescue area")
	assert_signal_emitted_with_parameters(sim, "player_tagged", [1])


func test_tagged_opponents_go_to_the_right_sidewalk_at_the_bottom_facing_in() -> void:
	var sim: MatchSim = _sim()
	var them: PlayerState = sim.add_player(2, 1)
	sim.damage(2, 500)
	assert_false(LAYOUT.lane_rect().has_point(them.position), "outside the play area")
	assert_gt(them.position.x, 0.0)
	assert_gt(them.position.y, 0.0)
	assert_eq(them.facing, Vector2.LEFT)
	var area: Rect2 = _area(sim, 1)
	assert_between(them.position.y, area.position.y, area.end.y, "level with the rescue area")


func test_tagged_kids_stand_in_a_row_along_the_sidewalk_each_beside_the_rescue_area() -> void:
	var sim: MatchSim = _sim()
	var a: PlayerState = sim.add_player(1, 0)
	var b: PlayerState = sim.add_player(2, 0)
	var c: PlayerState = sim.add_player(3, 0)
	sim.kill(1)
	sim.kill(2)
	sim.kill(3)
	assert_eq([a.position.x, b.position.x], [c.position.x, c.position.x], "one row against the street's edge")
	assert_lt(a.position.y, b.position.y)
	assert_lt(b.position.y, c.position.y)
	var area: Rect2 = _area(sim, 0)
	for kid: PlayerState in [a, b, c]:
		assert_between(kid.position.y, area.position.y, area.end.y)


func test_tagged_players_cannot_move_act_or_be_hit() -> void:
	var sim: MatchSim = _sim()
	var me: PlayerState = sim.add_player(1, 0)
	sim.set_loadout(1, &"bato_light", &"lata")
	sim.kill(1)
	var at: Vector2 = me.position
	for i: int in 10:
		sim.set_input(1, PlayerInput.create(Vector2(1.0, 0.0), Vector2.ZERO, PlayerInput.BTN_WEAPON_1, sim.tick))
		sim.step(DT)
	assert_eq(me.position, at, "waiting on the sidewalk")
	assert_false(me.can_act())
	assert_false(sim.weapon_ready(me, 0))
	assert_false(sim.is_targetable(me))
	assert_eq(sim.heal(1, 50), 0, "no healing while tagged")


## Stands `friend` in the middle of its team's rescue area.
func _in_area(sim: MatchSim, friend: PlayerState) -> void:
	friend.position = _area(sim, friend.team).get_center()


func test_you_get_tagged_every_time_not_just_once() -> void:
	var sim: MatchSim = _sim()
	var me: PlayerState = sim.add_player(1, 0)
	var friend: PlayerState = sim.add_player(2, 0)
	for round: int in 3:
		sim.damage(1, 500)
		assert_true(me.tagged, "tag %d" % round)
		_in_area(sim, friend)
		_wait(sim, RULES.rescue_hold_time + 0.2)
		assert_false(me.tagged, "rescue %d" % round)
		friend.position = Vector2.ZERO
		sim.step(DT)


func test_a_teammate_who_stays_five_seconds_in_the_rescue_area_rescues_you_back_to_your_post() -> void:
	var sim: MatchSim = _sim()
	var me: PlayerState = sim.add_player(1, 0)
	var friend: PlayerState = sim.add_player(2, 0)
	sim.set_loadout(1, &"bato_light", &"lata")
	me.weapon_cooldowns = [2.0, 0.0] as Array[float]
	sim.kill(1)
	watch_signals(sim)
	_in_area(sim, friend)
	assert_eq(RULES.rescue_hold_time, 5.0, "the owner's number")
	_wait(sim, 4.5)
	assert_true(me.tagged, "not yet at 4.5 s")
	assert_gt(sim.rescue_elapsed[0], 4.0, "the count shows how far it is")
	_wait(sim, 0.7)
	assert_false(me.tagged)
	assert_eq(me.hp, RULES.player_max_hp)
	assert_eq(me.position, me.spawn_position, "back at the post")
	assert_true(LAYOUT.in_base(sim.side_for_team(0), me.position))
	assert_signal_emitted_with_parameters(sim, "player_rescued", [1, 2])


func test_a_touch_is_not_enough_and_leaving_starts_the_count_over() -> void:
	var sim: MatchSim = _sim()
	var me: PlayerState = sim.add_player(1, 0)
	var friend: PlayerState = sim.add_player(2, 0)
	sim.kill(1)
	_in_area(sim, friend)
	_wait(sim, 4.0)
	friend.position = Vector2(0.0, 0.0)
	_wait(sim, 0.2)
	assert_eq(sim.rescue_rescuer[0], -1, "nobody rescues any more")
	assert_eq(sim.rescue_elapsed[0], 0.0, "the count started over")
	_in_area(sim, friend)
	_wait(sim, 4.0)
	assert_true(me.tagged, "4 s since coming back is not 5")
	_wait(sim, 1.2)
	assert_false(me.tagged)


func test_only_the_rescue_area_beside_the_sidewalk_counts_not_the_street_round_it() -> void:
	var sim: MatchSim = _sim()
	var me: PlayerState = sim.add_player(1, 0)
	var friend: PlayerState = sim.add_player(2, 0)
	sim.kill(1)
	var area: Rect2 = _area(sim, 0)
	assert_almost_eq(area.position.x, LAYOUT.lane_rect().position.x, 0.001, "against the street's side edge")
	# just outside the area, further in: nothing happens however long
	friend.position = Vector2(area.end.x + friend.radius + 0.3, area.get_center().y)
	_wait(sim, 6.0)
	assert_true(me.tagged, "outside the rescue area")
	# at the street's edge, level with the kids: rescued
	friend.position = Vector2(area.position.x + friend.radius, area.get_center().y)
	_wait(sim, 5.2)
	assert_false(me.tagged, "at the edge beside the tagged kids")


func test_only_one_teammate_rescues_at_a_time_and_a_second_adds_no_speed() -> void:
	var sim: MatchSim = _sim()
	var me: PlayerState = sim.add_player(1, 0)
	var first: PlayerState = sim.add_player(2, 0)
	var second: PlayerState = sim.add_player(3, 0)
	sim.kill(1)
	_in_area(sim, first)
	_wait(sim, 2.0)
	_in_area(sim, second)
	_wait(sim, 2.0)
	assert_eq(sim.rescue_rescuer[0], 2, "the first to arrive holds the rescue")
	assert_true(me.tagged, "4 s in: the second added nothing")
	_wait(sim, 1.2)
	assert_false(me.tagged, "5 s after the first arrived")


func test_when_the_rescuer_goes_the_next_one_in_the_area_starts_from_zero() -> void:
	var sim: MatchSim = _sim()
	var me: PlayerState = sim.add_player(1, 0)
	var first: PlayerState = sim.add_player(2, 0)
	var second: PlayerState = sim.add_player(3, 0)
	sim.kill(1)
	_in_area(sim, first)
	_in_area(sim, second)
	_wait(sim, 4.0)
	first.position = Vector2.ZERO
	_wait(sim, 0.2)
	assert_eq(sim.rescue_rescuer[0], 3, "the other one takes over")
	assert_lt(sim.rescue_elapsed[0], 0.5, "from the start")
	_wait(sim, 4.0)
	assert_true(me.tagged)
	_wait(sim, 1.2)
	assert_false(me.tagged)


func test_a_rescuer_who_is_tagged_loses_the_rescue() -> void:
	var sim: MatchSim = _sim()
	var me: PlayerState = sim.add_player(1, 0)
	var friend: PlayerState = sim.add_player(2, 0)
	sim.kill(1)
	_in_area(sim, friend)
	_wait(sim, 3.0)
	sim.kill(2)
	_wait(sim, 0.2)
	assert_eq(sim.rescue_rescuer[0], -1)
	assert_true(me.tagged and friend.tagged)


func test_the_first_tagged_is_the_first_rescued() -> void:
	var sim: MatchSim = _sim()
	var a: PlayerState = sim.add_player(1, 0)
	var b: PlayerState = sim.add_player(2, 0)
	var c: PlayerState = sim.add_player(3, 0)
	var friend: PlayerState = sim.add_player(4, 0)
	sim.kill(3)
	sim.kill(1)
	sim.kill(2)
	_in_area(sim, friend)
	_wait(sim, 5.2)
	assert_false(c.tagged, "tagged first, rescued first")
	assert_true(a.tagged and b.tagged, "the others still wait")
	assert_eq(sim.rescue_rescuer[0], 4, "and the rescuer keeps going for the next one")
	_wait(sim, 5.2)
	assert_false(a.tagged, "tagged second, rescued second")
	assert_true(b.tagged)


func test_an_enemy_in_the_rescue_area_rescues_nobody() -> void:
	var sim: MatchSim = _sim()
	var me: PlayerState = sim.add_player(1, 0)
	var them: PlayerState = sim.add_player(2, 1)
	sim.kill(1)
	them.position = _area(sim, 0).get_center()
	_wait(sim, 6.0)
	assert_true(me.tagged)


func test_with_the_toggle_on_one_rescue_frees_everyone_waiting() -> void:
	var rules: GameRules = RULES.duplicate() as GameRules
	rules.rescue_frees_all = true
	var sim: MatchSim = MatchSim.new(rules, LAYOUT, 3)
	var a: PlayerState = sim.add_player(1, 0)
	var b: PlayerState = sim.add_player(2, 0)
	var friend: PlayerState = sim.add_player(3, 0)
	sim.kill(1)
	sim.kill(2)
	_in_area(sim, friend)
	_wait(sim, 5.2)
	assert_false(a.tagged)
	assert_false(b.tagged)


func test_a_hold_time_of_zero_is_the_old_touch_rescue() -> void:
	var rules: GameRules = RULES.duplicate() as GameRules
	rules.rescue_hold_time = 0.0
	var sim: MatchSim = MatchSim.new(rules, LAYOUT, 3)
	var me: PlayerState = sim.add_player(1, 0)
	var friend: PlayerState = sim.add_player(2, 0)
	sim.kill(1)
	_in_area(sim, friend)
	sim.step(DT)
	assert_false(me.tagged)


func test_tagged_training_dummies_walk_out_on_their_own() -> void:
	var sim: MatchSim = _sim()
	var dummy: PlayerState = sim.add_dummy(5, 1, Vector2(2.0, 3.0), DummyBrain.standing())
	sim.kill(5)
	assert_true(dummy.tagged)
	_wait(sim, RULES.dummy_release_time - 0.2)
	assert_true(dummy.tagged)
	watch_signals(sim)
	_wait(sim, 0.4)
	assert_false(dummy.tagged)
	assert_eq(dummy.position, Vector2(2.0, 3.0))
	assert_signal_emitted_with_parameters(sim, "player_rescued", [5, MatchSim.RELEASED_ALONE])


func test_players_never_walk_out_on_their_own() -> void:
	var sim: MatchSim = _sim()
	var me: PlayerState = sim.add_player(1, 0)
	sim.add_player(2, 1)
	sim.kill(1)
	_wait(sim, RULES.dummy_release_time + 1.0)
	assert_true(me.tagged)


func test_weapons_can_be_swapped_only_while_tagged() -> void:
	var sim: MatchSim = _sim()
	sim.add_player(1, 0)
	sim.set_loadout(1, &"bato_light", &"lata")
	assert_false(sim.swap_loadout(1, &"jacks", &"bola"), "not while free")
	sim.kill(1)
	assert_true(sim.swap_loadout(1, &"jacks", &"bola"))
	assert_eq(sim.players[1].weapons, [&"jacks", &"bola"] as Array[StringName])


func test_a_free_kid_walking_to_the_edge_stops_in_the_street_and_never_touches_a_tagged_kid() -> void:
	var sim: MatchSim = _sim()
	var kid: PlayerState = sim.add_player(1, 0)
	var enemy: PlayerState = sim.add_player(2, 1)
	sim.kill(1)
	var spot: Vector2 = kid.position
	var edge: Rect2 = _area(sim, 0)
	enemy.position = edge.get_center()
	for i: int in 60:
		sim.set_input(2, PlayerInput.create(Vector2.LEFT, Vector2.ZERO, 0, sim.tick))
		sim.step(DT)
	assert_true(LAYOUT.lane_rect().has_point(enemy.position), "the street's edge holds a free kid in")
	assert_eq(kid.position, spot, "the tagged kid is not pushed")
	assert_eq(kid.hp, 0)


func test_a_clients_mirror_shows_a_tagged_kid_on_the_sidewalk_facing_in() -> void:
	var sim: MatchSim = _sim()
	sim.add_player(1, 0)
	sim.add_player(2, 1)
	sim.kill(2)
	var mirror: MatchSim = _sim()
	mirror.add_player(1, 0)
	mirror.add_player(2, 1)
	Snapshot.apply(mirror, bytes_to_var(NetProtocol.encode(Snapshot.capture(sim, {}))) as Dictionary, 1)
	assert_eq(mirror.players[2].position, sim.players[2].position, "the same place on the wire, nothing clamps it back into the street")
	assert_false(LAYOUT.lane_rect().has_point(mirror.players[2].position))
	assert_eq(mirror.players[2].facing, Vector2.LEFT)
