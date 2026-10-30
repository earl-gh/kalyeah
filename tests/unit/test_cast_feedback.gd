extends GutTest
## What the street shows around a cast: the aim preview (what it covers, who it reaches, when it
## would cast nothing), the warning while a delayed snip winds up, the burst on a kid who is hit,
## and the cancel tile.

var RULES: GameRules = load("res://data/rules/game_rules.tres") as GameRules
const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
const PRACTICE_SCENE: PackedScene = preload("res://scenes/match/practice.tscn")
const DT: float = 1.0 / 30.0
const CASTER: int = 1
const FOE: int = 2


func _sim(first: StringName, second: StringName = &"papel_shield") -> MatchSim:
	var sim: MatchSim = MatchSim.new(RULES, LAYOUT, 5)
	var caster: PlayerState = sim.add_player(CASTER, 0)
	caster.position = Vector2.ZERO
	caster.facing = Vector2(0.0, -1.0)
	assert_true(sim.set_loadout(CASTER, first, second))
	return sim


func _foe(sim: MatchSim, at: Vector2, id: int = FOE) -> PlayerState:
	return sim.add_dummy(id, 1, at, DummyBrain.standing())


func _practice() -> PracticeMatch:
	var practice: PracticeMatch = autofree(PRACTICE_SCENE.instantiate()) as PracticeMatch
	add_child(practice)
	practice.finish_pick()
	practice.skip_intro()
	return practice


# ---- would_cast: nothing to throw / nobody to heal ---------------------------------

func test_a_homing_rock_with_nobody_to_lock_casts_nothing() -> void:
	var sim: MatchSim = _sim(&"bato_light")
	var def: WeaponDef = sim.weapon_defs[&"bato_light"]
	var caster: PlayerState = sim.players[CASTER]
	assert_false(sim.weapons.would_cast(sim, caster, def, Vector2.ZERO, -1), "nobody locked")
	var foe: PlayerState = _foe(sim, Vector2(0.0, -5.0))
	assert_true(sim.weapons.would_cast(sim, caster, def, Vector2.ZERO, foe.id), "locked and in range")
	foe.position = Vector2(0.0, -def.max_range - 3.0)
	assert_false(sim.weapons.would_cast(sim, caster, def, Vector2.ZERO, foe.id), "locked but walked out of range")


func test_a_heal_dragged_at_nobody_casts_nothing_but_a_tap_heals_you() -> void:
	var sim: MatchSim = _sim(&"heal")
	var def: WeaponDef = sim.weapon_defs[&"heal"]
	var caster: PlayerState = sim.players[CASTER]
	assert_true(sim.weapons.would_cast(sim, caster, def, Vector2.ZERO), "a tap heals yourself")
	assert_false(sim.weapons.would_cast(sim, caster, def, Vector2(1.0, 0.0)), "a drag at nobody does nothing")
	var friend: PlayerState = sim.add_player(7, 0)
	friend.position = Vector2(5.0, 0.0)
	assert_true(sim.weapons.would_cast(sim, caster, def, Vector2(1.0, 0.0)), "a teammate in the dragged direction")


func test_every_other_weapon_always_casts() -> void:
	var sim: MatchSim = _sim(&"lata")
	for def: WeaponDef in RULES.weapons:
		if def.shape == WeaponDef.Shape.TARGETED or def.shape == WeaponDef.Shape.HEAL:
			continue
		assert_true(sim.weapons.would_cast(sim, sim.players[CASTER], def, Vector2.ZERO), def.id)


# ---- would_reach: who the preview rings ---------------------------------------------

func test_a_cone_reaches_who_it_would_hit_and_nobody_else() -> void:
	var sim: MatchSim = _sim(&"gunting_light")
	var def: WeaponDef = sim.weapon_defs[&"gunting_light"]
	var ahead: PlayerState = _foe(sim, Vector2(0.0, -2.0))
	_foe(sim, Vector2(0.0, 2.0), 3)
	_foe(sim, Vector2(2.9, 0.2), 4)
	var reached: Array[PlayerState] = sim.weapons.would_reach(sim, sim.players[CASTER], def, Vector2(0.0, -1.0))
	assert_eq(reached, [ahead] as Array[PlayerState], "only the kid in the wedge")


