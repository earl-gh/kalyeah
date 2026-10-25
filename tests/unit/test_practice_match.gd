extends GutTest

var _saved_loadout: Array[StringName] = []

const PRACTICE_SCENE: PackedScene = preload("res://scenes/match/practice.tscn")
const CAMERA: CameraRules = preload("res://data/rules/camera_rules.tres")
const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
const DT: float = 1.0 / 30.0


func _practice() -> PracticeMatch:
	var practice: PracticeMatch = _picking()
	practice.finish_pick()
	practice.skip_intro()
	return practice


## A practice match still on its opening weapon pick.
func _picking() -> PracticeMatch:
	var practice: PracticeMatch = autofree(PRACTICE_SCENE.instantiate()) as PracticeMatch
	add_child(practice)
	return practice


func _pick_screen(practice: PracticeMatch) -> WeaponPickScreen:
	return practice.get_node("%PickScreen") as WeaponPickScreen


## A quick tap on the card in `slot` (press and release on the card at once): the sim auto-aims.
func _tap(practice: PracticeMatch, slot: int) -> void:
	practice.card_pressed(slot)
	practice.card_released(slot, Vector2.ZERO, true, true)


## The card in `slot` dragged onto street `point` (sim XZ) and let go there.
func _cast_at(practice: PracticeMatch, slot: int, point: Vector2) -> void:
	practice.input.card_press(slot, Time.get_ticks_msec())
	practice.input.card_drag(false, point)
	practice.advance(DT)
	practice.input.card_release(Time.get_ticks_msec())


## Puts one enemy dummy straight ahead of the local player and the other far away.
func _enemy_ahead(practice: PracticeMatch, distance: float) -> PlayerState:
	var player: PlayerState = practice.sim.players[PracticeMatch.LOCAL_ID]
	player.position = Vector2(0.0, 4.0)
	practice.sim.players[PracticeMatch.ENEMY_PATROL_ID].position = Vector2(7.0, -25.0)
	var enemy: PlayerState = practice.sim.players[PracticeMatch.ENEMY_STAND_ID]
	enemy.position = player.position + Vector2(0.0, -distance)
	return enemy


func test_hurt_button_path_and_no_health_panel_in_the_corner() -> void:
	var practice: PracticeMatch = _practice()
	var player: PlayerState = practice.sim.players[PracticeMatch.LOCAL_ID]
	assert_eq(player.hp, 100)
	practice.hurt_local(30)
	assert_eq(player.hp, 70)
	assert_null(practice.get_node_or_null("%HpLabel"), "health lives over the character, not top-left")
	assert_null(practice.get_node_or_null("%PlayerCard"))


func test_zero_hp_tags_you_into_the_box_with_the_tagged_card() -> void:
	var practice: PracticeMatch = _practice()
	var player: PlayerState = practice.sim.players[PracticeMatch.LOCAL_ID]
	practice.hurt_local(100)
	assert_true(player.tagged)
	assert_true(LAYOUT.jail_rect(practice.own_side()).has_point(player.position), "sent to our box")
	assert_true(practice.hud.tagged_panel.visible)
	practice.advance(DT)
	assert_false(practice.hud.hand.visible, "the hand is hidden while you wait")
	assert_true((practice.get_node("%Actors").get_child(0) as Node3D).visible, "you still stand in the box")
	assert_false(practice.is_picking(), "the field stays in view: the swap screen only opens on request")


func test_skill_presses_do_nothing_while_tagged() -> void:
	var practice: PracticeMatch = _practice()
	var player: PlayerState = practice.sim.players[PracticeMatch.LOCAL_ID]
	practice.hurt_local(100)
	var at: Vector2 = player.position
	practice.input.move_press(at + Vector2(3.0, 0.0))
	_tap(practice, 0)
	for i: int in 5:
		practice.advance(DT)
	assert_eq(player.position, at)
	assert_eq(player.weapon_cooldowns[0], 0.0, "nothing cast")
	assert_false(practice.hud.street.active, "street taps are off in the box")
	assert_true(player.tagged)


