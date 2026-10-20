class_name MinimapCanvas
extends Control
## What the minimap draws (see LaneMinimap): the street, the bases, the taped boxes, the cover
## walls and every player's face. It lives in a SubViewport that renders it several times bigger
## than it is shown, and LaneMinimap scales that image down smoothly, so nothing is pixelated.
## It draws in the minimap's own pixels (the viewport is scaled up for it).

var _minimap: LaneMinimap
var _sim: MatchSim
var _local_id: int = -1
var _local_team: int = 0


func setup(minimap: LaneMinimap, sim: MatchSim, local_id: int, local_team: int) -> void:
	_minimap = minimap
	_sim = sim
	_local_id = local_id
	_local_team = local_team
	mouse_filter = Control.MOUSE_FILTER_IGNORE


func _draw() -> void:
	if _sim == null:
		return
	var layout: MapLayout = _sim.layout
	draw_rect(_minimap.street_rect(), LaneMinimap.ROAD)
	var own_side: int = _sim.side_for_team(_local_team)
	for side: int in [MapLayout.SIDE_OWN, MapLayout.SIDE_ENEMY]:
		var color: Color = LaneMinimap.OWN if side == own_side else LaneMinimap.ENEMY
		_draw_base(side, Color(color, LaneMinimap.BASE_ALPHA))
		_draw_world_rect(layout.jail_rect(side), Color(color, LaneMinimap.JAIL_ALPHA))
	for rect: Rect2 in _sim.obstacles:
		_draw_world_rect(rect, LaneMinimap.OBSTACLE)
	# the local player last, so you are never covered by another face
	var ids: Array = _sim.players.keys()
	ids.erase(_local_id)
	ids.append(_local_id)
	for id: int in ids:
		if _sim.players.has(id):
			_draw_player(_sim.players[id] as PlayerState)
	for ping_data: Dictionary in _minimap.pings():
		var ring: Dictionary = _minimap.ring_of(ping_data)
		draw_arc(_minimap.to_map(ping_data["at"] as Vector2), ring["radius"] as float, 0.0, TAU, LaneMinimap.FACE_STEPS, Color(ping_data["color"] as Color, ring["alpha"] as float), LaneMinimap.PING_WIDTH, true)


## One player: their face (cropped from the boot screen art; the top of the portrait without one)
## cut to a circle, with one thin ring in the team colour (blue yours, red theirs). You are a little
## bigger. Tagged kids, and enemies hiding in their box, are dimmed and ringed darker.
func _draw_player(state: PlayerState) -> void:
	var at: Vector2 = _minimap.to_map(state.position)
	var dim: bool = state.tagged or _sim.hides_in_box(state, _local_team)
	var team_color: Color = LaneMinimap.OWN if state.team == _local_team else LaneMinimap.ENEMY
	var ring: Color = team_color.darkened(0.35) if dim else team_color
	var radius: float = LaneMinimap.FACE * (LaneMinimap.SELF_SCALE if state.id == _local_id else 1.0)
	var texture: Texture2D = KidModel.minimap_face(state.character_id)
	if texture == null:
		texture = KidModel.portrait(state.character_id)
	# the face reaches under the ring, so no gap shows between them
	draw_colored_polygon(LaneMinimap.face_points(at, radius), Color.WHITE.darkened(LaneMinimap.TAGGED_DIM) if dim else Color.WHITE, LaneMinimap.face_uvs(texture), texture)
	draw_arc(at, radius, 0.0, TAU, LaneMinimap.FACE_STEPS, ring, LaneMinimap.RING, true)


func _draw_world_rect(rect: Rect2, color: Color) -> void:
	var a: Vector2 = _minimap.to_map(rect.position)
	var b: Vector2 = _minimap.to_map(rect.end)
	draw_rect(Rect2(Vector2(minf(a.x, b.x), minf(a.y, b.y)), (b - a).abs()), color)


## The base circle, cut off where it meets the edges of the street.
func _draw_base(side: int, color: Color) -> void:
	var layout: MapLayout = _sim.layout
	var center: Vector2 = layout.base_center(side)
	var street: Rect2 = layout.lane_rect()
	var points: PackedVector2Array = PackedVector2Array()
	for i: int in LaneMinimap.BASE_STEPS:
		var angle: float = TAU * float(i) / float(LaneMinimap.BASE_STEPS)
		var point: Vector2 = center + Vector2(cos(angle), sin(angle)) * layout.base_radius
		point = point.clamp(street.position, street.end)
		points.append(_minimap.to_map(point))
	draw_colored_polygon(points, color)
