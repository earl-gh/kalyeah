extends GutTest
## The main screen's Practice row (owner): above Play, a game picker and a kid picker, 80 : 20; and a Practice match of each street game.

const TITLE_SCENE: PackedScene = preload("res://scenes/ui/title_screen.tscn")
const PRACTICE_SCENE: PackedScene = preload("res://scenes/match/practice.tscn")
const PRACTICE_MODE: int = 2
const DT: float = 1.0 / 30.0
const TUMBANG_HOME: float = 12.0

var _game: StringName
var _avatar: String
var _mode: int
var _kind: StringName
var _coach: bool


func before_each() -> void:
	_game = Session.practice_game
	_avatar = Session.avatar_id
	_mode = Session.menu_mode
	_kind = Session.practice_kind
	_coach = Session.coach_pending
	Session.practice_game = &"street"


func after_each() -> void:
	Session.practice_game = _game
	Session.avatar_id = _avatar
	Session.menu_mode = _mode
	Session.practice_kind = _kind
	Session.coach_pending = _coach
	Session.save()


func _phone_screen() -> TitleScreen:
	var phone: Control = autofree(Control.new()) as Control
	phone.size = Vector2(720.0, 1280.0)
	add_child(phone)
	var screen: TitleScreen = autofree(TITLE_SCENE.instantiate()) as TitleScreen
	phone.add_child(screen)
	return screen


func test_the_row_stands_above_play_only_in_practice_split_80_20() -> void:
	Session.menu_mode = PRACTICE_MODE
	var screen: TitleScreen = _phone_screen()
	await wait_frames(3)
	var row: PracticeRow = screen.practice_row
	assert_true(row.is_visible_in_tree(), "Practice shows the row")
	var game: Rect2 = row.game_button.get_global_rect()
	var kid: Rect2 = row.kid_button.get_global_rect()
	var play: Rect2 = screen.primary_button.get_global_rect()
	assert_lt(game.end.y, play.position.y, "above Play")
	assert_almost_eq(game.size.x / (game.size.x + kid.size.x), 0.8, 0.02, "game : kid is 80 : 20")
	assert_lt(game.end.x, kid.position.x, "the kid button on the right")
	assert_gte(kid.size.y, UiMetrics.TOUCH, "thumb-sized")
	assert_gte(game.position.x, 0.0)
	assert_lte(kid.end.x, 720.0, "fits the screen")
	screen.carousel.select(1, false)
	await wait_frames(2)
	assert_false(row.is_visible_in_tree(), "Local has Create and Join only")


func test_the_game_picker_lists_the_three_games_and_remembers_the_pick() -> void:
	Session.menu_mode = PRACTICE_MODE
	Session.practice_game = &"street"
	var screen: TitleScreen = _phone_screen()
	await wait_frames(2)
	assert_eq(screen.practice_row.game_button.text, "Agawan Base")
	var card: MenuDialog = screen.practice_row.open_game_picker()
	await wait_frames(2)
	assert_true(card.is_open, "the picker is shown")
	for id: String in ["street", "tumbang_preso", "patintero"]:
		assert_not_null(card.find_child("Game_" + id, true, false), id)
	(card.find_child("Game_patintero", true, false) as Button).pressed.emit()
	assert_eq(Session.practice_game, &"patintero")
	assert_eq(screen.practice_row.game_button.text, "Patintero")


func test_the_kid_picker_changes_your_kid() -> void:
	Session.menu_mode = PRACTICE_MODE
	Session.avatar_id = "junjun"
	var screen: TitleScreen = _phone_screen()
	await wait_frames(2)
	var card: MenuDialog = screen.practice_row.open_kid_picker()
	await wait_frames(2)
	var grid: GridContainer = card.find_child("Kids", true, false) as GridContainer
	assert_eq(grid.get_child_count(), 6, "the six kids")
	(card.find_child("Kid_inday", true, false) as Button).pressed.emit()
	assert_eq(Session.avatar_id, "inday")
	assert_eq(screen.practice_row.kid_button.icon, KidModel.portrait(&"inday"), "the button shows the new face")


func test_practice_confirm_names_the_game() -> void:
	Session.menu_mode = PRACTICE_MODE
	Session.practice_game = &"tumbang_preso"
	var screen: TitleScreen = _phone_screen()
	await wait_frames(2)
	var card: MenuDialog = screen.open_practice_confirm()
	assert_eq(card.title_label.text, "Tumbang Preso")


func _practice(game: StringName) -> PracticeMatch:
	Session.practice_kind = &"match"
	Session.practice_game = game
	Session.coach_pending = false
	var practice: PracticeMatch = autofree(PRACTICE_SCENE.instantiate()) as PracticeMatch
	add_child(practice)
	practice.set_process(false)
	practice.finish_pick()
	practice.skip_intro()
	return practice


func test_a_tumbang_preso_practice_is_every_kid_for_itself_with_one_taya() -> void:
	var practice: PracticeMatch = _practice(&"tumbang_preso")
	var game: TumbangPreso = practice.sim.mode as TumbangPreso
	assert_eq(practice.sim.players.size(), 6, "you and five bots")
	for state: PlayerState in practice.sim.players.values():
		assert_eq(state.team, 0, "nobody is on a team")
	assert_ne(game.taya_id, practice.local_id, "a bot is the taya to begin with")
	assert_false(practice._map.bases_shown(), "no bases or rescue areas in Tumbang Preso")
	assert_not_null(practice.mode_view.can_node(), "the can is drawn")
	# the match's seed is the clock's: a thrower bot waits up to two seconds, aims and throws, so watch for any bot doing anything for a while
	var acted: bool = false
	for i: int in 240:
		practice.advance(DT)
		for id: int in PracticeMatch.BOT_ALLY_IDS + PracticeMatch.BOT_ENEMY_IDS:
			acted = acted or practice.sim.players[id].position != practice.sim.players[id].spawn_position
		acted = acted or not game.hold.is_empty() or not game.flights.is_empty() or not game.slippers.is_empty()
	assert_true(acted, "the bots play")
	assert_lt(practice.sim.clock_left(), 300.0, "the five-minute clock runs")