func test_the_ally_dummy_runs_over_and_rescues_you() -> void:
	var practice: PracticeMatch = _practice()
	var player: PlayerState = practice.sim.players[PracticeMatch.LOCAL_ID]
	var ally: PlayerState = practice.sim.players[PracticeMatch.ALLY_ID]
	var home: Vector2 = ally.position
	practice.hurt_local(100)
	for i: int in 30 * 15:
		practice.advance(DT)
		if not player.tagged:
			break
	assert_false(player.tagged, "rescued")
	assert_eq(player.hp, 100)
	assert_true(LAYOUT.in_base(practice.own_side(), player.position), "back at our post")
	assert_false(practice.hud.tagged_panel.visible)
	for i: int in 30 * 15:
		practice.advance(DT)
	assert_lt(ally.position.distance_to(home), 0.5, "and walks back to its spot")


func test_practice_has_three_enemy_dummies_and_one_ally() -> void:
	var practice: PracticeMatch = _practice()
	assert_eq(practice.sim.players.size(), 5)
	var stand: PlayerState = practice.sim.players[PracticeMatch.ENEMY_STAND_ID]
	var patrol: PlayerState = practice.sim.players[PracticeMatch.ENEMY_PATROL_ID]
	var ally: PlayerState = practice.sim.players[PracticeMatch.ALLY_ID]
	assert_eq(stand.team, 1)
	assert_eq(patrol.team, 1)
	assert_eq(ally.team, 0)
	var stand_start: Vector2 = stand.position
	var patrol_start: Vector2 = patrol.position
	for i: int in 45:
		practice.advance(DT)
	assert_eq(stand.position, stand_start, "one enemy stands still")
	assert_ne(patrol.position.x, patrol_start.x, "the other walks")
	assert_eq(ally.position, Vector2(-6.0, 6.0))


func test_a_free_enemy_stands_in_its_own_box_and_its_bar_is_hidden() -> void:
	var practice: PracticeMatch = _practice()
	var boxed: PlayerState = practice.sim.players[PracticeMatch.ENEMY_BOX_ID]
	assert_eq(boxed.team, 1)
	assert_false(boxed.tagged, "free, not tagged")
	assert_true(LAYOUT.jail_rect(-practice.own_side()).has_point(boxed.position), "in the enemy's box (bottom right)")
	var start: Vector2 = boxed.position
	for i: int in 30:
		practice.advance(DT)
	assert_eq(boxed.position, start, "it stands still")
	assert_true(practice.overhead().bar_visible(boxed), "its bar shows like any free kid's")
	assert_true(practice.overhead().bar_visible(practice.sim.players[PracticeMatch.ENEMY_STAND_ID]), "the other enemies keep theirs")


# ---- tap to move -----------------------------------------------------------------

func test_a_street_tap_walks_the_kid_to_the_point_under_the_finger() -> void:
	var practice: PracticeMatch = _practice()
	practice.advance(DT)
	var player: PlayerState = practice.sim.players[PracticeMatch.LOCAL_ID]
	var goal: Vector2 = player.position + Vector2(1.0, -4.0)
	var screen: Vector2 = practice._camera.screen_position(Vector3(goal.x, 0.0, goal.y))
	practice.move_pressed(screen)
	practice.move_released()
	assert_almost_eq((practice.street_point(screen) as Vector2).distance_to(goal), 0.0, 0.05, "the ray from the camera lands where it was drawn")
	assert_eq(practice.hud.marker.target(), practice.input.move_point, "the ring shows at once where you tapped")
	for i: int in 45:
		practice.advance(DT)
	assert_almost_eq(player.position.distance_to(goal), 0.0, 0.15, "walked there and stopped")
	assert_false(player.has_move_target)
	assert_null(practice.hud.marker.target(), "arrived: the ring goes")


func test_match_waits_on_the_opening_weapon_pick() -> void:
	var practice: PracticeMatch = _picking()
	assert_true(practice.is_picking())
	assert_true(_pick_screen(practice).visible)
	practice.input.move_press(Vector2(0.0, -5.0))
	for i: int in 30:
		practice.advance(DT)
	assert_eq(practice.sim.tick, 0, "sim paused while picking")


