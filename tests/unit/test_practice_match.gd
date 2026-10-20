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


func _weapon_button(practice: PracticeMatch, slot: int) -> AimButton:
	return practice.hud.weapon_buttons[slot]


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
	assert_false(practice.hud.weapon_buttons[0].visible, "controls hidden while you wait")
	assert_true((practice.get_node("%Actors").get_child(0) as Node3D).visible, "you still stand in the box")
	assert_string_contains(practice.hud.feed_texts()[-1], "TAGGED!")
	assert_false(practice.is_picking(), "the field stays in view: the swap screen only opens on request")


func test_skill_presses_do_nothing_while_tagged() -> void:
	var practice: PracticeMatch = _practice()
	var player: PlayerState = practice.sim.players[PracticeMatch.LOCAL_ID]
	practice.hurt_local(100)
	var at: Vector2 = player.position
	practice.set_stick(Vector2(1.0, 0.0))
	practice.aim_started(0)
	practice.aim_released(Vector2.ZERO, false, 0)
	for i: int in 5:
		practice.advance(DT)
	assert_eq(player.position, at)
	assert_eq(player.weapon_cooldowns[0], 0.0, "nothing cast")
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
	assert_string_contains(practice.hud.feed_texts()[-1], "RESCUED!")
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
	assert_false(practice.overhead().bar_visible(boxed), "its bar is hidden from you")
	assert_true(practice.overhead().bar_visible(practice.sim.players[PracticeMatch.ENEMY_STAND_ID]), "the other enemies keep theirs")


# ---- the movement stick ---------------------------------------------------------

func test_pushing_the_stick_up_runs_up_the_screen() -> void:
	var practice: PracticeMatch = _practice()
	var player: PlayerState = practice.sim.players[PracticeMatch.LOCAL_ID]
	var start: Vector2 = player.position
	practice.hud.joystick.changed.emit(Vector2(0.0, -1.0))
	for i: int in 30:
		practice.advance(DT)
	var ran: Vector2 = LocalInput.to_screen(player.position - start, practice._flip(), CAMERA.yaw_offset_degrees)
	assert_lt(ran.y, -3.0, "toward the top of the screen")
	assert_almost_eq(ran.x, 0.0, 0.3, "straight up the screen (the world direction turns with the camera's yaw)")
	practice.hud.joystick.changed.emit(Vector2.ZERO)
	practice.advance(DT)
	var at: Vector2 = player.position
	for i: int in 10:
		practice.advance(DT)
	assert_eq(player.position, at, "let go: standing still")


func test_match_waits_on_the_opening_weapon_pick() -> void:
	var practice: PracticeMatch = _picking()
	assert_true(practice.is_picking())
	assert_true(_pick_screen(practice).visible)
	practice.set_stick(Vector2(0.0, -1.0))
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
	assert_eq(_weapon_button(practice, 0).label_text, "Lata")
	assert_eq(_weapon_button(practice, 0).sub_text, "CC")


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
	practice.aim_started(0)
	practice.aim_released(Vector2.ZERO, false, 0)
	practice.advance(DT)
	practice.advance(DT)
	var player: PlayerState = practice.sim.players[PracticeMatch.LOCAL_ID]
	assert_gt(player.weapon_cooldowns[0], 3.9)
	for i: int in 30:
		practice.advance(DT)
	assert_eq(enemy.hp, 86)
	assert_gt(_weapon_button(practice, 0).cooldown_fraction, 0.0)


func test_drag_aim_points_the_cast_up_the_screen() -> void:
	var practice: PracticeMatch = _practice()
	_equip(practice, &"gunting_heavy", &"papel_shield")
	var enemy: PlayerState = _enemy_ahead(practice, 2.5)
	practice.aim_started(0)
	practice.advance(DT)
	practice.aim_released(_toward_enemy(practice), false, 0)
	for i: int in 30:
		practice.advance(DT)
	assert_eq(enemy.hp, 76, "screen up is toward the enemy base")