func test_a_patintero_practice_draws_the_court_two_horizontal_lines_and_a_vertical_one() -> void:
	var practice: PracticeMatch = _practice(&"patintero")
	assert_true(practice.sim.mode is Patintero)
	assert_eq(practice.mode_view.guard_line_count(), 3, "two horizontal lines and the vertical one")
	assert_null(practice.mode_view.can_node())
	var lines: Array[Node] = practice.mode_view.find_children("*", "MeshInstance3D", true, false).filter(func(n: Node) -> bool: return (n as MeshInstance3D).mesh is PlaneMesh)
	assert_eq(lines.size(), 3, "only the court: its first and last line are the edges, with no narrowing lines or tinted patches")
	for line: Node in lines:
		var chalk: Color = (((line as MeshInstance3D).mesh as PlaneMesh).material as ShaderMaterial).get_shader_parameter("chalk") as Color
		assert_eq(Color(chalk, 1.0), Color.WHITE, "white chalk, every line")
	var turned: int = 0
	for line: Node in lines:
		if is_equal_approx(absf((line as MeshInstance3D).rotation.y), PI / 2.0):
			turned += 1
	assert_eq(turned, 1, "one of them runs along the street: the vertical line")


func test_a_patintero_runner_on_a_sidewalk_has_a_centred_knob_and_no_dive_and_the_camera_turns_when_the_roles_swap() -> void:
	var practice: PracticeMatch = _practice(&"patintero")
	var lines: Patintero = practice.sim.mode as Patintero
	var local: int = practice.local_id
	assert_true(lines.is_attacker(practice.sim.players[local]), "you run first")
	var own: int = practice.own_side()
	lines.jailed[local] = 1
	practice.sim.players[local].position = Vector2(18.0, 0.0)
	practice.advance(DT)
	assert_true(practice.hud.street_pad.knob_centered, "a tagged runner steers with a centred knob")
	assert_eq(practice.hud.street_pad.action, StreetGamePad.Action.NONE, "and has no dive button")
	lines.jailed.erase(local)
	practice.sim.players[local].position = Vector2(0.0, lines.rules.home_z + 1.0)
	practice.advance(DT)
	assert_false(practice.hud.street_pad.knob_centered)
	# the guards' view is the other way round, like Agawan Base
	lines.attack_team = 1 - lines.attack_team
	assert_ne(practice.own_side(), own, "the camera turns when your team guards")


func test_the_street_games_have_no_hand_no_tray_and_no_cards() -> void:
	for game: StringName in [&"tumbang_preso", &"patintero"]:
		var practice: PracticeMatch = _practice(game)
		for i: int in 3:
			practice.advance(DT)
		assert_false(practice.hud.hand.visible, "%s: no hand" % game)
		assert_false(practice.hud.console.visible, "%s: no card tray" % game)
		assert_true(practice.hud.street_pad.visible, "%s: the knob and the buttons" % game)
		assert_eq(practice.hud.console_height(), MatchHud.STREET_TRAY + practice.hud.bottom_inset, "%s: only room for the knob" % game)
		for id: int in practice.sim.players:
			assert_eq(practice.sim.players[id].weapons.size(), 0, "%s: nobody holds a card" % game)


func test_the_electric_posts_stand_in_the_same_corners_in_every_game() -> void:
	var layout: MapLayout = load("res://data/rules/map_layout.tres") as MapLayout
	for game: StringName in [&"street", &"tumbang_preso", &"patintero"]:
		var practice: PracticeMatch = _practice(game)
		practice.advance(DT)
		assert_eq(practice._map.visible_posts(), 2, "%s: both electric posts" % game)
		for side: int in [MapLayout.SIDE_OWN, MapLayout.SIDE_ENEMY]:
			var post: Node3D = practice._map._posts[side]
			var corner: Vector2 = layout.post_position(side)
			assert_almost_eq(post.global_position.x, corner.x, 0.01, "%s: the post of side %d" % [game, side])
			assert_almost_eq(post.global_position.z, corner.y, 0.01)


func test_the_vignette_stays_behind_the_top_boundary_pictures_in_every_game() -> void:
	for game: StringName in [&"street", &"tumbang_preso", &"patintero"]:
		var practice: PracticeMatch = _practice(game)
		practice.advance(DT)
		var film: FilmFinish = practice.find_children("*", "FilmFinish", true, false)[0] as FilmFinish
		var clear: float = film.finish.get_shader_parameter("clear_above") as float
		var row: float = practice._camera.screen_position(Vector3(0.0, 0.0, StreetMap.top_end(practice.own_side()) * (practice.layout.lane_length / 2.0 + StreetMap.BARRIER_SETBACK))).y / practice.get_viewport().get_visible_rect().size.y
		assert_gt(clear, 0.03, "%s: no vignette over the top pictures" % game)
		assert_lt(clear, 0.35, "%s: only over them, not the street" % game)
		assert_almost_eq(clear, row, 0.001, "%s: from the line they stand on" % game)


