extends GutTest
## The tag toast (owner): a bar above the card tray that pops in and out for a tag, with the tagger's face on the left, "TAYA!" in the middle and
## the tagged kid's face on the right. Every toast is on screen for exactly two seconds, the next waits its turn, and a sound plays as it pops
## in and as it pops out. Seam: TagToast (tick, say, signals), MatchSim (who tagged), the match's HUD and presentation.

const DT: float = 1.0 / 30.0
const RULES: GameRules = preload("res://data/rules/game_rules.tres")
const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")

var _reduced: bool
var _kind: StringName
var _game: StringName
var _coach: bool


func before_each() -> void:
	_reduced = Settings.reduced_motion
	Settings.reduced_motion = false
	_kind = Session.practice_kind
	_game = Session.practice_game
	_coach = Session.coach_pending


func after_each() -> void:
	Settings.reduced_motion = _reduced
	Session.practice_kind = _kind  # (a test that starts a match leaves nothing behind for the next test file)
	Session.practice_game = _game
	Session.coach_pending = _coach


## What the toast is told about a player.
func _side(character: StringName, number: int = 1, color: Color = Color.BLUE) -> Dictionary:
	return {"character": character, "number": number, "color": color}


func _toast() -> TagToast:
	var toast: TagToast = add_child_autofree(TagToast.new())
	toast.size = Vector2(720.0, TagToast.HEIGHT)
	return toast


## Ticks `seconds` of frames; returns the seconds the toast was on screen in them.
func _run(toast: TagToast, seconds: float) -> float:
	var shown: float = 0.0
	for i: int in ceili(seconds / DT):
		toast.tick(DT)
		if toast.is_showing():
			shown += DT
	return shown


func test_it_has_the_tagger_the_word_and_the_tagged_in_that_order() -> void:
	var toast: TagToast = _toast()
	toast.say(_side(&"junjun"), _side(&"ligaya"))
	toast.tick(DT)
	var row: Node = toast.word_label.get_parent()
	assert_eq(row.get_child(0), toast.tagger_slot, "the tagger on the left")
	assert_eq(row.get_child(1), toast.word_label, "the word in the middle")
	assert_eq(row.get_child(2), toast.tagged_slot, "the tagged on the right")
	assert_eq(toast.word_label.text, "TAYA!")
	var left: TextureRect = toast.tagger_slot.get_node("Face") as TextureRect
	var right: TextureRect = toast.tagged_slot.get_node("Face") as TextureRect
	assert_eq(left.texture, KidModel.minimap_face(&"junjun"), "the kid's profile picture in a circle")
	assert_eq(right.texture, KidModel.minimap_face(&"ligaya"))
	assert_not_null(left.material, "cropped to a circle")
	var names: int = 0
	for label: Node in toast.find_children("*", "Label", true, false):
		if label.name != "Number" and label.name != "Word":
			names += 1
	assert_eq(names, 0, "no names: only the word and the numbers")


func test_each_circle_has_its_players_number_on_a_team_coloured_square_at_its_bottom_corner() -> void:
	var toast: TagToast = _toast()
	var ally: Color = TeamColors.world_own()
	var enemy: Color = TeamColors.world_enemy()
	toast.say(_side(&"junjun", 2, ally), _side(&"ligaya", 5, enemy), TagToast.ALLY)
	toast.tick(DT)
	var left: Panel = toast.tagger_slot.get_node("Badge") as Panel
	var right: Panel = toast.tagged_slot.get_node("Badge") as Panel
	assert_eq((left.get_node("Number") as Label).text, "2", "the tagger's number")
	assert_eq((right.get_node("Number") as Label).text, "5", "the tagged kid's number")
	assert_eq((left.get_theme_stylebox("panel") as StyleBoxFlat).bg_color, ally, "in the tagger's team colour")
	assert_eq((right.get_theme_stylebox("panel") as StyleBoxFlat).bg_color, enemy)
	assert_lt((left.get_theme_stylebox("panel") as StyleBoxFlat).corner_radius_top_left, int(TagToast.BADGE / 2.0), "a rounded square, not a circle")
	var circle: Vector2 = Vector2.ONE * TagToast.AVATAR
	assert_lt(left.position.x, circle.x / 2.0, "the tagger's is on the left of its circle")
	assert_gt(right.position.x + right.size.x, circle.x / 2.0 + 1.0)
	assert_gt(right.position.x, circle.x / 2.0 - 1.0, "the tagged one's is on the right of its circle")
	for badge: Panel in [left, right]:
		assert_gt(badge.position.y + badge.size.y, circle.y - 1.0, "at the bottom")
		assert_gt(badge.position.y, circle.y / 2.0)


