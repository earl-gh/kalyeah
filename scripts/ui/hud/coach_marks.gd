class_name CoachMarks
extends Control
## The first-run walkthrough over a practice match: the screen dims, a round spotlight shows the control being
## explained (the hand of cards), and a card says what to do in one or two sentences. Four
## steps: move, aim and throw, how a point is scored, what being tagged means. Next goes on, Skip (or Back on
## Android) ends it. It runs once, the first time you play practice, and is replayed from Settings, How to play.
## While it shows the match's own controls are off, so a stray touch cannot throw anything.

signal finished(completed: bool)

const SPOT: Shader = preload("res://shaders/coach_spot.gdshader")
const RING: Color = Color("ffc233")

var steps: Array[Dictionary] = []
var index: int = 0
var running: bool = false

var title_label: Label
var text_label: Label
var kicker_label: Label
var next_button: Button
var skip_button: Button
var dots: HBoxContainer

var _hud: MatchHud
var _dim: ColorRect
var _material: ShaderMaterial
var _spots: Array[Vector3] = []
var _pulse: Tween


## The four steps. `target` names what is lit: the hand, or nothing.
static func steps_for(capture_seconds: float, points_to_win: int) -> Array[Dictionary]:
	return [
		{"title": "Move", "target": &"", "text": "Tap the street to run there; hold and drag to steer. Running uses stamina: stand still to get it back."},
		{"title": "Aim and throw", "target": &"hand", "text": "Drag a card onto the street and let go to throw it. A quick tap aims for you. Drop it back on your hand to cancel. Each card costs the elixir in its drop; the bar fills by itself."},
		{"title": "Score a point", "target": &"",
			"text": "Get your whole team into the enemy base and hold it for %s seconds, with no defender left inside it, or tag everyone on the other team. First to %d points wins." % [String.num(capture_seconds, 0 if is_equal_approx(capture_seconds, roundf(capture_seconds)) else 1), points_to_win]},
		{"title": "Tagged?", "target": &"", "text": "When your health runs out you wait on the sidewalk. A free teammate who stays in your rescue area for five seconds sets you free."},
	]


func _init(hud: MatchHud = null) -> void:
	_hud = hud
	name = "CoachMarks"
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	visible = false
	_dim = ColorRect.new()
	_dim.name = "Dim"
	_dim.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_dim.mouse_filter = Control.MOUSE_FILTER_STOP
	_material = ShaderMaterial.new()
	_material.shader = SPOT
	_dim.material = _material
	add_child(_dim)
	var center: CenterContainer = CenterContainer.new()
	center.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	center.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(center)
	var card: PanelContainer = UiKit.card(null, "raised", 28.0)
	card.name = "Card"
	card.custom_minimum_size = Vector2(580.0, 0.0)
	center.add_child(card)
	var column: VBoxContainer = VBoxContainer.new()
	column.add_theme_constant_override("separation", UiMetrics.GAP)
	card.add_child(column)
	kicker_label = UiKit.section_label("")
	kicker_label.name = "Kicker"
	column.add_child(kicker_label)
	title_label = Label.new()
	title_label.name = "Title"
	UiKit.letter(title_label, UiMetrics.DISPLAY, Color("ffd25a"))
	column.add_child(title_label)
	text_label = UiKit.text("", UiMetrics.BODY, KalyeahTheme.TEXT)
	text_label.name = "Words"
	column.add_child(text_label)
	dots = HBoxContainer.new()
	dots.name = "Dots"
	dots.alignment = BoxContainer.ALIGNMENT_CENTER
	dots.add_theme_constant_override("separation", 10)
	column.add_child(dots)
	var buttons: HBoxContainer = HBoxContainer.new()
	buttons.add_theme_constant_override("separation", UiMetrics.COMPACT_GAP)
	column.add_child(buttons)
	skip_button = MenuDialog.make_button("Skip", "grey", UiMetrics.ACTION, Vector2(0.0, 92.0))
	skip_button.name = "Skip"
	skip_button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	skip_button.size_flags_stretch_ratio = 0.8
	skip_button.pressed.connect(skip)
	buttons.add_child(skip_button)
	next_button = MenuDialog.make_button("Next", "orange", UiMetrics.ACTION, Vector2(0.0, 92.0))
	next_button.name = "Next"
	next_button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	next_button.size_flags_stretch_ratio = 1.4
	next_button.pressed.connect(next)
	buttons.add_child(next_button)