func test_the_preview_matches_what_the_real_cone_hits() -> void:
	var sim: MatchSim = _sim(&"gunting_light")
	var def: WeaponDef = sim.weapon_defs[&"gunting_light"]
	var spots: Array[Vector2] = [Vector2(0.5, -2.0), Vector2(-2.0, -2.0), Vector2(0.0, -3.5), Vector2(1.0, 1.0)]
	var id: int = 10
	for at: Vector2 in spots:
		_foe(sim, at, id)
		id += 1
	var aim: Vector2 = Vector2(0.0, -1.0)
	var predicted: Array[int] = []
	for target: PlayerState in sim.weapons.would_reach(sim, sim.players[CASTER], def, aim):
		predicted.append(target.id)
	var before: Dictionary[int, int] = {}
	for foe_id: int in range(10, id):
		before[foe_id] = sim.players[foe_id].hp
	sim.set_input(CASTER, PlayerInput.create(Vector2.ZERO, aim, PlayerInput.BTN_WEAPON_1, sim.tick))
	sim.step(DT)
	sim.set_input(CASTER, PlayerInput.create(Vector2.ZERO, aim, 0, sim.tick))
	sim.step(DT)
	for foe_id: int in range(10, id):
		assert_eq(sim.players[foe_id].hp < before[foe_id], predicted.has(foe_id), "kid %d" % foe_id)


func test_a_landing_circle_and_a_lane_reach_the_kids_inside() -> void:
	var sim: MatchSim = _sim(&"bato_heavy", &"tsinelas_heavy")
	var inside: PlayerState = _foe(sim, Vector2(0.0, -6.0))
	_foe(sim, Vector2(5.0, -6.0), 3)
	var blast: WeaponDef = sim.weapon_defs[&"bato_heavy"]
	var aim: Vector2 = Vector2(0.0, -6.0) / blast.max_range
	assert_eq(sim.weapons.would_reach(sim, sim.players[CASTER], blast, aim), [inside] as Array[PlayerState])
	var slipper: WeaponDef = sim.weapon_defs[&"tsinelas_heavy"]
	assert_eq(sim.weapons.would_reach(sim, sim.players[CASTER], slipper, Vector2(0.0, -1.0)), [inside] as Array[PlayerState], "along the lane, not beside it")


func test_a_shield_reaches_nobody_and_a_heal_rings_the_patient() -> void:
	var sim: MatchSim = _sim(&"papel_shield", &"heal")
	_foe(sim, Vector2(0.0, -1.5))
	assert_true(sim.weapons.would_reach(sim, sim.players[CASTER], sim.weapon_defs[&"papel_shield"], Vector2(0.0, -1.0)).is_empty())
	var heal: WeaponDef = sim.weapon_defs[&"heal"]
	assert_eq(sim.weapons.would_reach(sim, sim.players[CASTER], heal, Vector2.ZERO), [sim.players[CASTER]] as Array[PlayerState])


# ---- the wind-up warning ------------------------------------------------------------

func test_only_a_delayed_cone_announces_a_wind_up() -> void:
	var sim: MatchSim = _sim(&"gunting_heavy", &"gunting_light")
	var winds: Array[Vector2] = []
	sim.weapons.cone_winding.connect(func(_owner: int, _def: WeaponDef, _origin: Vector2, direction: Vector2) -> void: winds.append(direction))
	sim.set_input(CASTER, PlayerInput.create(Vector2.ZERO, Vector2(0.0, -1.0), PlayerInput.BTN_WEAPON_2, sim.tick))
	sim.step(DT)
	sim.set_input(CASTER, PlayerInput.create(Vector2.ZERO, Vector2(0.0, -1.0), 0, sim.tick))
	sim.step(DT)
	assert_eq(winds.size(), 0, "the quick snip strikes at once")
	var heavy: MatchSim = _sim(&"gunting_heavy")
	heavy.weapons.cone_winding.connect(func(_owner: int, _def: WeaponDef, _origin: Vector2, direction: Vector2) -> void: winds.append(direction))
	heavy.set_input(CASTER, PlayerInput.create(Vector2.ZERO, Vector2(0.0, -1.0), PlayerInput.BTN_WEAPON_1, heavy.tick))
	heavy.step(DT)
	heavy.set_input(CASTER, PlayerInput.create(Vector2.ZERO, Vector2(0.0, -1.0), 0, heavy.tick))
	heavy.step(DT)
	assert_eq(winds, [Vector2(0.0, -1.0)] as Array[Vector2], "the big snip announces where it will land")