func test_touch_controls_are_off_under_the_pick_screen() -> void:
	var practice: PracticeMatch = _picking()
	assert_false(practice.controls_active())
	practice.finish_pick()
	assert_true(practice.controls_active())
	practice.sim.kill(PracticeMatch.LOCAL_ID)
	practice.open_swap()
	assert_false(practice.controls_active(), "off again on the box swap screen")


func test_tapping_two_weapons_and_ready_sets_the_loadout() -> void:
	var practice: PracticeMatch = _picking()
	var screen: WeaponPickScreen = _pick_screen(practice)
	screen.tap(&"lata")
	screen.tap(&"trumpo")
	screen.press_ready()
	assert_false(practice.is_picking())
	assert_eq(practice.sim.players[PracticeMatch.LOCAL_ID].weapons, [&"lata", &"trumpo"] as Array[StringName])
	practice.advance(DT)
	assert_eq(practice.sim.tick, 1, "the match starts")
	assert_eq(practice.hud.hand.defs[0].id, &"lata", "the hand shows the pick")
	assert_eq(practice.hud.hand.defs[1].id, &"trumpo")


func test_pick_screen_has_a_card_for_every_weapon() -> void:
	var practice: PracticeMatch = _picking()
	var cards: Array[Node] = _pick_screen(practice).find_children("Weapon_*", "Button", true, false)
	assert_eq(cards.size(), 14, "14 weapons")


func test_practice_setup_waits_for_confirmation() -> void:
	var practice: PracticeMatch = _picking()
	_pick_screen(practice).tap(&"jacks")
	for i: int in 310:
		practice.advance(DT)
	assert_true(practice.is_picking())
	assert_eq(_pick_screen(practice).pick.picks, [&"jacks"] as Array[StringName])
	assert_eq(practice.sim.tick, 0)


func test_the_swap_screen_opens_from_the_box_with_your_weapons_and_a_rescue_closes_it() -> void:
	var practice: PracticeMatch = _practice()
	var weapons: Array[StringName] = practice.sim.players[PracticeMatch.LOCAL_ID].weapons.duplicate()
	practice.sim.kill(PracticeMatch.LOCAL_ID)
	practice.open_swap()
	assert_true(practice.is_picking())
	assert_eq(_pick_screen(practice).pick.picks, weapons)
	practice.advance(DT)
	assert_eq(practice.sim.tick, 1, "the match keeps running while you swap")
	for i: int in 30 * 15:
		practice.advance(DT)
	assert_false(practice.sim.players[PracticeMatch.LOCAL_ID].tagged, "the ally dummy came")
	assert_false(practice.is_picking())
	assert_eq(practice.sim.players[PracticeMatch.LOCAL_ID].weapons, weapons)


# ---- weapon buttons ------------------------------------------------------------

func _equip(practice: PracticeMatch, first: StringName, second: StringName) -> void:
	assert_true(practice.sim.set_loadout(PracticeMatch.LOCAL_ID, first, second))


func test_a_quick_tap_still_casts_with_auto_aim() -> void:
	var practice: PracticeMatch = _practice()
	_equip(practice, &"bato_light", &"papel_shield")
	var enemy: PlayerState = _enemy_ahead(practice, 5.0)
	_tap(practice, 0)
	practice.advance(DT)
	practice.advance(DT)
	var player: PlayerState = practice.sim.players[PracticeMatch.LOCAL_ID]
	assert_gt(player.weapon_cooldowns[0], 3.9)
	for i: int in 30:
		practice.advance(DT)
	assert_eq(enemy.hp, 88)
	assert_lt(practice.hud.hand.reached[0], 1.0, "the card shows it is not ready")


func test_a_card_dropped_on_a_street_point_casts_toward_it() -> void:
	var practice: PracticeMatch = _practice()
	_equip(practice, &"gunting_heavy", &"papel_shield")
	var enemy: PlayerState = _enemy_ahead(practice, 2.5)
	_cast_at(practice, 0, enemy.position)
	for i: int in 30:
		practice.advance(DT)
	assert_eq(enemy.hp, 76, "the cone goes toward the point")