func test_aiming_down_misses_the_enemy_ahead() -> void:
	var practice: PracticeMatch = _practice()
	_equip(practice, &"gunting_heavy", &"papel_shield")
	var enemy: PlayerState = _enemy_ahead(practice, 2.5)
	practice.aim_started(0)
	practice.advance(DT)
	practice.aim_released(Vector2(0.0, 1.0), false, 0)
	for i: int in 30:
		practice.advance(DT)
	assert_eq(enemy.hp, 100)


func test_release_over_cancel_does_not_cast() -> void:
	var practice: PracticeMatch = _practice()
	_equip(practice, &"bato_light", &"papel_shield")
	var enemy: PlayerState = _enemy_ahead(practice, 5.0)
	practice.aim_started(0)
	practice.advance(DT)
	practice.aim_released(Vector2.ZERO, true, 0)
	for i: int in 30:
		practice.advance(DT)
	assert_eq(practice.sim.players[PracticeMatch.LOCAL_ID].weapon_cooldowns[0], 0.0)
	assert_eq(enemy.hp, 100)


func test_second_slot_casts_its_own_weapon() -> void:
	var practice: PracticeMatch = _practice()
	_equip(practice, &"bato_light", &"papel_shield")
	_enemy_ahead(practice, 5.0)
	practice.aim_started(1)
	practice.aim_released(Vector2.ZERO, false, 1)
	practice.advance(DT)
	practice.advance(DT)
	assert_eq(practice.sim.weapons.shields.size(), 1)
	assert_eq(practice.sim.players[PracticeMatch.LOCAL_ID].weapon_cooldowns[0], 0.0)


func test_weapon_buttons_lock_under_stun_and_hide_while_tagged() -> void:
	var practice: PracticeMatch = _practice()
	practice.sim.apply_effect(PracticeMatch.LOCAL_ID, StatusEffects.Type.STUN, 1.0, 0.0)
	practice.advance(DT)
	assert_true(_weapon_button(practice, 0).locked)
	for i: int in 40:
		practice.advance(DT)
	assert_false(_weapon_button(practice, 0).locked)
	practice.hurt_local(100)
	practice.advance(DT)
	assert_false(_weapon_button(practice, 1).visible)


func test_aim_button_drag_math() -> void:
	assert_eq(AimButton.aim_from_drag(Vector2(5.0, 5.0)), Vector2.ZERO, "tiny drag = tap = auto-aim")
	assert_almost_eq(AimButton.aim_from_drag(Vector2(0.0, -85.0)).y, -0.5, 0.001)
	assert_almost_eq(AimButton.aim_from_drag(Vector2(0.0, -900.0)).length(), 1.0, 0.001)


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
	practice.aim_started(0)
	practice.aim_released(Vector2.ZERO, false, 0)
	practice.advance(DT)
	practice.advance(DT)
	practice.aim_started(1)
	practice.aim_released(Vector2.ZERO, false, 1)
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
	for i: int in ceili((practice.sim.rules.capture_time + 0.3) / DT):
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
	for i: int in 2:
		_score_point(practice)
		_finish_freeze(practice)
	assert_eq(practice.sim.phase, MatchSim.Phase.MATCH_OVER, "best of 3: two points win")
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


func test_an_enemy_in_its_box_has_no_bar_over_it() -> void:
	var practice: PracticeMatch = _practice()
	var hud: OverheadHud = practice.overhead()
	var enemy: PlayerState = practice.sim.players[PracticeMatch.ENEMY_STAND_ID]
	assert_true(hud.bar_visible(enemy), "out in the street it has a bar")
	enemy.position = LAYOUT.jail_rect(-practice.own_side()).get_center()
	assert_false(hud.bar_visible(enemy), "inside its team's box it blends in with the tagged kids")
	assert_true(hud.bar_visible(practice.sim.players[PracticeMatch.ALLY_ID]), "your own team is always shown")
	var ally: PlayerState = practice.sim.players[PracticeMatch.ALLY_ID]
	ally.position = LAYOUT.jail_rect(practice.own_side()).get_center()
	assert_true(hud.bar_visible(ally), "even in your own box")