func test_the_wind_up_is_relayed_to_online_clients() -> void:
	assert_true((ServerMatch.RELAYED["weapons"] as Array).has("cone_winding"))


func test_the_warning_shows_while_the_big_snip_winds_up_and_goes_when_it_lands() -> void:
	var practice: PracticeMatch = _practice()
	var player: PlayerState = practice.sim.players[PracticeMatch.LOCAL_ID]
	practice.sim.set_loadout(PracticeMatch.LOCAL_ID, &"gunting_heavy", &"papel_shield")
	player.position = Vector2(0.0, 4.0)
	practice.sim.players[PracticeMatch.ENEMY_STAND_ID].position = Vector2(0.0, 1.0)
	var fx: WeaponFxView = practice.get_node("%WeaponFx") as WeaponFxView
	assert_eq(fx.windup_count(), 0)
	practice.card_pressed(0)
	practice.card_released(0, Vector2.ZERO, true, true)
	for i: int in 4:
		practice.advance(DT)
	assert_eq(fx.windup_count(), 1, "the wedge is on the ground for the kid in it")
	for i: int in 30:
		practice.advance(DT)
	assert_eq(fx.windup_count(), 0, "it strikes after half a second and the warning is gone")


func test_the_big_snip_hand_lands_on_the_strike() -> void:
	var practice: PracticeMatch = _practice()
	var player: PlayerState = practice.sim.players[PracticeMatch.LOCAL_ID]
	practice.sim.set_loadout(PracticeMatch.LOCAL_ID, &"gunting_heavy", &"papel_shield")
	player.position = Vector2(0.0, 4.0)
	practice.sim.players[PracticeMatch.ENEMY_STAND_ID].position = Vector2(0.0, 1.0)
	var kid: KidModel = practice._views[PracticeMatch.LOCAL_ID]
	practice.card_pressed(0)
	practice.card_released(0, Vector2.ZERO, true, true)
	for i: int in 3:
		practice.advance(DT)
	var delay: float = practice.sim.weapon_defs[&"gunting_heavy"].delay
	assert_almost_eq(kid.cast_seek, maxf(kid.cast_release("cast") - delay, 0.0), 0.001, "the clip starts half a second early, so the hand comes down on the strike")
	assert_eq(PracticeMatchPresentation.cast_lead(practice.sim, &"gunting_light"), 0.0, "an instant snip releases on the cast")


# ---- the hit burst ------------------------------------------------------------------

func test_a_kid_who_is_hit_gets_a_short_burst() -> void:
	Settings.set_reduced_motion(false)  # with Reduce motion nothing flies, so there is no burst to count
	var practice: PracticeMatch = _practice()
	var fx: WeaponFxView = practice.get_node("%WeaponFx") as WeaponFxView
	assert_eq(fx.burst_count(), 0)
	practice.sim.damage(PracticeMatch.ENEMY_STAND_ID, 14)
	practice.advance(DT)
	assert_eq(fx.burst_count(), 1)
	for i: int in 20:
		practice.advance(DT)
	assert_eq(fx.burst_count(), 0, "gone in a third of a second")


# ---- the aim preview itself ---------------------------------------------------------

func test_the_preview_is_one_quiet_colour_that_goes_pale_or_red_when_it_should() -> void:
	assert_eq(AimIndicator.color_for(true, false), AimIndicator.LINE_COLOR)
	assert_eq(AimIndicator.color_for(false, false), AimIndicator.NO_CAST_COLOR, "nothing to hit: pale")
	assert_eq(AimIndicator.color_for(true, true), AimIndicator.CANCEL_COLOR, "over cancel: red")
	assert_eq(AimIndicator.color_for(false, true), AimIndicator.CANCEL_COLOR, "cancel wins")
	assert_lt(AimIndicator.LINE_WIDTH, 0.25, "a thin line (a street tile is 1.5 m): it must not hide the street")
	assert_gt(AimIndicator.LINE_WIDTH, 0.15, "and wide enough to see on a phone at the Classic camera's distance")


