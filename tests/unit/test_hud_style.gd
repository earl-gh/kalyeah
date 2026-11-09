extends GutTest
## The landscape match's HUD in one calm style (docs/audits/2026-11-09-landscape-hud.md): controls keep one margin from the safe edges and stand
## in the same places in every game, the header is drawn smaller, words use the three-size ramp in the navy ink and stand under the score (off
## your kid and off the far end of the street), the ice water bar sits with the arc, and far kids keep only their badge and bar.
## Seam: HudStyle, MoveStick, AbilityArc, StreetGamePad, MatchHudLayout, MatchFeedback, OverheadHud.

const PRACTICE: PackedScene = preload("res://scenes/match/practice.tscn")
const SCREENS: Array[Vector2] = [Vector2(1280.0, 720.0), Vector2(1560.0, 720.0)]
## Insets: none, and a notch on the left with a home bar.
const INSETS: Array[Vector4] = [Vector4.ZERO, Vector4(60.0, 0.0, 0.0, 24.0)]
const DT: float = 1.0 / 30.0
## How far the ice water drop reaches past its bar (it is drawn over the bar's left end).
const SLIM_DROP_HALF: float = AbilityArc.SLIM_DROP / 2.0

var _kind: StringName
var _game: StringName


func before_each() -> void:
	_kind = Session.practice_kind
	_game = Session.practice_game
	Session.practice_kind = &"match"


func after_each() -> void:
	Session.practice_kind = _kind
	Session.practice_game = _game


func _practice(game: StringName = &"street") -> PracticeMatch:
	Session.practice_game = game
	var practice: PracticeMatch = autofree(PRACTICE.instantiate()) as PracticeMatch
	add_child(practice)
	practice.set_process(false)
	practice.finish_pick()
	practice.skip_intro()
	for i: int in 3:
		practice.advance(DT)
	return practice


func test_the_joystick_and_the_arc_keep_one_margin_from_the_safe_edges() -> void:
	for screen: Vector2 in SCREENS:
		for insets: Vector4 in INSETS:
			var rest: Vector2 = MoveStick.rest_point(screen, Vector2(insets.x, insets.w))
			assert_almost_eq(rest.x - MoveStick.BASE_RADIUS, insets.x + HudStyle.EDGE, 0.01, "%s: the joystick's left edge" % screen)
			assert_almost_eq(screen.y - insets.w - (rest.y + MoveStick.BASE_RADIUS), HudStyle.EDGE, 0.01, "its bottom edge")
			var arc: AbilityArc = autofree(AbilityArc.new()) as AbilityArc
			add_child(arc)
			arc.size = screen
			arc.set_inset(Vector2(insets.z, insets.w))
			var right: float = 0.0
			var bottom: float = 0.0
			for rect: Rect2 in arc.card_rects:
				right = maxf(right, rect.end.x)
				bottom = maxf(bottom, rect.end.y)
			assert_almost_eq(screen.x - insets.z - right, HudStyle.EDGE, 0.01, "%s: the arc's right edge" % screen)
			assert_almost_eq(screen.y - insets.w - bottom, HudStyle.EDGE, 0.01, "the arc's bottom edge")


func test_the_ice_water_and_stamina_bars_sit_at_the_bottom_centre_and_the_cancel_x_above_the_arc() -> void:
	for screen: Vector2 in SCREENS:
		for inset: Vector2 in [Vector2.ZERO, Vector2(0.0, 24.0)]:
			var arc: AbilityArc = autofree(AbilityArc.new()) as AbilityArc
			add_child(arc)
			arc.size = screen
			arc.set_inset(inset)
			assert_almost_eq(arc.bar_rect.get_center().x, screen.x / 2.0, 0.01, "%s: the ice water bar is centred" % screen)
			assert_almost_eq(arc.stamina_rect.get_center().x, screen.x / 2.0, 0.01, "the stamina bar too")
			assert_lt(arc.bar_rect.end.y, arc.stamina_rect.position.y, "ice water over stamina")
			assert_almost_eq(screen.y - inset.y - arc.stamina_rect.end.y, HudStyle.EDGE, 0.01, "the HUD's margin from the bottom, over the home bar")
			assert_lt(arc.stamina_rect.size.y, arc.bar_rect.size.y, "the stamina bar is the thinner one")
			for rect: Rect2 in arc.card_rects:
				assert_false(rect.intersects(arc.bar_rect.grow(SLIM_DROP_HALF)), "the bars clear the cards")
				assert_lt(arc.cancel_center.y + AbilityArc.CANCEL_RADIUS, rect.position.y, "the cancel X stands above the arc")


