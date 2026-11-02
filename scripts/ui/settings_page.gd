class_name SettingsPage
extends VBoxContainer
## The Settings page: Accessibility, Controls, Audio, Language (when there is a second one) and Graphics, then quitting. Your name and ID photo are changed on the name tag
## on Home (owner: no profile card here), and there is no How to play page (the walkthrough opens the first Practice by
## itself). There is no server address here: the online server is built into the game (the player only needs a room code).

signal quit_pressed

var quit_button: Button
var _content: VBoxContainer


func _ready() -> void:
	name = "SettingsPage"
	add_theme_constant_override("separation", UiMetrics.SECTION_GAP)
	add_child(UiKit.page_header("Settings"))
	_content = VBoxContainer.new()
	_content.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_content.add_theme_constant_override("separation", UiMetrics.SECTION_GAP)
	UiKit.add_gutter_scroll(self, _content, true)
	_content.add_child(_group("Accessibility", AccessibilitySection.new()))
	var controls: VBoxContainer = VBoxContainer.new()
	controls.add_theme_constant_override("separation", UiMetrics.GAP)
	if Settings.can_vibrate():
		controls.add_child(_vibration_row())
		controls.add_child(UiKit.divider())
	var layout: ControlOptions = ControlOptions.new()
	layout.part = ControlOptions.Part.LAYOUT
	controls.add_child(layout)
	_content.add_child(_group("Controls", controls))
	_content.add_child(_group("Audio", AudioOptions.new()))
	var languages: Array[String] = Settings.available_languages()
	if languages.size() > 1:
		_content.add_child(_group("Language", _language_row(languages)))
	var graphics: GraphicsOptions = GraphicsOptions.new()
	graphics.with_shake = false
	_content.add_child(_group("Graphics", graphics))
	quit_button = MenuDialog.make_button("Quit game", "red", UiMetrics.ACTION, Vector2(0.0, UiMetrics.TOUCH))
	quit_button.name = "QuitButton"
	quit_button.pressed.connect(quit_pressed.emit)
	_content.add_child(quit_button)


## A titled section: its heading and its card. The Graphics one keeps its name for the tests and the tour.
static func _group(title: String, body: Control) -> VBoxContainer:
	var group: VBoxContainer = VBoxContainer.new()
	group.name = title
	group.add_theme_constant_override("separation", UiMetrics.SECTION_GAP)
	group.add_child(UiKit.section_label(title))
	group.add_child(UiKit.card(body))
	return group


## The Vibration switch's row (phones that can vibrate).
static func _vibration_row() -> Control:
	var toggle: UiToggle = UiToggle.new()
	toggle.name = "VibrationToggle"
	toggle.set_state(Settings.vibration)
	toggle.pressed.connect(func() -> void: Settings.set_vibration(toggle.button_pressed))
	return UiKit.setting_row("Vibration", "Buzz on buttons and when you are hit", toggle)


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


## Vibration (phones only) and comfort, added to `parent` as setting rows.
static func fill(parent: Control, in_match: bool = false) -> void:
	if Settings.can_vibrate():
		var toggle: UiToggle = UiToggle.new()
		toggle.name = "VibrationToggle"
		toggle.set_state(Settings.vibration)
		toggle.pressed.connect(func() -> void: Settings.set_vibration(toggle.button_pressed))
		parent.add_child(UiKit.setting_row("Vibration", "Buzz on buttons and when you are hit", toggle))
		parent.add_child(UiKit.divider())
	parent.add_child(AudioOptions.new())
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