func test_the_minimap_shows_each_players_boot_art_face_in_a_circle() -> void:
	var practice: PracticeMatch = _practice()
	for def: CharacterDef in practice.sim.rules.characters:
		var face: Texture2D = KidModel.minimap_face(def.id)
		assert_not_null(face, "%s has a face cropped from the boot art" % def.id)
		assert_eq(face.get_width(), face.get_height(), "square")
	for id: int in practice.sim.players:
		var face: Texture2D = KidModel.minimap_face(practice.sim.players[id].character_id)
		var uvs: PackedVector2Array = LaneMinimap.face_uvs(face)
		var points: PackedVector2Array = LaneMinimap.face_points(Vector2(50.0, 80.0), LaneMinimap.FACE)
		assert_eq(points.size(), uvs.size())
		assert_eq(points.size(), LaneMinimap.FACE_STEPS)
		for i: int in points.size():
			assert_almost_eq(points[i].distance_to(Vector2(50.0, 80.0)), LaneMinimap.FACE, 0.001, "a circle")
			assert_between(uvs[i].x, 0.0, 1.0, "inside the face image, across")
			assert_between(uvs[i].y, 0.0, 1.0, "and down")
	assert_null(KidModel.minimap_face(&"nobody"), "no face for an unknown character")


func test_minimap_puts_our_base_at_the_bottom() -> void:
	var practice: PracticeMatch = _practice()
	var minimap: LaneMinimap = practice.hud.minimap
	var own_base: Vector2 = minimap.to_map(LAYOUT.base_center(practice.own_side()))
	var their_base: Vector2 = minimap.to_map(LAYOUT.base_center(-practice.own_side()))
	assert_gt(own_base.y, their_base.y, "at the bottom")
	assert_lt(own_base.x, their_base.x, "our base bottom left, theirs top right")
	var own_box: Vector2 = minimap.to_map(LAYOUT.jail_rect(practice.own_side()).get_center())
	assert_lt(own_box.y, own_base.y, "our box is at the top")
	assert_lt(own_box.x, their_base.x, "on the left")


func test_skill_buttons_show_cooldown_seconds() -> void:
	var practice: PracticeMatch = _practice()
	_equip(practice, &"papel_shield", &"bato_light")
	practice.aim_started(0)
	practice.aim_released(Vector2.ZERO, false, 0)
	practice.advance(DT)
	practice.advance(DT)
	assert_eq(ceili(_weapon_button(practice, 0).cooldown_seconds), ceili(practice.sim.weapon_defs[&"papel_shield"].cooldown))


func test_a_status_is_shown_as_words_and_clears() -> void:
	var practice: PracticeMatch = _practice()
	var hud: OverheadHud = practice.overhead()
	assert_eq(hud.text_for(PracticeMatch.ENEMY_STAND_ID), "")
	practice.sim.apply_effect(PracticeMatch.ENEMY_STAND_ID, StatusEffects.Type.STUN, 2.0, 0.0)
	assert_eq(hud.text_for(PracticeMatch.ENEMY_STAND_ID), "Stunned")
	practice.sim.players[PracticeMatch.ENEMY_STAND_ID].effects.clear()
	assert_eq(hud.text_for(PracticeMatch.ENEMY_STAND_ID), "")
	practice.hurt_local(100)
	assert_eq(hud.text_for(PracticeMatch.LOCAL_ID), "Tagged")


func test_the_status_font_is_a_slanted_italic() -> void:
	var practice: PracticeMatch = _practice()
	var italic: FontVariation = practice.overhead()._italic
	assert_not_null(italic)
	assert_lt(italic.variation_transform.y.x, 0.0, "slanted")


func test_skill_buttons_are_art_with_only_the_type_tag() -> void:
	var practice: PracticeMatch = _practice()
	_equip(practice, &"papel_shield", &"bato_light")
	practice.advance(DT)
	var block: AimButton = _weapon_button(practice, 0)
	assert_eq(block.icon_id, &"papel_shield")
	assert_eq(block.sub_text, "BLK")
	assert_eq(_weapon_button(practice, 1).sub_text, "ATK")


