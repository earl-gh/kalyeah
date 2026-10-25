class_name SettingsPage
extends ScrollContainer
## The Settings page: who you are (face and name, with a way to rename), vibration (phones)
## and comfort, then quitting. There is no server address here: the online server is built into the
## game (the player only needs a room code).

signal profile_pressed
signal quit_pressed
signal howto_pressed
signal book_pressed
signal history_pressed

var profile_button: Button
var howto_button: Button
var book_button: Button
var history_button: Button
var quit_button: Button
var name_label: Label
var face: TextureRect
var _content: VBoxContainer


func _ready() -> void:
	name = "SettingsPage"
	horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	follow_focus = true
	_content = VBoxContainer.new()
	_content.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_content.add_theme_constant_override("separation", UiMetrics.SECTION_GAP)
	add_child(_content)
	_content.add_child(UiKit.page_header("Settings", "Make Kalyeah yours."))
	_content.add_child(_profile_card())
	var preferences: VBoxContainer = VBoxContainer.new()
	preferences.add_theme_constant_override("separation", UiMetrics.SECTION_GAP)
	preferences.add_child(UiKit.section_label("Comfort"))
	var rows: VBoxContainer = VBoxContainer.new()
	rows.add_theme_constant_override("separation", UiMetrics.GAP)
	fill(rows)
	preferences.add_child(UiKit.card(rows))
	_content.add_child(preferences)
	book_button = MenuDialog.make_button("Book", "grey", UiMetrics.ACTION, Vector2(0.0, UiMetrics.TOUCH))
	book_button.name = "BookButton"
	book_button.accessibility_description = "Your kids and the weapons"
	book_button.pressed.connect(book_pressed.emit)
	_content.add_child(book_button)
	history_button = MenuDialog.make_button("History", "grey", UiMetrics.ACTION, Vector2(0.0, UiMetrics.TOUCH))
	history_button.name = "HistoryButton"
	history_button.accessibility_description = "Your last battles"
	history_button.pressed.connect(history_pressed.emit)
	_content.add_child(history_button)
	howto_button = MenuDialog.make_button("How to play", "grey", UiMetrics.ACTION, Vector2(0.0, UiMetrics.TOUCH))
	howto_button.name = "HowToButton"
	howto_button.pressed.connect(howto_pressed.emit)
	_content.add_child(howto_button)
	quit_button = MenuDialog.make_button("Quit game", "red", UiMetrics.ACTION, Vector2(0.0, UiMetrics.TOUCH))
	quit_button.name = "QuitButton"
	quit_button.pressed.connect(quit_pressed.emit)
	_content.add_child(quit_button)
	visibility_changed.connect(refresh)


## The language choice: shown only when a second language is ready (see Settings.available_languages).
static func _language_row(languages: Array[String]) -> Control:
	var options: Array[Dictionary] = []
	for code: String in languages:
		options.append({"id": StringName(code), "label": Settings.LANGUAGES[code]})
	var choice: UiSegmented = UiSegmented.new(options, 72.0, UiMetrics.BODY)
	choice.name = "LanguageChoice"
	choice.select(StringName(Settings.language), false)
	choice.chosen.connect(func(id: StringName) -> void: Settings.set_language(String(id)))
	var column: VBoxContainer = VBoxContainer.new()
	column.name = "LanguageOptions"
	column.add_theme_constant_override("separation", UiMetrics.GAP)
	column.add_child(UiKit.setting_row("Language", "The words in the game", Control.new()))
	column.add_child(choice)
	return column


## The profile card: your face, your name and the button that changes it.
func _profile_card() -> Control:
	var row: HBoxContainer = HBoxContainer.new()
	row.add_theme_constant_override("separation", UiMetrics.GAP)
	face = UiKit.avatar(Session.avatar_id, 104.0)
	row.add_child(face)
	var words: VBoxContainer = VBoxContainer.new()
	words.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	words.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	words.add_theme_constant_override("separation", 6)
	name_label = Label.new()
	name_label.name = "PlayerName"
	# the body font, so the name is shown as it was typed (the display font is all capitals)
	name_label.add_theme_font_override("font", KalyeahTheme.BUTTON_FONT)
	name_label.add_theme_font_size_override("font_size", UiMetrics.HEADLINE)
	name_label.add_theme_constant_override("outline_size", 6)
	name_label.text_overrun_behavior = TextServer.OVERRUN_TRIM_ELLIPSIS
	name_label.text = Session.player_name
	words.add_child(name_label)
	profile_button = MenuDialog.make_button("Edit profile", "grey", UiMetrics.DETAIL, Vector2(0.0, UiMetrics.MIN_TARGET))
	profile_button.name = "ProfileButton"
	profile_button.size_flags_horizontal = Control.SIZE_SHRINK_BEGIN
	profile_button.pressed.connect(profile_pressed.emit)
	words.add_child(profile_button)
	row.add_child(words)
	return UiKit.card(row)


## Shows the name and face you have now (after a rename or a new profile picture).
func refresh() -> void:
	if name_label == null:
		return
	name_label.text = Session.player_name
	var picture: Texture2D = KidModel.minimap_face(StringName(Session.avatar_id))
	face.texture = picture if picture != null else KidModel.portrait(StringName(Session.avatar_id))


## Vibration (phones only) and comfort, added to `parent` as setting rows.
static func fill(parent: Control, in_match: bool = false) -> void:
	if Settings.can_vibrate():
		var toggle: UiToggle = UiToggle.new()
		toggle.name = "VibrationToggle"
		toggle.set_state(Settings.vibration)
		toggle.pressed.connect(func() -> void: Settings.set_vibration(toggle.button_pressed))
		parent.add_child(UiKit.setting_row("Vibration", "Buzz on buttons and when you are hit", toggle))
		parent.add_child(UiKit.divider())
	parent.add_child(AccessibilityOptions.new())
	parent.add_child(UiKit.divider())
	var controls: ControlOptions = ControlOptions.new()
	controls.in_match = in_match
	parent.add_child(controls)
	var languages: Array[String] = Settings.available_languages()
	if languages.size() > 1:
		parent.add_child(UiKit.divider())
		parent.add_child(_language_row(languages))
