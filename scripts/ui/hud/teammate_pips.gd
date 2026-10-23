class_name TeammatePips
extends Control
## While you wait in the box: one small face per teammate, so you can see at a glance whether anyone is free to rescue you and
## who is on the way (visual pass; the tray was words only). A free teammate has a ring in your team's colour; one running toward
## your box also gets a gold ring and a chevron under the face; a tagged one is greyed with a cross. Shapes say it as well as
## colour. It only reads the sim; it never takes a touch and nothing in it can be picked.

enum State { FREE, COMING, TAGGED }

const FACE_RADIUS: float = 22.0
const GAP: float = 18.0
const RING: float = 4.0
const OUTLINE: float = 3.0
## A teammate counts as coming when it got at least this much nearer your box (m) since the last look.
const COMING_STEP: float = 0.02
const CHEVRON: Vector2 = Vector2(12.0, 7.0)
const COMING_COLOR: Color = RoyaleStyle.GOLD
const TAGGED_TINT: Color = Color(0.45, 0.47, 0.55)
const FACE_BACK: Color = Color(0.2, 0.28, 0.5)
const FACE_POINTS: int = 32
## The cross over a tagged face: its dark outline and its light stroke (px), reaching this share of the face's radius.
const CROSS_INK: float = 9.0
const CROSS_STROKE: float = 4.0
const CROSS_REACH: float = 0.55

## {id, state, face} for each teammate, in id order.
var pips: Array[Dictionary] = []
## Each teammate's last distance to your box, to tell who is heading there.
var _last_distance: Dictionary[int, float] = {}
## A face-sized circle (moved to each face as it is drawn) and the picture coordinates for it, built once.
var _circle: PackedVector2Array = PackedVector2Array()
var _disc: PackedVector2Array = PackedVector2Array()
var _disc_uv: PackedVector2Array = PackedVector2Array()


func _init() -> void:
	name = "TeammatePips"
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	custom_minimum_size = Vector2(0.0, (FACE_RADIUS + RING + OUTLINE) * 2.0 + CHEVRON.y + 4.0)
	accessibility_name = "Teammates"
	for i: int in FACE_POINTS:
		var direction: Vector2 = Vector2.from_angle(TAU * float(i) / float(FACE_POINTS))
		_circle.append(direction * FACE_RADIUS)
		_disc_uv.append(Vector2(0.5, 0.5) + direction * 0.5)
	_disc.resize(FACE_POINTS)


## Reads where `player`'s teammates are and whether they are tagged.
func sync(sim: MatchSim, player: PlayerState) -> void:
	var box: Vector2 = sim.layout.jail_rect(sim.side_for_team(player.team)).get_center()
	var next: Array[Dictionary] = []
	var free: int = 0
	var coming: int = 0
	for id: int in sim.players:
		var mate: PlayerState = sim.players[id]
		if id == player.id or mate.team != player.team:
			continue
		var distance: float = mate.position.distance_to(box)
		var state: State = State.FREE
		if mate.tagged:
			state = State.TAGGED
		elif _last_distance.has(id) and _last_distance[id] - distance >= COMING_STEP:
			state = State.COMING
		_last_distance[id] = distance
		if state != State.TAGGED:
			free += 1
		if state == State.COMING:
			coming += 1
		next.append({"id": id, "state": state, "face": KidModel.minimap_face(mate.character_id)})
	var changed: bool = next.size() != pips.size()
	for index: int in mini(next.size(), pips.size()):
		changed = changed or next[index]["state"] != pips[index]["state"] or next[index]["id"] != pips[index]["id"]
	pips = next
	if changed:
		custom_minimum_size.x = maxf(_step() * float(pips.size()) - GAP, 0.0)
		accessibility_description = "%d free, %d on the way" % [free, coming]
		queue_redraw()


## How many teammates are in `state`.
func count(state: State) -> int:
	return pips.filter(func(pip: Dictionary) -> bool: return pip["state"] == state).size()


func _draw() -> void:
	if pips.is_empty():
		return
	var step: float = _step()
	var left: float = size.x / 2.0 - step * float(pips.size() - 1) / 2.0
	var y: float = FACE_RADIUS + RING + OUTLINE
	for index: int in pips.size():
		_draw_pip(Vector2(left + step * float(index), y), pips[index])


## From one face's centre to the next.
func _step() -> float:
	return (FACE_RADIUS + RING + OUTLINE) * 2.0 + GAP


func _draw_pip(at: Vector2, pip: Dictionary) -> void:
	for i: int in FACE_POINTS:
		_disc[i] = at + _circle[i]
	var state: State = pip["state"] as State
	var ring_color: Color = TeamColors.ui_own()
	if state == State.COMING:
		ring_color = COMING_COLOR
	elif state == State.TAGGED:
		ring_color = TAGGED_TINT
	draw_circle(at, FACE_RADIUS + RING + OUTLINE, RoyaleStyle.NAVY)
	draw_circle(at, FACE_RADIUS + RING, ring_color)
	draw_circle(at, FACE_RADIUS, FACE_BACK)
	var face: Texture2D = pip["face"] as Texture2D
	var tint: Color = TAGGED_TINT if state == State.TAGGED else Color.WHITE
	if face != null:
		# the face cut round: a circle of points with the picture's matching UVs
		draw_colored_polygon(_disc, tint, _disc_uv, face)
	if state == State.TAGGED:
		var arm: float = FACE_RADIUS * CROSS_REACH
		for sign: float in [-1.0, 1.0]:
			draw_line(at + Vector2(-arm, -arm * sign), at + Vector2(arm, arm * sign), RoyaleStyle.NAVY, CROSS_INK, true)
			draw_line(at + Vector2(-arm, -arm * sign), at + Vector2(arm, arm * sign), RoyaleStyle.CREAM, CROSS_STROKE, true)
	elif state == State.COMING:
		var tip: Vector2 = at + Vector2(0.0, FACE_RADIUS + RING + OUTLINE + CHEVRON.y + 2.0)
		var wing: PackedVector2Array = PackedVector2Array([tip, tip + Vector2(-CHEVRON.x, -CHEVRON.y), tip + Vector2(CHEVRON.x, -CHEVRON.y)])
		draw_colored_polygon(wing, COMING_COLOR)
		draw_polyline(PackedVector2Array([wing[0], wing[1], wing[2], wing[0]]), RoyaleStyle.NAVY, 2.0, true)
