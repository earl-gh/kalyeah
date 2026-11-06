extends GutTest
## Langit Lupa on the phone (docs/GDD.md "Langit Lupa"): the Games page, the Practice match (you and five bots, the kids' column, the
## platforms, the Lupa button and its mound, a kid drawn on top of a langit) and the brownout (the dark, the far kids hidden).

const PRACTICE_SCENE: PackedScene = preload("res://scenes/match/practice.tscn")
const PRACTICE_TAYA: GDScript = preload("res://tests/fixtures/practice_taya.gd")
const DT: float = 1.0 / 30.0
const BOT_TAYA: int = 6

var _game: StringName
var _kind: StringName
var _coach: bool


func before_each() -> void:
	_game = Session.practice_game
	_kind = Session.practice_kind
	_coach = Session.briefing_pending


func after_each() -> void:
	Session.practice_game = _game
	Session.practice_kind = _kind
	Session.briefing_pending = _coach
	Session.save()


## A Langit Lupa Practice with bot 6 the taya and every bot standing still, everyone moved off the platforms.
func _practice() -> PracticeMatch:
	Session.practice_kind = &"match"
	Session.practice_game = &"langit_lupa"
	Session.briefing_pending = false
	var practice: PracticeMatch = autofree(PRACTICE_TAYA.make(self, PRACTICE_SCENE)) as PracticeMatch
	var game: LangitLupa = practice.sim.mode as LangitLupa
	game.taya_id = BOT_TAYA
	game.place_teams(practice.sim)
	game.grace_left = 0.0
	practice._mode_coach.ids.clear()
	var x: float = -10.0
	for id: int in practice.sim.players:
		practice.sim.players[id].position = Vector2(x, 21.0)
		practice.sim.set_input(id, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, 0, 0))
		x += 4.0
	practice.sim.players[BOT_TAYA].position = Vector2(10.0, -21.0)
	return practice


func test_the_games_page_the_practice_picker_and_the_rooms_offer_langit_lupa() -> void:
	assert_true(StreetPage.playable(&"langit_lupa"), "Practice, Local and Online")
	assert_true(&"langit_lupa" in RoomService.MAP_IDS)
	assert_not_null(StreetPage.picture_for(&"langit_lupa"), "a stand-in picture until the owner's")
	assert_eq(PracticeRow.game_entry(&"langit_lupa")["game"], "Langit Lupa")
	var lines: Array[String] = StreetPage.mechanics_for(&"langit_lupa", load(StreetPage.RULES_PATH) as GameRules)
	assert_gt(lines.size(), 4)
	assert_true(" ".join(lines).contains("Brownout"), "the brownout is explained")
	var words: Dictionary = MatchBriefing.words_for(StreetMode.rules_for(&"langit_lupa"))
	assert_eq(words["title"], "Langit Lupa")
	assert_eq((words["rows"] as Array)[1][0], "Lupa")
	assert_eq(HotspotRoomView.MAP_NAMES[&"langit_lupa"], "Langit Lupa")


func test_a_practice_is_you_and_five_bots_with_the_kids_column_the_platforms_and_the_lupa_button() -> void:
	var practice: PracticeMatch = _practice()
	practice.advance(DT)
	assert_eq(practice.sim.players.size(), 6)
	for state: PlayerState in practice.sim.players.values():
		assert_eq(state.team, 0, "every kid for itself")
	assert_not_null(practice.hud.score_column, "the kids' column, like Tumbang Preso")
	assert_false(practice._map.bases_shown(), "no bases or rescue areas")
	var view: LangitLupaView = practice.mode_view.langit
	assert_not_null(view)
	for index: int in practice.sim.mode.rules.langit_spots.size():
		assert_true(view.platform_shown(index))
	var pad: StreetGamePad = practice.hud.street_pad
	assert_eq(pad.action, StreetGamePad.Action.LUPA)
	assert_true(pad.act_ready)
	assert_eq(pad.title_label.text, "RUN!")
	pad.action_pressed.emit()
	for i: int in 10:
		practice.advance(DT)
	var game: LangitLupa = practice.sim.mode as LangitLupa
	assert_true(game.mounds.has(practice.local_id), "the Lupa button raises a mound")
	assert_eq(view.mound_count(), 1, "drawn")
	assert_false(pad.act_ready, "it cools down")
	assert_lt(pad.charge, 0.1)
	assert_eq(pad.title_label.text, "SAFE!")
	var kid: KidModel = practice._views[practice.local_id]
	assert_gt(kid.position.y, 0.1, "the kid stands on top of its mound")


func test_the_taya_has_no_button_and_sees_tag_circles_only_round_kids_off_a_langit() -> void:
	var practice: PracticeMatch = _practice()
	var game: LangitLupa = practice.sim.mode as LangitLupa
	game.taya_id = practice.local_id
	game.place_teams(practice.sim)
	game.grace_left = 0.0
	var other: PlayerState = practice.sim.players[BOT_TAYA]
	var spot: Vector2 = game.rules.langit_spots[0].get_center()
	other.position = spot
	practice.sim.players[2].position = Vector2(-6.0, 21.0)
	practice.advance(DT)
	assert_eq(practice.hud.street_pad.action, StreetGamePad.Action.NONE)
	assert_eq(practice.hud.street_pad.title_label.text, "YOU'RE IT!")
	assert_false(practice.mode_view.tag_circle_ids().has(BOT_TAYA), "safe on its langit: no circle")
	assert_gt(practice._views[BOT_TAYA].position.y, 0.1, "drawn on top of the platform")
	assert_true(practice.mode_view.tag_circle_ids().has(2), "a kid on the street has one")


func test_a_brownout_darkens_the_street_and_hides_the_kids_out_of_sight() -> void:
	var practice: PracticeMatch = _practice()
	var game: LangitLupa = practice.sim.mode as LangitLupa
	var me: PlayerState = practice.sim.players[practice.local_id]
	var near: int = 2
	me.position = Vector2(-6.0, 20.0)
	practice.sim.players[near].position = Vector2(-4.0, 20.0)
	game.time_left = game.rules.match_seconds - game.rules.brownout_first - 0.1
	practice.advance(DT)
	assert_true(game.is_brownout())
	var shade: BrownoutShade = practice.find_child("BrownoutShade", false, false) as BrownoutShade
	assert_not_null(shade)
	for i: int in 20:
		practice.advance(DT)
	assert_true(shade.shade.visible, "the dark")
	assert_true(practice._views[near].visible, "a kid near you is seen")
	assert_false(practice._views[BOT_TAYA].visible, "the far taya is not")
	assert_true(practice.hud.overhead.hidden_ids.has(BOT_TAYA), "nor its bar")
	assert_false(practice.hud.overhead.bar_visible(practice.sim.players[BOT_TAYA]))
	assert_true(practice.hud.street_pad.hint_label.text.contains("dark"))
	game.time_left = game.rules.match_seconds - game.rules.brownout_first - game.rules.brownout_seconds - 1.0
	for i: int in 20:
		practice.advance(DT)
	assert_false(shade.shade.visible, "the lights are back")
	assert_true(practice._views[BOT_TAYA].visible)
