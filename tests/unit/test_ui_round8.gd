extends GutTest
## Round 8 of the interface plan: the room code as boxes (join, paste, errors, the room header), the first-run
## walkthrough and How to play.

const PRACTICE_SCENE: PackedScene = preload("res://scenes/match/practice.tscn")
const TITLE_SCENE: PackedScene = preload("res://scenes/ui/title_screen.tscn")
const ALPHABET: String = "ABCDEFGHJKMNPQRSTUVWXYZ23456789"

var _seen: bool
var _pending: bool
var _motion: bool


func before_each() -> void:
	_seen = Session.coach_seen
	_pending = Session.coach_pending
	_motion = Settings.reduced_motion
	Session.coach_seen = true
	Session.coach_pending = false
	Settings.set_reduced_motion(true)


func after_each() -> void:
	Session.coach_seen = _seen
	Session.coach_pending = _pending
	Session.save()
	Settings.set_reduced_motion(_motion)
	var layer: Node = get_tree().root.get_node_or_null(UiToast.LAYER_NAME)
	if layer != null:
		for toast: Node in layer.get_children():
			toast.queue_free()


func _toast_texts() -> Array[String]:
	var found: Array[String] = []
	var layer: Node = get_tree().root.get_node_or_null(UiToast.LAYER_NAME)
	if layer != null:
		for child: Node in layer.get_children():
			if child is UiToast and not child.is_queued_for_deletion():
				found.append((child as UiToast).text)
	return found


func _lobby_join() -> Node:
	Session.lobby_hotspot = false
	Session.lobby_preview = false
	Session.lobby_intent = 1
	var lobby: Node = autofree((load("res://scenes/ui/lobby.tscn") as PackedScene).instantiate()) as Node
	add_child(lobby)
	return lobby


# ---- code boxes -------------------------------------------------------------------------

func test_the_boxes_keep_capitals_from_the_code_alphabet_and_at_most_eight() -> void:
	var boxes: UiCodeBoxes = autofree(UiCodeBoxes.new(8, ALPHABET)) as UiCodeBoxes
	add_child(boxes)
	assert_eq(boxes.clean("abc 234-xy"), "ABC234XY")
	assert_eq(boxes.clean("a0b1c"), "ABC", "0 and 1 are not in the alphabet")
	assert_eq(boxes.clean("ILO"), "", "no I, L or O either")
	assert_eq(boxes.clean("ABCDEFGHJKM"), "ABCDEFGH", "eight at most")
	assert_eq(boxes.clean(""), "")


func test_the_boxes_show_what_was_typed_one_letter_per_box_and_cap_the_input() -> void:
	var boxes: UiCodeBoxes = autofree(UiCodeBoxes.new(8, ALPHABET)) as UiCodeBoxes
	add_child(boxes)
	assert_eq(boxes.get_child(0).get_child_count(), 8, "eight boxes")
	boxes.edit.text = "abc23"
	boxes.edit.text_changed.emit("abc23")
	assert_eq(boxes.edit.text, "ABC23", "capitals, whatever was typed")
	var letters: String = ""
	for box: Node in boxes.get_child(0).get_children():
		letters += (box.get_node("Letter") as Label).text
	assert_eq(letters, "ABC23", "each letter in its own box, the rest empty")
	assert_eq(boxes.get_text(), "ABC23")


func test_a_text_set_from_code_shows_in_the_boxes_without_a_signal() -> void:
	var boxes: UiCodeBoxes = autofree(UiCodeBoxes.new(8, ALPHABET)) as UiCodeBoxes
	add_child(boxes)
	boxes.edit.text = "K7M2"
	await wait_process_frames(2)
	assert_eq((boxes.get_child(0).get_child(3).get_node("Letter") as Label).text, "2")


