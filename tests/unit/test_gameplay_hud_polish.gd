extends GutTest

func _hud() -> MatchHud:
	var phone: Control = autofree(Control.new()) as Control
	phone.size = Vector2(720.0, 1280.0)
	add_child(phone)
	var hud: MatchHud = MatchHud.new()
	phone.add_child(hud)
	return hud

func _sim() -> MatchSim:
	var sim: MatchSim = MatchSim.new(load("res://data/rules/game_rules.tres") as GameRules, load("res://data/rules/map_layout.tres") as MapLayout, 19)
	sim.add_player(1, 0)
	sim.add_player(2, 1)
	sim.equip(1)
	return sim

func test_score_cells_restore_original_header_spacing() -> void:
	var hud: MatchHud = _hud()
	var board: Scoreboard = hud.scoreboard
	assert_eq(board.size, MatchHud.SCORE_SIZE)
	var cells: Array[Rect2] = board.cell_rects()
	assert_eq(cells[0], Rect2(6.0, 6.0, 91.2, 76.0))
	assert_almost_eq(cells[1].end.x, board.size.x - 6.0, 0.1)
	assert_eq(cells[0].size, cells[1].size)
	assert_true(hud.header.get_global_rect().encloses(board.get_global_rect()))
	assert_false(board.get_global_rect().intersects(hud.clock_chip.get_global_rect()))
	assert_false(board.get_global_rect().intersects(hud.settings_button.get_global_rect()))

func test_point_pause_and_results_hide_the_hud_and_restore_it_after_the_freeze() -> void:
	var hud: MatchHud = _hud()
	var sim: MatchSim = _sim()
	for phase: int in [MatchSim.Phase.POINT_FREEZE, MatchSim.Phase.MATCH_OVER, MatchSim.Phase.PLAYING]:
		sim.phase = phase as MatchSim.Phase
		sim.score.winner = 0
		sim.point_log = [Vector2i(0, MatchSim.CaptureKind.BASE)] as Array[Vector2i]
		hud.sync_player(sim, sim.players[1], false)
		hud.sync_score(sim, 0, 0.0, 1)
		var normal: bool = phase == MatchSim.Phase.PLAYING
		for item: CanvasItem in [hud.console, hud.top, hud.hand, hud.scoreboard, hud.overhead, hud.marker, hud.chat]:
			assert_eq(item.visible, normal)
		assert_eq(hud.point_result.visible, phase == MatchSim.Phase.POINT_FREEZE)
		assert_eq(hud.results.visible, phase == MatchSim.Phase.MATCH_OVER)
		assert_eq(hud.controls_active(), normal)

func test_point_copy_identifies_the_base_or_team_that_was_captured_or_tagged() -> void:
	var hud: MatchHud = _hud()
	var sim: MatchSim = _sim()
	sim.phase = MatchSim.Phase.POINT_FREEZE
	sim.score.points = [1, 1] as Array[int]
	sim.point_log = [Vector2i(1, MatchSim.CaptureKind.BASE)] as Array[Vector2i]
	hud.point_result.sync(sim, 0)
	assert_eq(hud.point_result.subject.text, "Ally base")
	assert_eq(hud.point_result.outcome.text, "CAPTURED")
	assert_eq(hud.point_result.score.text, "1 - 1")
	assert_gt(hud.point_result.outcome.get_theme_font_size("font_size"), hud.point_result.score.get_theme_font_size("font_size"))
	assert_gt(hud.point_result.score.get_theme_font_size("font_size"), hud.point_result.subject.get_theme_font_size("font_size"))
	sim.point_log = [Vector2i(0, MatchSim.CaptureKind.ALL_TAGGED)] as Array[Vector2i]
	hud.point_result.sync(sim, 0)
	assert_eq(hud.point_result.subject.text, "All enemies")
	assert_eq(hud.point_result.outcome.text, "TAGGED")

func test_chat_has_two_rows_of_four_over_the_skills_and_blocks_card_input() -> void:
	var hud: MatchHud = _hud()
	var sim: MatchSim = _sim()
	hud.sync_player(sim, sim.players[1], false)
	hud.chat.set_open(true)
	await wait_process_frames(3)
	assert_eq(hud.chat.menu.columns, 4)
	assert_eq(hud.chat.phrase_buttons.size(), 8)
	assert_false(hud.controls_active())
	assert_true(hud.chat.menu.get_global_rect().encloses(Rect2(hud.hand.global_position + hud.hand.bar_rect.position, hud.hand.bar_rect.size)))
	for button: Button in hud.chat.phrase_buttons:
		var style: StyleBoxFlat = button.get_theme_stylebox("normal") as StyleBoxFlat
		assert_eq(style.bg_color, Color.WHITE)
		assert_eq(style.border_color, Color.BLACK)
		assert_almost_eq(button.size.y, (hud.chat.menu.size.y - UiMetrics.COMPACT_GAP) / 2.0, 1.0)
		assert_true(hud.get_global_rect().encloses(button.get_global_rect()))
	hud.chat.set_open(false)
	assert_true(hud.controls_active())

func test_opening_score_positions_follow_a_safe_area_change() -> void:
	var game: PracticeMatch = autofree(preload("res://scenes/match/practice.tscn").instantiate()) as PracticeMatch
	add_child(game)
	game.set_process(false)
	game.sim.phase = MatchSim.Phase.INTRO
	game.hud.set_top_inset(60.0)
	game._presentation.sync(0.0)
	assert_almost_eq(game.hud.scoreboard.get_global_rect().position.y, MatchHud.TOP_ROW_Y + 60.0, 0.1)
	assert_eq(game.hud.scoreboard.get_parent(), game.hud.top)

func test_tag_marker_stays_at_the_play_area_position_and_expires_during_point_freeze() -> void:
	var game: PracticeMatch = autofree(preload("res://scenes/match/practice.tscn").instantiate()) as PracticeMatch
	add_child(game)
	game.set_process(false)
	game.finish_pick()
	game.skip_intro()
	var player: PlayerState = game.sim.players[game.local_id]
	player.position = Vector2(1.0, 0.0)
	game._presentation.sync(0.0)
	var world: Vector3 = (game._views[game.local_id] as KidModel).position
	game.sim.kill(game.local_id)
	var overhead: OverheadHud = game.hud.overhead
	assert_eq(overhead.tag_count(), 1)
	assert_eq(overhead._tags[game.local_id]["world"], world)
	assert_ne(player.position, Vector2(world.x, world.z), "the kid already moved to the sidewalk")
	assert_eq(OverheadHud.TAG_FONT, OverheadHud.LABEL_FONT)
	game.sim.phase = MatchSim.Phase.POINT_FREEZE
	game._presentation.sync(0.0)
	assert_true(overhead.visible, "the brief tag feedback survives the score pause")
	overhead.tick(OverheadHud.TAG_TIME)
	game.hud.sync_score(game.sim, game.local_team, 0.0, game.local_id)
	assert_eq(overhead.tag_count(), 0)
	assert_false(overhead.visible, "no HUD remains over the point pause")
