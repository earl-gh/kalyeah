class_name SettingsPage
extends VBoxContainer
## The Settings tab: your profile, sound, vibration (phones) and quitting. There is no server
## address here: the online server is built into the game (the player only needs a room code).

signal profile_pressed
signal quit_pressed

var profile_button: Button
var quit_button: Button


func _ready() -> void:
	name = "SettingsPage"
	add_theme_constant_override("separation", UiMetrics.SECTION_GAP)
	var title: Label = Label.new()
	title.theme_type_variation = &"DisplayLabel"
	title.text = "SETTINGS"
	title.add_theme_font_size_override("font_size", UiMetrics.TITLE)
	add_child(title)
	profile_button = MenuDialog.make_button("PROFILE", "grey", 30, Vector2(0.0, 88.0))
	profile_button.name = "ProfileButton"
	profile_button.pressed.connect(profile_pressed.emit)
	add_child(profile_button)
	fill(self)
	quit_button = MenuDialog.make_button("QUIT GAME", "grey", 26, Vector2(0.0, 88.0))
	quit_button.name = "QuitButton"
	quit_button.pressed.connect(quit_pressed.emit)
	add_child(quit_button)


## Sound and vibration (phones only), added to `parent`.
static func fill(parent: Control) -> void:
	var sound: HBoxContainer = _row(parent, "Sound")
	var slider: HSlider = DialogButtons.slider(Settings.volume)
	slider.name = "VolumeSlider"
	parent.add_child(slider)
	var percent: Label = Label.new()
	percent.custom_minimum_size = Vector2(84.0, 0.0)
	percent.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	percent.add_theme_font_size_override("font_size", UiMetrics.BODY)
	percent.text = "%d%%" % roundi(Settings.volume * 100.0)
	sound.add_child(percent)
	slider.value_changed.connect(func(value: float) -> void:
		Settings.set_volume(value)
		percent.text = "%d%%" % roundi(value * 100.0))
	slider.drag_ended.connect(func(_changed: bool) -> void: Settings.play_ui())
	if Settings.can_vibrate():
		var buzz: HBoxContainer = _row(parent, "Vibration")
		var toggle: Button = MenuDialog.make_button("ON" if Settings.vibration else "OFF", "green" if Settings.vibration else "grey", 28, Vector2(150.0, UiMetrics.TOUCH))
		toggle.name = "VibrationToggle"
		toggle.pressed.connect(func() -> void:
			Settings.set_vibration(not Settings.vibration)
			toggle.text = "ON" if Settings.vibration else "OFF"
			KalyeahTheme.style_button(toggle, "green" if Settings.vibration else "grey"))
		buzz.add_child(toggle)


static func _row(parent: Control, label_text: String) -> HBoxContainer:
	var row: HBoxContainer = HBoxContainer.new()
	row.add_theme_constant_override("separation", UiMetrics.SECTION_GAP)
	var label: Label = Label.new()
	label.text = label_text
	label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	label.add_theme_font_size_override("font_size", UiMetrics.BODY)
	row.add_child(label)
	parent.add_child(row)
	return row