func test_changed_and_completed_fire_and_an_error_clears_on_the_next_edit() -> void:
	var boxes: UiCodeBoxes = autofree(UiCodeBoxes.new(8, ALPHABET)) as UiCodeBoxes
	add_child(boxes)
	var changes: Array[String] = []
	var done: Array[String] = []
	boxes.changed.connect(func(text: String) -> void: changes.append(text))
	boxes.completed.connect(func(text: String) -> void: done.append(text))
	boxes.edit.text = "ABC234X"
	boxes.edit.text_changed.emit("ABC234X")
	assert_eq(done.size(), 0, "not complete yet")
	boxes.set_error(true)
	assert_true(boxes.has_error)
	boxes.edit.text = "ABC234XY"
	boxes.edit.text_changed.emit("ABC234XY")
	assert_false(boxes.has_error, "editing clears the red")
	assert_eq(done, ["ABC234XY"] as Array[String], "completed once, with the whole code")
	assert_eq(changes.size(), 2)


func test_wrong_boxes_wear_a_red_line_and_the_next_empty_box_a_gold_one_when_focused() -> void:
	var boxes: UiCodeBoxes = autofree(UiCodeBoxes.new(8, ALPHABET)) as UiCodeBoxes
	add_child(boxes)
	boxes.set_text("ABC")
	boxes.set_error(true)
	var box: StyleBoxFlat = (boxes.get_child(0).get_child(0) as Control).get_theme_stylebox("panel") as StyleBoxFlat
	assert_eq(box.border_color, UiCodeBoxes.ERROR_LINE)
	boxes.set_error(false)
	box = (boxes.get_child(0).get_child(0) as Control).get_theme_stylebox("panel") as StyleBoxFlat
	assert_eq(box.border_color, UiCodeBoxes.BOX_FILLED, "a filled box")
	box = (boxes.get_child(0).get_child(5) as Control).get_theme_stylebox("panel") as StyleBoxFlat
	assert_eq(box.border_color, UiCodeBoxes.BOX_LINE, "an empty box")


func test_read_only_boxes_just_show_a_code() -> void:
	var boxes: UiCodeBoxes = autofree(UiCodeBoxes.new(8, "", false)) as UiCodeBoxes
	add_child(boxes)
	assert_null(boxes.edit, "nothing to type into")
	boxes.set_text("k7m2q9xa")
	assert_eq(boxes.get_text(), "K7M2Q9XA")
	assert_eq((boxes.get_child(0).get_child(7).get_node("Letter") as Label).text, "A")
	boxes.set_text("")
	assert_eq(boxes.get_text(), "")


# ---- join form --------------------------------------------------------------------------

func test_the_join_form_uses_the_boxes_and_a_paste_button() -> void:
	var lobby: Node = _lobby_join()
	await wait_process_frames(2)
	assert_not_null(lobby._code_boxes)
	assert_eq(lobby._code_edit, lobby._code_boxes.edit, "the screen still talks to one LineEdit")
	assert_eq(lobby._code_boxes.length, lobby.NET_RULES.passcode_length)
	assert_eq(lobby._code_boxes.alphabet, lobby.NET_RULES.passcode_alphabet)
	assert_not_null(lobby.find_child("PasteButton", true, false))
	assert_gte((lobby.find_child("PasteButton", true, false) as Button).custom_minimum_size.y, 72.0)


func test_a_short_code_is_refused_next_to_the_boxes_which_turn_red() -> void:
	var saved_url: String = Settings.server_url
	Settings.server_url = "ws://127.0.0.1:1"
	var lobby: Node = _lobby_join()
	await wait_process_frames(2)
	lobby._code_edit.text = "ABC"
	lobby.submit()
	Settings.server_url = saved_url
	assert_eq(lobby.status_text(), "Enter all 8 characters.")
	assert_true(lobby._code_boxes.has_error)
	assert_true(lobby._code_error.visible, "the message is right under the boxes")


func test_paste_cleans_what_a_friend_sent_and_says_so() -> void:
	var lobby: Node = _lobby_join()
	await wait_process_frames(2)
	lobby.paste_code("  k7m2 q9xa \n")
	assert_eq(lobby._code_edit.text, "K7M2Q9XA")
	await wait_process_frames(2)
	assert_true(_toast_texts().has("Code pasted"))