func test_a_card_dropped_behind_the_kid_misses_the_enemy_ahead() -> void:
	var practice: PracticeMatch = _practice()
	_equip(practice, &"gunting_heavy", &"papel_shield")
	var enemy: PlayerState = _enemy_ahead(practice, 2.5)
	var player: PlayerState = practice.sim.players[PracticeMatch.LOCAL_ID]
	_cast_at(practice, 0, player.position * 2.0 - enemy.position)
	for i: int in 30:
		practice.advance(DT)
	assert_eq(enemy.hp, 100)


func test_a_card_dropped_back_on_the_hand_does_not_cast() -> void:
	var practice: PracticeMatch = _practice()
	_equip(practice, &"bato_light", &"papel_shield")
	var enemy: PlayerState = _enemy_ahead(practice, 5.0)
	practice.card_pressed(0)
	practice.input.card_drag(false, enemy.position)
	practice.advance(DT)
	practice.card_released(0, Vector2.ZERO, false, true)
	for i: int in 30:
		practice.advance(DT)
	assert_eq(practice.sim.players[PracticeMatch.LOCAL_ID].weapon_cooldowns[0], 0.0)
	assert_eq(enemy.hp, 100)


func test_second_slot_casts_its_own_weapon() -> void:
	var practice: PracticeMatch = _practice()
	_equip(practice, &"bato_light", &"papel_shield")
	_enemy_ahead(practice, 5.0)
	_tap(practice, 1)
	practice.advance(DT)
	practice.advance(DT)
	assert_eq(practice.sim.weapons.shields.size(), 1)
	assert_eq(practice.sim.players[PracticeMatch.LOCAL_ID].weapon_cooldowns[0], 0.0)


func test_cards_lock_under_stun_and_hide_while_tagged() -> void:
	var practice: PracticeMatch = _practice()
	practice.sim.apply_effect(PracticeMatch.LOCAL_ID, StatusEffects.Type.STUN, 1.0, 0.0)
	practice.advance(DT)
	assert_false(practice.hud.hand.pickable[0])
	assert_eq(practice.hud.hand.blocked_text, "STUNNED")
	for i: int in 40:
		practice.advance(DT)
	assert_eq(practice.hud.hand.blocked_text, "")
	practice.hurt_local(100)
	practice.advance(DT)
	assert_false(practice.hud.hand.visible)


func test_a_reconnect_is_not_reported_restored_until_the_server_confirms_the_slot() -> void:
	var practice: PracticeMatch = _practice()
	practice.finish_pick()
	practice.skip_intro()
	practice._on_connection_changed(false)
	practice._on_connection_changed(true)
	assert_eq(practice.hud.feedback.message.text, "Restoring match…")
	practice.hud.sync_score(practice.sim, practice.local_team, 10.0)
	assert_true(practice.hud.feedback.connection.visible)
	practice.hud.menu.closed.emit()
	assert_false(practice.controls_active())
	practice._on_welcomed(false)
	assert_eq(practice.hud.feedback.message.text, "Match unavailable")
	assert_true(practice.hud.feedback.leave.visible)
	assert_false(practice.controls_active())
	assert_true(practice._resume_rejected)
	practice._on_connection_changed(false)
	assert_true(practice._resume_rejected, "an expired match never restarts recovery")
	assert_eq(practice.hud.feedback.message.text, "Match unavailable")


# ---- visuals -------------------------------------------------------------------

func test_projectiles_zones_and_shields_get_drawn_and_cleaned_up() -> void:
	var practice: PracticeMatch = _practice()
	_equip(practice, &"tsinelas_heavy", &"papel_shield")
	_enemy_ahead(practice, 5.0)
	var fx: WeaponFxView = practice.get_node("%WeaponFx") as WeaponFxView
	_tap(practice, 0)
	practice.advance(DT)
	practice.advance(DT)
	_tap(practice, 1)
	practice.advance(DT)
	practice.advance(DT)
	assert_eq(fx.live_count(), 2, "one slipper and one paper wall")
	for i: int in 150:
		practice.advance(DT)
	assert_eq(fx.live_count(), 0)