## Begins the walkthrough (the match's controls go quiet until it ends).
func start(capture_seconds: float, points_to_win: int) -> void:
	steps = steps_for(capture_seconds, points_to_win)
	index = 0
	running = true
	visible = true
	if _hud != null:
		_hud.set_controls_active(false)
	for child: Node in dots.get_children():
		dots.remove_child(child)
		child.queue_free()
	for i: int in steps.size():
		var dot: Panel = Panel.new()
		dot.custom_minimum_size = Vector2(14.0, 14.0)
		dots.add_child(dot)
	_show_step()
	next_button.grab_focus.call_deferred()


func next() -> void:
	if not running:
		return
	if index >= steps.size() - 1:
		_end(true)
		return
	index += 1
	_show_step()


func skip() -> void:
	if running:
		_end(false)


func _show_step() -> void:
	var step: Dictionary = steps[index]
	kicker_label.text = "Step %d of %d" % [index + 1, steps.size()]
	title_label.text = (step["title"] as String).to_upper()
	text_label.text = step["text"] as String
	next_button.text = "Got it" if index == steps.size() - 1 else "Next"
	skip_button.visible = index < steps.size() - 1
	for i: int in dots.get_child_count():
		var dot: Panel = dots.get_child(i) as Panel
		var box: StyleBoxFlat = StyleBoxFlat.new()
		box.bg_color = Color("ffc233") if i == index else Color("3a4a7e")
		box.set_corner_radius_all(UiSkin.RADIUS_S)
		dot.add_theme_stylebox_override("panel", box)
	_spots = _spots_for(step["target"] as StringName)
	_material.set_shader_parameter("spot_a", _spots[0] if _spots.size() > 0 else Vector3.ZERO)
	_material.set_shader_parameter("spot_b", _spots[1] if _spots.size() > 1 else Vector3.ZERO)
	queue_redraw()
	_start_pulse()


## The circles (x, y, radius) round the controls a step is about, in this overlay's own pixels.
func _spots_for(target: StringName) -> Array[Vector3]:
	var found: Array[Vector3] = []
	if _hud == null:
		return found
	if target != &"hand":
		return found
	# two circles, each round a pair of cards (the shader lights two spots)
	var hand: CardHand = _hud.hand
	var origin: Vector2 = hand.get_global_rect().position - global_position
	for pair: int in 2:
		var rect: Rect2 = hand.card_rects[pair * 2].merge(hand.card_rects[pair * 2 + 1])
		var centre: Vector2 = origin + rect.get_center()
		found.append(Vector3(centre.x, centre.y, rect.size.x * 0.5 + 12.0))
	return found


func _draw() -> void:
	for spot: Vector3 in _spots:
		draw_arc(Vector2(spot.x, spot.y), spot.z, 0.0, TAU, 64, Color(RING, 0.95), 5.0, true)


func _start_pulse() -> void:
	if is_instance_valid(_pulse):
		_pulse.kill()
	modulate = Color.WHITE
	if Settings.reduced_motion or not is_inside_tree() or _spots.is_empty():
		return
	_pulse = create_tween().set_loops()
	_pulse.tween_method(_set_ring, 0.55, 1.0, 0.6).set_trans(Tween.TRANS_SINE)
	_pulse.tween_method(_set_ring, 1.0, 0.55, 0.6).set_trans(Tween.TRANS_SINE)


func _set_ring(strength: float) -> void:
	_material.set_shader_parameter("feather", 6.0 + (1.0 - strength) * 10.0)


func _end(completed: bool) -> void:
	running = false
	visible = false
	if is_instance_valid(_pulse):
		_pulse.kill()
	Session.coach_seen = true
	Session.coach_pending = false
	Session.save()
	if _hud != null:
		_hud.set_controls_active(true)
	finished.emit(completed)


## The phone's back button skips the walkthrough.
func _notification(what: int) -> void:
	if what == NOTIFICATION_WM_GO_BACK_REQUEST:
		skip.call_deferred()