func test_paste_of_a_short_or_empty_clipboard_warns_instead_of_guessing() -> void:
	var lobby: Node = _lobby_join()
	await wait_process_frames(2)
	lobby.paste_code("K7M")
	assert_eq(lobby._code_edit.text, "K7M")
	await wait_process_frames(2)
	assert_true(_toast_texts().has("Pasted 3 of 8 characters"))
	lobby.paste_code("---")
	await wait_process_frames(2)
	assert_eq(lobby._code_edit.text, "K7M", "nothing usable on the clipboard changes nothing")
	assert_true(_toast_texts().has("Nothing to paste. Copy the room code first."))


func test_a_refused_join_marks_the_boxes_and_shows_the_reason_under_them() -> void:
	var lobby: Node = _lobby_join()
	await wait_process_frames(2)
	lobby._on_error("No room with that code.")
	assert_true(lobby._code_boxes.has_error)
	assert_eq(lobby.status_text(), "No room with that code.", "the words are what the server said")
	assert_true(lobby._code_error.visible)
	lobby._code_edit.text = "K7M2Q9XA"
	lobby._code_boxes.edit.text_changed.emit("K7M2Q9XA")
	assert_false(lobby._code_boxes.has_error)
	assert_false(lobby._code_error.visible, "typing again clears the message")


# ---- room header ------------------------------------------------------------------------

func test_the_online_room_header_shows_the_code_as_boxes_and_a_hidden_label_keeps_the_text() -> void:
	var view: HotspotRoomView = autofree(HotspotRoomView.new()) as HotspotRoomView
	add_child(view)
	view.set_online(true)
	view.show_room({"code": "K7M2Q9XA", "team_size": 2, "host": 1, "first_to": 3, "state": 0, "members": [
		{"player_id": 1, "name": "Ana", "team": 0, "ready": false, "connected": true, "char": "junjun"}],
		"can_start": false, "can_start_host": true, "map": &"street"}, 1)
	assert_eq(view.code_label.text, "K7M2Q9XA")
	assert_false(view.code_label.visible, "the label is data; people see the boxes")
	assert_eq(view.code_boxes.get_text(), "K7M2Q9XA")
	view.clear_room()
	assert_eq(view.code_boxes.get_text(), "", "leaving removes a stale code from the boxes too")


# ---- walkthrough ------------------------------------------------------------------------

func _practice() -> PracticeMatch:
	var practice: PracticeMatch = autofree(PRACTICE_SCENE.instantiate()) as PracticeMatch
	add_child(practice)
	practice.finish_pick()
	return practice


func test_the_four_steps_use_the_rules_numbers() -> void:
	var steps: Array[Dictionary] = CoachMarks.steps_for(5.0, 2)
	assert_eq(steps.size(), 4)
	assert_eq(steps[0]["target"], &"", "moving is a tap anywhere on the street: nothing to light")
	assert_true((steps[0]["text"] as String).contains("Tap the street"))
	assert_eq(steps[1]["target"], &"hand")
	assert_true((steps[1]["text"] as String).contains("Drag a card"))
	assert_true((steps[2]["text"] as String).contains("5 seconds"))
	assert_true((steps[2]["text"] as String).contains("First to 2 points"))
	assert_true((CoachMarks.steps_for(4.5, 3)[2]["text"] as String).contains("4.5 seconds"))


func test_the_first_practice_match_opens_with_the_walkthrough_and_quiets_the_controls() -> void:
	Session.coach_pending = true
	var practice: PracticeMatch = _practice()
	practice.skip_intro()
	var coach: CoachMarks = practice.hud.coach
	assert_true(coach.running)
	assert_true(coach.visible)
	assert_false(Session.coach_pending, "it opens once")
	assert_false(practice.hud.controls_active(), "no stray throw while it is up")
	assert_eq(coach.kicker_label.text, "Step 1 of 4")
	assert_eq(coach.title_label.text, "MOVE")
	assert_eq(coach._spots.size(), 0, "the street is the control: nothing to light")


