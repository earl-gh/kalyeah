class_name ConsolePanel
extends Control
## The bottom of the screen, like the body of a Game Boy: a light grey shell with a rounded top edge that the arena
## sits above (there are no cards to deploy here, so the space is the controller). The D-pad and the two skill
## buttons sit in round sockets on it. It only draws; it never takes a touch.

const BODY: Color = Color(0.77, 0.78, 0.82)
const LIP: Color = Color(0.93, 0.94, 0.97)
const SHADE: Color = Color(0.52, 0.53, 0.6)
const SOCKET: Color = Color(0.6, 0.61, 0.69)
const SOCKET_RIM: Color = Color(0.93, 0.94, 0.97, 0.7)
const CORNER: float = 44.0
const SLOT_COLOR: Color = Color(0.5, 0.51, 0.58)
const HEALTH_FONT: int = 40

## Round recesses {centre (from this panel's top-left), radius}: set by the HUD's layout.
var sockets: Array[Dictionary] = []
## The speaker slots sit toward this side (true = the right).
var slots_right: bool = true
## Your health, shown in the empty middle of the shell: 0 to 1, the number, and whether it shows at all (not while tagged).
var health_fraction: float = 1.0
var health_text: String = ""
var health_shown: bool = false


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
	draw_string(font, Vector2(free.x - label_size.x / 2.0, top + label_size.y * 0.7), label, HORIZONTAL_ALIGNMENT_LEFT, -1.0, HEALTH_FONT, Color(0.12, 0.13, 0.2))
	var track: Rect2 = Rect2(free.x - width / 2.0, top + label_size.y + 8.0, width, 16.0)
	var shell: StyleBoxFlat = StyleBoxFlat.new()
	shell.bg_color = Color(0.2, 0.21, 0.29)
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
	shell.bg_color = BODY
	shell.corner_radius_top_left = int(CORNER)
	shell.corner_radius_top_right = int(CORNER)
	shell.border_color = LIP
	shell.border_width_top = 5
	shell.anti_aliasing = true
	shell.shadow_color = Color(0.0, 0.0, 0.0, 0.35)
	shell.shadow_size = 14
	draw_style_box(shell, Rect2(0.0, 0.0, w, h + 30.0))
	# a darker lower edge where the shell turns under
	draw_rect(Rect2(0.0, h - 14.0, w, 14.0), Color(SHADE, 0.35))
	for socket: Dictionary in sockets:
		var at: Vector2 = socket["at"] as Vector2
		var radius: float = socket["radius"] as float
		draw_circle(at, radius, SOCKET)
		draw_arc(at, radius, PI * 0.15, PI * 1.15, 32, Color(0.0, 0.0, 0.0, 0.25), 3.0, true)
		draw_arc(at, radius, PI * 1.15, PI * 2.15, 32, SOCKET_RIM, 3.0, true)
	_draw_health()
	# the speaker slots in the lower corner: four slanted grooves
	var edge: float = w - 70.0 if slots_right else 70.0
	var direction: float = -1.0 if slots_right else 1.0
	for i: int in 4:
		var x: float = edge + direction * 22.0 * float(i)
		draw_line(Vector2(x, h - 52.0), Vector2(x + 26.0 * (-direction), h - 18.0), SLOT_COLOR, 7.0, true)
