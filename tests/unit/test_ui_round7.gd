extends GutTest
## Round 7 of the interface plan: a toast, a dressed status line, the profile sheet and the UI gallery.

const TITLE_SCENE: PackedScene = preload("res://scenes/ui/title_screen.tscn")

var _name: String
var _avatar: String
var _motion: bool


func before_each() -> void:
	_name = Session.player_name
	_avatar = Session.avatar_id
	_motion = Settings.reduced_motion
	Session.player_name = "Ana"
	Session.avatar_id = "junjun"
	Settings.set_reduced_motion(true)


func after_each() -> void:
	Session.player_name = _name
	Session.avatar_id = _avatar
	Session.save()
	Settings.set_reduced_motion(_motion)
	var layer: Node = get_tree().root.get_node_or_null(UiToast.LAYER_NAME)
	if layer != null:
		for toast: Node in layer.get_children():
			toast.queue_free()


func _toasts() -> Array[UiToast]:
	var found: Array[UiToast] = []
	var layer: Node = get_tree().root.get_node_or_null(UiToast.LAYER_NAME)
	if layer != null:
		for child: Node in layer.get_children():
			if child is UiToast and not child.is_queued_for_deletion():
				found.append(child as UiToast)
	return found


func _screen() -> TitleScreen:
	var phone: Control = autofree(Control.new()) as Control
	phone.custom_minimum_size = Vector2(720.0, 1280.0)
	phone.size = Vector2(720.0, 1280.0)
	add_child(phone)
	var screen: TitleScreen = autofree(TITLE_SCENE.instantiate()) as TitleScreen
	phone.add_child(screen)
	return screen


# ---- toast -----------------------------------------------------------------------------

func test_a_toast_shows_its_words_and_mark_over_everything_and_takes_no_touches() -> void:
	UiToast.say(self, "Room code copied!", "ok", 5.0)
	await wait_process_frames(2)
	var toasts: Array[UiToast] = _toasts()
	assert_eq(toasts.size(), 1)
	assert_eq((toasts[0].find_child("Words", true, false) as Label).text, "Room code copied!")
	assert_eq((toasts[0].find_child("Mark", true, false) as Label).text, "✓")
	assert_eq(toasts[0].get_parent().layer, UiToast.LAYER, "its own layer, above the screen")
	assert_gt(UiToast.LAYER, 100)
	for node: Node in toasts[0].find_children("*", "Control", true, false):
		assert_eq((node as Control).mouse_filter, Control.MOUSE_FILTER_IGNORE, "%s lets touches through" % node.name)
	assert_eq(toasts[0].mouse_filter, Control.MOUSE_FILTER_IGNORE)


func test_a_new_toast_replaces_the_one_on_screen() -> void:
	UiToast.say(self, "First", "ok", 5.0)
	await wait_process_frames(2)
	UiToast.say(self, "Second", "warn", 5.0)
	await wait_process_frames(3)
	var toasts: Array[UiToast] = _toasts()
	assert_eq(toasts.size(), 1, "never a stack of them")
	assert_eq(toasts[0].text, "Second")
	assert_eq(toasts[0].kind, "warn")


func test_every_kind_has_a_colour_and_a_mark_and_an_unknown_kind_is_a_plain_note() -> void:
	for kind: String in UiToast.KINDS:
		assert_true((UiToast.KINDS[kind] as Dictionary).has("color"))
		assert_false(String((UiToast.KINDS[kind] as Dictionary)["mark"]).is_empty())
	var odd: UiToast = UiToast.say(self, "Hmm", "nonsense", 5.0)
	assert_eq(odd.kind, "info")
	await wait_process_frames(2)


func test_a_toast_goes_away_by_itself() -> void:
	UiToast.say(self, "Bye", "ok", 0.3)
	await wait_process_frames(2)
	assert_eq(_toasts().size(), 1)
	await wait_seconds(0.7)
	assert_eq(_toasts().size(), 0)


func test_with_motion_on_a_toast_eases_in_and_with_reduce_motion_it_is_simply_there() -> void:
	UiToast.say(self, "Calm", "ok", 5.0)
	await wait_process_frames(2)
	assert_eq((_toasts()[0].find_child("Pill", true, false) as Control).modulate.a, 1.0, "reduce motion: no fade")
	Settings.set_reduced_motion(false)
	UiToast.say(self, "Lively", "ok", 5.0)
	await wait_process_frames(2)
	var pill: Control = _toasts()[0].find_child("Pill", true, false) as Control
	assert_lt(pill.modulate.a, 1.0, "it fades in")
	await wait_seconds(0.4)
	assert_eq(pill.modulate.a, 1.0)