func test_next_walks_the_steps_lights_the_hand_and_ends_with_got_it() -> void:
	Session.coach_pending = true
	var practice: PracticeMatch = _practice()
	practice.skip_intro()
	var coach: CoachMarks = practice.hud.coach
	var ended: Array[bool] = []
	coach.finished.connect(func(completed: bool) -> void: ended.append(completed))
	coach.next()
	assert_eq(coach.title_label.text, "AIM AND THROW")
	assert_eq(coach._spots.size(), 2, "the hand is lit, two cards to a spot")
	coach.next()
	assert_eq(coach._spots.size(), 0, "nothing to light for the rules")
	coach.next()
	assert_eq(coach.next_button.text, "Got it")
	assert_false(coach.skip_button.visible, "no skipping the last card: just finish")
	Session.coach_seen = false
	coach.next()
	assert_eq(ended, [true] as Array[bool])
	assert_false(coach.visible)
	assert_true(Session.coach_seen, "seen: it will not open by itself again")
	assert_true(practice.hud.controls_active(), "the controls are back")


func test_skip_ends_it_for_good_and_back_on_android_skips() -> void:
	Session.coach_pending = true
	var practice: PracticeMatch = _practice()
	practice.skip_intro()
	var coach: CoachMarks = practice.hud.coach
	var ended: Array[bool] = []
	coach.finished.connect(func(completed: bool) -> void: ended.append(completed))
	Session.coach_seen = false
	var cancel: InputEventAction = InputEventAction.new()
	cancel.action = &"ui_cancel"
	cancel.pressed = true
	coach._unhandled_input(cancel)
	assert_eq(ended, [false] as Array[bool], "skipped, not completed")
	assert_true(Session.coach_seen)
	assert_true(practice.hud.controls_active())


func test_without_a_pending_walkthrough_a_match_starts_clean() -> void:
	var practice: PracticeMatch = _practice()
	practice.skip_intro()
	assert_false(practice.hud.coach.running)
	assert_true(practice.hud.controls_active())


func test_seen_is_saved_with_the_player() -> void:
	Session.coach_seen = true
	Session.save()
	var loaded: Node = autofree((load("res://scripts/autoload/session.gd") as GDScript).new()) as Node
	add_child(loaded)
	assert_true(loaded.get("coach_seen") as bool, "a reloaded page remembers it was shown")


# ---- how to play ------------------------------------------------------------------------

func _screen() -> TitleScreen:
	var phone: Control = autofree(Control.new()) as Control
	phone.custom_minimum_size = Vector2(720.0, 1280.0)
	phone.size = Vector2(720.0, 1280.0)
	add_child(phone)
	var screen: TitleScreen = autofree(TITLE_SCENE.instantiate()) as TitleScreen
	phone.add_child(screen)
	return screen


func test_how_to_play_is_reached_from_settings_and_shows_the_same_four_steps() -> void:
	var screen: TitleScreen = _screen()
	screen.open_page(&"settings")
	screen.settings_page.howto_button.pressed.emit()
	assert_eq(screen.current_page, &"howto")
	assert_eq(screen.tab_bar.active, &"settings", "a part of Settings")
	var page: HowToPlayPage = screen.howto_page
	assert_eq(page.cards.size(), 4)
	var rules: GameRules = load("res://data/rules/game_rules.tres") as GameRules
	var steps: Array[Dictionary] = CoachMarks.steps_for(rules.capture_time, rules.first_to)
	for i: int in 4:
		assert_eq((page.cards[i].find_child("Words", true, false) as Label).text, steps[i]["text"], "step %d text" % (i + 1))
		assert_eq((page.cards[i].find_child("Number", true, false).get_node("Text") as Label).text, str(i + 1))


func test_the_walkthrough_button_asks_the_screen_to_start_one() -> void:
	var screen: TitleScreen = _screen()
	screen.open_page(&"howto")
	var asked: Array[bool] = []
	screen.howto_page.walkthrough_requested.connect(func() -> void: asked.append(true))
	screen.howto_page.walkthrough_button.pressed.emit()
	assert_eq(asked.size(), 1)
	assert_gte(screen.howto_page.walkthrough_button.custom_minimum_size.y, 88.0)
