extends GutTest
## Round 14 of the interface plan: awareness. A breath on the screen edge when you are low, a bar on the score card
## while a capture runs, a marker over the rescue box, and rings on the minimap when a teammate is tagged or a base is taken.

const PRACTICE_SCENE: PackedScene = preload("res://scenes/match/practice.tscn")
const DT: float = 1.0 / 30.0

var _loadout: Array[StringName]
var _motion: bool


func before_each() -> void:
	_loadout = Session.saved_loadout.duplicate()
	_motion = Settings.reduced_motion
	Settings.set_reduced_motion(false)


func after_each() -> void:
	Session.saved_loadout = _loadout
	Session.save()
	Settings.set_reduced_motion(_motion)


func _practice() -> PracticeMatch:
	var practice: PracticeMatch = autofree(PRACTICE_SCENE.instantiate()) as PracticeMatch
	add_child(practice)
	practice.finish_pick()
	practice.skip_intro()
	return practice


func _run(practice: PracticeMatch, frames: int) -> void:
	for i: int in frames:
		practice.advance(DT)


func _pulse() -> HurtPulse:
	var pulse: HurtPulse = autofree(HurtPulse.new()) as HurtPulse
	add_child(pulse)
	return pulse


func _tick(pulse: HurtPulse, seconds: float) -> void:
	for i: int in ceili(seconds / DT):
		pulse.tick(DT)


# ---- low health --------------------------------------------------------------------------

func test_below_a_third_of_your_health_the_edge_breathes_until_you_are_healed() -> void:
	var pulse: HurtPulse = _pulse()
	pulse.sync_health(30, 100, true)
	_tick(pulse, 1.0)
	assert_true(pulse.low_active(), "30 of 100 is low")
	var seen: Array[float] = []
	for i: int in int(HurtPulse.LOW_PERIOD / DT):
		pulse.tick(DT)
		seen.append(pulse.low)
	assert_lt(seen.min(), HurtPulse.LOW_MAX * 0.7, "it breathes out")
	assert_gt(seen.max(), HurtPulse.LOW_MAX * 0.9, "and in")
	assert_lte(seen.max(), HurtPulse.LOW_MAX + 0.001)
	assert_gte(seen.min(), HurtPulse.LOW_MIN - 0.001)
	pulse.sync_health(80, 100, true)
	_tick(pulse, 0.5)
	assert_false(pulse.low_active(), "healed: gone")


func test_a_healthy_player_has_no_breath() -> void:
	var pulse: HurtPulse = _pulse()
	pulse.sync_health(31, 100, true)
	_tick(pulse, 1.0)
	assert_false(pulse.low_active())


func test_no_breath_when_tagged_or_out_of_play() -> void:
	var pulse: HurtPulse = _pulse()
	pulse.sync_health(0, 100, true)
	_tick(pulse, 1.0)
	assert_false(pulse.low_active(), "tagged kids have no health")
	pulse.sync_health(10, 100, false)
	_tick(pulse, 1.0)
	assert_false(pulse.low_active(), "not while the match is not being played")


func test_reduce_motion_makes_the_breath_a_steady_faint_edge() -> void:
	Settings.set_reduced_motion(true)
	var pulse: HurtPulse = _pulse()
	pulse.sync_health(10, 100, true)
	_tick(pulse, 1.0)
	for i: int in 20:
		pulse.tick(DT)
		assert_almost_eq(pulse.low, HurtPulse.LOW_STILL, 0.001)


func test_a_hit_over_the_breath_shows_the_stronger_of_the_two() -> void:
	var pulse: HurtPulse = _pulse()
	pulse.sync_health(10, 100, true)
	_tick(pulse, 1.0)
	pulse.hit(40, 100)
	assert_gt(pulse.strength, pulse.low, "the hit glow is the stronger")
	_tick(pulse, HurtPulse.HIT_TIME + 0.1)
	assert_false(pulse.active())
	assert_true(pulse.low_active(), "and the breath is still there when it fades")


func test_the_match_turns_the_breath_on_at_low_health() -> void:
	var practice: PracticeMatch = _practice()
	_run(practice, 3)
	var hp: int = practice.sim.players[practice.local_id].hp
	practice.sim.damage(practice.local_id, hp - 10)
	_run(practice, 3)
	await wait_process_frames(30)
	assert_true(practice.hud.pulse.low_active())


# ---- the capture bar ---------------------------------------------------------------------