func test_a_ribbon_is_two_triangles_per_segment_and_a_fill_one_per_fan_step() -> void:
	var mesh: ImmediateMesh = ImmediateMesh.new()
	mesh.surface_begin(Mesh.PRIMITIVE_TRIANGLES)
	AimIndicator.ribbon(mesh, PackedVector2Array([Vector2.ZERO, Vector2(1.0, 0.0), Vector2(1.0, 1.0)]), 0.2, Color.WHITE)
	AimIndicator.fill(mesh, AimIndicator.circle(Vector2.ZERO, 1.0), Color.WHITE)
	mesh.surface_end()
	var vertices: PackedVector3Array = mesh.surface_get_arrays(0)[Mesh.ARRAY_VERTEX] as PackedVector3Array
	assert_eq(vertices.size(), 2 * 6 + (AimIndicator.CIRCLE_POINTS + 1 - 3) * 3)


func test_a_dragged_aim_draws_the_shape_and_a_tap_draws_only_the_rings() -> void:
	var sim: MatchSim = _sim(&"bato_heavy", &"gunting_light")
	var foe: PlayerState = _foe(sim, Vector2(0.0, -4.0))
	var indicator: AimIndicator = autofree(AimIndicator.new()) as AimIndicator
	add_child(indicator)
	var blast: WeaponDef = sim.weapon_defs[&"bato_heavy"]
	var aim: Vector2 = Vector2(0.0, -4.0) / blast.max_range
	indicator.show_aim(sim, sim.players[CASTER], blast, aim, foe.id, false, 0.0, true)
	var dragged: int = (indicator.mesh.surface_get_arrays(0)[Mesh.ARRAY_VERTEX] as PackedVector3Array).size()
	indicator.show_aim(sim, sim.players[CASTER], blast, aim, foe.id, false, 0.0, false)
	var tapped: int = (indicator.mesh.surface_get_arrays(0)[Mesh.ARRAY_VERTEX] as PackedVector3Array).size()
	assert_gt(dragged, tapped, "the landing circle is drawn only when dragged")
	assert_gt(tapped, 0, "the ring under the kid it will hit is still there")
	indicator.show_aim(sim, sim.players[CASTER], blast, Vector2(0.0, -1.0), foe.id, false, 0.0, false)
	assert_eq(indicator.mesh.get_surface_count(), 0, "aimed at nobody and not dragged: nothing at all")


func test_every_weapons_preview_draws_without_error_and_no_range_circle() -> void:
	var sim: MatchSim = _sim(&"bato_light")
	var foe: PlayerState = _foe(sim, Vector2(0.0, -3.0))
	var indicator: AimIndicator = autofree(AimIndicator.new()) as AimIndicator
	add_child(indicator)
	for def: WeaponDef in RULES.weapons:
		indicator.show_aim(sim, sim.players[CASTER], def, Vector2(0.0, -0.8), foe.id, false)
		indicator.show_aim(sim, sim.players[CASTER], def, Vector2(0.0, -0.8), foe.id, true)
		assert_lte(indicator.mesh.get_surface_count(), 1, def.id)
	indicator.clear()
	assert_eq(indicator.mesh.get_surface_count(), 0)


func test_the_shield_preview_is_the_walls_real_thickness() -> void:
	var sim: MatchSim = _sim(&"papel_shield", &"lata")
	var def: WeaponDef = sim.weapon_defs[&"papel_shield"]
	var lines: Array[PackedVector2Array] = AimIndicator.outline(sim, sim.players[CASTER], def, Vector2(0.0, -1.0))
	assert_eq(lines.size(), 1)
	assert_gte(lines[0].size(), 4, "a closed rectangle (it can be filled)")


# ---- the guide under a dragged card --------------------------------------------------

func test_the_guide_shows_while_a_card_is_over_the_street_and_goes_when_it_is_dropped() -> void:
	var practice: PracticeMatch = _practice()
	var player: PlayerState = practice.sim.players[PracticeMatch.LOCAL_ID]
	player.weapons[0] = &"gunting_light"
	player.elixir = practice.sim.rules.elixir_max
	player.position = Vector2(0.0, 4.0)
	var indicator: AimIndicator = practice.get_node("%AimIndicator") as AimIndicator
	practice.input.card_press(0, Time.get_ticks_msec())
	practice.input.card_drag(false, Vector2(0.0, 1.0))
	practice.advance(DT)
	assert_gt(indicator.mesh.get_surface_count(), 0, "the cone toward the finger's street point")
	practice.input.card_drag(false, null)
	practice.advance(DT)
	assert_gt(indicator.mesh.get_surface_count(), 0, "back over the hand: still drawn (in red: letting go cancels)")
	practice.input.card_cancel()
	practice.advance(DT)
	assert_eq(indicator.mesh.get_surface_count(), 0, "dropped: gone")


