class_name ActorView
extends Node3D
## Sub-frame visual actor wrapper (`30 Hz` MatchSim -> `60+ Hz` presentation) with critically-damped
## XZ translation, vertical posture/air Y, and facing basis smoothing so zero `30 Hz` micro-stutter
## ever reaches the character mesh or `ArenaCamera`. Also exposes locomotion VFX cadence triggers
## for sprint/slide/jump/trip dust puffs and *Tabing-Ilog* canal water ripples.

const DEFAULT_TRANSLATION_SMOOTH_TIME: float = 0.022
const DEFAULT_VERTICAL_SMOOTH_TIME: float = 0.028
const DEFAULT_FACING_SMOOTH_TIME: float = 0.042
const TELEPORT_SNAP_DISTANCE: float = 2.0
const CANAL_HALF_Z: float = 7.4
const SPRINT_STEP_CADENCE: float = 0.24
const CANAL_WADE_CADENCE: float = 0.28

@export var translation_smooth_time: float = DEFAULT_TRANSLATION_SMOOTH_TIME
@export var vertical_smooth_time: float = DEFAULT_VERTICAL_SMOOTH_TIME
@export var facing_smooth_time: float = DEFAULT_FACING_SMOOTH_TIME

var actor_id: int = -1
var kid_model: KidModel = null

var _initialized: bool = false
var _visual_xz: Vector2 = Vector2.ZERO
var _xz_velocity: Vector2 = Vector2.ZERO
var _visual_y: float = 0.0
var _y_velocity: float = 0.0
var _visual_yaw_deg: float = 0.0
var _yaw_velocity_deg: float = 0.0
var _visual_facing: Vector2 = Vector2(0.0, -1.0)
var _was_in_air: bool = false
var _was_sliding: bool = false
var _was_tripping: bool = false
var _step_timer: float = 0.0
var _wade_timer: float = 0.0


func bind_kid_model(model: KidModel, id: int = -1) -> void:
	kid_model = model
	actor_id = id
	if kid_model != null and kid_model.get_parent() == null:
		add_child(kid_model)


## Resets smoothing state immediately to `xz`, `height_y`, and `facing` (spawn, respawn, teleport).
func snap_to(xz: Vector2, facing: Vector2, height_y: float = 0.0) -> void:
	_visual_xz = xz
	_xz_velocity = Vector2.ZERO
	_visual_y = height_y
	_y_velocity = 0.0
	if facing.length_squared() > 0.0001:
		_visual_facing = facing.normalized()
		_visual_yaw_deg = fposmod(rad_to_deg(atan2(-_visual_facing.x, -_visual_facing.y)), 360.0)
	_yaw_velocity_deg = 0.0
	_initialized = true
	_apply_visual_transform()


## Frame-rate-independent sub-frame smoothing across `30 Hz` simulation ticks.
func sync_subframe(
	target_xz: Vector2,
	target_facing: Vector2,
	target_y: float,
	delta: float,
	snap: bool = false
) -> void:
	var dt: float = maxf(delta, 0.0)
	var teleported: bool = _initialized and target_xz.distance_to(_visual_xz) > TELEPORT_SNAP_DISTANCE
	if not _initialized or snap or teleported or Settings.reduced_motion or dt <= 0.0:
		snap_to(target_xz, target_facing, target_y)
		return

	if translation_smooth_time > 0.0:
		var damped_xz: Array[Vector2] = CameraRules.smooth_damp_vec2(_visual_xz, target_xz, _xz_velocity, translation_smooth_time, 0.0, dt)
		_visual_xz = damped_xz[0]
		_xz_velocity = damped_xz[1]
	else:
		_visual_xz = target_xz
		_xz_velocity = Vector2.ZERO

	if vertical_smooth_time > 0.0:
		var damped_y: Vector2 = CameraRules.smooth_damp_scalar(_visual_y, target_y, _y_velocity, vertical_smooth_time, 0.0, dt)
		_visual_y = damped_y.x
		_y_velocity = damped_y.y
	else:
		_visual_y = target_y
		_y_velocity = 0.0

	if target_facing.length_squared() > 0.0001:
		var goal_yaw: float = fposmod(rad_to_deg(atan2(-target_facing.x, -target_facing.y)), 360.0)
		if facing_smooth_time > 0.0:
			var damped_yaw: Vector2 = CameraRules.smooth_damp_angle_step(_visual_yaw_deg, goal_yaw, _yaw_velocity_deg, facing_smooth_time, 720.0, dt)
			_visual_yaw_deg = damped_yaw.x
			_yaw_velocity_deg = damped_yaw.y
		else:
			_visual_yaw_deg = goal_yaw
			_yaw_velocity_deg = 0.0
		var yaw_rad: float = deg_to_rad(_visual_yaw_deg)
		_visual_facing = Vector2(-sin(yaw_rad), -cos(yaw_rad)).normalized()

	_apply_visual_transform()