func test_the_bar_is_blue_for_an_ally_red_for_an_enemy_and_the_night_plate_for_nobody() -> void:
	var toast: TagToast = _toast()
	var colors: Dictionary = {}
	for tone: StringName in [TagToast.ALLY, TagToast.ENEMY, &""]:
		toast.say(_side(&"junjun"), _side(&"ligaya"), tone)
		toast.tick(DT)
		var box: StyleBox = toast.panel.get_theme_stylebox("panel")
		colors[tone] = (box as StyleBoxFlat).bg_color if box is StyleBoxFlat else null
		toast.clear()
	var blue: Color = colors[TagToast.ALLY] as Color
	var red: Color = colors[TagToast.ENEMY] as Color
	assert_gt(blue.b, blue.r, "blue for an ally")
	assert_gt(red.r, red.b, "red for an enemy")
	assert_eq(blue, TeamColors.ui_own().darkened(TagToast.DEEPEN))
	assert_eq(red, TeamColors.ui_enemy().darkened(TagToast.DEEPEN))
	assert_null(colors[&""], "no tagger: the night plate, not a team colour")


func test_the_colour_blind_setting_changes_the_pair() -> void:
	var saved: bool = Settings.colorblind
	Settings.colorblind = true
	var toast: TagToast = _toast()
	toast.say(_side(&"junjun"), _side(&"ligaya"), TagToast.ENEMY)
	toast.tick(DT)
	assert_eq((toast.panel.get_theme_stylebox("panel") as StyleBoxFlat).bg_color, TeamColors.ui_enemy().darkened(TagToast.DEEPEN))
	Settings.colorblind = saved


func test_the_match_calls_a_tagger_an_ally_when_it_is_you_or_on_your_team_and_in_tumbang_preso_only_you() -> void:
	Session.practice_kind = &"match"
	Session.practice_game = &"street"
	var practice: PracticeMatch = autofree(preload("res://scenes/match/practice.tscn").instantiate()) as PracticeMatch
	add_child(practice)
	practice.set_process(false)
	practice.finish_pick()
	practice.skip_intro()
	var presentation: PracticeMatchPresentation = practice._presentation
	assert_true(presentation._is_ally(practice.sim.players[practice.local_id]), "you")
	for id: int in practice.sim.players:
		var state: PlayerState = practice.sim.players[id]
		assert_eq(presentation._is_ally(state), id == practice.local_id or state.team == practice.local_team, "kid %d" % id)
	var side: Dictionary = presentation._toast_side(practice.sim.players[practice.local_id])
	assert_eq(side["character"], practice.sim.players[practice.local_id].character_id)
	assert_eq(side["number"], practice.hud.overhead.player_number(practice.sim.players[practice.local_id]), "the number over its head")
	assert_eq(side["color"], practice.hud.overhead.badge_color(practice.sim.players[practice.local_id]), "its team colour")
	assert_true(presentation._toast_side(null).is_empty())
	Session.practice_game = &"tumbang_preso"
	var free_for_all: PracticeMatch = autofree(preload("res://scenes/match/practice.tscn").instantiate()) as PracticeMatch
	add_child(free_for_all)
	free_for_all.set_process(false)
	free_for_all.finish_pick()
	free_for_all.skip_intro()
	for id: int in free_for_all.sim.players:
		assert_eq(free_for_all._presentation._is_ally(free_for_all.sim.players[id]), id == free_for_all.local_id, "everyone else is against you")


func test_a_tagger_that_is_not_known_leaves_an_empty_circle() -> void:
	var toast: TagToast = _toast()
	toast.say({}, _side(&"toni"))
	toast.tick(DT)
	assert_null(toast.tagger_slot.get_node_or_null("Face"))
	assert_not_null(toast.tagger_slot.get_node_or_null("Ring"))
	assert_not_null(toast.tagged_slot.get_node("Face"))


func test_every_toast_is_on_screen_for_exactly_two_seconds() -> void:
	assert_eq(TagToast.SECONDS, 2.0)
	var toast: TagToast = _toast()
	toast.say(_side(&"junjun"), _side(&"ligaya"))
	assert_false(toast.is_showing(), "nothing before the first tick")
	assert_almost_eq(_run(toast, 3.0), 2.0, DT + 0.001, "two seconds, in and out included")
	assert_false(toast.visible)


func test_tags_close_together_each_get_their_full_two_seconds_in_turn() -> void:
	var toast: TagToast = _toast()
	for pair: Array in [[&"junjun", &"ligaya"], [&"migo", &"toni"], [&"popoy", &"inday"]]:
		toast.say(_side(pair[0] as StringName), _side(pair[1] as StringName))
	assert_eq(toast.waiting(), 3)
	toast.tick(DT)
	assert_eq(toast.waiting(), 2, "one on the bar, two waiting")
	assert_almost_eq(_run(toast, 8.0), 6.0, 3.0 * DT + 0.001, "three toasts, two seconds each, one after the other")
	assert_false(toast.is_showing())
	assert_eq(toast.waiting(), 0)


func test_the_wait_is_capped_and_the_oldest_waiting_one_is_dropped() -> void:
	var toast: TagToast = _toast()
	for i: int in TagToast.MAX_WAITING + 4:
		toast.say(_side(&"junjun"), _side(&"ligaya"))
	assert_eq(toast.waiting(), TagToast.MAX_WAITING)