func test_your_stamina_is_drawn_at_the_bottom_not_under_your_kid_in_agawang_base() -> void:
	var practice: PracticeMatch = _practice()
	var arc: AbilityArc = practice.hud.hand as AbilityArc
	assert_false(practice.hud.overhead.stamina_on_bar, "not under your kid's bar")
	assert_true(arc.stamina_shown, "under the ice water bar while you play")
	var me: PlayerState = practice.sim.players[practice.local_id]
	me.stamina = practice.sim.rules.stamina_max * 0.1
	practice.hud.sync_player(practice.sim, me, false)
	assert_almost_eq(arc.stamina_share, 0.1, 0.001)
	assert_eq(arc.stamina_color, OverheadHud.STAMINA_LOW_COLOR, "amber when low, as before")
	var street: PracticeMatch = _practice(&"patintero")
	assert_true(street.hud.overhead.stamina_on_bar, "the street games have no arc: their stamina stays under the kid's bar")


func test_the_street_games_controls_stand_where_the_joystick_and_the_arc_do() -> void:
	var practice: PracticeMatch = _practice(&"patintero")
	var pad: StreetGamePad = practice.hud.street_pad
	assert_true(pad.landscape)
	var screen: Vector2 = practice.hud.size
	var insets: Vector4 = pad.safe
	assert_eq(pad.knob_center(), MoveStick.rest_point(screen, Vector2(insets.x, insets.w)), "the knob rests where the joystick rests")
	assert_eq(pad.knob_radius(), MoveStick.BASE_RADIUS, "and is as big")
	assert_eq(pad.button_center(), AbilityArc.corner_point(screen, Vector2(insets.z, insets.w)), "the action button is the right thumb's home")
	assert_eq(pad.title_label.get_theme_font_size("font_size"), HudStyle.TITLE, "the role line in the ramp's title size")
	assert_eq(pad.hint_label.get_theme_font_size("font_size"), HudStyle.LABEL)
	assert_eq(pad.title_label.get_theme_color("font_outline_color"), HudStyle.INK, "lettered in the HUD's ink")
	assert_eq(pad.title_label.get_theme_constant("outline_size"), HudStyle.outline(HudStyle.TITLE))


func test_the_header_is_smaller_and_the_words_stand_under_it_above_the_tag_toast() -> void:
	var practice: PracticeMatch = _practice()
	var hud: MatchHud = practice.hud
	assert_eq(HudStyle.HEADER_SCALE, 0.65, "the top centre is small (owner)")
	assert_eq(hud.scoreboard.scale, Vector2.ONE * HudStyle.HEADER_SCALE, "the score at 65%")
	assert_null(hud.find_child("StreetMinimap", true, false), "no minimap (owner)")
	assert_eq(hud.clock_chip.scale, Vector2.ONE * HudStyle.HEADER_SCALE, "the clock with it")
	var score: Rect2 = hud.scoreboard.get_global_rect()
	assert_almost_eq(score.get_center().x, hud.size.x / 2.0, 1.0, "still in the middle")
	assert_lt(score.size.y, MatchHud.SCORE_SIZE.y, "and shorter than the portrait one")
	var hint: Label = hud.feedback.hint
	assert_gte(hint.get_global_rect().position.y, score.end.y, "the hint line is under the score")
	assert_lt(hint.get_global_rect().position.y, hud.size.y * 0.3, "in the top band: off your kid and off the far end of the street")
	assert_gte(hud.tag_toast.get_global_rect().position.y, hint.get_global_rect().position.y + HudStyle.WORDS_HEIGHT - 0.5, "the tag toast pops in under the words")
	assert_eq(hint.get_theme_font_size("font_size"), HudStyle.LABEL)


func test_far_kids_show_no_name_in_the_shoulder_view_and_near_ones_do() -> void:
	var practice: PracticeMatch = _practice()
	var overhead: OverheadHud = practice.hud.overhead
	var rules: CameraRules = practice._camera.rules
	assert_eq(overhead.name_reach, rules.shoulder_name_reach)
	var me: PlayerState = practice.sim.players[practice.local_id]
	assert_ne(overhead.label_for(me), "", "your own kid, close to the camera, keeps its name")
	var far: PlayerState = null
	for id: int in practice.sim.players:
		var state: PlayerState = practice.sim.players[id]
		var reach: float = practice._camera.global_position.distance_to(Vector3(state.position.x, 0.0, state.position.y))
		if reach > rules.shoulder_name_reach and overhead._status_text(state).is_empty():
			far = state
	if far == null:
		pass_test("nobody stood far enough at the start")
		return
	assert_eq(overhead.label_for(far), "", "a far kid keeps only its badge and bar")