## Syncs directly from `PlayerState` + optional `TickBlend` + optional `WeaponFxView` locomotion FX.
func sync_player(
	player: PlayerState,
	blend: TickBlend,
	delta: float,
	fx_view: WeaponFxView = null,
	layout: MapLayout = null
) -> void:
	if player == null:
		return
	var target_xz: Vector2 = blend.kid(player) if blend != null else player.position
	var air_y: float = maxf(player.air_y, 0.0)
	var is_alive: bool = not player.tagged
	sync_subframe(target_xz, player.facing, air_y, delta, not is_alive)
	if fx_view != null and is_alive and not Settings.reduced_motion:
		_emit_locomotion_fx(player, fx_view, layout, delta)


## Smoothed world XZ pivot for `ArenaCamera.follow(...)`.
func smoothed_pivot_xz() -> Vector2:
	return _visual_xz


## Smoothed unit ground facing vector (`sim XZ`) for `ArenaCamera.sync_behind_character(...)`.
func smoothed_facing() -> Vector2:
	return _visual_facing


## Smoothed visual yaw in degrees (`0 = -Z`).
func smoothed_yaw_degrees() -> float:
	return _visual_yaw_deg


func _apply_visual_transform() -> void:
	var yaw_rad: float = deg_to_rad(_visual_yaw_deg)
	position = Vector3(_visual_xz.x, _visual_y, _visual_xz.y)
	rotation = Vector3(0.0, yaw_rad, 0.0)
	if kid_model != null and kid_model.get_parent() != self:
		kid_model.position = Vector3(_visual_xz.x, _visual_y, _visual_xz.y)
		kid_model.rotation = Vector3(0.0, yaw_rad, 0.0)


func _emit_locomotion_fx(player: PlayerState, fx_view: WeaponFxView, layout: MapLayout, delta: float) -> void:
	var dt: float = maxf(delta, 0.0)
	var in_air: bool = player.air_y > 0.03
	var sliding: bool = player.slide_left > 0.0
	var tripping: bool = player.effects != null and player.effects.has(StatusEffects.Type.TRIPPED)
	var in_canal: bool = _is_in_canal(_visual_xz, layout)
	var speed_est: float = _xz_velocity.length()
	var at_feet: Vector3 = Vector3(_visual_xz.x, 0.04, _visual_xz.y)
	var ctx: FxContext = fx_view.context()
	if ctx == null or ctx.particles == null:
		return

	if (in_air and not _was_in_air) or (not in_air and _was_in_air):
		ctx.particles.puff(FxParticles.Preset.DUST, at_feet, 4, Color(0.24, 0.84, 0.80) if in_canal else Color(0.95, 0.90, 0.82))
	if tripping and not _was_tripping:
		ctx.particles.puff(FxParticles.Preset.DUST, at_feet, 6, Color(0.92, 0.84, 0.72))
	if sliding and not _was_sliding:
		ctx.particles.puff(FxParticles.Preset.DUST, at_feet, 6, Color(0.26, 0.88, 0.82) if in_canal else Color(0.96, 0.91, 0.82))

	if not in_air and speed_est > 2.4:
		if in_canal:
			_wade_timer -= dt
			if _wade_timer <= 0.0:
				_wade_timer = CANAL_WADE_CADENCE
				ctx.particles.puff(FxParticles.Preset.DUST, at_feet, 2, Color(0.32, 0.88, 0.84))
		elif player.sprinting or sliding:
			_step_timer -= dt
			if _step_timer <= 0.0:
				_step_timer = SPRINT_STEP_CADENCE
				ctx.particles.puff(FxParticles.Preset.DUST, at_feet, 2, Color(0.94, 0.89, 0.80))
	else:
		_step_timer = minf(_step_timer, 0.06)
		_wade_timer = minf(_wade_timer, 0.06)

	_was_in_air = in_air
	_was_sliding = sliding
	_was_tripping = tripping


static func _is_in_canal(xz: Vector2, layout: MapLayout) -> bool:
	if layout != null:
		if not layout.is_circular():
			return false
		if layout.has_method(&"is_in_canal"):
			return bool(layout.call(&"is_in_canal", xz))
		if layout.has_method(&"in_canal"):
			return bool(layout.call(&"in_canal", xz))
		if layout.has_method(&"z_creek"):
			var cz_creek: float = float(layout.call(&"z_creek", xz.x))
			return absf(xz.y - cz_creek) <= CANAL_HALF_Z
		if layout.has_method(&"canal_z"):
			var cz_canal: float = float(layout.call(&"canal_z", xz.x))
			return absf(xz.y - cz_canal) <= CANAL_HALF_Z
		if layout.has_method(&"canal_center_z"):
			var cz: float = float(layout.call(&"canal_center_z", xz.x))
			return absf(xz.y - cz) <= CANAL_HALF_Z
	return absf(xz.y) <= CANAL_HALF_Z
