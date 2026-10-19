class_name DecorativeSway
extends Node
## Decorative NPC motion can stop/restart independently of the gameplay simulation.

var target: Node3D
var angle: float
var seconds: float
var base_yaw: float
var _tween: Tween
var _initialized: bool = false
var _reduced: bool = false


func _init(subject: Node3D, degrees: float, duration: float) -> void:
	target = subject
	angle = deg_to_rad(degrees)
	seconds = duration
	base_yaw = target.rotation.y


func _ready() -> void:
	Settings.accessibility_changed.connect(_refresh)
	_refresh()


func _refresh() -> void:
	if _initialized and _reduced == Settings.reduced_motion:
		return
	_initialized = true
	_reduced = Settings.reduced_motion
	if is_instance_valid(_tween):
		_tween.kill()
	target.rotation.y = base_yaw
	if Settings.reduced_motion:
		return
	_tween = create_tween().set_loops()
	_tween.tween_property(target, "rotation:y", base_yaw + angle, seconds).set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN_OUT)
	_tween.tween_property(target, "rotation:y", base_yaw - angle, seconds).set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN_OUT)