func test_the_movement_controls_and_the_main_button_are_white_only() -> void:
	for color: Color in [HudStyle.WHITE_BODY, HudStyle.WHITE_RIM, HudStyle.WHITE_THUMB]:
		assert_eq(Vector3(color.r, color.g, color.b), Vector3.ONE, "white, no tint (owner)")
	var practice: PracticeMatch = _practice(&"patintero")
	var pad: StreetGamePad = practice.hud.street_pad
	pad.act_ready = true
	assert_eq(pad.white_strength(), 1.0, "a ready button at full strength")
	pad.act_ready = false
	if pad.action != StreetGamePad.Action.THROW:
		assert_eq(pad.white_strength(), HudStyle.WHITE_OFF, "not ready: the same white, a dimmer word (no grey or colour)")


## The main button's outline is the movement controls' rim (owner): both are drawn by `HudStyle.draw_white_disc` (one rim, never dimmed), and the
## charge is a sweep inside it, never a second ring outside.
func test_the_main_buttons_charge_stays_inside_the_shared_rim() -> void:
	for share: float in [0.1, 0.5, 1.0]:
		var points: PackedVector2Array = HudStyle.sweep_points(Vector2.ZERO, 56.0, share)
		assert_gt(points.size(), 2)
		for point: Vector2 in points:
			assert_lte(point.length(), 56.0 - HudStyle.WHITE_RIM_WIDTH / 2.0 + 0.001, "inside the rim at %.1f" % share)
	assert_eq(HudStyle.sweep_points(Vector2.ZERO, 56.0, 0.0).size(), 0, "no charge, no sweep")


## Tumbang Preso's points (owner, 2026-11-09): the kids' cards in a row at the top centre, three, the clock, three; the words stand under them.
func test_tumbang_presos_points_are_a_row_at_the_top_centre_three_each_side_of_the_clock() -> void:
	var practice: PracticeMatch = _practice(&"tumbang_preso")
	var hud: MatchHud = practice.hud
	var column: ScoreColumn = hud.score_column
	assert_gte(column.row_gap, 0.0, "a row, not the portrait column")
	var clock: Rect2 = Rect2(hud.clock_chip.get_global_transform() * Vector2.ZERO, hud.clock_chip.size * hud.clock_chip.get_global_transform().get_scale())
	var ids: Array[int] = column.order()
	assert_eq(ids.size(), practice.sim.players.size(), "a card per kid")
	assert_eq(column.left_count(), ceili(ids.size() / 2.0))
	var row_top: float = -1.0
	for index: int in ids.size():
		var card: Control = column.get_node("Row%d" % ids[index]) as Control
		var place: Rect2 = Rect2(column.get_global_transform() * column.target_of(ids[index]), card.size * column.get_global_transform().get_scale())
		if index < column.left_count():
			assert_lte(place.end.x, clock.position.x + 0.5, "card %d on the clock's left" % index)
		else:
			assert_gte(place.position.x, clock.end.x - 0.5, "card %d on the clock's right" % index)
		if row_top < 0.0:
			row_top = place.position.y
		assert_almost_eq(place.position.y, row_top, 0.5, "one row")
	assert_almost_eq((column.get_global_transform() * Vector2(column.row_width() / 2.0, 0.0)).x, hud.size.x / 2.0, 1.0, "centred")
	assert_almost_eq(clock.get_center().x, hud.size.x / 2.0, 1.0, "the clock in the middle of it")
	var bottom: float = (column.get_global_transform() * Vector2(0.0, ScoreColumn.ROW_HEIGHT)).y
	assert_gte(hud.street_pad.title_label.get_global_rect().position.y, bottom - 0.5, "the role line stands under the cards")


## The kids' cards in the points are squares showing the kid from the chest up (owner, 2026-11-09), cut from the owner's 3:4 close-ups.
func test_the_point_cards_are_square_chest_up_close_ups() -> void:
	assert_eq(ScoreColumn.CARD.x, ScoreColumn.CARD.y, "square")
	var practice: PracticeMatch = _practice(&"tumbang_preso")
	var column: ScoreColumn = practice.hud.score_column
	for id: int in column.order():
		var picture: TextureRect = column.get_node("Row%d/Face/Card" % id) as TextureRect
		var face: Control = column.get_node("Row%d/Face" % id) as Control
		assert_eq(face.size.x, face.size.y, "kid %d: a square card" % id)
		var square: AtlasTexture = picture.texture as AtlasTexture
		assert_not_null(square, "kid %d: cut from its close-up" % id)
		var close_up: Texture2D = KidModel.card_face(practice.sim.players[id].character_id)
		assert_eq(square.atlas, close_up, "the owner's 3:4 close-up of that kid")
		assert_eq(square.region.size.x, square.region.size.y, "a square of it")
		assert_almost_eq(square.region.position.y, close_up.get_height() * ScoreColumn.CHEST_UP_TOP, 0.5, "from near its top: face and chest")
		assert_eq(square.region.size.x, float(close_up.get_width()), "its full width")