func test_aim_indicator_shows_range_and_landing_circle() -> void:
	var sim: MatchSim = MatchSim.new(load("res://data/rules/game_rules.tres") as GameRules, LAYOUT, 1)
	var caster: PlayerState = sim.add_player(1, 0)
	caster.position = Vector2.ZERO
	var def: WeaponDef = sim.weapon_defs[&"bato_heavy"]
	var lines: Array[PackedVector2Array] = AimIndicator.outline(sim, caster, def, Vector2(0.0, -0.5), -1)
	assert_eq(lines.size(), 2, "range circle and landing circle, no line from the player to it")
	assert_almost_eq(lines[0][0].length(), def.max_range, 0.001)
	assert_almost_eq(lines[1][0].distance_to(Vector2(0.0, -4.0)), def.radius, 0.001, "half the stick = half the range")
	var cone: Array[PackedVector2Array] = AimIndicator.outline(sim, caster, sim.weapon_defs[&"gunting_light"], Vector2(1.0, 0.0))
	assert_eq(cone.size(), 2)


func test_no_team_has_a_character_twice_and_no_name_over_their_head() -> void:
	var practice: PracticeMatch = _practice()
	var seen: Dictionary[String, bool] = {}
	for id: int in practice.sim.players:
		var state: PlayerState = practice.sim.players[id]
		assert_ne(state.character_id, &"")
		var key: String = "%d:%s" % [state.team, state.character_id]
		assert_false(seen.has(key), "%s twice on team %d" % [state.character_id, state.team])
		seen[key] = true
	for id: int in practice.sim.players:
		assert_eq(practice.overhead().text_for(id), "", "no name text over a healthy hero")


func test_status_effects_show_over_the_hero_in_words() -> void:
	var practice: PracticeMatch = _practice()
	practice.sim.apply_effect(PracticeMatch.ENEMY_STAND_ID, StatusEffects.Type.POLYMORPH, 2.0, 0.0)
	practice.advance(DT)
	var enemy_view: Node3D = practice.get_node("%Actors").get_child(1) as Node3D
	assert_eq(practice.overhead().text_for(PracticeMatch.ENEMY_STAND_ID), "Polymorphed")
	assert_true((enemy_view as KidModel).is_can(), "turned into a can")


# ---- box swap (D13: no limit while tagged) ------------------------------------

func test_while_tagged_weapons_can_be_swapped_again_and_again() -> void:
	var practice: PracticeMatch = _practice()
	var screen: WeaponPickScreen = _pick_screen(practice)
	var player: PlayerState = practice.sim.players[PracticeMatch.LOCAL_ID]
	practice.sim.set_loadout(PracticeMatch.LOCAL_ID, &"bato_light", &"lata")
	practice.sim.kill(PracticeMatch.LOCAL_ID)
	practice.open_swap()
	screen.tap(&"bato_light")
	screen.tap(&"jacks")
	assert_eq(player.weapons, [&"lata", &"jacks"] as Array[StringName], "applied at once")
	for i: int in 60:
		practice.advance(DT)
	assert_true(practice.is_picking(), "no pick timer: stays open while tagged")
	screen.select_slot(0)
	screen.tap(&"trumpo")
	assert_eq(player.weapons, [&"trumpo", &"jacks"] as Array[StringName], "only the chosen slot changes")


func test_done_closes_and_the_swap_button_reopens_while_tagged() -> void:
	var practice: PracticeMatch = _practice()
	var screen: WeaponPickScreen = _pick_screen(practice)
	var swap: Button = practice.hud.swap_button
	practice.sim.kill(PracticeMatch.LOCAL_ID)
	practice.advance(DT)
	assert_true(swap.visible)
	practice.open_swap()
	practice.advance(DT)
	assert_false(swap.visible, "hidden under the open swap screen")
	screen.press_ready()
	practice.advance(DT)
	assert_false(practice.is_picking())
	assert_true(swap.visible)
	practice.open_swap()
	assert_true(practice.is_picking(), "as many times as you like")
	for i: int in 30 * 15:
		practice.advance(DT)
	assert_false(practice.sim.players[PracticeMatch.LOCAL_ID].tagged)
	assert_false(practice.is_picking(), "a rescue closes it")
	assert_false(swap.visible)
	practice.open_swap()
	assert_false(practice.is_picking(), "no swapping while free")


