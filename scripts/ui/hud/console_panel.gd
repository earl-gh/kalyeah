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

## Round recesses {centre (from this panel's top-left), radius}: set by the HUD's layout.
var sockets: Array[Dictionary] = []
## The speaker slots sit toward this side (true = the right).
var slots_right: bool = true


func set_sockets(list: Array[Dictionary], right: bool) -> void:
	sockets = list
	slots_right = right
	queue_redraw()


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
	# the speaker slots in the lower corner: four slanted grooves
	var edge: float = w - 70.0 if slots_right else 70.0
	var direction: float = -1.0 if slots_right else 1.0
	for i: int in 4:
		var x: float = edge + direction * 22.0 * float(i)
		draw_line(Vector2(x, h - 52.0), Vector2(x + 26.0 * (-direction), h - 18.0), SLOT_COLOR, 7.0, true)
