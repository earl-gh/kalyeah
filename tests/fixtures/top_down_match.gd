extends RefCounted
## The top-down landscape match (the follow camera looking down at 45°, `CameraRules.follow`) for the tests that measure things on the screen
## from above: the shoulder view (`CameraRules.third_person`, on by default; docs/GDD.md "Shoulder view") looks along the street instead.
## `use(true)` before building a match, `use(false)` after (an after_each).

const RULES: CameraRules = preload("res://data/rules/camera_rules.tres")
static var _saved: Variant = null


static func use(on: bool) -> void:
	var rules: CameraRules = RULES
	if on:
		if _saved == null:
			_saved = rules.third_person
		rules.third_person = false
	elif _saved != null:
		rules.third_person = _saved as bool
		_saved = null