# ---- scoring -------------------------------------------------------------------

func _score_point(practice: PracticeMatch) -> void:
	var player: PlayerState = practice.sim.players[PracticeMatch.LOCAL_ID]
	player.position = LAYOUT.base_center(-practice.own_side())
	for i: int in ceili((practice.sim.rules.capture_solo_time + 0.3) / DT):
		practice.advance(DT)


func _finish_freeze(practice: PracticeMatch) -> void:
	for i: int in 100:
		practice.advance(DT)


func test_scoring_shows_a_banner_and_updates_the_scoreboard() -> void:
	var practice: PracticeMatch = _practice()
	assert_string_starts_with(practice.scoreboard().summary(), "YOU 0 - 0 THEM")
	_score_point(practice)
	assert_string_starts_with(practice.scoreboard().summary(), "YOU 1 - 0 THEM")
	assert_eq(practice.banner_text(), "SCORED!  1-0")
	_finish_freeze(practice)
	var player: PlayerState = practice.sim.players[PracticeMatch.LOCAL_ID]
	assert_eq(player.position, player.spawn_position, "back at base")


func test_match_over_shows_play_again() -> void:
	var practice: PracticeMatch = _practice()
	var again: Button = practice.hud.again_button
	for i: int in 3:
		_score_point(practice)
		_finish_freeze(practice)
	assert_eq(practice.sim.phase, MatchSim.Phase.MATCH_OVER, "first to 3: three points win")
	assert_true(again.visible)
	assert_eq(again.text, "Practice again")
	assert_eq(practice.banner_text(), "VICTORY!")


func test_damage_shows_no_numbers() -> void:
	var practice: PracticeMatch = _practice()
	practice.sim.damage(PracticeMatch.ENEMY_STAND_ID, 14)
	assert_eq(practice.overhead().popup_count(), 0, "no damage text on heroes")


func test_overhead_bars_use_ml_colours() -> void:
	var practice: PracticeMatch = _practice()
	var hud: OverheadHud = practice.overhead()
	assert_eq(hud.bar_color(practice.sim.players[PracticeMatch.LOCAL_ID]), OverheadHud.SELF_COLOR)
	assert_eq(hud.bar_color(practice.sim.players[PracticeMatch.ALLY_ID]), OverheadHud.ALLY_COLOR)
	assert_eq(hud.bar_color(practice.sim.players[PracticeMatch.ENEMY_STAND_ID]), OverheadHud.ENEMY_COLOR)


func test_every_free_kid_has_a_bar_wherever_it_stands_and_a_tagged_kid_has_none() -> void:
	var practice: PracticeMatch = _practice()
	var hud: OverheadHud = practice.overhead()
	var enemy: PlayerState = practice.sim.players[PracticeMatch.ENEMY_STAND_ID]
	assert_true(hud.bar_visible(enemy), "out in the street it has a bar")
	enemy.position = LAYOUT.jail_rect(-practice.own_side()).get_center()
	assert_true(hud.bar_visible(enemy), "in its team's box too: nothing hides there any more (owner)")
	var ally: PlayerState = practice.sim.players[PracticeMatch.ALLY_ID]
	ally.position = LAYOUT.jail_rect(practice.own_side()).get_center()
	assert_true(hud.bar_visible(ally), "a free kid on your team is shown, even standing in your own box")
	ally.tagged = true
	assert_false(hud.bar_visible(ally), "but a tagged kid has no health bar or number (owner)")


