class_name BarangayNav
extends RefCounted
## Deterministic waypoint A* navigation graph over MapLayout's circular 90s provincial barangay.
## Pure data + math (no Node, no NavigationServer3D) so 5v5 bots route cleanly along the
## 3 main North-South pathways (`X = -34, 0, +34`), 2 connecting East-West alleyways (`Z = +-26`),
## and right through the walkable shallow mid canal (`Z = 0`) identically on client, server, and tests.

const CORNER_MARGIN: float = 0.92
const CLEARANCE_MARGIN: float = 0.16
const EDGE_EXTRA_MARGIN: float = 0.08
const MAX_EDGE_LENGTH: float = 72.0
const MIN_WAYPOINT_DIST_SQ: float = 2.56

var waypoints: Array[Vector2] = []
var _neighbors: Array[PackedInt32Array] = []
var _weights: Array[PackedFloat32Array] = []
var _padded_rects: Array[Rect2] = []
var _edge_rects: Array[Rect2] = []
var _layout: MapLayout
var _bounds: Rect2 = Rect2()
var _agent_pad: float = 0.7
var _has_obstacles: bool = false


func _init(layout: MapLayout, agent_radius: float = 0.45) -> void:
	_layout = layout
	_agent_pad = agent_radius + 0.25
	var raw_rects: Array[Rect2] = layout.walk_obstacle_rects()
	_bounds = layout.lane_rect().grow(-_agent_pad)
	_has_obstacles = not raw_rects.is_empty()
	if not _has_obstacles:
		return
	var line_pad: float = agent_radius + CLEARANCE_MARGIN
	var edge_pad: float = line_pad + EDGE_EXTRA_MARGIN
	for rect: Rect2 in raw_rects:
		_padded_rects.append(rect.grow(line_pad))
		_edge_rects.append(rect.grow(edge_pad))
	_build_waypoints(layout, raw_rects, agent_radius + CORNER_MARGIN)
	_build_edges()


## Returns `goal` (clamped inside the arena) when unobstructed, otherwise the furthest
## unobstructed waypoint along the shortest A* path around buildings and Estero creek channels.
func next_waypoint(from: Vector2, goal: Vector2) -> Vector2:
	var clamped_goal: Vector2 = _clamp_walkable(goal)
	if not _has_obstacles or waypoints.is_empty():
		return clamped_goal
	if is_clear(from, clamped_goal):
		return clamped_goal
	var path: PackedInt32Array = _find_path(from, clamped_goal)
	if path.is_empty():
		return clamped_goal
	var chosen: Vector2 = waypoints[path[0]]
	for idx: int in range(1, path.size()):
		var candidate: Vector2 = waypoints[path[idx]]
		if is_clear(from, candidate):
			chosen = candidate
		else:
			break
	if path.size() >= 2 and from.distance_squared_to(chosen) < 0.36 and chosen == waypoints[path[0]]:
		chosen = waypoints[path[1]]
	return chosen


## True when the straight segment from `a` to `b` stays inside the arena and clears every walk obstacle.
func is_clear(a: Vector2, b: Vector2) -> bool:
	if _layout != null and _layout.arena_radius > 0.0:
		if not _layout.in_arena(a, 0.05) or not _layout.in_arena(b, 0.05):
			return false
	for rect: Rect2 in _padded_rects:
		if FightBrain.segment_hits_rect(a, b, rect):
			return false
	return true


func _is_edge_clear(a: Vector2, b: Vector2) -> bool:
	for rect: Rect2 in _edge_rects:
		if FightBrain.segment_hits_rect(a, b, rect):
			return false
	return true


func _clamp_walkable(point: Vector2) -> Vector2:
	var result: Vector2 = _layout.clamp_to_arena(point, _agent_pad) if _layout != null else point.clamp(_bounds.position, _bounds.end)
	for rect: Rect2 in _padded_rects:
		if not rect.has_point(result):
			continue
		var exits: Array[Vector2] = [
			Vector2(rect.position.x - 0.05, result.y),
			Vector2(rect.end.x + 0.05, result.y),
			Vector2(result.x, rect.position.y - 0.05),
			Vector2(result.x, rect.end.y + 0.05),
		]
		var best: Vector2 = result
		var best_d: float = INF
		for ex: Vector2 in exits:
			if _is_walkable_point(ex):
				var d: float = ex.distance_squared_to(result)
				if d < best_d:
					best_d = d
					best = ex
		result = best
	return result