## The screen direction (stick or drag) that heads straight for the enemy base.
func _toward_enemy(practice: PracticeMatch) -> Vector2:
	var flip: bool = practice.own_side() == MapLayout.SIDE_ENEMY
	return LocalInput.to_screen(Vector2(0.0, -float(practice.own_side())), flip, CAMERA.yaw_offset_degrees)


func test_a_spinner_and_countdown_show_while_holding_the_enemy_base() -> void:
	var practice: PracticeMatch = _practice()
	var player: PlayerState = practice.sim.players[PracticeMatch.LOCAL_ID]
	practice.advance(DT)
	assert_false(practice.overhead().spinner_visible(player), "not in the enemy base")
	assert_false(practice.hud.capture_label.visible)
	player.position = LAYOUT.base_center(-practice.own_side())
	practice.advance(DT)
	assert_true(practice.overhead().spinner_visible(player), "counting toward the point")
	assert_true(practice.hud.capture_label.visible)
	assert_string_contains(practice.hud.capture_label.text, "HOLD THE BASE!")
	assert_string_ends_with(practice.hud.capture_label.text, "5")


func test_the_score_card_shows_a_match_clock_counting_up() -> void:
	var practice: PracticeMatch = _practice()
	assert_string_ends_with(practice.scoreboard().summary(), "0:00")
	for i: int in 30 * 65 + 6:
		practice.advance(DT)
	assert_string_ends_with(practice.scoreboard().summary(), "1:05")
	assert_null(practice.hud.get("info_label"), "no counter pill under the score card")


# ---- the opening show (like the video) --------------------------------------------

func test_the_match_opens_with_both_teams_then_go() -> void:
	var practice: PracticeMatch = _picking()
	practice.finish_pick()
	var player: PlayerState = practice.sim.players[PracticeMatch.LOCAL_ID]
	var camera: FollowCamera = practice.get_node("%FollowCamera") as FollowCamera
	assert_eq(practice.sim.phase, MatchSim.Phase.INTRO)
	practice.advance(DT)
	assert_true(practice.hud.intro_bar.visible)
	assert_eq(practice.hud.intro_title.text, "TAG THEIR BASE!")
	assert_false(practice.hud.weapon_buttons[0].visible, "no buttons during the show")
	var enemy_post: Vector2 = LAYOUT.base_center(-practice.own_side())
	var seen: Vector2 = Vector2(camera.position.x, camera.position.z)
	assert_lt(seen.distance_to(enemy_post), seen.distance_to(LAYOUT.base_center(practice.own_side())), "first: their post and team")
	var start: Vector2 = player.position
	practice.set_stick(Vector2(0.0, -1.0))
	for i: int in ceili(practice.rules.intro_time * 0.6 / DT):
		practice.advance(DT)
	var own_post: Vector2 = LAYOUT.base_center(practice.own_side())
	seen = Vector2(camera.position.x, camera.position.z)
	assert_lt(seen.distance_to(own_post), seen.distance_to(enemy_post), "then: our post and team")
	assert_eq(player.position, start, "nobody moves during the show")
	assert_string_ends_with(practice.scoreboard().summary(), "0:00", "the clock waits")
	for i: int in ceili(practice.rules.intro_time * 0.5 / DT):
		practice.advance(DT)
	assert_eq(practice.sim.phase, MatchSim.Phase.PLAYING)
	assert_eq(practice.banner_text(), "GO!")
	assert_false(practice.hud.intro_bar.visible)
	assert_true(practice.hud.weapon_buttons[0].visible)


# ---- watching others while tagged -------------------------------------------------