func test_it_pops_in_and_out_and_a_sound_plays_for_each() -> void:
	var toast: TagToast = _toast()
	var ins: Array[int] = [0]
	var outs: Array[int] = [0]
	toast.popped_in.connect(func() -> void: ins[0] += 1)
	toast.popped_out.connect(func() -> void: outs[0] += 1)
	toast.say(_side(&"junjun"), _side(&"ligaya"))
	toast.tick(DT)
	assert_eq(ins[0], 1, "pops in with the first frame")
	assert_lt(toast.panel.scale.x, 1.0, "growing")
	assert_lt(toast.panel.modulate.a, 1.0, "fading in")
	_run(toast, TagToast.IN_SECONDS + 0.2)
	assert_almost_eq(toast.panel.scale.x, 1.0, 0.0001, "still while it stays")
	assert_almost_eq(toast.panel.modulate.a, 1.0, 0.0001)
	assert_eq(outs[0], 0)
	_run(toast, TagToast.SECONDS - TagToast.OUT_SECONDS - toast.age() + DT * 2.0)
	assert_eq(outs[0], 1, "pops out at the end")
	assert_lt(toast.panel.scale.x, 1.0, "shrinking")
	_run(toast, 1.0)
	assert_eq([ins[0], outs[0]], [1, 1], "once each")
	for sound: String in [TagToast.SOUND_IN, TagToast.SOUND_OUT]:
		assert_true(ResourceLoader.exists(SfxView.DIR + sound + ".mp3"), "%s is one of the owner's files" % sound)


func test_with_reduce_motion_it_just_appears_and_goes() -> void:
	Settings.reduced_motion = true
	var toast: TagToast = _toast()
	toast.say(_side(&"junjun"), _side(&"ligaya"))
	toast.tick(DT)
	assert_eq(toast.panel.scale, Vector2.ONE)
	assert_eq(toast.panel.modulate.a, 1.0)
	assert_almost_eq(_run(toast, 3.0), 2.0, 2.0 * DT, "still two seconds")


func test_clear_drops_everything() -> void:
	var toast: TagToast = _toast()
	toast.say(_side(&"junjun"), _side(&"ligaya"))
	toast.say(_side(&"migo"), _side(&"toni"))
	toast.tick(DT)
	toast.clear()
	assert_false(toast.is_showing())
	assert_eq(toast.waiting(), 0)
	assert_false(toast.visible)


func test_the_toast_sits_just_above_the_tray_in_every_layout() -> void:
	var phone: Control = autofree(Control.new()) as Control
	phone.size = Vector2(720.0, 1600.0)
	add_child(phone)
	var hud: MatchHud = MatchHud.new()
	phone.add_child(hud)
	await wait_frames(2)
	for sides: bool in [false, true]:
		Settings.cards_tray = &"sides" if sides else &"bottom"
		hud.layout_controls()
		var bottom: float = hud.size.y - hud.console_height() - TagToast.GAP
		assert_almost_eq(hud.tag_toast.position.y + hud.tag_toast.size.y, bottom, 1.0, "sides=%s: its foot is a gap above the tray" % sides)
		assert_almost_eq(hud.tag_toast.size.y, TagToast.HEIGHT, 1.0)
		assert_almost_eq(hud.tag_toast.size.x, 720.0, 1.0, "the bar is centred in the full width")
	Settings.cards_tray = &"bottom"
	assert_eq(hud.tag_toast.mouse_filter, Control.MOUSE_FILTER_IGNORE, "never takes a touch")


## The (id, by_id) of every tag the sim announces while `act` runs.
func _tags(sim: MatchSim, act: Callable) -> Array[Vector2i]:
	var heard: Array[Vector2i] = []
	sim.player_tagged.connect(func(id: int, by_id: int) -> void: heard.append(Vector2i(id, by_id)))
	act.call()
	return heard


func test_the_sim_says_who_tagged_who_in_agawan_base() -> void:
	var sim: MatchSim = MatchSim.new(RULES, LAYOUT, 3)
	sim.add_player(1, 0)
	sim.add_player(2, 1)
	assert_eq(_tags(sim, func() -> void: sim.damage(2, 9999, 1)), [Vector2i(2, 1)] as Array[Vector2i])
	var other: MatchSim = MatchSim.new(RULES, LAYOUT, 3)
	other.add_player(1, 0)
	assert_eq(_tags(other, func() -> void: other.kill(1)), [Vector2i(1, -1)] as Array[Vector2i], "a scripted tag has nobody to credit")


func test_a_weapon_hit_credits_whoever_threw_it() -> void:
	var sim: MatchSim = MatchSim.new(RULES, LAYOUT, 3)
	sim.add_player(1, 0)
	sim.add_player(2, 1)
	sim.players[2].hp = 1
	var def: WeaponDef = load("res://data/weapons/bato_light.tres") as WeaponDef
	assert_eq(_tags(sim, func() -> void: sim.weapons._hit(sim, sim.players[2], def, 1)), [Vector2i(2, 1)] as Array[Vector2i])