# ---- status strip -----------------------------------------------------------------------

func test_the_strip_tells_waiting_from_a_problem_from_a_note() -> void:
	for text: String in ["Connecting...", "Connecting…", "Looking for the room...", "Starting the room...", "Rejoining...", "Loading..."]:
		assert_eq(UiStatusStrip.kind_of(text), "wait", text)
	for text: String in ["Connection lost. Try again!", "No room with that code. Check it and try again!", "Can't reach that address.", "That battle is over!", "Enter all 8 characters!", "Turn on your hotspot or connect to Wi-Fi first."]:
		assert_eq(UiStatusStrip.kind_of(text), "error", text)
	assert_eq(UiStatusStrip.kind_of("Online is coming soon!"), "info", "a promise is a note, not a problem")
	assert_eq(UiStatusStrip.kind_of(""), "info")


func test_dressing_a_label_gives_it_a_tinted_strip_and_keeps_its_words() -> void:
	var label: Label = autofree(Label.new()) as Label
	label.text = "Connection lost. Try again!"
	UiStatusStrip.style(label, "error")
	var box: StyleBoxFlat = label.get_theme_stylebox("normal") as StyleBoxFlat
	assert_not_null(box)
	assert_almost_eq(box.border_color.a, 0.85, 0.001)
	assert_eq(label.text, "Connection lost. Try again!", "dressing never changes what it says")
	var other: Label = autofree(UiStatusStrip.strip("x", "wait")) as Label
	assert_ne(box.border_color, (other.get_theme_stylebox("normal") as StyleBoxFlat).border_color, "each kind has its own colour")


func test_the_lobby_status_line_is_dressed_when_it_says_something() -> void:
	Session.lobby_hotspot = false
	Session.lobby_intent = 1
	var lobby: Node = autofree((load("res://scenes/ui/lobby.tscn") as PackedScene).instantiate()) as Node
	add_child(lobby)
	await wait_process_frames(2)
	lobby._set_status("Connection lost. Try again!")
	var box: StyleBoxFlat = lobby._status.get_theme_stylebox("normal") as StyleBoxFlat
	assert_eq(box.border_color, Color(UiStatusStrip.KINDS["error"], 0.85), "a problem is red")
	lobby._set_status("Connecting...")
	box = lobby._status.get_theme_stylebox("normal") as StyleBoxFlat
	assert_eq(box.border_color, Color(UiStatusStrip.KINDS["wait"], 0.85), "waiting is amber")
	assert_eq(lobby.status_text(), "Connecting...", "the words are unchanged")


# ---- contrast ---------------------------------------------------------------------------

func test_the_contrast_ratio_is_the_standard_one() -> void:
	assert_almost_eq(UiKit.contrast(Color.WHITE, Color.BLACK), 21.0, 0.01)
	assert_almost_eq(UiKit.contrast(Color.WHITE, Color.WHITE), 1.0, 0.001)
	assert_almost_eq(UiKit.contrast(Color("2fbf5a"), Color.WHITE), 2.41, 0.02, "the old Heal green that failed")


func test_every_chip_fill_keeps_white_text_readable() -> void:
	var fills: Dictionary[String, Color] = {
		"green": UiKit.CHIP_GREEN, "red": UiKit.CHIP_RED, "blue": UiKit.CHIP_BLUE, "purple": UiKit.CHIP_PURPLE,
		"slate": UiKit.CHIP_SLATE, "team blue": KalyeahTheme.TEAM_BLUE, "team red": KalyeahTheme.TEAM_RED,
	}
	for label: String in fills:
		assert_gte(UiKit.contrast(fills[label], Color.WHITE), 4.5, "%s chip" % label)
	# the weapon type is coloured words now (no pill): they must read on the dark card
	for kind: int in WeaponsPage.KIND_COLORS:
		assert_gte(UiKit.contrast((WeaponsPage.KIND_COLORS[kind] as Color).lightened(0.35), KalyeahTheme.SURFACE), 4.5, "weapon kind %d" % kind)


