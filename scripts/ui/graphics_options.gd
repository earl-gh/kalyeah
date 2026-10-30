class_name GraphicsOptions
extends VBoxContainer
## The Graphics settings (Settings page): Frame rate (30 or 60), Performance (Standard or Turbo) and Screen shake. Each applies at once
## (`Settings`); the engine side is `Settings.apply_graphics`.

var frame_choice: UiSegmented
var performance_choice: UiSegmented
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
	add_child(UiKit.divider())
	shake_toggle = UiToggle.new()
	shake_toggle.name = "ScreenShake"
	shake_toggle.set_state(Settings.screen_shake)
	shake_toggle.toggled.connect(Settings.set_screen_shake)
	add_child(UiKit.setting_row("Screen shake", "The street shakes when you are hit", shake_toggle))


func _choice(choice_name: String, options: Array) -> UiSegmented:
	var typed: Array[Dictionary] = []
	typed.assign(options)
	var choice: UiSegmented = UiSegmented.new(typed, UiMetrics.TOUCH, UiMetrics.BODY)
	choice.name = choice_name
	return choice
