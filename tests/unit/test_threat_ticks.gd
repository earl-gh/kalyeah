extends GutTest
## The ticks on the screen edge for enemies that could hit you but are off screen.

const PRACTICE_SCENE: PackedScene = preload("res://scenes/match/practice.tscn")
const DT: float = 1.0 / 30.0


func _practice() -> PracticeMatch:
	var practice: PracticeMatch = autofree(PRACTICE_SCENE.instantiate()) as PracticeMatch
	add_child(practice)
	practice.finish_pick()
	practice.skip_intro()
	return practice


func _clear_street(practice: PracticeMatch) -> PlayerState:
	var me: PlayerState = practice.sim.players[PracticeMatch.LOCAL_ID]
	me.position = Vector2(0.0, 5.0)
	for id: int in [PracticeMatch.ENEMY_STAND_ID, PracticeMatch.ENEMY_PATROL_ID, PracticeMatch.ENEMY_BOX_ID, PracticeMatch.ALLY_ID]:
		practice.sim.players[id].position = Vector2(11.0, -22.0)
	practice._snap_camera = true
	practice.advance(DT)
	return me


func test_an_enemy_on_screen_gets_no_tick_and_one_just_off_the_side_does() -> void:
	var practice: PracticeMatch = _practice()
	var me: PlayerState = _clear_street(practice)
	var foe: PlayerState = practice.sim.players[PracticeMatch.ENEMY_STAND_ID]
	foe.position = me.position + Vector2(0.0, -4.0)
	practice.advance(DT)
	assert_eq(practice.hud.threats.ticks.size(), 0, "ahead and on screen: you can see it")
	foe.position = me.position + Vector2(10.0, 0.0)
	practice.advance(DT)
	assert_eq(practice.hud.threats.ticks.size(), 1, "beside you and past the edge: a tick")
	var tick: Dictionary = practice.hud.threats.ticks[0]
	assert_eq(tick["id"], PracticeMatch.ENEMY_STAND_ID)
	assert_gt((tick["point"] as Vector2).x, practice.hud.size.x * 0.8, "on the right edge, toward the enemy")


func test_an_enemy_behind_and_to_the_side_gets_a_tick_in_the_lower_half() -> void:
	var practice: PracticeMatch = _practice()
	var me: PlayerState = _clear_street(practice)
	practice.sim.set_loadout(PracticeMatch.LOCAL_ID, &"bola", &"papel_shield")
	practice.sim.players[PracticeMatch.ENEMY_STAND_ID].position = me.position + Vector2(9.0, 6.0)
	practice.advance(DT)
	var ticks: Array[Dictionary] = practice.hud.threats.ticks
	assert_eq(ticks.size(), 1)
	assert_gt((ticks[0]["point"] as Vector2).y, practice.hud.size.y * 0.5, "toward the bottom of the screen")
	assert_gt((ticks[0]["point"] as Vector2).x, practice.hud.size.x * 0.5, "and the right")


func test_only_enemies_within_weapon_reach_count_and_the_nearest_four_show() -> void:
	var practice: PracticeMatch = _practice()
	var me: PlayerState = _clear_street(practice)
	practice.sim.set_loadout(PracticeMatch.LOCAL_ID, &"bato_light", &"papel_shield")
	var reach: float = practice.hud.threats.reach_for(me)
	assert_almost_eq(reach, ThreatTicks.MIN_REACH + ThreatTicks.REACH_PAD, 0.0001, "short weapons: the minimum reach")
	practice.sim.set_loadout(PracticeMatch.LOCAL_ID, &"bola", &"papel_shield")
	assert_almost_eq(practice.hud.threats.reach_for(me), 9.0 + ThreatTicks.REACH_PAD, 0.0001, "the bola reaches 9 m")
	practice.sim.players[PracticeMatch.ENEMY_STAND_ID].position = me.position + Vector2(40.0, 0.0)
	practice.advance(DT)
	assert_eq(practice.hud.threats.ticks.size(), 0, "too far to matter")


func test_no_ticks_while_tagged_or_for_enemies_hiding_in_their_box() -> void:
	var practice: PracticeMatch = _practice()
	var me: PlayerState = _clear_street(practice)
	practice.sim.players[PracticeMatch.ENEMY_STAND_ID].position = me.position + Vector2(10.0, 0.0)
	practice.advance(DT)
	assert_eq(practice.hud.threats.ticks.size(), 1)
	practice.sim.kill(PracticeMatch.LOCAL_ID)
	practice.advance(DT)
	assert_eq(practice.hud.threats.ticks.size(), 0, "tagged: nothing to dodge")
