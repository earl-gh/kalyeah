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
	assert_false(practice._map.bases_shown(), "no posts or bases in Tumbang Preso")
	assert_not_null(practice.mode_view.can_node(), "the can is drawn")
	for i: int in 90:
		practice.advance(DT)
	var moved: bool = false
	for id: int in PracticeMatch.BOT_ALLY_IDS + PracticeMatch.BOT_ENEMY_IDS:
		moved = moved or practice.sim.players[id].position != practice.sim.players[id].spawn_position
	assert_true(moved, "the bots play")
	assert_lt(practice.sim.clock_left(), 300.0, "the five-minute clock runs")


func test_a_patintero_practice_draws_the_court_two_horizontal_lines_and_a_vertical_one() -> void:
	var practice: PracticeMatch = _practice(&"patintero")
	assert_true(practice.sim.mode is Patintero)
	assert_eq(practice.mode_view.guard_line_count(), 3, "two horizontal lines and the vertical one")
	assert_null(practice.mode_view.can_node())
	var lines: Array[Node] = practice.mode_view.find_children("*", "MeshInstance3D", true, false).filter(func(n: Node) -> bool: return (n as MeshInstance3D).mesh is PlaneMesh)
	assert_eq(lines.size(), 5, "the start line, the far line and the three of the court")
	var turned: int = 0
	for line: Node in lines:
		if is_equal_approx(absf((line as MeshInstance3D).rotation.y), PI / 2.0):
			turned += 1
	assert_eq(turned, 1, "one of them runs along the street: the vertical line")


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
	assert_almost_eq(practice.mode_view.taya_marker_at().x, taya.position.x, 0.5, "the taya wears the ring and the word IT")


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
	assert_false(practice.hud.hand.visible)
	assert_false(practice.hud.console.visible, "no tray")
	assert_false(practice.hud.scoreboard.visible, "the points are on the left sidewalk, like Tumbang Preso's")
	assert_not_null(practice.hud.score_column)
	assert_eq(practice.hud.score_column.order().size(), 2, "one badge for each team")
	assert_eq(pad.action, StreetGamePad.Action.DIVE, "the button at the right is Dive")
	assert_true(pad.act_ready)
	assert_eq(pad.charge, 1.0, "ready: the ring is full")
	assert_gt(pad.knob_rect().size.x, 100.0, "and the knob at the left")
	assert_true(practice.hud.chat.visible, "chat under the timer")
	pad.knob_moved.emit(Vector2(0.0, -1.0))
	pad.action_pressed.emit()
	for i: int in 4:
		practice.advance(DT)
	pad.knob_moved.emit(Vector2.ZERO)  # the knob is let go: the kid does not run into the guards afterwards
	assert_true(lines.is_diving(practice.local_id), "pressing Dive dives")
	assert_false(pad.act_ready, "cooling down")
	assert_lt(pad.charge, 0.3, "the ring has just started to fill")
	for i: int in ceili(lines.rules.dive_cooldown / 2.0 / DT):
		practice.advance(DT)
	assert_between(pad.charge, 0.35, 0.75, "and fills as it cools down")
	for i: int in ceili(lines.rules.dive_cooldown / DT):
		practice.advance(DT)
	assert_true(pad.act_ready)


func test_patintero_points_stand_in_a_column_by_team_most_first() -> void:
	var practice: PracticeMatch = _practice(&"patintero")
	var column: ScoreColumn = practice.hud.score_column
	practice.advance(DT)
	assert_eq(column.order(), [ScoreColumn.TEAM_KEY, ScoreColumn.TEAM_KEY + 1] as Array[int], "your team first when level")
	practice.sim.score.points[1] = 2
	practice.advance(DT)
	assert_eq(column.order(), [ScoreColumn.TEAM_KEY + 1, ScoreColumn.TEAM_KEY] as Array[int], "the leading team goes up")
	assert_eq(column.points_text(ScoreColumn.TEAM_KEY + 1), "2")


func test_a_dive_leans_the_kid_into_the_air_slides_and_gets_up() -> void:
	# the pose by itself
	assert_lt(KidModel.dive_pose(0.05)["squash"] as float, 1.0, "a crouch first")
	var leap: Dictionary = KidModel.dive_pose(0.3)
	assert_lt(leap["lean"] as float, -0.3, "then it leans forward")
	assert_gt(leap["lift"] as float, 0.1, "and leaves the ground")
	var slide: Dictionary = KidModel.dive_pose(0.6)
	assert_almost_eq(slide["lean"] as float, -KidModel.DIVE_LEAN, 0.001, "flat along the street")
	assert_lt(slide["lift"] as float, leap["lift"] as float, "skimming it")
	assert_almost_eq(KidModel.dive_pose(1.0)["lean"] as float, 0.0, 0.001, "and upright again at the end")
	assert_almost_eq(KidModel.dive_pose(1.0)["lift"] as float, 0.0, 0.001)
	# on a kid in a match
	var practice: PracticeMatch = _practice(&"patintero")
	practice.advance(DT)
	var view: KidModel = practice._views[practice.local_id] as KidModel
	practice.hud.street_pad.knob_moved.emit(Vector2(0.0, -1.0))
	practice.hud.street_pad.action_pressed.emit()
	var poses: Array[String] = []
	var leaned: float = 0.0
	for i: int in 24:
		practice.advance(DT)
		poses.append(view.pose)
		leaned = minf(leaned, (view.get_node("Body") as Node3D).rotation.x)
	assert_has(poses, "dive", "the kid is in its dive pose")
	assert_lt(leaned, -0.8, "and really leans into it")
	assert_ne(view.pose, "dive", "it is over after the dive's time")
	assert_eq((view.get_node("Body") as Node3D).rotation.x, 0.0, "the body is upright again")
	assert_eq((view.get_node("Body") as Node3D).position.y, 0.0)