func test_a_status_is_shown_as_words_and_clears() -> void:
	var practice: PracticeMatch = _practice()
	var hud: OverheadHud = practice.overhead()
	assert_eq(hud.text_for(PracticeMatch.ENEMY_STAND_ID), "")
	practice.sim.apply_effect(PracticeMatch.ENEMY_STAND_ID, StatusEffects.Type.STUN, 2.0, 0.0)
	assert_eq(hud.text_for(PracticeMatch.ENEMY_STAND_ID), "Stunned")
	practice.sim.players[PracticeMatch.ENEMY_STAND_ID].effects.clear()
	assert_eq(hud.text_for(PracticeMatch.ENEMY_STAND_ID), "")
	practice.hurt_local(100)
	assert_true(practice.sim.players[PracticeMatch.LOCAL_ID].tagged)
	assert_eq(hud.text_for(PracticeMatch.LOCAL_ID), "", "a tagged kid stands in the box, which says it: no word over each kid (audit 4)")


func test_the_status_font_is_the_regular_font_not_slanted() -> void:
	var practice: PracticeMatch = _practice()
	assert_null(practice.overhead().get("_italic"), "no slanted font any more (owner)")
	assert_eq(OverheadHud.STATUS_FONT, OverheadHud.COUNT_FONT, "the status word is the health number's size (owner)")


# ---- the opening show (like the video) --------------------------------------------

func test_the_match_opens_on_the_street_with_the_objective_then_go() -> void:
	var practice: PracticeMatch = _picking()
	practice.finish_pick()
	var player: PlayerState = practice.sim.players[PracticeMatch.LOCAL_ID]
	var camera: ArenaCamera = practice.get_node("%ArenaCamera") as ArenaCamera
	assert_eq(practice.sim.phase, MatchSim.Phase.INTRO)
	practice.advance(DT)
	assert_true(practice.hud.intro_bar.visible)
	assert_eq(practice.hud.intro_title.text, "TAG THEIR BASE!")
	assert_true(practice.hud.hand.visible, "the first hand is on the tray during the show (not hidden)")
	assert_false(practice.hud.hand.active, "but locked")
	assert_true(camera.position.is_equal_approx(camera.desired_position(practice._flip())), "no flight past the two teams: the fixed street all along")
	var start: Vector2 = player.position
	practice.input.move_press(start + Vector2(0.0, -5.0))
	for i: int in ceili(practice.rules.intro_time * 0.6 / DT):
		practice.advance(DT)
	assert_true(camera.position.is_equal_approx(camera.desired_position(practice._flip())))
	assert_eq(player.position, start, "nobody moves during the show")
	assert_string_ends_with(practice.scoreboard().summary(), "0:00", "the clock waits")
	for i: int in ceili(practice.rules.intro_time * 0.5 / DT):
		practice.advance(DT)
	assert_eq(practice.sim.phase, MatchSim.Phase.PLAYING)
	assert_eq(practice.banner_text(), "GO!")
	assert_false(practice.hud.intro_bar.visible)
	assert_true(practice.hud.hand.active)


# ---- the tagged message ------------------------------------------------------------

func test_tagged_shows_a_message_on_the_deck_as_tall_as_the_console_and_no_player_cards() -> void:
	var practice: PracticeMatch = _practice()
	practice.hurt_local(100)
	practice.advance(DT)
	var panel: TaggedPanel = practice.hud.tagged_panel
	assert_true(panel.visible)
	assert_almost_eq(panel.size.y, practice.hud.console_height(), 0.5, "the deck keeps its size")
	assert_almost_eq(panel.get_global_rect().end.y, practice.hud.console.get_global_rect().end.y, 0.5, "and its place")
	assert_false(practice.hud.hand.visible, "the hand gives way to the message")
	assert_eq(panel.find_children("*", "Button", true, false).size(), 1, "no other player can be picked: only Change weapons")


func test_the_camera_does_not_move_when_you_are_tagged() -> void:
	var practice: PracticeMatch = _practice()
	var camera: ArenaCamera = practice.get_node("%ArenaCamera") as ArenaCamera
	practice.advance(DT)
	var at: Vector3 = camera.position
	practice.hurt_local(100)
	for i: int in 30:
		practice.advance(DT)
	assert_eq(camera.position, at, "the whole street is on screen: there is nobody to follow")
	practice.sim.objectives.rescue(practice.sim, practice.sim.players[practice.local_id], 99)
	practice.advance(DT)
	assert_true(practice.hud.hand.visible, "rescued: the hand comes back")


