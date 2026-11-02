class_name GraphicsOptions
extends VBoxContainer
## The Graphics settings (Settings page): Frame rate (30 or 60) and Performance (Standard or Turbo) (and Screen shake when `with_shake`). Each applies at once
## (`Settings`); the engine side is `Settings.apply_graphics`.

var frame_choice: UiSegmented
var performance_choice: UiSegmented
## Screen shake is an accessibility setting (motion sensitivity): the Settings page shows it with the others (`AccessibilitySection`), so
## it is left out here; the room's own graphics list, if one ever shows this block alone, keeps it.
var with_shake: bool = true
var shake_toggle: UiToggle


func _ready() -> void:
	name = "GraphicsOptions"
	add_theme_constant_override("separation", UiMetrics.GAP)
	frame_choice = _choice("FrameRate", Settings.FRAME_RATES.map(func(rate: int) -> Dictionary: return {"id": StringName(str(rate)), "label": str(rate)}))
	frame_choice.select(StringName(str(Settings.frame_rate)), false)
	frame_choice.chosen.connect(func(id: StringName) -> void: Settings.set_frame_rate(int(String(id))))
	add_child(UiKit.setting_row("Frame rate", "60 is smoother, 30 saves battery", Control.new()))
	add_child(frame_choice)
	add_child(UiKit.divider())
	performance_choice = _choice("Performance", [{"id": &"standard", "label": "Standard"}, {"id": &"turbo", "label": "Turbo"}])
	performance_choice.select(Settings.performance, false)
	performance_choice.chosen.connect(Settings.set_performance)
	add_child(UiKit.setting_row("Performance", "Turbo draws the street a little softer so slower phones keep up", Control.new()))
	add_child(performance_choice)
	if with_shake:
		add_child(UiKit.divider())
		shake_toggle = shake_toggle_new()
		add_child(shake_row(shake_toggle))


## The Screen shake switch (on = the street shakes when you are hit).
static func shake_toggle_new() -> UiToggle:
	var toggle: UiToggle = UiToggle.new()
	toggle.name = "ScreenShake"
	toggle.set_state(Settings.screen_shake)
	toggle.toggled.connect(Settings.set_screen_shake)
	return toggle


static func shake_row(toggle: UiToggle) -> Control:
	return UiKit.setting_row("Screen shake", "The street shakes when you are hit", toggle)


func _choice(choice_name: String, options: Array) -> UiSegmented:
	var typed: Array[Dictionary] = []
	typed.assign(options)
	var choice: UiSegmented = UiSegmented.new(typed, UiMetrics.TOUCH, UiMetrics.BODY)
	choice.name = choice_name
	return choice
