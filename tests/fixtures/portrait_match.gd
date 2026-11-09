extends RefCounted
## The portrait match (the fixed whole-street camera, the bottom tray, tap to move) for the tests that pin it: the match is landscape by default
## now (docs/GDD.md "Landscape layout", `CameraRules.follow` in data/rules/camera_rules.tres), and the portrait layout stays behind that data
## toggle until the menus are landscape too. `use(true)` before building a match, `use(false)` after (an after_each).

const RULES: CameraRules = preload("res://data/rules/camera_rules.tres")
## What the data says, kept by the first `use(true)` and put back by `use(false)`.
static var _saved: Dictionary = {}


static func use(on: bool) -> void:
	var rules: CameraRules = RULES
	if on:
		if _saved.is_empty():
			_saved = {"follow": RULES.follow, "yaw": RULES.yaw_offset_degrees, "third_person": RULES.third_person}
		rules.follow = false
		rules.third_person = false
		rules.yaw_offset_degrees = 0.0
	elif not _saved.is_empty():
		rules.follow = _saved["follow"] as bool
		rules.yaw_offset_degrees = _saved["yaw"] as float
		rules.third_person = _saved["third_person"] as bool
		_saved = {}