func test_the_whole_street_is_on_screen_at_once_with_your_base_at_the_bottom() -> void:
	var practice: PracticeMatch = _practice()
	practice.advance(DT)
	var camera: ArenaCamera = practice.get_node("%ArenaCamera") as ArenaCamera
	var size: Vector2 = camera.get_viewport().get_visible_rect().size
	var rect: Rect2 = Rect2(Vector2.ZERO, size)
	var corners: Array[Vector2] = [LAYOUT.lane_rect().position, LAYOUT.lane_rect().end, Vector2(LAYOUT.lane_rect().position.x, LAYOUT.lane_rect().end.y), Vector2(LAYOUT.lane_rect().end.x, LAYOUT.lane_rect().position.y)]
	for corner: Vector2 in corners:
		assert_true(rect.has_point(camera.screen_position(Vector3(corner.x, 0.0, corner.y))), "corner %s on screen" % corner)
	var own_base: Vector2 = camera.screen_position(Vector3(LAYOUT.base_center(practice.own_side()).x, 0.0, LAYOUT.base_center(practice.own_side()).y))
	var their_base: Vector2 = camera.screen_position(Vector3(LAYOUT.base_center(-practice.own_side()).x, 0.0, LAYOUT.base_center(-practice.own_side()).y))
	assert_gt(own_base.y, their_base.y, "your base is lower on the screen")
	assert_lt(own_base.x, their_base.x, "your post bottom left, theirs top right")
	var player: PlayerState = practice.sim.players[PracticeMatch.LOCAL_ID]
	var before: Vector3 = camera.position
	player.position = Vector2(8.0, -15.0)
	for i: int in 10:
		practice.advance(DT)
	assert_eq(camera.position, before, "the camera never follows anyone")

func before_each() -> void:
	_saved_loadout = Session.saved_loadout.duplicate()
	Session.saved_loadout.clear()


func after_each() -> void:
	Session.saved_loadout = _saved_loadout
	Session.save()


func test_reconnect_releases_controls_only_after_confirmed_resume() -> void:
	var practice: PracticeMatch = _practice()
	practice._on_connection_changed(false)
	assert_false(practice.controls_active())
	practice._on_connection_changed(true)
	assert_false(practice.controls_active())
	practice._on_welcomed(true)
	assert_true(practice.controls_active())
	assert_false(practice.hud.feedback.connection.visible)


func test_control_hint_explains_stun_and_the_held_card() -> void:
	var practice: PracticeMatch = _practice()
	practice.advance(DT)
	assert_eq(practice.hud.feedback.hint.text, "", "nothing to say while you just walk")
	practice.sim.apply_effect(PracticeMatch.LOCAL_ID, StatusEffects.Type.STUN, 1.0, 0.0)
	practice.advance(DT)
	assert_string_contains(practice.hud.feedback.hint.text, "Stunned")
	assert_string_contains(practice.hud.feedback.hint.text, "cards unavailable")
	var player: PlayerState = practice.sim.players[PracticeMatch.LOCAL_ID]
	player.effects.clear()
	# a cone card always casts (a targeted card with nobody in range says "Nobody in range": its own test)
	player.weapons[0] = &"gunting_light"
	player.elixir = practice.sim.rules.elixir_max
	practice.input.card_press(0, Time.get_ticks_msec())
	practice.advance(DT)
	assert_string_contains(practice.hud.feedback.hint.text, "auto-aim", "on its card: a tap")
	practice.input.card_drag(false, player.position + Vector2(0.0, -3.0))
	practice.advance(DT)
	assert_eq(practice.hud.feedback.hint.text, "Back on your hand to cancel")
	practice.input.card_drag(false, null)
	practice.advance(DT)
	assert_eq(practice.hud.feedback.hint.text, "Release here to cancel")
	practice.input.card_cancel()