# ---- the snip charge ----------------------------------------------------------------

func test_the_snipping_scissors_show_one_pip_per_possible_snip() -> void:
	var sim: MatchSim = _sim(&"gunting_light", &"gunting_heavy")
	var light: WeaponDef = sim.weapon_defs[&"gunting_light"]
	var pips: Array[PackedVector2Array] = AimIndicator.snip_pips(Vector2.ZERO, Vector2(0.0, -1.0), light)
	assert_eq(pips.size(), light.snips_max, "six pips: the most snips a hold can give")
	for pip: PackedVector2Array in pips:
		assert_lt(pip[1].length(), light.max_range, "inside the wedge")
	assert_true(AimIndicator.snip_pips(Vector2.ZERO, Vector2(0.0, -1.0), sim.weapon_defs[&"gunting_heavy"]).is_empty(), "the big snip is one snip")


func test_more_pips_light_up_the_longer_the_button_is_held() -> void:
	var sim: MatchSim = _sim(&"gunting_light")
	var light: WeaponDef = sim.weapon_defs[&"gunting_light"]
	assert_eq(light.snip_count(0.0), light.snips_min, "a tap lights the first two")
	assert_gt(light.snip_count(1.0), light.snip_count(0.0))
	assert_eq(light.snip_count(10.0), light.snips_max)
	var indicator: AimIndicator = autofree(AimIndicator.new()) as AimIndicator
	add_child(indicator)
	indicator.show_aim(sim, sim.players[CASTER], light, Vector2.ZERO, -1, false, 1.0, false)
	assert_eq(indicator.mesh.get_surface_count(), 1, "the pips show even when a tap auto-aims")


# ---- the aim guide switch -----------------------------------------------------------

func test_the_aim_guide_can_be_turned_off_and_then_nothing_is_drawn() -> void:
	var sim: MatchSim = _sim(&"bato_heavy", &"gunting_light")
	var foe: PlayerState = _foe(sim, Vector2(0.0, -4.0))
	var indicator: AimIndicator = autofree(AimIndicator.new()) as AimIndicator
	add_child(indicator)
	var blast: WeaponDef = sim.weapon_defs[&"bato_heavy"]
	var saved: bool = Settings.aim_guide
	Settings.set_aim_guide(true)
	indicator.show_aim(sim, sim.players[CASTER], blast, Vector2(0.0, -0.5), foe.id, false)
	assert_eq(indicator.mesh.get_surface_count(), 1, "on by default")
	Settings.set_aim_guide(false)
	indicator.show_aim(sim, sim.players[CASTER], blast, Vector2(0.0, -0.5), foe.id, false)
	assert_eq(indicator.mesh.get_surface_count(), 0, "off: nothing on the street")
	Settings.set_aim_guide(saved)


func test_the_aim_guide_switch_lives_with_the_other_control_options() -> void:
	var options: ControlOptions = autofree(ControlOptions.new()) as ControlOptions
	add_child(options)
	assert_not_null(options.guide_toggle)
	var saved: bool = Settings.aim_guide
	options.guide_toggle.toggled.emit(false)
	assert_false(Settings.aim_guide)
	options.guide_toggle.toggled.emit(true)
	assert_true(Settings.aim_guide)
	Settings.set_aim_guide(saved)


func test_the_match_menu_keeps_its_list_short_so_the_aim_guide_is_a_settings_page_option() -> void:
	var settings_options: ControlOptions = autofree(ControlOptions.new()) as ControlOptions
	add_child(settings_options)
	assert_not_null(settings_options.guide_toggle, "on the Settings page")
	var match_options: ControlOptions = autofree(ControlOptions.new()) as ControlOptions
	match_options.in_match = true
	add_child(match_options)
	assert_null(match_options.guide_toggle, "not in the match menu: Resume and Leave stay in view")
