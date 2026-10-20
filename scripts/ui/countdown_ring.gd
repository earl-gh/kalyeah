class_name CountdownRing
extends Control
## A round gold-rimmed countdown: the seconds in the middle and a ring that empties
## as time runs out (turns red in the last seconds). Pick timer and respawn timer.

const FACE: Color = Color(0.08, 0.1, 0.24, 0.95)
const TRACK: Color = Color(1.0, 1.0, 1.0, 0.12)
const FILL: Color = Color(1.0, 0.8, 0.3)
const URGENT: Color = Color(1.0, 0.35, 0.3)
const INK: Color = Color(0.1, 0.06, 0.06)
const URGENT_SECONDS: float = 3.0
const POINTS: int = 48
## The size on the loadout screen: big enough to feel the seconds, not just read them.
const LOADOUT_SIZE: float = 104.0

var seconds: float = 0.0
var total: float = 1.0


func show_time(p_seconds: float, p_total: float) -> void:
	seconds = maxf(p_seconds, 0.0)
	total = maxf(p_total, 0.001)
	# in the last seconds the ring pulses (still, with Reduce motion)
	set_process(is_urgent() and not Settings.reduced_motion)
	queue_redraw()


func is_urgent() -> bool:
	return seconds <= URGENT_SECONDS and seconds > 0.0


func _process(_delta: float) -> void:
	queue_redraw()


func fraction() -> float:
	return clampf(seconds / total, 0.0, 1.0)


func _draw() -> void:
	var center: Vector2 = size / 2.0
	var radius: float = minf(size.x, size.y) / 2.0
	var width: float = radius * 0.2
	draw_circle(center + Vector2(0.0, 4.0), radius, Color(0.0, 0.0, 0.0, 0.35))
	draw_circle(center, radius, INK)
	draw_circle(center, radius - 3.0, FACE)
	draw_arc(center, radius - width / 2.0 - 4.0, 0.0, TAU, POINTS, TRACK, width)
	var color: Color = URGENT if seconds <= URGENT_SECONDS else FILL
	if is_urgent():
		var beat: float = 0.5 + 0.5 * sin(float(Time.get_ticks_msec()) / 1000.0 * TAU * 1.6) if not Settings.reduced_motion else 1.0
		draw_arc(center, radius + 3.0, 0.0, TAU, POINTS, Color(URGENT, 0.25 + 0.4 * beat), 6.0, true)
	if fraction() > 0.0:
		draw_arc(center, radius - width / 2.0 - 4.0, -PI / 2.0, -PI / 2.0 + TAU * fraction(), POINTS, color, width, true)
	var font: Font = ThemeDB.fallback_font
	var text: String = "%d" % ceili(seconds)
	var font_size: int = int(radius * 0.8)
	var text_size: Vector2 = font.get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, font_size)
	var origin: Vector2 = center + Vector2(-text_size.x / 2.0, font_size * 0.36)
	draw_string_outline(font, origin, text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, font_size, 8, INK)
	draw_string(font, origin, text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, font_size, Color.WHITE)