func test_the_street_games_frame_the_street_exactly_like_agawan_base() -> void:
	var agawan: PracticeMatch = _practice(&"agawan_base")
	agawan.advance(DT)
	var reference: float = agawan._camera.size
	for game: StringName in [&"tumbang_preso", &"patintero"]:
		var practice: PracticeMatch = _practice(game)
		practice.advance(DT)
		assert_eq(practice._camera.bottom_reserve_px, agawan._camera.bottom_reserve_px, "%s: the same room kept for the controls" % game)
		assert_almost_eq(practice._camera.size, reference, 0.001, "%s: the same zoom as Agawan Base" % game)


func test_the_quick_chat_is_under_the_timer_and_lists_its_phrases_vertically() -> void:
	for game: StringName in [&"tumbang_preso", &"patintero"]:
		var practice: PracticeMatch = _practice(game)
		practice.advance(DT)
		var hud: MatchHud = practice.hud
		assert_true(hud.chat.visible, "%s: chat is there" % game)
		assert_gte(hud.chat.get_global_rect().position.y, hud.clock_chip.get_global_rect().end.y - 1.0, "%s: under the timer panel" % game)
		assert_almost_eq(hud.chat.get_global_rect().end.x, hud.clock_chip.get_global_rect().end.x - 0.0, MatchHud.MARGIN + 2.0, "%s: at the right edge" % game)
		hud.chat.set_open(true)
		await wait_frames(2)
		assert_eq(hud.chat.menu.columns, 1, "%s: one phrase under the other" % game)
		var rows: Array[float] = []
		for pick: Button in hud.chat.phrase_buttons:
			rows.append(pick.get_global_rect().position.y)
		var sorted: Array[float] = rows.duplicate()
		sorted.sort()
		assert_eq(rows, sorted, "%s: top to bottom" % game)
		assert_gt(rows[-1], rows[0] + UiMetrics.TOUCH * 6.0, "%s: a column" % game)
		hud.chat.set_open(false)


func test_tumbang_preso_shows_every_kids_points_on_the_left_sidewalk_most_first_and_the_rows_slide() -> void:
	var practice: PracticeMatch = _practice(&"tumbang_preso")
	var game: TumbangPreso = practice.sim.mode as TumbangPreso
	var column: ScoreColumn = practice.hud.score_column
	assert_not_null(column, "the column is there")
	assert_false(practice.hud.scoreboard.visible, "no two-team score in a free-for-all")
	practice.advance(DT)
	assert_lt(column.get_global_rect().end.x, 120.0, "on the left sidewalk")
	assert_eq(column.order().size(), 6, "a row for every kid")
	game.points[3] = 2
	game.points[5] = 1
	practice.advance(DT)
	assert_eq(column.order().slice(0, 3), [3, 5, 1] as Array[int], "most points first, then by id")
	assert_eq(column.points_text(3), "2")
	assert_eq(column.target_y(3), 0.0)
	assert_eq(column.target_y(5), ScoreColumn.ROW_HEIGHT, "descending order")
	game.points[5] = 3
	practice.advance(DT)
	assert_eq(column.order().slice(0, 2), [5, 3] as Array[int], "the kid that passed moves up")
	var row: Control = column.get_node("Row3") as Control
	assert_eq(column.target_y(3), ScoreColumn.ROW_HEIGHT, "it is on its way to the second place")
	await wait_seconds(ScoreColumn.SLIDE_TIME + 0.2)
	assert_almost_eq(row.position.y, ScoreColumn.ROW_HEIGHT, 0.5, "and arrives")


func test_the_knob_walks_you_and_the_buttons_follow_what_you_hold() -> void:
	var practice: PracticeMatch = _practice(&"tumbang_preso")
	var game: TumbangPreso = practice.sim.mode as TumbangPreso
	var pad: StreetGamePad = practice.hud.street_pad
	practice.advance(DT)
	assert_eq(pad.action, StreetGamePad.Action.THROW, "you start with your slipper: the Throw button")
	assert_true(pad.knob_rect().end.x < pad.button_rect().position.x, "knob at the left, button at the right")
	var from: Vector2 = practice.sim.players[1].position
	pad.knob_moved.emit(Vector2(1.0, 0.0))
	for i: int in 15:
		practice.advance(DT)
	assert_gt(practice.sim.players[1].position.x, from.x + 1.0, "pushed right: walks right")
	pad.knob_moved.emit(Vector2(0.0, -1.0))
	for i: int in 60:
		practice.advance(DT)
	assert_almost_eq(practice.sim.players[1].position.y, TUMBANG_HOME, 0.05, "the throw line holds you while you have your slipper")
	pad.knob_moved.emit(Vector2.ZERO)
	# without the slipper the button is Kick
	game.slippers[1] = Vector2(0.0, 5.0)
	practice.advance(DT)
	assert_eq(pad.action, StreetGamePad.Action.KICK, "no slipper in hand: Kick")
	# as the taya there is no button
	game.become_taya(practice.sim, 1, &"tagged")
	practice.advance(DT)
	assert_eq(pad.action, StreetGamePad.Action.NONE)
	assert_eq(practice.hud.street_pad.title_label.text, "YOU'RE IT!")


func test_the_kick_button_kicks_the_can() -> void:
	var practice: PracticeMatch = _practice(&"tumbang_preso")
	var game: TumbangPreso = practice.sim.mode as TumbangPreso
	practice.advance(DT)
	game.slippers[1] = Vector2(0.0, 5.0)
	game.slipper_busy[1] = 0.0
	practice.sim.players[1].position = game.rules.can_spot + Vector2(1.0, 0.0)
	practice.advance(DT)
	practice.hud.street_pad.action_pressed.emit()
	for i: int in 3:
		practice.advance(DT)
	assert_false(game.can_up, "kicked")


