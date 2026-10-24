class_name UiToast
extends Control
## A short message that answers an action ("Room code copied", "Profile saved") and goes away by itself.
## One at a time: a new one replaces the one on screen. It floats above the tab bar and the bottom
## controls, over everything (its own canvas layer), and never takes a touch. Reduce motion shows and
## hides it without the rise and fade.

const SECONDS: float = 2.2
const LAYER: int = 120
const LAYER_NAME: String = "UiToastLayer"
const BOTTOM: float = 170.0
const KINDS: Dictionary[String, Dictionary] = {
	"ok": {"color": Color("45dd78"), "mark": "✓"},
	"info": {"color": Color("4aa0ff"), "mark": "i"},
	"warn": {"color": Color("ffc233"), "mark": "!"},
	"error": {"color": Color("ff7a70"), "mark": "×"},
}

var text: String = ""
var kind: String = "ok"
var lifetime: float = SECONDS

var _panel: PanelContainer
var _tween: Tween


## Shows `message` above everything on the screen `host` belongs to. `kind` is ok, info, warn or error.
## Returns the toast (it joins the tree on the next frame).
static func say(host: Node, message: String, tone: String = "ok", seconds: float = SECONDS) -> UiToast:
	var root: Window = host.get_tree().root
	var layer: CanvasLayer = root.get_node_or_null(LAYER_NAME) as CanvasLayer
	if layer == null:
		layer = CanvasLayer.new()
		layer.name = LAYER_NAME
		layer.layer = LAYER
		root.add_child.call_deferred(layer)
	for old: Node in layer.get_children():
		if old is UiToast:
			(old as UiToast).dismiss(true)
	var toast: UiToast = UiToast.new()
	toast.text = message
	toast.kind = tone if KINDS.has(tone) else "info"
	toast.lifetime = seconds
	layer.add_child.call_deferred(toast)
	return toast


func _ready() -> void:
	name = "Toast"
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	var margin: MarginContainer = MarginContainer.new()
	margin.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	margin.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(margin)
	LobbyWidgets.apply_safe_area(margin, UiMetrics.PAGE_MARGIN, 0.0)
	margin.add_theme_constant_override("margin_bottom", int(BOTTOM) + margin.get_theme_constant("margin_bottom"))
	var column: VBoxContainer = VBoxContainer.new()
	column.alignment = BoxContainer.ALIGNMENT_END
	column.mouse_filter = Control.MOUSE_FILTER_IGNORE
	margin.add_child(column)
	_panel = PanelContainer.new()
	_panel.name = "Pill"
	_panel.size_flags_horizontal = Control.SIZE_SHRINK_CENTER
	_panel.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_panel.add_theme_stylebox_override("panel", UiSkin.plate("night", 16.0, UiSkin.RADIUS_XL))
	column.add_child(_panel)
	var row: HBoxContainer = HBoxContainer.new()
	row.add_theme_constant_override("separation", UiMetrics.COMPACT_GAP)
	row.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_panel.add_child(row)
	var spec: Dictionary = KINDS[kind]
	var mark: Label = Label.new()
	mark.name = "Mark"
	mark.text = spec["mark"] as String
	mark.theme_type_variation = &"DisplayLabel"
	mark.add_theme_font_size_override("font_size", UiMetrics.ACTION)
	mark.add_theme_color_override("font_color", spec["color"] as Color)
	mark.add_theme_constant_override("outline_size", 0)
	mark.mouse_filter = Control.MOUSE_FILTER_IGNORE
	row.add_child(mark)
	var words: Label = UiKit.text(text, UiMetrics.DETAIL, KalyeahTheme.TEXT, false)
	words.name = "Words"
	row.add_child(words)
	_panel.modulate.a = 0.0 if not Settings.reduced_motion else 1.0
	_panel.pivot_offset = _panel.get_combined_minimum_size() / 2.0
	_panel.scale = Vector2.ONE * (0.92 if not Settings.reduced_motion else 1.0)
	if Settings.reduced_motion:
		get_tree().create_timer(lifetime).timeout.connect(dismiss.bind(true))
		return
	_tween = create_tween()
	_tween.set_parallel(true)
	_tween.tween_property(_panel, "modulate:a", 1.0, 0.16)
	_tween.tween_property(_panel, "scale", Vector2.ONE, 0.16).set_trans(Tween.TRANS_BACK).set_ease(Tween.EASE_OUT)
	_tween.chain().tween_interval(maxf(lifetime - 0.5, 0.1))
	_tween.chain().tween_property(_panel, "modulate:a", 0.0, 0.3)
	_tween.chain().tween_callback(queue_free)


## Takes it away (`now`: without the fade).
func dismiss(now: bool = false) -> void:
	if is_queued_for_deletion():
		return
	if is_instance_valid(_tween):
		_tween.kill()
	if now or _panel == null or Settings.reduced_motion:
		queue_free()
		return
	var fade: Tween = create_tween()
	fade.tween_property(_panel, "modulate:a", 0.0, 0.15)
	fade.tween_callback(queue_free)