func _is_walkable_point(point: Vector2) -> bool:
	if _layout != null and _layout.arena_radius > 0.0:
		if not _layout.in_arena(point, _agent_pad):
			return false
	elif not _bounds.has_point(point):
		return false
	for rect: Rect2 in _padded_rects:
		if rect.has_point(point):
			return false
	return true


func _build_waypoints(layout: MapLayout, raw_rects: Array[Rect2], corner_pad: float) -> void:
	var candidates: Array[Vector2] = []
	# 1. Walkable shallow mid-canal spine (`Z = 0.0`) & power-up objective spots (`-34, 0`, `0, 0`, `+34, 0`).
	for pu_spot: Vector2 in layout.powerup_spots:
		candidates.append(pu_spot)
	for canal_x: float in [-74.0, -54.5, -34.0, -17.0, 0.0, 17.0, 34.0, 54.5, 74.0]:
		candidates.append(Vector2(canal_x, 0.0))

	# 2. Symmetric 3-pathway N-S spines (`X = -34, 0, +34`), 2 connecting E-W alleyways (`Z = +-26`),
	#    canal towpaths (`Z = +-5.4`), base plazas, and rescue patios on both halves.
	for side: int in [MapLayout.SIDE_OWN, MapLayout.SIDE_ENEMY]:
		var s: float = float(side)
		candidates.append(layout.base_center(side))
		candidates.append(layout.spawn_center(side))
		var rescue: Rect2 = layout.rescue_rect(side)
		candidates.append(rescue.get_center())
		candidates.append(Vector2(rescue.get_center().x, rescue.position.y - 2.2))
		candidates.append(Vector2(rescue.get_center().x, rescue.end.y + 2.2))
		var inward_rx: float = rescue.end.x + 2.4 if side == MapLayout.SIDE_OWN else rescue.position.x - 2.4
		candidates.append(Vector2(inward_rx, rescue.get_center().y))

		# Shallow canal North/South bank towpaths (`Z = +-5.4`)
		for bx: float in [-74.0, -54.5, -34.0, -17.0, 0.0, 17.0, 34.0, 53.5, 74.0]:
			candidates.append(Vector2(bx * s, 5.4 * s))

		# 3 Main North-South Pathways (`X = -34.0` West, `X = 0.0` Center, `X = +34.0` East) + outer flanks
		for lane_x: float in [-72.0, -34.0, 0.0, 34.0, 72.0]:
			candidates.append(Vector2(lane_x * s, 14.2 * s))
			candidates.append(Vector2(lane_x * s, 38.8 * s))

		# 2 Connecting East-West Alleyways (`Z = +-26.0`, clear `10.2m` wide cross-lanes)
		for ax: float in [-72.0, -62.0, -49.5, -34.0, -17.2, 0.0, 17.2, 34.0, 49.5, 60.2, 72.0]:
			candidates.append(Vector2(ax * s, 26.0 * s))

		# Base approach promenade (`Z = +-48.4`) & Base Basketball/Hall Plaza (`Z = +-57.5, +-69.0, +-78.5`)
		for px: float in [-56.0, -34.0, -15.5, 0.0, 15.5, 34.0, 56.0]:
			candidates.append(Vector2(px * s, 48.4 * s))
		for cx: float in [-34.0, -15.2, 0.0, 15.2, 34.0]:
			candidates.append(Vector2(cx * s, 57.5 * s))
		for rx: float in [-28.0, -14.5, 14.5, 28.0]:
			candidates.append(Vector2(rx * s, 69.0 * s))
		candidates.append(Vector2(0.0, 78.5 * s))

	# 3. Circular outer perimeter flank ring inside `arena_radius` (`r = arena_radius - 3.6m`).
	if layout.arena_radius > 10.0:
		var ring_r: float = layout.arena_radius - 3.6
		var ring_steps: int = 24
		for step_i: int in ring_steps:
			var angle: float = TAU * float(step_i) / float(ring_steps)
			candidates.append(Vector2(cos(angle) * ring_r, sin(angle) * ring_r))

	# 4. Padded obstacle corners so bots route cleanly around every house block & cover wall.
	for rect: Rect2 in raw_rects:
		var grown: Rect2 = rect.grow(corner_pad)
		candidates.append(grown.position)
		candidates.append(Vector2(grown.end.x, grown.position.y))
		candidates.append(Vector2(grown.position.x, grown.end.y))
		candidates.append(grown.end)

	for pt: Vector2 in candidates:
		if not _is_walkable_point(pt):
			continue
		var duplicate_pt: bool = false
		for existing: Vector2 in waypoints:
			if existing.distance_squared_to(pt) < MIN_WAYPOINT_DIST_SQ:
				duplicate_pt = true
				break
		if not duplicate_pt:
			waypoints.append(pt)