func test_holding_the_throw_button_aims_on_the_ground_with_a_line_that_grows_and_a_turn_that_speeds_up() -> void:
	var practice: PracticeMatch = _practice(&"tumbang_preso")
	var game: TumbangPreso = practice.sim.mode as TumbangPreso
	practice.advance(DT)
	var pad: StreetGamePad = practice.hud.street_pad
	pad.aim_started.emit()
	practice.advance(DT)
	practice.advance(DT)
	assert_true(game.is_aiming(practice.local_id), "the sim sees the button held")
	assert_true(practice.mode_view.aim_visible(), "the aim line is drawn")
	var short: float = practice.mode_view.aim_length()
	assert_gt(short, 0.0)
	# a drag turns the aim a little while the line is short ...
	var angle: float = practice._throw_angle
	pad.aim_dragged.emit(100.0)
	var fine_turn: float = absf(practice._throw_angle - angle)
	assert_gt(fine_turn, 0.0)
	# ... and much more at full length
	for i: int in 60:
		practice.advance(DT)
	assert_eq(game.charge(practice.local_id), 1.0)
	assert_gt(practice.mode_view.aim_length(), short + 5.0, "the line grew with the hold")
	angle = practice._throw_angle
	pad.aim_dragged.emit(100.0)
	assert_gt(absf(practice._throw_angle - angle), fine_turn * 2.5, "high sensitivity at the longest")
	# the line lies on the ground: tested against the depth (a kid in front of it hides it) and a hand's breadth above the street
	var line: MeshInstance3D = practice.mode_view.find_child("AimLine", true, false) as MeshInstance3D
	assert_false(((line.mesh as BoxMesh).material as StandardMaterial3D).no_depth_test, "never drawn over a kid")
	assert_lt(line.position.y, 0.15, "on the ground")
	pad.aim_released.emit(false)
	for i: int in 3:
		practice.advance(DT)
	assert_false(game.is_aiming(practice.local_id))
	assert_false(game.has_slipper(practice.local_id), "the slipper flew")


func test_aiming_at_the_can_shows_a_short_solid_line_for_the_can_and_a_dashed_one_for_the_slipper() -> void:
	var practice: PracticeMatch = _practice(&"tumbang_preso")
	var game: TumbangPreso = practice.sim.mode as TumbangPreso
	practice._mode_coach.ids.clear()  # the bots do not throw meanwhile: the can stands
	practice.advance(DT)
	practice.sim.players[1].position = Vector2(game.rules.can_spot.x, 14.0)
	practice.hud.street_pad.aim_started.emit()
	for i: int in 50:
		practice.advance(DT)
	assert_true(practice.mode_view.guides_visible(), "the aim reaches the can")
	var solid: MeshInstance3D = practice.mode_view.find_child("CanGuide", true, false) as MeshInstance3D
	assert_almost_eq(solid.scale.x, game.rules.guide_length, 0.01, "only a short guide")
	var dashes: Array[Node] = practice.mode_view.find_children("SlipperGuide*", "MeshInstance3D", true, false)
	assert_eq(dashes.size(), StreetModeView.GUIDE_DASHES, "dashed, not solid")
	assert_lt((dashes[0] as MeshInstance3D).scale.x * StreetModeView.GUIDE_DASHES * 2.0, game.rules.guide_length + 0.01, "just as short")
	assert_ne((solid.mesh as BoxMesh).material, (dashes[0] as MeshInstance3D).mesh.material, "a different style for each")
	# aimed wide there is no guide, but the line is still there
	practice.hud.street_pad.aim_dragged.emit(300.0)
	practice.advance(DT)
	assert_false(practice.mode_view.guides_visible())
	assert_true(practice.mode_view.aim_visible())


func test_dragging_back_onto_the_button_cancels_the_throw() -> void:
	var practice: PracticeMatch = _practice(&"tumbang_preso")
	var game: TumbangPreso = practice.sim.mode as TumbangPreso
	practice.advance(DT)
	practice.hud.street_pad.aim_started.emit()
	for i: int in 5:
		practice.advance(DT)
	practice.hud.street_pad.aim_released.emit(true)
	for i: int in 3:
		practice.advance(DT)
	assert_true(game.has_slipper(practice.local_id), "nothing was thrown")


func test_only_your_own_slipper_has_the_white_pickup_ring_and_the_taya_is_marked() -> void:
	var practice: PracticeMatch = _practice(&"tumbang_preso")
	var game: TumbangPreso = practice.sim.mode as TumbangPreso
	practice.advance(DT)
	assert_false(practice.mode_view.pickup_visible(), "your slipper is in your hand")
	game.slippers[2] = Vector2(3.0, 5.0)
	game.slipper_busy[2] = 0.0
	practice.advance(DT)
	assert_false(practice.mode_view.pickup_visible(), "somebody else's slipper has no ring")
	game.slippers[1] = Vector2(-3.0, 5.0)
	game.slipper_busy[1] = 0.0
	practice.advance(DT)
	assert_true(practice.mode_view.pickup_visible(), "yours has")
	var ring: Node3D = practice.mode_view.find_child("PickupRing", true, false) as Node3D
	assert_eq(ring.position.x, -3.0)
	var tint: MeshInstance3D = ring.get_node("Tint") as MeshInstance3D
	assert_almost_eq((tint.mesh as CylinderMesh).top_radius, game.rules.pickup_radius, 0.001, "as big as the pick-up area")
	assert_gt(game.rules.pickup_radius, 0.4, "a little bigger than the slipper itself")
	game.slipper_busy[1] = 0.5
	practice.advance(DT)
	assert_false(practice.mode_view.pickup_visible(), "not while it is still flying")
	var taya: PlayerState = practice.sim.players[game.taya_id]
	assert_eq(practice.find_children("It", "Label3D", true, false).size(), 0, "no word IT over the taya's head (owner)")
	practice.advance(DT)
	var hud: OverheadHud = practice.hud.overhead
	assert_eq(hud.badge_color(taya), TeamColors.world_enemy(), "the taya is the one red kid")
	assert_eq(hud.bar_color(taya), TeamColors.bar_enemy())
	for id: int in practice.sim.players:
		if id != game.taya_id:
			assert_eq(hud.badge_color(practice.sim.players[id]), TeamColors.world_own(), "everyone else keeps the blue badge")


