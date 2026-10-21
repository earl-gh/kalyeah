extends Node
## Prints how big every prop and building is drawn in a match, next to what it really measures times the world factor.
##   godot --headless res://tools/art/scale_audit.tscn   (a scene, so the autoloads are there)
## `ratio` 1.00 is true to the kids; the allowed range is in ScaleReference. Flags the ones outside it.


func _ready() -> void:
	var camera: CameraRules = load("res://data/rules/camera_rules.tres") as CameraRules
	var layout: MapLayout = load("res://data/rules/map_layout.tres") as MapLayout
	var factor: float = ScaleReference.world_factor(camera.kid_view_scale)
	print("kid drawn %.2f m (model %.2f x %.2f), real %.2f m, world factor %.2f, scenery_scale %.2f" % [KidModel.KID_HEIGHT * camera.kid_view_scale, KidModel.KID_HEIGHT, camera.kid_view_scale, ScaleReference.REAL_KID, factor, layout.scenery_scale])
	var map: StreetMap = (load("res://scenes/map/street_map.tscn") as PackedScene).instantiate() as StreetMap
	map.show_overview_camera = false
	add_child(map)
	var seen: Dictionary[String, Array] = {}
	for child: Node in map.get_node("%Geometry").get_children():
		var node: Node3D = child as Node3D
		if node == null or not node.scene_file_path.begins_with("res://assets/models/props/"):
			continue
		var id: String = node.scene_file_path.get_file().get_basename()
		var natural: Vector3 = PropModel.size_of(id)
		var drawn: Vector3 = natural * node.scale
		if not seen.has(id):
			seen[id] = []
		seen[id].append(drawn)
	var ids: Array = seen.keys()
	ids.sort()
	print("%-18s %5s %7s %7s %7s %7s %6s  %s" % ["model", "count", "high", "wide", "deep", "real*K", "ratio", ""])
	for id: String in ids:
		var drawn: Vector3 = (seen[id] as Array)[0]
		var high: float = drawn.y
		if id == "basketball_pole":
			high = drawn.x  # lies on its side: its length is the model's tallest side turned
			high = maxf(drawn.x, maxf(drawn.y, drawn.z))
		if id == "drainage_cover":
			high = maxf(drawn.x, drawn.z)
		if ScaleReference.BUILDINGS.has(id):
			var real_width: float = drawn.x / factor
			var real_high: float = drawn.y / factor
			var lot: float = ScaleReference.REAL_BUILDING_WIDTH
			var bad: bool = absf(real_width / lot - 1.0) > ScaleReference.BUILDING_WIDTH_TOLERANCE or real_high < ScaleReference.REAL_BUILDING_HEIGHT.x or real_high > ScaleReference.REAL_BUILDING_HEIGHT.y
			print("%-18s %5d  building: %.1f m wide and %.1f m tall at true scale (lot %.1f m, %.1f to %.1f m tall)  %s" % [id, (seen[id] as Array).size(), real_width, real_high, lot, ScaleReference.REAL_BUILDING_HEIGHT.x, ScaleReference.REAL_BUILDING_HEIGHT.y, "OUT OF RANGE" if bad else ""])
			continue
		var real: float = ScaleReference.REAL_HEIGHT.get(id, 0.0) as float
		var target: float = real * factor
		var ratio: float = high / target if target > 0.0 else 0.0
		var range: Vector2 = ScaleReference.allowed(id)
		var flag: String = ""
		if real <= 0.0:
			flag = "NO REFERENCE"
		elif ratio < range.x:
			flag = "TOO SMALL"
		elif ratio > range.y:
			flag = "TOO BIG"
		print("%-18s %5d %7.2f %7.2f %7.2f %7.2f %6.2f  %s" % [id, (seen[id] as Array).size(), high, drawn.x, drawn.z, target, ratio, flag])
	get_tree().quit()