func test_the_score_card_fills_while_a_capture_runs_blue_for_you_red_for_them() -> void:
	var practice: PracticeMatch = _practice()
	_run(practice, 3)
	var card: Scoreboard = practice.hud.scoreboard
	assert_eq(card.capture, -1.0, "nothing is being captured")
	practice.sim.capture_team = practice.local_team
	practice.sim.capture_elapsed = practice.sim.rules.capture_time / 2.0
	card.show_capture(practice.sim, practice.local_team)
	assert_almost_eq(card.capture, 0.5, 0.021)
	assert_true(card.capture_ours)
	practice.sim.capture_team = 1 - practice.local_team
	card.show_capture(practice.sim, practice.local_team)
	assert_false(card.capture_ours, "theirs is red")
	practice.sim.capture_team = -1
	card.show_capture(practice.sim, practice.local_team)
	assert_eq(card.capture, -1.0, "gone when it ends")


func test_the_capture_bar_is_not_shown_in_a_freeze() -> void:
	var practice: PracticeMatch = _practice()
	_run(practice, 3)
	practice.sim.capture_team = practice.local_team
	practice.sim.capture_elapsed = 2.0
	practice.sim.phase = MatchSim.Phase.POINT_FREEZE
	practice.hud.scoreboard.show_capture(practice.sim, practice.local_team)
	assert_eq(practice.hud.scoreboard.capture, -1.0)


func test_the_capture_bar_does_not_change_the_cards_size_or_put_anything_under_it() -> void:
	var practice: PracticeMatch = _practice()
	var card: Scoreboard = practice.hud.scoreboard
	var before: Vector2 = card.size
	practice.sim.capture_team = practice.local_team
	practice.sim.capture_elapsed = 2.0
	card.show_capture(practice.sim, practice.local_team)
	assert_eq(card.size, before, "the bar is inside the card (nothing sits under it)")


# ---- the rescue marker -------------------------------------------------------------------

func _beside_own_box(practice: PracticeMatch) -> void:
	var me: PlayerState = practice.sim.players[practice.local_id]
	me.position = practice.layout.jail_rect(practice.own_side()).get_center() + Vector2(1.0, 4.0)
	practice._snap_camera = true
	_run(practice, 6)


func _arrows_settled(practice: PracticeMatch) -> void:
	for i: int in 30:
		practice.hud.arrows.tick(DT)


func test_the_rescue_marker_shows_over_the_box_when_it_is_on_screen_and_the_arrow_gives_way() -> void:
	var practice: PracticeMatch = _practice()
	_run(practice, 3)
	for id: int in practice.sim.players:
		var player: PlayerState = practice.sim.players[id]
		if player.team == practice.local_team and id != practice.local_id:
			practice.sim.damage(id, 9999)
			break
	var me: PlayerState = practice.sim.players[practice.local_id]
	me.position = practice.layout.base_center(-practice.own_side()) + Vector2(0.0, 2.0)
	practice._snap_camera = true
	_run(practice, 6)
	_arrows_settled(practice)
	var far: Dictionary = practice.hud.arrows.arrows[1]
	assert_eq(far["kind"], &"rescue")
	assert_eq(far["marker"], 0.0, "far from the box there is the arrow, not the marker")
	_beside_own_box(practice)
	_arrows_settled(practice)
	var near: Dictionary = practice.hud.arrows.arrows[1]
	assert_eq(near["marker"], 1.0, "at the box the marker is there")
	assert_eq(near["alpha"], 0.0, "and the arrow has gone")


func test_no_rescue_marker_without_a_tagged_teammate() -> void:
	var practice: PracticeMatch = _practice()
	_run(practice, 3)
	_beside_own_box(practice)
	_arrows_settled(practice)
	for arrow: Dictionary in practice.hud.arrows.arrows:
		assert_eq(arrow["marker"], 0.0)


# ---- the minimap rings -------------------------------------------------------------------

func test_a_teammate_being_tagged_rings_the_box_but_an_enemy_being_tagged_does_not() -> void:
	var practice: PracticeMatch = _practice()
	_run(practice, 3)
	var map: LaneMinimap = practice.hud.minimap
	assert_eq(map.pings().size(), 0)
	practice.sim.damage(PracticeMatch.ENEMY_STAND_ID, 9999)
	assert_eq(map.pings().size(), 0, "an enemy's tag would only tell you where they are")
	for id: int in practice.sim.players:
		var player: PlayerState = practice.sim.players[id]
		if player.team == practice.local_team and id != practice.local_id:
			practice.sim.damage(id, 9999)
			break
	assert_eq(map.pings().size(), 1)
	assert_eq(map.pings()[0]["color"], LaneMinimap.PING_ALERT, "gold, like Rescue")
	assert_true(practice.layout.jail_rect(practice.own_side()).grow(2.0).has_point(map.pings()[0]["at"] as Vector2), "at your box")