# ---- profile sheet ----------------------------------------------------------------------

func test_the_profile_sheet_has_only_your_name_and_no_face_to_pick() -> void:
	var screen: TitleScreen = _screen()
	var card: MenuDialog = screen.open_name_editor()
	assert_null(card.find_child("Faces", true, false), "no face selection at the main screen (owner)")
	assert_eq(card.find_children("Face_*", "Button", true, false).size(), 0)
	assert_eq((card.find_child("NameField", true, false) as LineEdit).text, "Ana")


func test_saving_changes_the_name_and_updates_the_name_tag() -> void:
	var screen: TitleScreen = _screen()
	await wait_frames(2)
	var card: MenuDialog = screen.open_name_editor()
	(card.find_child("NameField", true, false) as LineEdit).text = "Maria"
	(card.find_child("Save", true, false) as Button).pressed.emit()
	assert_eq(Session.player_name, "Maria")
	assert_eq(Session.avatar_id, "junjun", "the face is the kid you played last: saving the name leaves it")
	assert_false(card.is_open)
	assert_eq(screen.name_tag.name_label.text, "MARIA")
	await wait_process_frames(2)
	assert_eq(_toasts().size(), 1, "and says so")
	assert_eq(_toasts()[0].text, "Profile saved!")


func test_cancel_keeps_the_old_name() -> void:
	var screen: TitleScreen = _screen()
	var card: MenuDialog = screen.open_name_editor()
	(card.find_child("NameField", true, false) as LineEdit).text = "Changed"
	(card.find_child("Cancel", true, false) as Button).pressed.emit()
	assert_eq(Session.player_name, "Ana")
	assert_eq(_toasts().size(), 0, "nothing was saved, nothing is announced")


func test_an_empty_name_is_refused() -> void:
	var screen: TitleScreen = _screen()
	var card: MenuDialog = screen.open_name_editor()
	(card.find_child("NameField", true, false) as LineEdit).text = "   "
	(card.find_child("Save", true, false) as Button).pressed.emit()
	assert_true(card.is_open, "it stays open")
	assert_eq(Session.player_name, "Ana", "a refused save changes nothing")


func test_the_name_tag_opens_the_profile_sheet() -> void:
	var screen: TitleScreen = _screen()
	screen.name_tag.tap_button.pressed.emit()
	assert_not_null(screen.dialog.find_child("NameField", true, false), "the profile sheet (the Settings page has no profile card)")


# ---- copy code --------------------------------------------------------------------------

func test_copying_the_room_code_is_answered_with_a_toast() -> void:
	var view: HotspotRoomView = autofree(HotspotRoomView.new()) as HotspotRoomView
	add_child(view)
	view.set_online(true)
	view.show_room({"code": "K7M2Q9XA", "team_size": 2, "host": 1, "first_to": 3, "state": 0, "members": [
		{"player_id": 1, "name": "Ana", "team": 0, "ready": false, "connected": true, "char": "junjun"}],
		"can_start": false, "can_start_host": true, "map": &"street"}, 1)
	view.set_online(true)
	assert_true(view.has_room())
	view.copy_code()
	await wait_process_frames(2)
	assert_eq(_toasts().size(), 1)
	assert_eq(_toasts()[0].text, "Room code copied!")


# ---- chips ------------------------------------------------------------------------------

func test_every_chip_colour_passes_contrast_for_its_small_white_text() -> void:
	var fills: Dictionary[String, Color] = {
		"Attack": UiKit.CHIP_RED, "Control": UiKit.CHIP_PURPLE, "Block": UiKit.CHIP_BLUE, "Heal": UiKit.CHIP_GREEN,
		"Bato-bato-pik": UiKit.CHIP_SLATE, "You": KalyeahTheme.TEAM_BLUE, "Them": KalyeahTheme.TEAM_RED,
	}
	for chip: String in fills:
		assert_gte(UiKit.contrast(fills[chip], Color.WHITE), 4.5, "%s chip" % chip)


func test_a_toast_of_each_kind_shows() -> void:
	var screen: TitleScreen = _screen()
	await wait_frames(2)
	for kind: String in UiToast.KINDS:
		UiToast.say(screen, "Kind %s" % kind, kind)
		await wait_process_frames(2)
		assert_eq(_toasts()[0].kind, kind)