func test_a_semi_transparent_white_ring_marks_where_the_can_belongs_and_brightens_while_it_is_down() -> void:
	var practice: PracticeMatch = _practice(&"tumbang_preso")
	var game: TumbangPreso = practice.sim.mode as TumbangPreso
	practice.advance(DT)
	var ring: MeshInstance3D = practice.mode_view.find_child("CanSpotRing", true, false) as MeshInstance3D
	assert_not_null(ring, "the ring is there")
	assert_almost_eq(ring.position.x, game.rules.can_spot.x, 0.001)
	assert_almost_eq(ring.position.z, game.rules.can_spot.y, 0.001, "on the can's spot")
	assert_eq(game.rules.can_spot, Vector2.ZERO, "in the middle of the street")
	var material: StandardMaterial3D = (ring.mesh as TorusMesh).material as StandardMaterial3D
	assert_eq(Color(material.albedo_color, 1.0), Color.WHITE, "white")
	var standing: float = material.albedo_color.a
	assert_between(standing, 0.2, 0.7, "semi-transparent")
	assert_almost_eq((ring.mesh as TorusMesh).outer_radius, game.rules.place_radius, 0.001, "as wide as the place the can is put back to")
	game.can_up = false
	practice.advance(DT)
	assert_gt(material.albedo_color.a, standing, "brighter while the can has to be carried back")


func test_a_taya_that_picks_the_can_up_scoops_it_with_both_hands_and_sets_it_down_on_the_ring() -> void:
	var practice: PracticeMatch = _practice(&"tumbang_preso")
	var game: TumbangPreso = practice.sim.mode as TumbangPreso
	practice.advance(DT)
	game.taya_id = practice.local_id  # you stand still when nobody touches the knob
	for id: int in practice.sim.players:
		if id != practice.local_id:
			practice.sim.players[id].position = Vector2(-10.0 + float(id) * 3.0, -20.0)  # the bots are far away: nobody throws at the can
	var taya: int = game.taya_id
	var view: KidModel = practice._views[taya] as KidModel
	var spot: Vector2 = game.rules.can_spot
	game.can_up = false
	game.can_position = Vector2(-4.0, 5.0)
	game.can_busy_left = 0.0
	practice.sim.players[taya].position = Vector2(-4.0, 5.3)
	practice.advance(DT)
	practice.advance(DT)
	assert_eq(game.can_carrier, taya)
	assert_true(view.is_acting(), "the pick-up plays")
	assert_true(view.has_can())
	for i: int in 40:
		practice.advance(DT)
	assert_false(view.is_acting())
	assert_true(view.has_can(), "carrying")
	assert_false(practice.mode_view.can_node() == null)
	var held: Dictionary = view.can_hold()
	assert_almost_eq(practice.mode_view.can_node().position.x, (held["position"] as Vector3).x, 0.01, "the can is drawn where the hands hold it")
	# set it down on its spot
	practice.sim.players[taya].position = spot + Vector2(0.0, 0.3)
	practice.advance(DT)
	practice.advance(DT)
	assert_true(game.can_up, "the sim put it back")
	assert_true(view.has_can(), "the kid is still setting it down")
	for i: int in 30:
		practice.advance(DT)
	assert_false(view.has_can())
	assert_almost_eq(practice.mode_view.can_node().position.x, spot.x, 0.01, "and it stands on the ring")


func test_a_kick_plays_the_kick_and_the_can_flies_when_the_foot_meets_it() -> void:
	var practice: PracticeMatch = _practice(&"tumbang_preso")
	var game: TumbangPreso = practice.sim.mode as TumbangPreso
	practice.advance(DT)
	var kicker: int = practice.local_id
	if game.taya_id == kicker:
		game.taya_id = 3
	for id: int in practice.sim.players:
		if id != kicker:
			practice.sim.players[id].position = Vector2(-10.0 + float(id) * 3.0, -20.0)
	var view: KidModel = practice._views[kicker] as KidModel
	var from: Vector2 = game.can_position
	practice.sim.players[kicker].position = from + Vector2(0.0, -1.0)
	game.slippers[kicker] = Vector2(5.0, 8.0)
	game.kick(practice.sim, practice.sim.players[kicker])
	practice.advance(DT)
	assert_true(view.is_kicking(), "the kicker kicks")
	assert_false(practice.mode_view.can_flying() and practice.mode_view.can_node().position.distance_to(Vector3(from.x, 0.7, from.y)) > 0.5, "the can has not left yet: it waits for the foot")
	var struck: bool = false
	for i: int in 40:
		practice.advance(DT)
		struck = struck or not view.is_kicking() or practice.mode_view.can_flying()
	assert_true(struck)
	assert_true(practice.mode_view.can_flying() or game.can_busy_left <= 0.0, "it flew after the strike")