func test_a_captured_base_rings_in_the_scoring_teams_colour() -> void:
	var practice: PracticeMatch = _practice()
	_run(practice, 3)
	var map: LaneMinimap = practice.hud.minimap
	practice.sim.point_scored.emit(practice.local_team, MatchSim.CaptureKind.BASE)
	assert_eq(map.pings()[0]["color"], LaneMinimap.OWN)
	assert_eq(map.pings()[0]["at"], practice.layout.base_center(-practice.own_side()), "you took their base")
	practice.sim.point_scored.emit(1 - practice.local_team, MatchSim.CaptureKind.BASE)
	assert_eq(map.pings()[1]["color"], LaneMinimap.ENEMY)
	assert_eq(map.pings()[1]["at"], practice.layout.base_center(practice.own_side()), "they took yours")


func test_a_ring_grows_fades_and_goes() -> void:
	var map: LaneMinimap = autofree(LaneMinimap.new()) as LaneMinimap
	var ring: Dictionary = {"at": Vector2.ZERO, "color": Color.WHITE, "radii": LaneMinimap.PING_RADII, "left": LaneMinimap.PING_TIME}
	var start: Dictionary = map.ring_of(ring)
	assert_almost_eq(start["radius"] as float, LaneMinimap.PING_RADII.x, 0.01)
	assert_almost_eq(start["alpha"] as float, 1.0, 0.01)
	ring["left"] = LaneMinimap.PING_TIME * 0.5
	var middle: Dictionary = map.ring_of(ring)
	assert_gt(middle["radius"] as float, start["radius"] as float)
	assert_lt(middle["alpha"] as float, 1.0)
	ring["left"] = 0.0
	assert_almost_eq(map.ring_of(ring)["radius"] as float, LaneMinimap.PING_RADII.y, 0.01)
	assert_almost_eq(map.ring_of(ring)["alpha"] as float, 0.0, 0.01)


func test_with_reduce_motion_a_ring_only_fades() -> void:
	Settings.set_reduced_motion(true)
	var map: LaneMinimap = autofree(LaneMinimap.new()) as LaneMinimap
	var first: Dictionary = {"at": Vector2.ZERO, "color": Color.WHITE, "radii": LaneMinimap.PING_RADII, "left": LaneMinimap.PING_TIME}
	var last: Dictionary = {"at": Vector2.ZERO, "color": Color.WHITE, "radii": LaneMinimap.PING_RADII, "left": 0.2}
	assert_eq(map.ring_of(first)["radius"], map.ring_of(last)["radius"], "same size throughout")
	assert_gt(map.ring_of(first)["alpha"] as float, map.ring_of(last)["alpha"] as float, "fading")


func test_rings_end_by_themselves() -> void:
	var practice: PracticeMatch = _practice()
	_run(practice, 3)
	var map: LaneMinimap = practice.hud.minimap
	map.ping(Vector2.ZERO, Color.WHITE)
	map._process(LaneMinimap.PING_TIME * 0.5)
	assert_eq(map.pings().size(), 1)
	map._process(LaneMinimap.PING_TIME)
	assert_eq(map.pings().size(), 0)


# ---- the skin warm list covers a match too ------------------------------------------------

func test_after_warming_the_match_screens_draw_nothing_new() -> void:
	UiSkin._cache.clear()
	UiSkin._warmed = 0
	while not UiSkin.warm_next():
		pass
	var warmed: Array = UiSkin._cache.keys()
	var practice: PracticeMatch = _practice()
	_run(practice, 3)
	practice.sim.damage(practice.local_id, 9999)
	_run(practice, 6)
	practice.hud.show_banner("GO!")
	practice.hud.toggle_menu()
	await wait_process_frames(3)
	practice.sim.phase = MatchSim.Phase.MATCH_OVER
	practice.hud.sync_score(practice.sim, practice.local_team, DT, practice.local_id)
	await wait_process_frames(3)
	var extra: Array[String] = []
	for key: String in UiSkin._cache.keys():
		if not warmed.has(key):
			extra.append(key)
	assert_eq(extra, [] as Array[String], "these were drawn when the match opened instead of during loading: add them to WARM_PLATES")