func _build_edges() -> void:
	var count: int = waypoints.size()
	_neighbors.resize(count)
	_weights.resize(count)
	for i: int in count:
		_neighbors[i] = PackedInt32Array()
		_weights[i] = PackedFloat32Array()
	for i: int in count:
		for j: int in range(i + 1, count):
			var dist: float = waypoints[i].distance_to(waypoints[j])
			if dist > MAX_EDGE_LENGTH:
				continue
			if _is_edge_clear(waypoints[i], waypoints[j]):
				var ni: PackedInt32Array = _neighbors[i]
				ni.append(j)
				_neighbors[i] = ni
				var wi: PackedFloat32Array = _weights[i]
				wi.append(dist)
				_weights[i] = wi
				var nj: PackedInt32Array = _neighbors[j]
				nj.append(i)
				_neighbors[j] = nj
				var wj: PackedFloat32Array = _weights[j]
				wj.append(dist)
				_weights[j] = wj


func _find_path(from: Vector2, goal: Vector2) -> PackedInt32Array:
	var count: int = waypoints.size()
	var g_score: PackedFloat32Array = PackedFloat32Array()
	var f_score: PackedFloat32Array = PackedFloat32Array()
	var came_from: PackedInt32Array = PackedInt32Array()
	var closed: PackedByteArray = PackedByteArray()
	var goal_link: PackedByteArray = PackedByteArray()
	g_score.resize(count)
	f_score.resize(count)
	came_from.resize(count)
	closed.resize(count)
	goal_link.resize(count)
	var open: Array[int] = []

	var nearest_start: int = -1
	var nearest_start_d: float = INF
	var nearest_goal: int = -1
	var nearest_goal_d: float = INF

	for i: int in count:
		g_score[i] = INF
		f_score[i] = INF
		came_from[i] = -1
		closed[i] = 0
		var d_start: float = from.distance_to(waypoints[i])
		if d_start < nearest_start_d:
			nearest_start_d = d_start
			nearest_start = i
		if d_start <= MAX_EDGE_LENGTH and is_clear(from, waypoints[i]):
			g_score[i] = d_start
			f_score[i] = d_start + waypoints[i].distance_to(goal)
			open.append(i)
		var d_goal: float = goal.distance_to(waypoints[i])
		if d_goal < nearest_goal_d:
			nearest_goal_d = d_goal
			nearest_goal = i
		goal_link[i] = 1 if (d_goal <= MAX_EDGE_LENGTH and is_clear(waypoints[i], goal)) else 0

	if open.is_empty() and nearest_start >= 0:
		g_score[nearest_start] = nearest_start_d
		f_score[nearest_start] = nearest_start_d + waypoints[nearest_start].distance_to(goal)
		open.append(nearest_start)
	if nearest_goal >= 0:
		var any_goal: bool = false
		for i: int in count:
			if goal_link[i] == 1:
				any_goal = true
				break
		if not any_goal:
			goal_link[nearest_goal] = 1

	var best_end: int = -1
	var best_total: float = INF

	while not open.is_empty():
		var pick_idx: int = 0
		var current: int = open[0]
		var current_f: float = f_score[current]
		for k: int in range(1, open.size()):
			var node_id: int = open[k]
			if f_score[node_id] < current_f:
				current_f = f_score[node_id]
				current = node_id
				pick_idx = k
		if current_f >= best_total:
			break
		open[pick_idx] = open[open.size() - 1]
		open.pop_back()
		if closed[current] == 1:
			continue
		closed[current] = 1

		if goal_link[current] == 1:
			var total: float = g_score[current] + waypoints[current].distance_to(goal)
			if total < best_total:
				best_total = total
				best_end = current

		var n_ids: PackedInt32Array = _neighbors[current]
		var n_weights: PackedFloat32Array = _weights[current]
		for k: int in n_ids.size():
			var nxt: int = n_ids[k]
			if closed[nxt] == 1:
				continue
			var tentative: float = g_score[current] + n_weights[k]
			if tentative < g_score[nxt]:
				came_from[nxt] = current
				g_score[nxt] = tentative
				f_score[nxt] = tentative + waypoints[nxt].distance_to(goal)
				if not open.has(nxt):
					open.append(nxt)

	if best_end < 0:
		return PackedInt32Array()
	var rev: Array[int] = []
	var walk: int = best_end
	while walk >= 0:
		rev.append(walk)
		walk = came_from[walk]
	rev.reverse()
	var result: PackedInt32Array = PackedInt32Array()
	for node_id: int in rev:
		result.append(node_id)
	return result