func test_a_flung_can_and_slipper_hop_to_where_they_lie() -> void:
	var practice: PracticeMatch = _practice(&"tumbang_preso")
	var game: TumbangPreso = practice.sim.mode as TumbangPreso
	practice.advance(DT)
	var path: Array[Vector2] = [Vector2(-3.0, -2.0), Vector2(-4.0, -3.0)] as Array[Vector2]
	game.can_up = false
	game.can_position = path[-1]
	game.slippers[1] = Vector2(4.0, 4.0)
	game.can_flung.emit(game.rules.can_spot, path)
	game.slipper_flung.emit(1, game.rules.can_spot, [Vector2(4.0, 4.0)] as Array[Vector2])
	practice.mode_view.sync(game.rules.fling_time * 0.5)
	assert_true(practice.mode_view.can_flying(), "the can is in the air")
	assert_gt(practice.mode_view.can_node().position.y, 0.3)
	assert_gt(practice.mode_view.slipper_node(1).position.y, 0.1, "and so is the slipper")
	practice.mode_view.sync(game.rules.fling_time * 2.0)
	practice.mode_view.sync(0.5)
	assert_false(practice.mode_view.can_flying(), "two hops later it lies")
	assert_almost_eq(practice.mode_view.can_node().position.z, -3.0 + -1.0 + 0.0, 1.5)
	assert_almost_eq(practice.mode_view.slipper_node(1).position.x, 4.0, 0.01)
	assert_almost_eq(practice.mode_view.slipper_node(1).position.y, 0.0, 0.01, "on the ground")


func test_the_can_uses_the_lata_model() -> void:
	var practice: PracticeMatch = _practice(&"tumbang_preso")
	assert_not_null(practice.mode_view.can_node().find_child("Squash", true, false), "the weapons' rig around the lata's model")


func test_the_lines_are_chalk_that_breaks_up_in_short_gaps() -> void:
	var practice: PracticeMatch = _practice(&"tumbang_preso")
	var lines: Array[Node] = practice.mode_view.find_children("*", "MeshInstance3D", true, false).filter(func(n: Node) -> bool: return (n as MeshInstance3D).mesh is PlaneMesh)
	assert_eq(lines.size(), 2, "the throw line and the far line")
	for line: Node in lines:
		var material: ShaderMaterial = ((line as MeshInstance3D).mesh as PlaneMesh).material as ShaderMaterial
		assert_eq(material.shader.resource_path, "res://shaders/chalk_line.gdshader")
		assert_gt(material.get_shader_parameter("breaks") as float, 0.0, "gaps in the chalk")
		assert_lte(material.get_shader_parameter("breaks") as float, 0.35, "but narrow ones: it is a boundary")
	assert_ne(((lines[0] as MeshInstance3D).mesh as PlaneMesh).material.get_shader_parameter("seed"), ((lines[1] as MeshInstance3D).mesh as PlaneMesh).material.get_shader_parameter("seed"), "each line breaks in its own places")


func test_throwers_in_the_fetch_area_have_a_tag_circle_only_the_taya_sees() -> void:
	var practice: PracticeMatch = _practice(&"tumbang_preso")
	var game: TumbangPreso = practice.sim.mode as TumbangPreso
	practice.advance(DT)
	assert_true(practice.mode_view.tag_circle_ids().is_empty(), "you are a thrower: you see no circles")
	# the taya's point of view: you are it, and two throwers are out in the fetch area
	game.taya_id = 1
	game.hold.clear()
	for id: int in [2, 3]:
		game.slippers[id] = Vector2(float(id), 5.0)  # they have thrown: free to be out in the fetch area
		game.slipper_busy[id] = 0.0
	practice._mode_coach.ids.clear()
	practice.sim.players[2].position = Vector2(-4.0, 6.0)
	practice.sim.players[3].position = Vector2(3.0, 8.0)
	practice.sim.players[4].position = Vector2(0.0, 15.0)  # still in the throwing area
	game.grace_left = 0.0
	practice.advance(DT)
	var ids: Array[int] = practice.mode_view.tag_circle_ids()
	ids.sort()
	assert_eq(ids, [2, 3] as Array[int], "a circle on each thrower in the fetch area, none in the throwing area")
	assert_true(practice.mode_view.tag_circle_active(2), "red: a touch tags")
	var circle: Node3D = practice.mode_view.find_child("TagCircle", true, false) as Node3D
	var tint: MeshInstance3D = circle.get_node("Tint") as MeshInstance3D
	assert_almost_eq((tint.mesh as CylinderMesh).top_radius, game.rules.tag_circle_radius, 0.001, "a medium-size circle")
	assert_gt(game.rules.tag_circle_radius, 0.8)
	game.can_up = false
	practice.advance(DT)
	assert_false(practice.mode_view.tag_circle_active(2), "grey while the can is down: it cannot tag")


func test_a_tag_plays_the_touch_for_the_tagger_and_then_the_round_resets() -> void:
	var practice: PracticeMatch = _practice(&"tumbang_preso")
	var game: TumbangPreso = practice.sim.mode as TumbangPreso
	var taya: int = game.taya_id
	practice.advance(DT)
	game.grace_left = 0.0
	practice.sim.players[1].position = Vector2(0.0, 6.0)
	game.slippers[1] = Vector2(0.0, 5.0)
	practice.sim.players[taya].position = Vector2(0.0, 6.5)
	practice.sim.step(DT)
	assert_eq(game.taya_id, 1)
	for i: int in 12:
		practice.advance(DT)  # the pause: the tagger stands still, its touch keeps playing
	var view: KidModel = practice._views[taya] as KidModel
	assert_eq(view.pose, "cast", "the taya still reaches out and touches while everyone stands still")
	assert_string_contains(practice.banner_text(), "IT")
	for i: int in ceili(game.rules.reset_seconds / DT):
		practice.advance(DT)
	assert_false(game.is_resetting())
	assert_true(game.at_home(practice.sim.players[taya].position), "the old taya is back among the throwers")


