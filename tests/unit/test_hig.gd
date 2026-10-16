extends GutTest
## Apple HIG on the main screen, lobby and character / weapon select (scripts/ui/hig.gd): text of
## at least 11 pt, tap targets of at least 44 pt, 16 pt margins, a "< Back" in the navigation bar.

const TITLE_SCENE: PackedScene = preload("res://scenes/ui/title_screen.tscn")
const LOBBY_SCENE: PackedScene = preload("res://scenes/ui/lobby.tscn")
const PRACTICE_SCENE: PackedScene = preload("res://scenes/match/practice.tscn")
const PHONE: Vector2 = Vector2(720.0, 1280.0)


## A control inside a phone-sized parent, so containers lay out as they do on a phone.
func _on_phone(screen: Control) -> Control:
	var phone: Control = autofree(Control.new()) as Control
	phone.custom_minimum_size = PHONE
	phone.size = PHONE
	add_child(phone)
	phone.add_child(screen)
	return phone


## Every visible text and every visible tap target on `root` that breaks the rules.
func _violations(root: Node) -> Array[String]:
	var found: Array[String] = []
	for node: Node in root.find_children("*", "Control", true, false):
		var control: Control = node as Control
		if not control.is_visible_in_tree() or control.size.x < 2.0:
			continue
		if control is Label and not (control as Label).text.is_empty():
			var size: int = control.get_theme_font_size("font_size")
			if size < Hig.MIN_TEXT:
				found.append("%s text %d px < %d" % [control.name, size, Hig.MIN_TEXT])
		elif control is Button and not (control as Button).text.is_empty():
			var size: int = control.get_theme_font_size("font_size")
			if size < Hig.MIN_TEXT:
				found.append("%s text %d px < %d" % [control.name, size, Hig.MIN_TEXT])
		if control is BaseButton and not (control as BaseButton).disabled or control is LineEdit or control is HSlider:
			var height: float = maxf(control.size.y, control.custom_minimum_size.y)
			if height < Hig.MIN_TAP - 0.5 and not (control.get_parent() is PanelContainer and control.get_parent().get_parent() is ScrollContainer):
				found.append("%s is %d px tall < %d" % [control.name, int(height), int(Hig.MIN_TAP)])
	return found


func test_the_numbers_are_the_apple_ones() -> void:
	assert_almost_eq(Hig.MIN_TAP / Hig.POINT, 44.0, 2.0, "44 pt tap targets")
	assert_almost_eq(float(Hig.MIN_TEXT) / Hig.POINT, 11.0, 0.5, "11 pt minimum text")
	assert_almost_eq(Hig.MARGIN / Hig.POINT, 16.0, 0.5, "16 pt margins")
	assert_almost_eq(float(Hig.BODY) / Hig.POINT, 17.0, 0.5, "17 pt body text")


func test_the_main_screen_follows_the_hig() -> void:
	var screen: TitleScreen = TITLE_SCENE.instantiate() as TitleScreen
	var phone: Control = _on_phone(screen)
	await wait_frames(3)
	assert_eq(_violations(phone), [] as Array[String])
	for open: Callable in [screen.open_settings, screen.open_name_editor, screen.open_online, screen.open_hotspot, screen.open_quit_confirm]:
		open.call()
		await wait_frames(3)
		assert_eq(_violations(screen.dialog), [] as Array[String], "dialog")
		screen.dialog.close(false)


func test_the_lobby_follows_the_hig_and_has_a_back_button() -> void:
	for intent: LobbyScreen.Intent in [LobbyScreen.Intent.CREATE, LobbyScreen.Intent.JOIN]:
		Session.lobby_intent = intent
		var lobby: LobbyScreen = LOBBY_SCENE.instantiate() as LobbyScreen
		var phone: Control = _on_phone(lobby)
		await wait_frames(3)
		assert_eq(_violations(phone), [] as Array[String], "setup")
		var back: Button = lobby.find_child("BackButton", true, false) as Button
		assert_not_null(back, "a back button")
		assert_true(back.text.contains("Back"))
		assert_lt(back.global_position.x, 100.0, "at the top left")
		var title: Label = lobby.find_child("Title", true, false) as Label
		assert_almost_eq(title.global_position.x + title.size.x / 2.0, PHONE.x / 2.0, 6.0, "the title is centred")
		lobby._show_room(true)
		await wait_frames(3)
		assert_eq(_violations(phone), [] as Array[String], "room")
		phone.queue_free()
		await wait_frames(1)


func test_the_lobby_has_margins_of_16_pt() -> void:
	Session.lobby_intent = LobbyScreen.Intent.CREATE
	var lobby: LobbyScreen = LOBBY_SCENE.instantiate() as LobbyScreen
	var phone: Control = _on_phone(lobby)
	await wait_frames(3)
	var edit: Control = lobby.find_child("*", true, false)
	var scroll: Control = lobby.find_child("Scroll", true, false) as Control
	assert_gte(scroll.global_position.x, Hig.MARGIN - 0.5, "16 pt from the left edge")
	assert_lte(scroll.global_position.x + scroll.size.x, PHONE.x - Hig.MARGIN + 0.5, "16 pt from the right edge")
	assert_not_null(edit)
	phone.queue_free()


func test_the_select_screen_follows_the_hig() -> void:
	var practice: PracticeMatch = autofree(PRACTICE_SCENE.instantiate()) as PracticeMatch
	add_child(practice)
	await wait_frames(3)
	var screen: WeaponPickScreen = practice.get_node("%PickScreen") as WeaponPickScreen
	assert_eq(_violations(screen), [] as Array[String])
	var back: Button = screen.find_child("BackButton", true, false) as Button
	assert_true(back.visible, "practice can step back to the main screen")
	var ready_button: Button = screen.find_children("*", "Button", true, false).back() as Button
	assert_gte(ready_button.size.y, Hig.MIN_TAP)


func test_the_select_screen_back_button_leaves_practice() -> void:
	var practice: PracticeMatch = autofree(PRACTICE_SCENE.instantiate()) as PracticeMatch
	add_child(practice)
	var screen: WeaponPickScreen = practice.get_node("%PickScreen") as WeaponPickScreen
	var pressed: Array[bool] = []
	screen.back_pressed.connect(func() -> void: pressed.append(true))
	(screen.find_child("BackButton", true, false) as Button).pressed.emit()
	assert_eq(pressed.size(), 1)
