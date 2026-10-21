class_name ConsolePanel
extends Control
## The bottom of the screen, a modern game controller deck: dark graphite that fades a little darker toward the screen's edge, a thin
## lit lip along its rounded top, and soft round wells where the D-pad and the two skill buttons sit. The arena stays above it
## (there are no cards to deploy here, so the space is the controller). It only draws; it never takes a touch.

const BODY_TOP: Color = Color(0.165, 0.175, 0.225)
const BODY_BOTTOM: Color = Color(0.055, 0.06, 0.085)
const LIP: Color = Color(0.62, 0.68, 0.85, 0.55)
const WELL: Color = Color(0.0, 0.0, 0.0, 0.34)
const WELL_RIM: Color = Color(0.7, 0.76, 0.95, 0.16)
const CORNER: float = 40.0
const HEALTH_FONT: int = 40
const HEALTH_TEXT: Color = Color(0.93, 0.95, 1.0)

## Round recesses {centre (from this panel's top-left), radius}: set by the HUD's layout.
var sockets: Array[Dictionary] = []
## The speaker slots sit toward this side (true = the right).
var slots_right: bool = true
## Your health, shown in the empty middle of the shell: 0 to 1, the number, and whether it shows at all (not while tagged).
var health_fraction: float = 1.0
var health_text: String = ""
var health_shown: bool = false
## The wells show only while the controls do (not while you wait in the box, when the deck carries a message instead).
var wells_shown: bool = true


## Shows or hides the wells under the controls.
func set_wells_shown(shown: bool) -> void:
	if shown == wells_shown:
		return
	wells_shown = shown
	queue_redraw()


func set_sockets(list: Array[Dictionary], right: bool) -> void:
	sockets = list
	slots_right = right
	queue_redraw()


## Your health in the middle of the console, between the pad and the buttons, where the thumbs do not cover it.
func set_health(hp: int, max_hp: int, shown: bool) -> void:
	var fraction: float = clampf(float(hp) / maxf(float(max_hp), 1.0), 0.0, 1.0)
	var text: String = str(hp)
	if shown == health_shown and text == health_text and is_equal_approx(fraction, health_fraction):
		return
	health_shown = shown
	health_text = text
	health_fraction = fraction
	queue_redraw()


## The free stretch of the shell between the pad's socket and the nearest weapon socket: (centre x, width). Zero width without sockets.
func gap() -> Vector2:
	if sockets.size() < 2:
		return Vector2.ZERO
	var pad: Dictionary = sockets[0]
	var pad_x: float = (pad["at"] as Vector2).x
	var pad_edge: float = pad_x
	var best: float = INF
	var near_edge: float = pad_x
	for index: int in range(1, sockets.size()):
		var socket: Dictionary = sockets[index]
		var x: float = (socket["at"] as Vector2).x
		if absf(x - pad_x) < best:
			best = absf(x - pad_x)
			near_edge = x - signf(x - pad_x) * (socket["radius"] as float)
	pad_edge = pad_x + signf(near_edge - pad_x) * (pad["radius"] as float)
	return Vector2((pad_edge + near_edge) / 2.0, absf(near_edge - pad_edge))


func _draw_health() -> void:
	var free: Vector2 = gap()
	if not health_shown or free.y < 90.0:
		return
	var width: float = minf(free.y - 24.0, 120.0)
	var top: float = 70.0
	var font: Font = KalyeahTheme.ACTION_FONT
	var label: String = health_text
	var label_size: Vector2 = font.get_string_size(label, HORIZONTAL_ALIGNMENT_LEFT, -1.0, HEALTH_FONT)
	draw_string(font, Vector2(free.x - label_size.x / 2.0, top + label_size.y * 0.7), label, HORIZONTAL_ALIGNMENT_LEFT, -1.0, HEALTH_FONT, HEALTH_TEXT)
	var track: Rect2 = Rect2(free.x - width / 2.0, top + label_size.y + 8.0, width, 16.0)
	var shell: StyleBoxFlat = StyleBoxFlat.new()
	shell.bg_color = Color(0.03, 0.035, 0.055)
	shell.set_corner_radius_all(8)
	shell.anti_aliasing = true
	draw_style_box(shell, track.grow(3.0))
	var fill: StyleBoxFlat = StyleBoxFlat.new()
	fill.bg_color = TeamColors.bar_self()
	fill.set_corner_radius_all(6)
	fill.anti_aliasing = true
	if health_fraction > 0.0:
		draw_style_box(fill, Rect2(track.position, Vector2(maxf(track.size.x * health_fraction, 12.0), track.size.y)))


func _draw() -> void:
	var w: float = size.x
	var h: float = size.y
	var shell: StyleBoxFlat = StyleBoxFlat.new()
	shell.bg_color = BODY_TOP
	shell.corner_radius_top_left = int(CORNER)
	shell.corner_radius_top_right = int(CORNER)
	shell.anti_aliasing = true
	shell.shadow_color = Color(0.0, 0.0, 0.0, 0.45)
	shell.shadow_size = 18
	draw_style_box(shell, Rect2(0.0, 0.0, w, h + 30.0))
	# the deck darkens toward the bottom edge
	var fade_top: Color = Color(BODY_BOTTOM, 0.0)
	var fade_bottom: Color = Color(BODY_BOTTOM, 1.0)
	draw_polygon(PackedVector2Array([Vector2(0.0, CORNER), Vector2(w, CORNER), Vector2(w, h + 30.0), Vector2(0.0, h + 30.0)]),
		PackedColorArray([fade_top, fade_top, fade_bottom, fade_bottom]))
	# the lit lip along the rounded top
	draw_arc(Vector2(CORNER, CORNER), CORNER - 1.5, PI, PI * 1.5, 16, LIP, 3.0, true)
	draw_arc(Vector2(w - CORNER, CORNER), CORNER - 1.5, PI * 1.5, TAU, 16, LIP, 3.0, true)
	draw_line(Vector2(CORNER, 1.5), Vector2(w - CORNER, 1.5), LIP, 3.0, true)
	for socket: Dictionary in sockets:
		if not wells_shown:
			break
		var at: Vector2 = socket["at"] as Vector2
		var radius: float = socket["radius"] as float
		draw_circle(at, radius, WELL)
		draw_arc(at, radius, PI * 0.1, PI * 1.1, 32, WELL_RIM, 2.0, true)
	_draw_health()


