class_name CameraRules
extends Resource
## The gameplay camera for a portrait (9:16) phone: a perspective camera above and
## behind the hero at a fixed, low downward angle (20 degrees: you see far up the
## street) with a wide lens, straight behind
## (`yaw_offset_degrees`), with the hero held near the lower centre of the screen
## so the battlefield ahead fills the rest. The distance is derived so that
## `visible_width` metres of lane are on screen at the camera's aim point, whatever
## the phone's aspect ratio. The camera always follows the hero and is never held
## back by the map: it is the hero (the sim) that is kept inside the play area, so
## the camera never gets stuck while the hero walks to an edge. Pure math on data
## from data/rules/camera_rules.tres.

## A lens curve of 1 is this barrel strength (k in shaders/lens_curve.gdshader).
const LENS_K_AT_FULL: float = 0.5
## The design screen: portrait 9:16.
const DESIGN_ASPECT: float = 9.0 / 16.0

## The camera sits behind and to the right of the hero (a bird's-eye view from the
## back right): it is turned this many degrees around the hero, so the street runs
## diagonally up the screen from bottom left to top right.
@export var yaw_offset_degrees: float = 0.0
## Pitch from horizontal, in degrees.
@export var pitch_degrees: float = 0.0
## The wide curve lens (0 = none, 0.5 = the owner's setting): distortion over the 3D view (negative = concave)
## (shaders/lens_curve.gdshader). The camera renders a wider field of view to feed it, so the middle
## of the picture keeps its size and the edges show more of the street.
@export var lens_curve: float = 0.0
## VERTICAL field of view, in degrees.
@export var vfov_degrees: float = 0.0
## Metres of lane width visible at the hero (the camera distance follows from it).
@export var visible_width: float = 0.0
## Where the hero sits on screen, as a fraction of the height from the top (0.5 =
## centre, larger = lower). The aim point is placed ahead of the hero to match.
@export var player_screen_y: float = 0.5
## How fast the camera catches up (1/s). Higher = snappier.
@export var follow_rate: float = 0.0
## Aspect ratios (width / height) the distance is computed for are kept in this range.
@export var min_aspect: float = 0.0
@export var max_aspect: float = 0.0


## Barrel strength of the lens for `lens_curve` (a curve of 1 would be k = 0.5).
func lens_k() -> float:
	return lens_curve * LENS_K_AT_FULL


## The vertical field of view the camera renders: the flat one widened by the lens, so the middle
## of the bent picture is as big as the flat camera's.
func render_vfov_degrees() -> float:
	return rad_to_deg(2.0 * atan(tan(deg_to_rad(vfov_degrees / 2.0)) * (1.0 + lens_k())))


## Camera distance from the target for a screen of `aspect` (width / height):
## visible width = 2 * d * tan(vfov / 2) * aspect.
func distance(aspect: float) -> float:
	var clamped: float = clampf(aspect, min_aspect, max_aspect)
	return visible_width / (2.0 * tan(deg_to_rad(vfov_degrees / 2.0)) * clamped)


## Camera position relative to the target. Own side (+Z, no flip) looks toward -Z,
## so the camera sits behind on the +Z side (and `yaw_offset_degrees` to the right);
## the other team is mirrored.
func camera_offset(camera_distance: float, flip: bool) -> Vector3:
	var pitch: float = deg_to_rad(pitch_degrees)
	var back: float = camera_distance * cos(pitch)
	var yaw: float = deg_to_rad(yaw_degrees(flip))
	return Vector3(back * sin(yaw), camera_distance * sin(pitch), back * cos(yaw))


## The camera's turn around the vertical axis (0 = looking straight down -Z).
func yaw_degrees(flip: bool) -> float:
	return yaw_offset_degrees + (180.0 if flip else 0.0)


## Ground distance (m) from the aim point to the place that shows `screen_y` of the
## way down the screen (0 = top, 1 = bottom): positive = behind the aim point
## (toward the camera), negative = ahead of it. Exact perspective, no approximation.
func ground_offset(camera_distance: float, screen_y: float) -> float:
	var pitch: float = deg_to_rad(pitch_degrees)
	var height: float = camera_distance * sin(pitch)
	var back: float = camera_distance * cos(pitch)
	# the lens bends the picture: the screen position maps to the flat picture first
	var from_middle: float = screen_y * 2.0 - 1.0
	var flat: float = from_middle * (1.0 + lens_k() * from_middle * from_middle)
	var below_axis: float = atan(flat * tan(deg_to_rad(vfov_degrees / 2.0)))
	var depression: float = pitch + below_axis
	if depression <= 0.001:
		return -INF  # the top of the screen shows the sky
	return back - height / tan(depression)


## How far ahead of the hero the camera aims so the hero sits at `player_screen_y`.
func look_ahead(camera_distance: float) -> float:
	return ground_offset(camera_distance, player_screen_y)


## Where the camera aims: straight on the hero, moved ahead toward the enemy base so the
## hero sits at `player_screen_y`. Nothing about the map holds it back (the hero is the one
## limited by the play area), so it follows the hero all the way to every edge.
func camera_target(world_pos: Vector2, flip: bool, camera_distance: float) -> Vector2:
	var forward: float = 1.0 if flip else -1.0  # toward the enemy base, in world Z
	return Vector2(world_pos.x, world_pos.y + forward * look_ahead(camera_distance))


## Fraction of the remaining distance to cover this frame (frame-rate independent).
func follow_weight(delta: float) -> float:
	return 1.0 - exp(-follow_rate * delta)