func test_patintero_has_the_same_hud_with_a_dive_button_whose_ring_fills_while_it_cools_down() -> void:
	var practice: PracticeMatch = _practice(&"patintero")
	var lines: Patintero = practice.sim.mode as Patintero
	var pad: StreetGamePad = practice.hud.street_pad
	practice.advance(DT)
	practice._mode_coach.ids.clear()  # the bots do not act: the dive stays the one thing in the test
	for id: int in practice.sim.players:
		if id != practice.local_id:
			practice.sim.set_input(id, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, 0, 0))
	lines.dive_phase.clear()
	assert_false(practice.hud.hand.visible)
	assert_false(practice.hud.console.visible, "no tray")
	assert_null(practice.hud.score_column, "no kids' column: Patintero is a team game")
	assert_true(practice.hud.scoreboard.visible, "the team score is at the top centre, like Agawan Base")
	assert_eq(pad.action, StreetGamePad.Action.DIVE, "the button at the right is Dive")
	assert_true(pad.act_ready)
	assert_eq(pad.charge, 1.0, "ready: the ring is full")
	assert_gt(pad.knob_rect().size.x, 100.0, "and the knob at the left")
	assert_true(practice.hud.chat.visible, "chat under the timer")
	pad.knob_moved.emit(Vector2(1.0, 0.0))  # along the base, away from the guards
	pad.action_pressed.emit()
	for i: int in 4:
		practice.advance(DT)
	pad.knob_moved.emit(Vector2.ZERO)  # the knob is let go: the kid does not run into the guards afterwards
	assert_true(lines.is_diving(practice.local_id), "pressing Dive dives")
	assert_false(pad.act_ready, "busy")
	assert_eq(pad.charge, 0.0, "the ring is empty while the kid dives and gets up: the cooldown has not started")
	for i: int in ceili((lines.rules.dive_time + lines.rules.dive_recover) / DT) + 3:
		practice.advance(DT)
	assert_false(lines.is_busy(practice.local_id), "standing again: now it cools down")
	for i: int in ceili(lines.rules.dive_cooldown / 2.0 / DT):
		practice.advance(DT)
	assert_between(pad.charge, 0.35, 0.7, "and the ring fills as it cools down")
	for i: int in ceili(lines.rules.dive_cooldown / 2.0 / DT) + 4:
		practice.advance(DT)
	assert_true(pad.act_ready)
	assert_eq(pad.charge, 1.0)


func test_patintero_shows_the_team_score_at_the_top_centre_like_agawan_base() -> void:
	var patintero: PracticeMatch = _practice(&"patintero")
	patintero.advance(DT)
	var board: Scoreboard = patintero.hud.scoreboard
	var middle: float = patintero.hud.get_global_rect().get_center().x
	assert_almost_eq(board.get_global_rect().get_center().x, middle, 2.0, "centred on the screen")
	assert_lt(board.get_global_rect().end.y, patintero.hud.header_height() + 20.0, "in the header")
	patintero.sim.score.points[0] = 2
	patintero.sim.score.points[1] = 1
	patintero.advance(DT)
	assert_eq([board.mine, board.theirs], [2, 1], "your team's points on the left, theirs on the right")
	assert_eq(patintero.sim.score.first_to, 0, "no first-to: a street game is played against the clock")
	assert_false(board.accessibility_name.contains("first to"), "and it does not say so")
	# the same place and the same card as Agawan Base's
	var base: PracticeMatch = _practice(&"street")
	base.advance(DT)
	assert_eq(board.get_global_rect().position, base.hud.scoreboard.get_global_rect().position, "exactly where Agawan Base has it")
	assert_eq(board.get_global_rect().size, base.hud.scoreboard.get_global_rect().size)


