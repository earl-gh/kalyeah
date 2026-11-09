class_name StreetMinimap
extends Control
## The minimap of the landscape match (owner, 2026-11-09: Wild Rift style; docs/GDD.md "Landscape layout"): the follow camera shows only part
## of the street, so the whole street is drawn small in the top-left corner, turned the way the camera shows it (your base at the left): the
## street, each team's base in its colour (Agawang Base only), every free kid as a dot (you in white, your team blue, the other red; a tagged
## kid is a faded dot on its sidewalk) and the frame of what the camera shows. View only: it reads the sim through `show_state()`.

## The minimap's size on the 1280 x 720 base (the street's 1:2 shape, turned on its side) and its padding round the street.
const SIZE: Vector2 = Vector2(176.0, 96.0)
const PAD: float = 8.0
## The panel is the HUD's (`HudStyle.PANEL` with its rim); the street a pale see-through grey with a faint edge, so the dots are what reads.
const STREET: Color = Color(0.62, 0.64, 0.7, 0.32)
const EDGE: Color = Color(0.93, 0.95, 1.0, 0.35)
## Every dot has a thin navy ring, so a blue dot reads on the navy panel.
const DOT_RING: Color = RoyaleStyle.NAVY
const VIEW: Color = Color(1.0, 1.0, 1.0, 0.85)
const DOT_RADIUS: float = 5.0
const SELF_RADIUS: float = 6.5
const TAGGED_ALPHA: float = 0.4
## The shoulder view's cone: where you look, this long (pixels) and this wide each side (radians).
const CONE: Color = Color(1.0, 1.0, 1.0, 0.35)
const CONE_LENGTH: float = 34.0
const CONE_HALF: float = 0.5

## The ground ways (sim XZ) that point right and up on the screen now (ArenaCamera.ground_axes), the arena's size (x, z), and the street
## rect the camera shows (in the same turned frame: centre u, v and half sizes), set by `show_state()`.
var axes: Array[Vector2] = [Vector2.RIGHT, Vector2.UP]
var arena: Vector2 = Vector2(24.0, 48.0)
var view_center: Vector2 = Vector2.ZERO
var view_half: Vector2 = Vector2.ZERO
## What is drawn: each kid's [position, colour, tagged, is you], and each base's [rect corners, colour].
var dots: Array[Array] = []
## The shoulder view: where you look (sim XZ, unit; zero: no cone), drawn as a cone from your dot, and your dot's turned position.
var look_way: Vector2 = Vector2.ZERO
var _self: Vector2 = Vector2.ZERO
var bases: Array[Array] = []


func _ready() -> void:
	name = "StreetMinimap"
	mouse_filter = Control.MOUSE_FILTER_IGNORE


## Reads the sim: the kids and the bases, turned like the camera (`ground_axes`); `view_center` (sim XZ) and `view_size` (metres across and
## along the ground up the screen) are what the camera shows.
func show_state(sim: MatchSim, local_id: int, local_team: int, ground_axes: Array[Vector2], view_center_xz: Vector2, view_size: Vector2,
		look: Vector2 = Vector2.ZERO) -> void:
	axes = ground_axes
	look_way = look
	arena = Vector2(sim.layout.lane_width, sim.layout.lane_length)
	view_center = turned(view_center_xz)
	view_half = view_size / 2.0
	dots.clear()
	for id: int in sim.players:
		var state: PlayerState = sim.players[id]
		var color: Color = TeamColors.bar_self() if id == local_id else (TeamColors.score_own() if state.team == local_team else TeamColors.score_enemy())
		dots.append([turned(state.position), color, state.tagged, id == local_id])
		if id == local_id:
			_self = turned(state.position)
	bases.clear()
	if sim.mode == null:
		for team: int in [local_team, 1 - local_team]:
			var rect: Rect2 = sim.layout.base_rect(sim.side_for_team(team))
			bases.append([turned(rect.position), turned(rect.end), TeamColors.score_own() if team == local_team else TeamColors.score_enemy()])
	queue_redraw()


## A street point (sim XZ) in the camera's turned frame: x along the screen's right, y down the screen.
func turned(point: Vector2) -> Vector2:
	return Vector2(point.dot(axes[0]), -point.dot(axes[1]))


## The street's size in the turned frame.
func turned_arena() -> Vector2:
	return Vector2(arena.x * absf(axes[0].x) + arena.y * absf(axes[0].y), arena.x * absf(axes[1].x) + arena.y * absf(axes[1].y))


## Where a turned point is on the minimap (own pixels): the street fitted inside the padding, centred.
func to_map(point: Vector2) -> Vector2:
	var street: Vector2 = turned_arena()
	var room: Vector2 = size - Vector2.ONE * PAD * 2.0
	var scale: float = minf(room.x / maxf(street.x, 0.01), room.y / maxf(street.y, 0.01))
	return size / 2.0 + point * scale


func _draw() -> void:
	var panel: StyleBoxFlat = StyleBoxFlat.new()
	panel.bg_color = HudStyle.PANEL
	panel.border_color = HudStyle.RIM
	panel.set_border_width_all(2)
	panel.set_corner_radius_all(roundi(HudStyle.CORNER))
	panel.anti_aliasing = true
	draw_style_box(panel, Rect2(Vector2.ZERO, size))
	var half: Vector2 = turned_arena() / 2.0
	var street: Rect2 = Rect2(to_map(-half), to_map(half) - to_map(-half))
	draw_rect(street, STREET)
	for base: Array in bases:
		var a: Vector2 = to_map(base[0] as Vector2)
		var b: Vector2 = to_map(base[1] as Vector2)
		var color: Color = base[2] as Color
		draw_rect(Rect2(a.min(b), (a - b).abs()), Color(color, 0.45))
	draw_rect(street, EDGE, false, 1.5)
	for dot: Array in dots:
		var color: Color = dot[1] as Color
		if dot[2] as bool:
			color.a = TAGGED_ALPHA
		var you: bool = dot[3] as bool
		var at: Vector2 = to_map(dot[0] as Vector2)
		var radius: float = SELF_RADIUS if you else DOT_RADIUS
		draw_circle(at, radius + 1.5, Color(DOT_RING, color.a))
		draw_circle(at, radius, color)
	if look_way != Vector2.ZERO:
		var from: Vector2 = to_map(_self)
		var way: Vector2 = turned(look_way).normalized()
		var reach: float = CONE_LENGTH
		draw_colored_polygon(PackedVector2Array([from, from + way.rotated(-CONE_HALF) * reach, from + way.rotated(CONE_HALF) * reach]), CONE)
	if view_half != Vector2.ZERO:
		var low: Vector2 = to_map(view_center - view_half)
		var high: Vector2 = to_map(view_center + view_half)
		draw_rect(Rect2(low, high - low).intersection(Rect2(Vector2.ZERO, size)), VIEW, false, 2.0)