func test_tagged_shows_a_grouped_sheet_with_2_by_3_cards_of_everyone_else() -> void:
	var practice: PracticeMatch = _practice()
	practice.hurt_local(100)
	practice.advance(DT)
	var panel: TaggedPanel = practice.hud.tagged_panel
	assert_true(panel.visible)
	assert_eq(panel.card_ids.size(), 4, "practice: the ally and the three enemy dummies")
	assert_eq(panel.card_ids[0], PracticeMatch.ALLY_ID, "teammates first")
	assert_almost_eq(TaggedPanel.CARD_SIZE.x / TaggedPanel.CARD_SIZE.y, 2.0 / 3.0, 0.001)
	assert_eq(TaggedPanel.MAX_CARDS, 5, "room for everyone else in a 3v3")


func test_tagged_camera_follows_the_watched_player_then_snaps_back_on_rescue() -> void:
	var practice: PracticeMatch = _practice()
	var camera: FollowCamera = practice.get_node("%FollowCamera") as FollowCamera
	practice.hurt_local(100)
	assert_eq(practice.watch_id, PracticeMatch.ALLY_ID, "first the free teammate coming for you")
	practice.hud.tagged_panel.press_card(1)
	assert_eq(practice.watch_id, practice.hud.tagged_panel.card_ids[1], "tap a card to watch that player")
	var enemy: PlayerState = practice.sim.players[practice.watch_id]
	for i: int in 30:
		practice.advance(DT)
	assert_true(camera.position.is_equal_approx(camera.desired_position(enemy.position, false)) or camera.position.distance_to(camera.desired_position(enemy.position, false)) < 0.5, "following them")
	practice.hud.tagged_panel.press_card(0)
	for i: int in 30 * 15:
		practice.advance(DT)
		if not practice.sim.players[PracticeMatch.LOCAL_ID].tagged:
			break
	var player: PlayerState = practice.sim.players[PracticeMatch.LOCAL_ID]
	assert_false(player.tagged)
	assert_eq(practice.watch_id, -1)
	assert_true(camera.position.is_equal_approx(camera.desired_position(player.position, false)), "snapped straight back to you at your post")


func test_the_hero_sits_near_the_lower_centre_of_a_portrait_screen() -> void:
	var practice: PracticeMatch = _practice()
	var player: PlayerState = practice.sim.players[PracticeMatch.LOCAL_ID]
	player.position = Vector2(0.0, 0.0)
	var camera: FollowCamera = practice.get_node("%FollowCamera") as FollowCamera
	camera.follow(player.position, practice.own_side() == MapLayout.SIDE_ENEMY, 0.0)
	var on_screen: Vector2 = camera.screen_position(Vector3(player.position.x, 0.0, player.position.y))
	var size: Vector2 = camera.get_viewport().get_visible_rect().size
	assert_almost_eq(on_screen.x / size.x, 0.5, 0.01, "centred left to right")
	assert_almost_eq(on_screen.y / size.y, CAMERA.player_screen_y, 0.02, "in the lower part, the field ahead above")




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


func test_control_hint_explains_stun_aim_and_auto_run() -> void:
	var practice: PracticeMatch = _practice()
	practice.hud.joystick.auto_run = true
	practice.advance(DT)
	assert_string_contains(practice.hud.feedback.hint.text, "Auto-run")
	practice.sim.apply_effect(PracticeMatch.LOCAL_ID, StatusEffects.Type.STUN, 1.0, 0.0)
	practice.advance(DT)
	assert_string_contains(practice.hud.feedback.hint.text, "Stunned")
	assert_string_contains(practice.hud.feedback.hint.text, "skills unavailable")
	var player: PlayerState = practice.sim.players[PracticeMatch.LOCAL_ID]
	player.effects.clear()
	practice.hud.joystick.auto_run = false
	var button: AimButton = _weapon_button(practice, 0)
	button.aiming = true
	practice.hud.sync_player(practice.sim, player, -1, false)
	assert_string_contains(practice.hud.feedback.hint.text, "Release to cast")
	button.over_cancel = true
	practice.hud.sync_player(practice.sim, player, -1, false)
	assert_eq(practice.hud.feedback.hint.text, "Release to cancel")
	button.cancel_aim()