func test_a_dive_crouches_leaps_slides_on_its_belly_and_gets_up() -> void:
	# the script by itself
	var crouch: Dictionary = KidModel.dive_step(0.05)
	assert_eq(crouch["clip"], "Airborne")
	assert_lt(crouch["squash"] as float, 1.0, "a crouch first")
	var leap: Dictionary = KidModel.dive_step(0.25)
	assert_eq(leap["clip"], "Airborne", "the leap, arms out")
	assert_lt(leap["lean"] as float, -0.5, "leaning forward")
	assert_gt(leap["lift"] as float, 0.2, "off the street")
	var slide: Dictionary = KidModel.dive_step(0.45)
	assert_eq(slide["clip"], "Tripped", "then flat on its belly")
	assert_eq(slide["lean"], 0.0, "the clip is already flat: no lean on top of it")
	assert_eq(slide["lift"], 0.0, "on the street")
	assert_between(slide["time"] as float, KidModel.DIVE_SLIDE_FROM, KidModel.DIVE_SLIDE_TO)
	var rise: Dictionary = KidModel.dive_step(0.8)
	assert_gt(rise["time"] as float, KidModel.DIVE_SLIDE_TO, "the get-up is the rest of the clip")
	assert_almost_eq(KidModel.dive_step(1.0)["time"] as float, KidModel.DIVE_GETUP_TO, 0.001)
	var last: float = -1.0
	for step: int in 21:
		var at: Dictionary = KidModel.dive_step(float(step) / 20.0)
		if (at["clip"] as String) == "Tripped":
			assert_gte(at["time"] as float, last, "the fall clip only moves forward")
			last = at["time"] as float
	# on a kid in a match: the burst and the get-up are one animation
	var practice: PracticeMatch = _practice(&"patintero")
	var lines: Patintero = practice.sim.mode as Patintero
	practice.advance(DT)
	var view: KidModel = practice._views[practice.local_id] as KidModel
	var total: float = lines.rules.dive_time + lines.rules.dive_recover
	practice.hud.street_pad.knob_moved.emit(Vector2(0.0, -1.0))
	practice.hud.street_pad.action_pressed.emit()
	practice.advance(DT)
	practice.hud.street_pad.knob_moved.emit(Vector2.ZERO)
	var poses: Array[String] = []
	var leaned: float = 0.0
	for i: int in ceili(total / DT) - 2:
		practice.advance(DT)
		poses.append(view.pose)
		leaned = minf(leaned, (view.get_node("Body") as Node3D).rotation.x)
	assert_eq(poses[0], "dive")
	assert_eq(poses[-1], "dive", "still getting up at the end of the burst and after it")
	assert_lt(leaned, -0.5, "it leaned into the leap")
	for i: int in 6:
		practice.advance(DT)
	assert_ne(view.pose, "dive", "over")
	assert_eq((view.get_node("Body") as Node3D).rotation.x, 0.0, "upright again")
	assert_eq((view.get_node("Body") as Node3D).position.y, 0.0)
	assert_eq(view._player.speed_scale, 1.0, "its clips play on their own again")
	# the kid could not steer the get-up
	assert_gt(practice.sim.players[practice.local_id].cast_lock_left, -0.001)


func test_a_kid_that_is_caught_stumbles_and_falls_and_one_that_dives_keeps_its_dive() -> void:
	var practice: PracticeMatch = _practice(&"patintero")
	var lines: Patintero = practice.sim.mode as Patintero
	practice.advance(DT)
	practice._mode_coach.ids.clear()  # the bots stand still: a dive of the guard's would take it out of the circle
	for id: int in practice.sim.players:
		practice.sim.set_input(id, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, 0, 0))
	lines.dive_phase.clear()  # (a bot guard may have dived on the first tick)
	practice.sim.players[4].position = Vector2(0.0, lines.rules.line_span)
	lines.grace_left = 0.0
	var runner: KidModel = practice._views[practice.local_id] as KidModel
	practice.sim.players[practice.local_id].position = Vector2(0.4, lines.rules.line_span + 0.6)  # in the circle of the guard on the near line
	practice.sim.step(DT)
	practice.advance(DT)
	assert_true(runner.is_tripping(), "caught: it stumbles")
	assert_eq(runner.pose, "tripped")
	for i: int in ceili(lines.rules.reset_seconds / DT) + 4:
		practice.advance(DT)
	assert_false(runner.is_tripping(), "and is back on its feet at the new round")
	# a dive in progress is not cut by a stumble
	var kid: KidModel = KidModel.new()
	kid.play_dive(0.9)
	kid.play_tripped(1.3)
	assert_false(kid.is_tripping())
	assert_gt(kid.dive_left(), 0.0)
	kid.free()


func test_the_results_card_names_the_winner_of_a_street_game() -> void:
	for game: StringName in [&"tumbang_preso", &"patintero"]:
		var practice: PracticeMatch = _practice(game)
		practice.advance(DT)
		if practice.sim.mode is TumbangPreso:
			(practice.sim.mode as TumbangPreso).points[practice.local_id] = 4
		else:
			practice.sim.score.points[0] = 4
		practice.sim.mode.time_left = DT / 2.0
		for i: int in 6:
			practice.advance(DT)
		assert_eq(practice.sim.phase, MatchSim.Phase.MATCH_OVER, "%s: time is up" % game)
		assert_true(practice.hud.results.visible, "%s: the card shows" % game)
		assert_eq(practice.hud.results.title.text, "Victory", "%s: you led" % game)
		assert_string_contains(practice.hud.results.subtitle.text, practice.sim.mode.rules.title)
		assert_eq(practice.hud.results.mine_label.text, "4", "%s: your points" % game)


func test_the_street_games_have_no_difficulty_and_no_tilt_and_the_column_follows_the_faces() -> void:
	Session.menu_mode = PRACTICE_MODE
	for game: StringName in [&"tumbang_preso", &"patintero", &"street"]:
		Session.practice_game = game
		var screen: TitleScreen = _phone_screen()
		await wait_frames(2)
		var card: MenuDialog = screen.open_practice_confirm()
		await wait_frames(1)
		assert_eq(card.find_child("DifficultyChoice", true, false) != null, game == &"street", "%s: bots have levels only in Agawan Base" % game)
		card.close(false)
	# tilt to run never steers a street game
	var practice: PracticeMatch = _practice(&"tumbang_preso")
	var saved: bool = Settings.tilt_to_run
	Settings.tilt_to_run = true
	practice.steer_by_tilt(Vector3(6.0, 6.0, 2.0))
	assert_false(practice.tilt_steering)
	Settings.tilt_to_run = saved
	# a face swap at the start shows in the points column
	practice.advance(DT)
	var column: ScoreColumn = practice.hud.score_column
	var other: StringName = &"popoy" if practice.sim.players[2].character_id != &"popoy" else &"inday"
	practice.sim.players[2].character_id = other
	practice.advance(DT)
	assert_eq(column._pictures[2].texture, CharactersPage.card_picture(other), "the kid's character card, not a round face")
