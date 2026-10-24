class_name CameraRules
extends Resource
## The gameplay camera, like Clash Royale's: one fixed camera above the whole arena that never moves, so the
## entire 27 x 48 m street is on screen at all times (no scrolling, no following, nothing off screen). It is an
## ORTHOGRAPHIC camera tilted about 45 degrees (a kid is the same size at either end of the street, and the top
## and the front of every kid, wall and tower show), sized so the whole arena fits the phone with a margin kept
## clear at the top (score row), at the bottom (the controls) and at the sides. The view size and the small shift
## that centres the arena in the space left are solved from the screen's shape, so any phone, tablet or tall
## screen shows the whole street.
## The kids are drawn bigger than life (`kid_view_scale`) so they stay readable at this size; their hit area is
## the real one. Pure math on data from data/rules/camera_rules.tres.

## A lens curve of 1 is this barrel strength (k in shaders/lens_curve.gdshader).
const LENS_K_AT_FULL: float = 0.5
## The design screen: portrait 9:16.
const DESIGN_ASPECT: float = 9.0 / 16.0
## The smallest and largest distance (metres) the fit searches.
const MIN_DISTANCE: float = 10.0
const MAX_DISTANCE: float = 3000.0
## How far the orthographic camera stands from the point it aims at (metres). It changes nothing on screen, only where
## the depth range (near and far planes) sits.
const STANDOFF: float = 150.0

## The camera's turn around the arena (0 = looking straight along the street).
@export var yaw_offset_degrees: float = 0.0
## Pitch from horizontal, in degrees.
@export var pitch_degrees: float = 0.0
## The wide curve lens (0 = none): distortion over the 3D view (shaders/lens_curve.gdshader).
@export var lens_curve: float = 0.0
## Fractions of the screen kept clear of the arena: the top (score and settings), the bottom (the controller body
## with the D-pad and the skills; at least this, and the real height of the controller when it is taller) and each side.
@export var frame_top: float = 0.0
@export var frame_bottom: float = 0.0
@export var frame_side: float = 0.0
## How much bigger than real the kids (and what they throw) are drawn, so they stay readable while the whole
## street is on screen. The hit area (the ring under a kid, the weapon shapes) is the real size and the same for every kid.
@export var kid_view_scale: float = 1.0
## How much smaller the health bars over the kids are drawn (1 = the full size).
@export var bar_scale: float = 1.0


## Barrel strength of the lens for `lens_curve` (a curve of 1 would be k = 0.5).
func lens_k() -> float:
	return lens_curve * LENS_K_AT_FULL


## Where a ground point (x, z) at `height` shows on a flat screen of `aspect` (width / height) when the orthographic view is
## `view_size` metres tall and aims at `(0, aim_z)`: normalised, -1..1 across and up (x right, y up). Nothing shrinks with
## distance: a metre is the same number of pixels at either end of the street.
func screen_point(view_size: float, aspect: float, ground: Vector2, aim_z: float, height: float = 0.0) -> Vector2:
	var pitch: float = deg_to_rad(pitch_degrees)
	var half: float = view_size / 2.0
	var from_aim: Vector3 = Vector3(ground.x, height, ground.y - aim_z)
	var up: Vector3 = Vector3(0.0, cos(pitch), -sin(pitch))
	return Vector2(from_aim.x / (half * aspect), from_aim.dot(up) / half)


## The shift of the aim point along the street (metres, + toward the own end) that centres the arena's two ends in
## the band of the screen left between the top and bottom margins, for a camera `distance` away.
func centring_shift(distance: float, aspect: float, arena: Vector2, bottom: float = -1.0, top_margin: float = -1.0) -> float:
	var kept: float = frame_bottom if bottom < 0.0 else bottom
	var kept_top: float = frame_top if top_margin < 0.0 else top_margin
	var want: float = ((1.0 - 2.0 * kept_top) + (-1.0 + 2.0 * kept)) / 2.0
	var low: float = -arena.y
	var high: float = arena.y
	for i: int in 50:
		var shift: float = (low + high) / 2.0
		var far: float = screen_point(distance, aspect, Vector2(0.0, -arena.y / 2.0), shift).y
		var near: float = screen_point(distance, aspect, Vector2(0.0, arena.y / 2.0), shift).y
		if (far + near) / 2.0 > want:
			high = shift  # too high on the screen: aim further toward the far end less
		else:
			low = shift
	return (low + high) / 2.0


## True when the whole `arena` (x, z extents) fits inside the margins at `distance`.
func fits(distance: float, aspect: float, arena: Vector2, bottom: float = -1.0, top_margin: float = -1.0) -> bool:
	var kept: float = frame_bottom if bottom < 0.0 else bottom
	var kept_top: float = frame_top if top_margin < 0.0 else top_margin
	var shift: float = centring_shift(distance, aspect, arena, bottom, top_margin)
	var top: float = screen_point(distance, aspect, Vector2(0.0, -arena.y / 2.0), shift).y
	var bottom_edge: float = screen_point(distance, aspect, Vector2(0.0, arena.y / 2.0), shift).y
	if top > 1.0 - 2.0 * kept_top or bottom_edge < -1.0 + 2.0 * kept:
		return false
	for sx: float in [-1.0, 1.0]:
		for sz: float in [-1.0, 1.0]:
			var corner: Vector2 = screen_point(distance, aspect, Vector2(sx * arena.x / 2.0, sz * arena.y / 2.0), shift)
			if absf(corner.x) > 1.0 - 2.0 * frame_side:
				return false
	return true


## The camera's distance from the aim point (x) and the aim point's shift along the street (y) that put the whole
## `arena` on a screen of `aspect`: the nearest the camera can be and still fit.
func frame(aspect: float, arena: Vector2, bottom: float = -1.0, top_margin: float = -1.0) -> Vector2:
	var near: float = MIN_DISTANCE
	var far: float = MAX_DISTANCE
	for i: int in 50:
		var middle: float = (near + far) / 2.0
		if fits(middle, aspect, arena, bottom, top_margin):
			far = middle
		else:
			near = middle
	return Vector2(far, centring_shift(far, aspect, arena, bottom, top_margin))


## Camera position relative to the aim point. Own side (+Z, no flip) looks toward -Z, so the camera sits on the
## +Z side; the other team's view is turned half way round.
func camera_offset(camera_distance: float, flip: bool) -> Vector3:
	var pitch: float = deg_to_rad(pitch_degrees)
	var back: float = camera_distance * cos(pitch)
	var yaw: float = deg_to_rad(yaw_degrees(flip))
	return Vector3(back * sin(yaw), camera_distance * sin(pitch), back * cos(yaw))


## The camera's turn around the vertical axis (0 = looking straight down -Z).
func yaw_degrees(flip: bool) -> float:
	return yaw_offset_degrees + (180.0 if flip else 0.0)


## Where the camera aims: the middle of the street, shifted `aim_z` toward the own end (the other team's view is
## turned half way round, so the shift is the other way). Never moves with anyone.
func camera_target(aim_z: float, flip: bool) -> Vector2:
	return Vector2(0.0, -aim_z if flip else aim_z)
