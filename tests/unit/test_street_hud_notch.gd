extends GutTest
## On a phone with a notch the top of the street games' HUD moves down by the inset (the gear and the clock are in `hud.top`). The points
## column (Tumbang Preso) and the quick chat button (both games) are placed outside it and must move with it: on the owner's phone the
## column sat over the gear and the chat button over the clock (2026-11-01). In landscape the points are a row at the top centre (2026-11-09).

const PRACTICE_SCENE: PackedScene = preload("res://scenes/match/practice.tscn")
## About a phone notch in the 720-wide UI.
const NOTCH: float = 110.0

var _kind: StringName
var _game: StringName


func before_each() -> void:
	_kind = Session.practice_kind
	_game = Session.practice_game


func after_each() -> void:
	Session.practice_kind = _kind
	Session.practice_game = _game


func _hud(game: StringName, inset: float) -> MatchHud:
	Session.practice_kind = &"match"
	Session.practice_game = game
	var practice: PracticeMatch = autofree(PRACTICE_SCENE.instantiate()) as PracticeMatch
	add_child(practice)
	practice.set_process(false)
	practice.hud.set_top_inset(inset)
	return practice.hud


func _assert_clear(hud: MatchHud, what: String) -> void:
	var clock: Rect2 = hud.clock_chip.get_global_rect()
	var chat: Rect2 = hud.chat.get_global_rect()
	assert_false(chat.intersects(clock), "%s: the chat button is not on the clock" % what)
	assert_gte(chat.position.y, hud.top_inset + MatchHud.TOP_ROW_Y - 1.0, "%s: it stands on the header's row, under the notch" % what)
	assert_gt(chat.position.x, clock.end.x, "%s: at the right of the clock" % what)
	if hud.score_column != null and hud.score_column.visible:
		var gear: Rect2 = hud.settings_button.get_global_rect()
		var column: Rect2 = _drawn_rect(hud.score_column)
		assert_false(column.intersects(gear), "%s: the points are not on the gear" % what)
		if hud.score_column.row_gap >= 0.0:
			# the landscape row at the top centre (3, the clock, 3): clear of the chat button, under the notch, its middle left to the clock
			assert_false(column.intersects(chat), "%s: the points row is not on the chat button" % what)
			assert_gte(column.position.y, hud.top_inset + MatchHud.TOP_ROW_Y - 1.0, "%s: the row moves down with the notch" % what)
			for id: int in hud.score_column.order():
				var card: Rect2 = _drawn_rect(hud.score_column.get_node("Row%d" % id) as Control)
				assert_false(card.intersects(_drawn_rect(hud.clock_chip)), "%s: card %d is not on the clock" % [what, id])
		else:
			assert_gte(column.position.y, gear.end.y, "%s: it starts under the gear" % what)


## Where `control` is drawn on the screen, its scale (and its parents') included.
func _drawn_rect(control: Control) -> Rect2:
	var transform: Transform2D = control.get_global_transform()
	return Rect2(transform * Vector2.ZERO, control.size * transform.get_scale())


func test_nothing_covers_the_gear_or_the_clock_with_a_notch_or_without() -> void:
	for game: StringName in [&"tumbang_preso", &"patintero"]:
		for inset: float in [0.0, NOTCH]:
			var hud: MatchHud = _hud(game, inset)
			await wait_process_frames(2)
			_assert_clear(hud, "%s, inset %d" % [game, int(inset)])


func test_tumbang_preso_has_its_points_column_to_check() -> void:
	var hud: MatchHud = _hud(&"tumbang_preso", NOTCH)
	await wait_process_frames(2)
	assert_not_null(hud.score_column, "the column this guards")
	assert_true(hud.score_column.visible)
